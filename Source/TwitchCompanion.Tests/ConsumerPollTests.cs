// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    /// <summary>Abstimmung starten und Raids (Server-Spec chat-bruecke.md, Teil C und D); nur gegen die Attrappe.</summary>
    public class ConsumerPollTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);
        private static EventCandidate C(string label) => new EventCandidate { DefName = label, Label = label, Karma = EventKarma.Neutral };

        private static VoteRound Round(params string[] labels) => new VoteRound(labels.Select(C).ToList(), T0, 120) { ChannelPointsPerVote = 3000 };

        [Fact]
        public void StartBodyHaeltDieTwitchGrenzenEin()
        {
            var r = Round("Handelskarawane kommt vorbei heute", "Grippe", "Nichts passiert");
            var o = Json.TryParseObject(ConsumerPolls.BuildStartBody("rimworld", r));
            Assert.Equal("rimworld", Json.GetString(o, "client"));
            Assert.Equal(VoteRound.DefaultTitle, Json.GetString(o, "title"));
            Assert.Equal(120, Json.GetNumber(o, "durationS"));
            Assert.Equal(3000, Json.GetNumber(o, "channelPointsPerVote"));
            var choices = Json.GetArray(o, "choices").Cast<string>().ToList();
            Assert.Equal(3, choices.Count);
            Assert.All(choices, c => Assert.True(c.Length <= 25));
            r.ChannelPointsPerVote = 0;
            Assert.False(Json.TryParseObject(ConsumerPolls.BuildStartBody("rimworld", r)).ContainsKey("channelPointsPerVote"));
        }

        [Fact]
        public void GleicheTitelWerdenEindeutig()
        {
            var r = Round("Grippe", "Grippe", "Transportkapsel stürzt ab und so weiter");
            var t = r.OptionTitles;
            Assert.Equal(3, t.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal("Grippe 2", t[1]);
            Assert.All(t, x => Assert.True(x.Length <= 25));
        }

        [Fact]
        public void StartAntwortenWerdenGelesen()
        {
            var ok = ConsumerPolls.ParseStart(200, "{\"ok\":true,\"pollId\":\"p1\",\"startedAt\":\"2026-11-20T18:00:00Z\",\"endsAt\":\"2026-11-20T18:02:00Z\",\"neu\":1}");
            Assert.True(ok.Ok);
            Assert.Equal("p1", ok.PollId);
            Assert.Equal(T0.AddMinutes(2), ok.EndsAt.Value.ToUniversalTime());

            var active = ConsumerPolls.ParseStart(409, "{\"ok\":false,\"reason\":\"poll_active\",\"error\":\"x\",\"activePollId\":\"q\",\"endsAt\":\"2026-11-20T18:01:00Z\"}");
            Assert.False(active.Ok);
            Assert.Equal("poll_active", active.Reason);
            Assert.Equal(TimeSpan.FromSeconds(90), ConsumerPolls.RetryAfter(active, T0));

            var fast = ConsumerPolls.ParseStart(429, "{\"ok\":false,\"reason\":\"too_fast\",\"error\":\"x\",\"retryAfterMs\":3000}");
            Assert.Equal(TimeSpan.FromSeconds(3), ConsumerPolls.RetryAfter(fast, T0));

            var scope = ConsumerPolls.ParseStart(403, "{\"ok\":false,\"reason\":\"missing_scope\",\"error\":\"x\"}");
            Assert.Null(ConsumerPolls.RetryAfter(scope, T0));
            Assert.Contains("channel:manage:polls", ConsumerPolls.Describe(scope));

            Assert.Equal("not_supported", ConsumerPolls.ParseStart(404, "{\"error\":\"not found\"}").Reason);
            Assert.Equal("unreachable", ConsumerPolls.ParseStart(0, null).Reason);
            Assert.Equal("unreadable", ConsumerPolls.ParseStart(200, "{\"ok\":true}").Reason);   // pollId fehlt
        }

        private const string ResultJson = "{\"pollId\":\"p1\",\"title\":\"t\",\"status\":\"COMPLETED\",\"choices\":["
            + "{\"id\":\"a\",\"title\":\"Grippe\",\"votes\":5,\"channelPointsVotes\":1},{\"id\":\"b\",\"title\":\"Handel\",\"votes\":5,\"channelPointsVotes\":0},"
            + "{\"id\":\"c\",\"title\":\"Nichts passiert\",\"votes\":1,\"channelPointsVotes\":0}],\"totalVotes\":11,"
            + "\"winners\":[{\"id\":\"a\",\"title\":\"Grippe\",\"votes\":5},{\"id\":\"b\",\"title\":\"Handel\",\"votes\":5}],"
            + "\"startedAt\":\"2026-11-20T18:00:00Z\",\"endedAt\":\"2026-11-20T18:02:00Z\",\"at\":\"2026-11-20T18:02:01Z\"}";

        [Fact]
        public void ErgebnisMitGleichstandLostUnterDenGewinnern()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
            {
                var r = Round("Grippe", "Handel", "Nichts passiert");
                r.ServerPollId = "p1";
                EventCandidate w = r.ApplyServerResult(ConsumerPolls.ParseResult(ResultJson), new Random(seed));
                Assert.Contains(w.DefName, new[] { "Grippe", "Handel" });
                Assert.Equal(new[] { 5, 5, 1 }, r.Votes);
                seen.Add(w.DefName);
            }
            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void AbgebrochenGibtKeinenGewinner()
        {
            var r = Round("Grippe", "Handel", "Nichts passiert");
            r.ServerPollId = "p1";
            Assert.Null(r.ApplyServerResult(ConsumerPolls.ParseResult(ResultJson.Replace("COMPLETED", "TERMINATED")), new Random(1)));
            Assert.Equal(VoteState.Cancelled, r.State);
        }

        [Fact]
        public void ErgebnisOhnePflichtfelderWirdVerworfen()
        {
            Assert.Null(ConsumerPolls.ParseResult("{\"status\":\"COMPLETED\"}"));
            Assert.Null(ConsumerPolls.ParseResult("kein json"));
        }

        [Fact]
        public void RaidWirdGelesen()
        {
            var r = ConsumerPolls.ParseRaid("{\"id\":\"m1\",\"at\":\"2026-11-20T18:00:00Z\",\"fromId\":\"9\",\"fromLogin\":\"spargelkanal\",\"fromName\":\"SpargelKanal\",\"viewers\":12,\"extra\":true}");
            Assert.Equal(12, r.Viewers);
            Assert.Equal("SpargelKanal", r.NameForGame);
            Assert.Equal("spargelkanal", new RaidInfo { Id = "x", FromLogin = "spargelkanal", FromName = "スパ" }.NameForGame);
            Assert.Null(ConsumerPolls.ParseRaid("{\"id\":\"m1\"}"));
        }

        [Fact]
        public void PlauderndeTretenBeiOhneRaiderUndOhneAlte()
        {
            var raid = new RaidInfo { Id = "m", FromLogin = "gast" };
            var chatters = new List<ActiveChatter>
            {
                new ActiveChatter { UserId = "1", Login = "anna", DisplayName = "Anna", LastChatAt = T0.AddMinutes(-2) },
                new ActiveChatter { UserId = "2", Login = "gast", DisplayName = "Gast", LastChatAt = T0.AddMinutes(-1) },
                new ActiveChatter { UserId = "3", Login = "bert", DisplayName = "Bert", LastChatAt = T0.AddMinutes(-30) },
            };
            Assert.Equal(new[] { "Anna" }, RaidJoiners.Select(chatters, raid, T0, TimeSpan.FromMinutes(10)));
        }

        [Theory]
        [InlineData(88f, 1f, 10, 2)]     // Stream 20.11.2026: Bedrohung 88, 10 Namen → 2 statt 10
        [InlineData(88f, 0.5f, 10, 1)]
        [InlineData(20f, 1f, 10, 1)]     // mindestens einer
        [InlineData(1000f, 1f, 10, 10)]  // nie mehr als Namen
        [InlineData(1000f, 2f, 50, 44)]
        public void KaempferNachBedrohung(float points, float factor, int names, int expected) =>
            Assert.Equal(expected, RaidJoiners.MaxFighters(points, factor, names));

        [Fact]
        public void KuerzenBehaeltDenRaidendenKanal()
        {
            var names = new List<string> { "TrakkarLP", "a", "b", "c", "d" };
            for (int seed = 0; seed < 20; seed++)
            {
                var kept = RaidJoiners.Trim(names, 2, new Random(seed));
                Assert.Equal(2, kept.Count);
                Assert.Equal("TrakkarLP", kept[0]);
            }
            Assert.Equal(names, RaidJoiners.Trim(names, 10, new Random(1)));
        }

        // ── gegen die Attrappe ──────────────────────────────────────────

        private static ServerLink NewLink(FakeServer server)
        {
            var cfg = new LinkConfig { Enabled = true, BaseUrl = server.BaseUrl, Prefixes = new[] { "!", "$" } };
            var link = new ServerLink(() => cfg, _ => { }) { ReconnectDelay = TimeSpan.FromMilliseconds(200), PollInterval = TimeSpan.FromMilliseconds(300) };
            link.Start();
            return link;
        }

        private static void WaitFor(Func<bool> condition, string what, int ms = 5000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (condition()) return; Thread.Sleep(20); }
            Assert.Fail("Zeitüberschreitung: " + what);
        }

        [Fact]
        public void StartGehtAnDenServerUndAntwortKommtAlsEreignis()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected, "verbunden");
                var round = Round("Grippe", "Handel", "Nichts passiert");
                link.StartPoll(round.Id, ConsumerPolls.BuildStartBody(ServerLink.Client, round));
                WaitFor(() => link.Events.OfType<PollStartEvent>().Any(), "Antwort");
                var ev = link.Events.OfType<PollStartEvent>().Single();
                Assert.True(ev.Result.Ok);
                Assert.Equal(round.Id, ev.RoundId);
                Assert.True(server.PollStarts.TryPeek(out string body));
                Assert.Equal("rimworld", Json.GetString(Json.TryParseObject(body), "client"));
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void AeltererServerOhneStartWirdErkannt()
        {
            using var server = new FakeServer { PollStartStatus = 404, PollStartJson = "{\"error\":\"not found\"}" };
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected, "verbunden");
                link.StartPoll("r", "{}");
                WaitFor(() => link.Events.OfType<PollStartEvent>().Any(), "Antwort");
                Assert.Equal("not_supported", link.Events.OfType<PollStartEvent>().Single().Result.Reason);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void OhneVerbindungKeinStartversuch()
        {
            using var server = new FakeServer();
            int port = server.Port;
            server.Stop();
            var cfg = new LinkConfig { Enabled = true, BaseUrl = "http://localhost:" + port, Prefixes = new[] { "!" } };
            var link = new ServerLink(() => cfg, _ => { });
            link.StartPoll("r", "{}");
            WaitFor(() => link.Events.OfType<PollStartEvent>().Any(), "Antwort");
            Assert.Equal("unreachable", link.Events.OfType<PollStartEvent>().Single().Result.Reason);
        }

        [Fact]
        public void ErgebnisUndRaidKommenGenauEinmal()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected && server.OpenStreams == 1, "verbunden");
                server.PushEvent("poll_result", ResultJson);
                server.PushEvent("poll_result", ResultJson);
                string raid = "{\"id\":\"m1\",\"at\":\"2026-11-20T18:00:00Z\",\"fromId\":\"9\",\"fromLogin\":\"gast\",\"fromName\":\"Gast\",\"viewers\":12}";
                server.PushEvent("raid", raid);
                server.PushEvent("raid", raid);
                server.PushEvent("raid", "{\"id\":\"m2\"}");          // unvollständig
                server.PushEvent("noch_unbekannt", "{}");
                server.PushEvent("chat", "{\"messageId\":\"z\",\"userId\":\"1\",\"userLogin\":\"a\",\"userName\":\"A\",\"text\":\"!pawn\",\"at\":\"2026-11-20T18:00:00Z\",\"badges\":{},\"private\":false}");
                WaitFor(() => link.Events.OfType<ChatEvent>().Any(), "Chat nach den übrigen Ereignissen");
                Assert.Single(link.Events.OfType<PollResultEvent>());
                Assert.Equal(12, link.Events.OfType<RaidEvent>().Single().Raid.Viewers);
            }
            finally { link.Stop(); }
        }
    }
}
