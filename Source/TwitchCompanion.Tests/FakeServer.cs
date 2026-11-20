// SPDX-License-Identifier: AGPL-3.0-or-later
// Attrappe des Servers nach der Spec „Chat-Brücke“ (Entwurf), nur für Tests auf localhost.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TwitchCompanion.RimWorld.Tests
{
    internal sealed class FakeServer : IDisposable
    {
        public int Port { get; }
        public string BaseUrl => "http://localhost:" + Port;

        public readonly ConcurrentQueue<string> StreamQueries = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<(DateTime at, string body)> Sends = new ConcurrentQueue<(DateTime, string)>();
        public volatile string ChannelJson = "{\"category\":\"RimWorld\",\"categoryChangedAt\":null,\"live\":true,\"updatedAt\":null}";
        public volatile bool ChannelMissing;
        public volatile string ActiveJson = "{\"updatedAt\":null,\"windowMinutes\":60,\"chatters\":[]}";
        public int TooEarlyResponses; // so viele Sendungen bekommen zuerst 429
        public volatile string PredictionJson = "{\"status\":\"WAITING\"}";
        public volatile string OptoutsJson = "{\"userIds\":[]}";
        public volatile int OptoutsStatus = 200;
        public int PredictionRequests;
        public int ActiveRequests;

        private HttpListener _listener;
        private readonly List<HttpListenerResponse> _streams = new List<HttpListenerResponse>();
        private readonly object _lock = new object();

        public FakeServer(int port = 0)
        {
            Port = port == 0 ? FreePort() : port;
            Start();
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            var t = new Thread(() => Loop(_listener)) { IsBackground = true };
            t.Start();
        }

        public void Stop()
        {
            CloseStreams();
            try { _listener?.Stop(); _listener?.Close(); } catch (Exception) { }
        }

        public void Dispose() => Stop();

        public int OpenStreams { get { lock (_lock) return _streams.Count; } }

        public void PushEvent(string name, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("event: " + name + "\ndata: " + json + "\n\n");
            lock (_lock)
            {
                foreach (var r in _streams.ToArray())
                {
                    try { r.OutputStream.Write(bytes, 0, bytes.Length); r.OutputStream.Flush(); }
                    catch (Exception) { _streams.Remove(r); }
                }
            }
        }

        public void CloseStreams()
        {
            lock (_lock)
            {
                foreach (var r in _streams) { try { r.Abort(); } catch (Exception) { } }
                _streams.Clear();
            }
        }

        private void Loop(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch (Exception) { return; }
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath;
            try
            {
                if (path == "/api/chat/stream")
                {
                    StreamQueries.Enqueue(ctx.Request.Url.Query);
                    var r = ctx.Response;
                    r.ContentType = "text/event-stream";
                    r.SendChunked = true;
                    byte[] hello = Encoding.UTF8.GetBytes("retry: 5000\n\nevent: hello\ndata: {\"server\":\"twitch-companion-js-server\",\"version\":\"test\",\"client\":\"rimworld\",\"commands\":[],\"prefixes\":[\"!\",\"$\"],\"botLogin\":null}\n\n");
                    r.OutputStream.Write(hello, 0, hello.Length);
                    r.OutputStream.Flush();
                    lock (_lock) _streams.Add(r);
                    return;
                }
                if (path == "/api/twitch/channel")
                {
                    if (ChannelMissing) Reply(ctx, 404, "{\"error\":\"not found\"}");
                    else Reply(ctx, 200, ChannelJson);
                    return;
                }
                if (path == "/api/chat/active")
                {
                    Interlocked.Increment(ref ActiveRequests);
                    Reply(ctx, 200, ActiveJson);
                    return;
                }
                if (path == "/api/twitch/prediction/current")
                {
                    Interlocked.Increment(ref PredictionRequests);
                    Reply(ctx, 200, PredictionJson);
                    return;
                }
                if (path == "/api/privacy/optouts")
                {
                    Reply(ctx, OptoutsStatus, OptoutsStatus == 200 ? OptoutsJson : "{\"error\":\"x\"}");
                    return;
                }
                if (path == "/api/chat/send" && ctx.Request.HttpMethod == "POST")
                {
                    string body = new StreamReader(ctx.Request.InputStream, Encoding.UTF8).ReadToEnd();
                    if (Interlocked.Decrement(ref TooEarlyResponses) >= 0)
                    {
                        Reply(ctx, 429, "{\"ok\":false,\"sent\":false,\"error\":\"too early\",\"retryAfterMs\":300}");
                        return;
                    }
                    Sends.Enqueue((DateTime.UtcNow, body));
                    Reply(ctx, 200, "{\"ok\":true,\"sent\":true,\"sender\":\"bot\"}");
                    return;
                }
                Reply(ctx, 404, "{\"error\":\"not found\"}");
            }
            catch (Exception) { /* Verbindung weg */ }
        }

        private static void Reply(HttpListenerContext ctx, int status, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
