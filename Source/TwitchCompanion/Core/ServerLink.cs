// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Verbindung zum Twitch Companion JS server (Hintergrund-Threads).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Einstellungen, die die Verbindung braucht; wird bei jedem Durchlauf neu abgefragt.</summary>
    public sealed class LinkConfig
    {
        public bool Enabled;
        public string BaseUrl;
        public string[] Prefixes;
        public bool RequireCategory = true;
        /// <summary>Wettbonus (Feature-Issue #11): Stand der Wette und Privat-Liste mit abfragen.</summary>
        public bool BetBonus;

        /// <summary>Ändert sich der Schlüssel, meldet sich die Mod mit einer neuen Verbindung neu an.</summary>
        public string Key => (BaseUrl ?? string.Empty).TrimEnd('/') + "|" + string.Join(",", Prefixes ?? new string[0]);
    }

    public sealed class HelloEvent { public HelloInfo Info; public bool Reconnect; }
    public sealed class ChatEvent { public ChatLine Line; public DateTime QueuedAtUtc = DateTime.UtcNow; }
    public sealed class PrivacyEvent { public PrivacyChange Change; }
    public sealed class ActiveEvent { public List<ActiveChatter> Chatters; public int TotalOnServer; }
    /// <summary>Eine aufgelöste Wette, je Wette höchstens einmal je Verbindung gemeldet; dazu die Privat-Liste zum Zeitpunkt.</summary>
    public sealed class PredictionEvent { public PredictionResult Result; public HashSet<string> PrivateUserIds; }
    /// <summary>Antwort auf einen Startversuch (Server-Spec <c>chat-bruecke.md</c>, Teil C).</summary>
    public sealed class PollStartEvent { public string RoundId; public PollStartResult Result; }
    public sealed class PollResultEvent { public PollResult Result; }
    public sealed class RaidEvent { public RaidInfo Raid; }

    /// <summary>
    /// Verbindung nach der Server-Spec „Chat-Brücke“ (Server-Issues #74, #79; gemergt, Server-PR #82):
    /// <list type="bullet">
    /// <item>Ereignisstrom <c>GET /api/chat/stream</c>, nach Abbruch nach <see cref="ReconnectDelay"/> neu verbinden.</item>
    /// <item>Jede <see cref="PollInterval"/>: Kategorie (<c>GET /api/twitch/channel</c>), dann, wenn erlaubt,
    /// aktive Chatter (<c>GET /api/chat/active</c>).</item>
    /// <item>Senden (<c>POST /api/chat/send</c>) mit mindestens <see cref="MinSendGap"/> Abstand, bei 429 nach
    /// <c>retryAfterMs</c> erneut.</item>
    /// </list>
    /// Keine Spielobjekte: Ergebnisse gehen über <see cref="Events"/> an den Hauptthread.
    /// </summary>
    public sealed class ServerLink
    {
        public const string Client = "rimworld";
        public const string Feature = "rimworld";
        public const int MaxPendingSends = 50;
        public const int MaxQueuedEvents = 500;

        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public TimeSpan MinSendGap { get; set; } = TimeSpan.FromMilliseconds(1050);
        public TimeSpan MaxSendAge { get; set; } = TimeSpan.FromSeconds(90);

        public readonly ConcurrentQueue<object> Events = new ConcurrentQueue<object>();

        private readonly Func<LinkConfig> _config;
        private readonly Action<string> _log;
        private readonly RecentIds _ids = new RecentIds();
        private readonly AutoResetEvent _pollWake = new AutoResetEvent(false);
        private readonly AutoResetEvent _sendWake = new AutoResetEvent(false);
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private readonly LinkedList<KeyValuePair<DateTime, string>> _outbox = new LinkedList<KeyValuePair<DateTime, string>>();
        private readonly object _lock = new object();
        private HttpWebRequest _sseRequest;
        private volatile bool _stop;
        private Thread[] _threads;

        private volatile bool _connected;
        private volatile ChannelInfo _channel;
        private volatile string _channelProblem = "noch nicht abgefragt";
        private volatile string _lastError;
        private volatile string _lastSendError;
        private DateTime _lastSendUtc = DateTime.MinValue;
        private int _sent, _dropped, _duplicates, _reconnects;

        public ServerLink(Func<LinkConfig> config, Action<string> log)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? (_ => { });
        }

        // ── Zustand (aus jedem Thread lesbar) ───────────────────────────

        /// <summary>Ereignisstrom steht und der Server hat <c>hello</c> geschickt.</summary>
        public bool Connected => _connected;
        public DateTime? ConnectedSinceUtc { get; private set; }
        public ChannelInfo Channel => _channel;
        /// <summary>Warum keine Kategorie bekannt ist (z. B. älterer Server ohne Teil B), sonst <c>null</c>.</summary>
        public string ChannelProblem => _channelProblem;
        public string LastError => _lastError;
        public string LastSendError => _lastSendError;
        public int SentCount => _sent;
        public int DroppedSendCount => _dropped;
        public int DuplicateCount => _duplicates;
        public int ReconnectCount => _reconnects;
        public DateTime? LastActiveRefreshUtc { get; private set; }

        public int PendingSends { get { lock (_lock) return _outbox.Count; } }

        /// <summary>Befehle weiterleiten und Aktivität auffrischen erlaubt (verbunden und Kategorie passt).</summary>
        public bool GateOpen
        {
            get
            {
                LinkConfig cfg = SafeConfig();
                return CategoryGate.IsOpen(_connected, cfg?.RequireCategory ?? true, _channel);
            }
        }

        // ── Steuerung ───────────────────────────────────────────────────

        public void Start()
        {
            if (_threads != null) return;
            _threads = new[]
            {
                new Thread(SseLoop) { IsBackground = true, Name = "TwitchCompanion SSE" },
                new Thread(PollLoop) { IsBackground = true, Name = "TwitchCompanion Poll" },
                new Thread(SendLoop) { IsBackground = true, Name = "TwitchCompanion Send" },
            };
            foreach (var t in _threads) t.Start();
        }

        public void Stop()
        {
            _stop = true;
            _stopEvent.Set();
            _pollWake.Set();
            _sendWake.Set();
            AbortStream();
            if (_threads == null) return;
            foreach (var t in _threads) t.Join(2000);
        }

        /// <summary>Neue Anmeldung erzwingen, z. B. nach Änderung der Einstellungen.</summary>
        public void Reconnect() => AbortStream();

        /// <summary>
        /// Startet eine Twitch-Abstimmung über den Server (<c>POST /api/twitch/poll/start</c>) im Hintergrund; die Antwort kommt
        /// als <see cref="PollStartEvent"/> über <see cref="Events"/>.
        /// </summary>
        public void StartPoll(string roundId, string body)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                LinkConfig cfg = SafeConfig();
                PollStartResult result;
                if (cfg == null || !_connected) result = new PollStartResult { Reason = "unreachable" };
                else
                {
                    HttpResult r = Request(Base(cfg) + "/api/twitch/poll/start", "POST", body);
                    result = ConsumerPolls.ParseStart(r.Status, r.Body);
                    if (r.Status == 0) result.Error = r.Error;
                }
                Events.Enqueue(new PollStartEvent { RoundId = roundId, Result = result });
            });
        }

        /// <summary>Nimmt eine fertige Chatnachricht zum Senden an. Bei mehr als <see cref="MaxPendingSends"/> fällt die älteste weg.</summary>
        public void EnqueueSend(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lock (_lock)
            {
                _outbox.AddLast(new KeyValuePair<DateTime, string>(DateTime.UtcNow, message));
                while (_outbox.Count > MaxPendingSends)
                {
                    _outbox.RemoveFirst();
                    _dropped++;
                }
            }
            _sendWake.Set();
        }

        // ── Ereignisstrom ───────────────────────────────────────────────

        private void SseLoop()
        {
            bool hadHello = false;
            while (!_stop)
            {
                LinkConfig cfg = SafeConfig();
                if (cfg == null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.BaseUrl) || cfg.Prefixes == null || cfg.Prefixes.Length == 0)
                {
                    SetDisconnected(null);
                    if (Wait(TimeSpan.FromSeconds(1))) break;
                    continue;
                }

                string key = cfg.Key;
                bool configChanged = false;
                try
                {
                    string url = Base(cfg) + "/api/chat/stream?client=" + Client + "&prefixes=" + Uri.EscapeDataString(string.Join(",", cfg.Prefixes));
                    HttpWebRequest req = CreateRequest(url, "GET");
                    req.Accept = "text/event-stream";
                    req.Timeout = 10000;
                    req.ReadWriteTimeout = (int)ReadTimeout.TotalMilliseconds;
                    lock (_lock) _sseRequest = req;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), new UTF8Encoding(false)))
                    {
                        var parser = new SseParser();
                        string line;
                        while (!_stop && (line = reader.ReadLine()) != null)
                        {
                            SseEvent ev = parser.Feed(line);
                            if (ev != null) HandleEvent(ev, ref hadHello);
                            LinkConfig now = SafeConfig();
                            if (now == null || !now.Enabled || now.Key != key) { configChanged = true; break; }
                        }
                    }
                    if (!_stop) SetDisconnected(configChanged ? null : "Server hat den Ereignisstrom beendet");
                }
                catch (WebException ex) { SetDisconnected(_stop ? null : Describe(ex)); }
                catch (IOException ex) { SetDisconnected(_stop ? null : "Verbindung abgebrochen: " + ex.Message); }
                catch (Exception ex) { SetDisconnected(_stop ? null : "Fehler im Ereignisstrom: " + ex.Message); }
                finally { lock (_lock) _sseRequest = null; }

                if (_stop) break;
                if (!configChanged && SafeConfig()?.Key == key && Wait(ReconnectDelay)) break;
            }
            SetDisconnected(null);
        }

        private void HandleEvent(SseEvent ev, ref bool hadHello)
        {
            switch (ev.Name)
            {
                case "hello":
                    HelloInfo info = ServerEvents.ParseHello(ev.Data);
                    if (hadHello) Interlocked.Increment(ref _reconnects);
                    Events.Enqueue(new HelloEvent { Info = info, Reconnect = hadHello });
                    hadHello = true;
                    _connected = true;
                    ConnectedSinceUtc = DateTime.UtcNow;
                    _lastError = null;
                    _pollWake.Set();
                    _sendWake.Set();
                    break;
                case "chat":
                    if (!_connected) return;
                    ChatLine line = ServerEvents.ParseChat(ev.Data);
                    if (line == null) { _log("chat-Ereignis ohne Pflichtfelder verworfen."); return; }
                    if (!_ids.TryAdd(line.MessageId)) { Interlocked.Increment(ref _duplicates); return; }
                    Events.Enqueue(new ChatEvent { Line = line });
                    // Ohne geladenen Spielstand arbeitet niemand die Warteschlange ab: begrenzen.
                    while (Events.Count > MaxQueuedEvents && Events.TryDequeue(out _)) { }
                    break;
                case "privacy":
                    PrivacyChange change = ServerEvents.ParsePrivacy(ev.Data);
                    if (change != null) Events.Enqueue(new PrivacyEvent { Change = change });
                    break;
                case "poll_result":
                    PollResult poll = ConsumerPolls.ParseResult(ev.Data);
                    if (poll == null) { _log("poll_result ohne Pflichtfelder verworfen."); return; }
                    if (!_ids.TryAdd("poll:" + poll.PollId)) return;
                    Events.Enqueue(new PollResultEvent { Result = poll });
                    break;
                case "raid":
                    RaidInfo raid = ConsumerPolls.ParseRaid(ev.Data);
                    if (raid == null) { _log("raid ohne Pflichtfelder verworfen."); return; }
                    if (!_ids.TryAdd("raid:" + raid.Id)) return;
                    Events.Enqueue(new RaidEvent { Raid = raid });
                    break;
                // Unbekannte Ereignisse ignorieren (Server-Spec chat-bruecke.md, Teil A).
            }
        }

        private void SetDisconnected(string error)
        {
            bool was = _connected;
            _connected = false;
            if (error != null)
            {
                if (_lastError != error) _log("Server nicht verbunden: " + error);
                _lastError = error;
            }
            if (was)
            {
                ConnectedSinceUtc = null;
                _channel = null;
                _channelProblem = "nicht verbunden";
            }
        }

        private void AbortStream()
        {
            HttpWebRequest req;
            lock (_lock) req = _sseRequest;
            try { req?.Abort(); } catch (Exception) { /* schon geschlossen */ }
        }

        // ── Kategorie und aktive Chatter ────────────────────────────────

        private void PollLoop()
        {
            while (!_stop)
            {
                _pollWake.WaitOne(PollInterval);
                if (_stop) break;
                if (!_connected) continue;
                LinkConfig cfg = SafeConfig();
                if (cfg == null) continue;
                PollChannel(cfg);
                bool gateOpen = _connected && CategoryGate.IsOpen(true, cfg.RequireCategory, _channel);
                if (gateOpen) PollActive(cfg);
                if (_connected && cfg.BetBonus) PollPrediction(cfg, gateOpen);
            }
        }

        private void PollChannel(LinkConfig cfg)
        {
            HttpResult r = Request(Base(cfg) + "/api/twitch/channel", "GET", null);
            if (r.Status == 200)
            {
                ChannelInfo info = ServerEvents.ParseChannel(r.Body);
                _channel = info;
                _channelProblem = info == null ? "Antwort von /api/twitch/channel unlesbar" : null;
            }
            else
            {
                _channel = null;
                _channelProblem = r.Status == 404
                    ? "Server kennt /api/twitch/channel nicht (Stand ohne Server-Issue #79)"
                    : "Kategorie nicht abrufbar: " + r.Describe();
            }
        }

        private void PollActive(LinkConfig cfg)
        {
            HttpResult r = Request(Base(cfg) + "/api/chat/active", "GET", null);
            if (r.Status != 200)
            {
                _log("Aktive Chatter nicht abrufbar: " + r.Describe());
                return;
            }
            List<ActiveChatter> all = ServerEvents.ParseActive(r.Body);
            if (all == null) { _log("Antwort von /api/chat/active unlesbar."); return; }
            var selected = CategoryGate.SelectForRefresh(all, cfg.RequireCategory, _channel, DateTime.UtcNow);
            LastActiveRefreshUtc = DateTime.UtcNow;
            Events.Enqueue(new ActiveEvent { Chatters = selected, TotalOnServer = all.Count });
        }

        // ── Wettbonus (Feature-Issue #11) ───────────────────────────────

        private string _lastPredictionSeen;

        /// <summary>
        /// Liest den letzten Stand der Wette (Server-Spec <c>wetten-abstimmungen.md</c>, Schritt 1, gemergt). Eine neu
        /// aufgelöste Wette wird nur gemeldet, wenn gerade RimWorld läuft und die Privat-Liste abrufbar ist; sonst gilt
        /// sie als gesehen und bringt keinen Bonus.
        /// </summary>
        private void PollPrediction(LinkConfig cfg, bool gateOpen)
        {
            HttpResult r = Request(Base(cfg) + "/api/twitch/prediction/current", "GET", null);
            if (r.Status != 200) return;
            PredictionResult result = BetBonus.Parse(r.Body);
            if (result == null || string.IsNullOrEmpty(result.Id) || !result.IsResolved) return;
            if (result.Id == _lastPredictionSeen) return;
            _lastPredictionSeen = result.Id;
            if (!BetBonus.IsFresh(result, DateTime.UtcNow))
            {
                _log("Letzte Wette ist schon länger beendet (" + (result.EndedAt?.ToLocalTime().ToString("dd.MM. HH:mm") ?? "Zeit unbekannt") + "): kein Wettbonus.");
                return;
            }
            if (!gateOpen) { _log("Wette aufgelöst, aber nicht in der Kategorie RimWorld: kein Wettbonus."); return; }

            HttpResult o = Request(Base(cfg) + "/api/privacy/optouts", "GET", null);
            HashSet<string> privateIds = o.Status == 200 ? ServerEvents.ParseOptouts(o.Body) : null;
            if (privateIds == null) { _log("Privat-Liste nicht abrufbar (" + o.Describe() + "): kein Wettbonus für diese Wette."); return; }
            Events.Enqueue(new PredictionEvent { Result = result, PrivateUserIds = privateIds });
        }

        // ── Senden ──────────────────────────────────────────────────────

        private void SendLoop()
        {
            while (!_stop)
            {
                _sendWake.WaitOne(500);
                while (!_stop && _connected)
                {
                    KeyValuePair<DateTime, string> next;
                    lock (_lock)
                    {
                        if (_outbox.Count == 0) break;
                        next = _outbox.First.Value;
                    }

                    if (DateTime.UtcNow - next.Key > MaxSendAge)
                    {
                        RemoveFirstIf(next);
                        Interlocked.Increment(ref _dropped);
                        _lastSendError = "Nachricht war zu alt (Server lange nicht erreichbar) und wurde verworfen";
                        continue;
                    }

                    TimeSpan wait = MinSendGap - (DateTime.UtcNow - _lastSendUtc);
                    if (wait > TimeSpan.Zero && Wait(wait)) return;

                    LinkConfig cfg = SafeConfig();
                    if (cfg == null) break;
                    HttpResult r = Request(Base(cfg) + "/api/chat/send", "POST", ServerEvents.BuildSendBody(Feature, next.Value));
                    _lastSendUtc = DateTime.UtcNow;

                    if (r.Status == 200)
                    {
                        RemoveFirstIf(next);
                        Interlocked.Increment(ref _sent);
                        if (ServerEvents.ParseSent(r.Body) == false)
                            _lastSendError = "Server hat angenommen, Twitch aber nicht gesendet (Grund im Serverprotokoll)";
                        else
                            _lastSendError = null;
                    }
                    else if (r.Status == 429)
                    {
                        if (Wait(TimeSpan.FromMilliseconds(ServerEvents.ParseRetryAfterMs(r.Body, 1000)))) return;
                    }
                    else if (r.Status == 0 || (r.Status >= 500 && r.Status != 503))
                    {
                        // Netz oder Server gestört: liegen lassen; Wiederholung, sobald wieder verbunden.
                        _lastSendError = "Senden fehlgeschlagen: " + r.Describe();
                        if (Wait(TimeSpan.FromSeconds(2))) return;
                    }
                    else
                    {
                        RemoveFirstIf(next);
                        Interlocked.Increment(ref _dropped);
                        _lastSendError = "Server lehnt ab: " + r.Describe();
                        _log("Chatnachricht abgelehnt: " + r.Describe());
                    }
                }
            }
        }

        private void RemoveFirstIf(KeyValuePair<DateTime, string> item)
        {
            lock (_lock)
            {
                if (_outbox.Count > 0 && _outbox.First.Value.Equals(item)) _outbox.RemoveFirst();
            }
        }

        // ── HTTP ────────────────────────────────────────────────────────

        private struct HttpResult
        {
            public int Status; // 0 = keine Antwort
            public string Body;
            public string Error;

            public string Describe()
            {
                if (Status == 0) return Error ?? "keine Antwort";
                string body = string.IsNullOrEmpty(Body) ? string.Empty : " " + (Body.Length > 200 ? Body.Substring(0, 200) : Body);
                return "HTTP " + Status + body;
            }
        }

        private HttpResult Request(string url, string method, string jsonBody)
        {
            try
            {
                HttpWebRequest req = CreateRequest(url, method);
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                req.Accept = "application/json";
                if (jsonBody != null)
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(jsonBody);
                    req.ContentType = "application/json";
                    req.ContentLength = bytes.Length;
                    using (Stream s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                    return new HttpResult { Status = (int)resp.StatusCode, Body = ReadBody(resp) };
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse resp)
            {
                using (resp) return new HttpResult { Status = (int)resp.StatusCode, Body = ReadBody(resp) };
            }
            catch (Exception ex)
            {
                return new HttpResult { Status = 0, Error = ex is WebException wex ? Describe(wex) : ex.Message };
            }
        }

        private static HttpWebRequest CreateRequest(string url, string method)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Proxy = null; // nur lokaler Server
            req.UserAgent = "TwitchCompanion-RimWorld";
            req.ServicePoint.ConnectionLimit = 8; // Ereignisstrom hält eine Verbindung dauerhaft
            return req;
        }

        private static string ReadBody(HttpWebResponse resp)
        {
            try
            {
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (Exception) { return null; }
        }

        private static string Describe(WebException ex)
        {
            if (ex.Response is HttpWebResponse resp)
            {
                string body = ReadBody(resp);
                if (body != null && body.Length > 200) body = body.Substring(0, 200);
                return "HTTP " + (int)resp.StatusCode + (string.IsNullOrEmpty(body) ? string.Empty : " " + body);
            }
            return ex.Status == WebExceptionStatus.ConnectFailure ? "Server nicht erreichbar" : ex.Status + ": " + ex.Message;
        }

        private static string Base(LinkConfig cfg) => (cfg.BaseUrl ?? string.Empty).Trim().TrimEnd('/');

        private LinkConfig SafeConfig()
        {
            try { return _config(); }
            catch (Exception) { return null; }
        }

        /// <summary>Wartet; wahr, wenn inzwischen gestoppt wurde.</summary>
        private bool Wait(TimeSpan span) => _stopEvent.WaitOne(span) || _stop;
    }
}
