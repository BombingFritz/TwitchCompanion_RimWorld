// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    /// <summary>Verbindung gegen die Attrappe (<see cref="FakeServer"/>); nie gegen den echten Server.</summary>
    public class ServerLinkTests
    {
        private static ServerLink NewLink(FakeServer server, bool requireCategory = true, string baseUrl = null, bool betBonus = false)
        {
            var cfg = new LinkConfig { Enabled = true, BaseUrl = baseUrl ?? server.BaseUrl, Prefixes = new[] { "!", "$" }, RequireCategory = requireCategory, BetBonus = betBonus };
            var link = new ServerLink(() => cfg, _ => { })
            {
                ReconnectDelay = TimeSpan.FromMilliseconds(200),
                PollInterval = TimeSpan.FromMilliseconds(300),
                MinSendGap = TimeSpan.FromMilliseconds(200),
            };
            link.Start();
            return link;
        }

        private static void WaitFor(Func<bool> condition, string what, int ms = 5000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until)
            {
                if (condition()) return;
                Thread.Sleep(20);
            }
            Assert.Fail("Zeitüberschreitung: " + what);
        }

        private static List<T> Drain<T>(ServerLink link)
        {
            var result = new List<T>();
            var rest = new List<object>();
            while (link.Events.TryDequeue(out object ev))
            {
                if (ev is T t) result.Add(t); else rest.Add(ev);
            }
            foreach (var ev in rest) link.Events.Enqueue(ev);
            return result;
        }

        private static string ChatJson(string id, string text, string userId = "42", bool priv = false)
            => "{\"messageId\":\"" + id + "\",\"userId\":\"" + userId + "\",\"userLogin\":\"anna\",\"userName\":\"Anna\",\"text\":\"" + text
               + "\",\"at\":\"2026-11-21T18:00:00Z\",\"badges\":{\"broadcaster\":false,\"moderator\":false,\"vip\":false,\"subscriber\":false},\"private\":" + (priv ? "true" : "false") + "}";

        [Fact]
        public void ConnectsAndRegistersPrefixes()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected, "hello");
                Assert.True(server.StreamQueries.TryPeek(out string query));
                var args = System.Web.HttpUtility.ParseQueryString(query);
                Assert.Equal("rimworld", args["client"]);
                Assert.Equal("!,$", args["prefixes"]);
                Assert.Single(Drain<HelloEvent>(link));
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void DeliversChatOnceEvenIfDuplicated()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected && server.OpenStreams == 1, "verbunden");
                server.PushEvent("chat", ChatJson("m1", "!bal"));
                server.PushEvent("chat", ChatJson("m1", "!bal"));
                server.PushEvent("chat", ChatJson("m2", "$beer"));
                server.PushEvent("unbekannt", "{}");
                WaitFor(() => link.DuplicateCount == 1 && link.Events.OfType<ChatEvent>().Count() == 2, "zwei Chat-Ereignisse");
                var chats = Drain<ChatEvent>(link);
                Assert.Equal(new[] { "!bal", "$beer" }, chats.Select(c => c.Line.Text));
                Assert.Equal("42", chats[0].Line.UserId);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void PrivacyEventArrives()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected && server.OpenStreams == 1, "verbunden");
                server.PushEvent("privacy", "{\"userId\":\"7\",\"userLogin\":\"x\",\"private\":true,\"at\":\"2026-11-21T18:00:00Z\"}");
                WaitFor(() => link.Events.OfType<PrivacyEvent>().Any(), "privacy");
                Assert.True(Drain<PrivacyEvent>(link)[0].Change.Private);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void ServerNotReachableIsReported()
        {
            using var server = new FakeServer();
            int port = server.Port;
            server.Stop();
            var link = NewLink(server, baseUrl: "http://localhost:" + port);
            try
            {
                WaitFor(() => link.LastError != null, "Fehlermeldung");
                Assert.False(link.Connected);
                Assert.False(link.GateOpen);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void ReconnectsAfterServerRestart()
        {
            var server = new FakeServer();
            int port = server.Port;
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected, "erste Verbindung");
                server.Stop();
                WaitFor(() => !link.Connected, "Trennung bemerkt");
                server = new FakeServer(port);
                WaitFor(() => link.Connected && link.ReconnectCount == 1, "Wiederverbindung");
                var hellos = Drain<HelloEvent>(link);
                Assert.Equal(2, hellos.Count);
                Assert.True(hellos[1].Reconnect);
                Assert.True(server.StreamQueries.Count >= 1); // Befehle neu angemeldet
            }
            finally { link.Stop(); server.Dispose(); }
        }

        [Fact]
        public void SendRetriesAfter429AndKeepsOrderAndGap()
        {
            using var server = new FakeServer { TooEarlyResponses = 1 };
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Connected, "verbunden");
                link.EnqueueSend("eins");
                link.EnqueueSend("zwei");
                WaitFor(() => server.Sends.Count == 2, "zwei Sendungen");
                var sends = server.Sends.ToArray();
                Assert.Equal("eins", Json.GetString(Json.TryParseObject(sends[0].body), "message"));
                Assert.Equal("rimworld", Json.GetString(Json.TryParseObject(sends[0].body), "feature"));
                Assert.Equal("zwei", Json.GetString(Json.TryParseObject(sends[1].body), "message"));
                Assert.True((sends[1].at - sends[0].at).TotalMilliseconds >= 150);
                Assert.Equal(2, link.SentCount);
                Assert.Equal(0, link.PendingSends);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void MissingChannelEndpointClosesGateUnlessDisabled()
        {
            using var server = new FakeServer { ChannelMissing = true };
            var strict = NewLink(server);
            try
            {
                WaitFor(() => strict.Connected && strict.ChannelProblem != null && strict.ChannelProblem.Contains("#79"), "Hinweis auf fehlende Fähigkeit");
                Assert.False(strict.GateOpen);
                Thread.Sleep(500);
                Assert.Equal(0, server.ActiveRequests);
            }
            finally { strict.Stop(); }

            var loose = NewLink(server, requireCategory: false);
            try
            {
                WaitFor(() => loose.Connected, "verbunden");
                Assert.True(loose.GateOpen);
            }
            finally { loose.Stop(); }
        }

        [Fact]
        public void OtherCategoryClosesGateAndSkipsActive()
        {
            using var server = new FakeServer { ChannelJson = "{\"category\":\"Just Chatting\",\"categoryChangedAt\":null,\"live\":true}" };
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Channel != null, "Kategorie");
                Assert.False(link.GateOpen);
                Thread.Sleep(500);
                Assert.Equal(0, server.ActiveRequests);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void ActiveChattersArriveInRimWorld()
        {
            string recent = DateTime.UtcNow.AddMinutes(-2).ToString("o");
            string before = DateTime.UtcNow.AddMinutes(-30).ToString("o");
            string changed = DateTime.UtcNow.AddMinutes(-10).ToString("o");
            using var server = new FakeServer
            {
                ChannelJson = "{\"category\":\"RimWorld\",\"categoryChangedAt\":\"" + changed + "\",\"live\":true}",
                ActiveJson = "{\"chatters\":[{\"id\":\"1\",\"login\":\"anna\",\"name\":\"Anna\",\"lastChatAt\":\"" + recent + "\",\"subscriber\":true},"
                    + "{\"id\":\"2\",\"login\":\"bob\",\"name\":\"bob\",\"lastChatAt\":\"" + before + "\"}]}",
            };
            var link = NewLink(server);
            try
            {
                WaitFor(() => link.Events.OfType<ActiveEvent>().Any(), "aktive Chatter");
                Assert.True(link.GateOpen);
                ActiveEvent ev = Drain<ActiveEvent>(link)[0];
                Assert.Equal(2, ev.TotalOnServer);
                Assert.Single(ev.Chatters);
                Assert.Equal("anna", ev.Chatters[0].Login);
                Assert.True(ev.Chatters[0].IsSubscriber);
            }
            finally { link.Stop(); }
        }

        private const string ResolvedPrediction = "{\"predictionId\":\"p1\",\"status\":\"RESOLVED\",\"winningOutcomeId\":\"o1\",\"outcomes\":["
            + "{\"id\":\"o1\",\"topPredictors\":[{\"userId\":\"42\",\"userLogin\":\"anna\",\"userName\":\"Anna\",\"channelPointsUsed\":500,\"channelPointsWon\":2000},"
            + "{\"userId\":\"7\",\"userLogin\":\"privo\",\"userName\":\"Privo\",\"channelPointsUsed\":100,\"channelPointsWon\":400}]},"
            + "{\"id\":\"o2\",\"topPredictors\":[{\"userId\":\"9\",\"userLogin\":\"bert\",\"userName\":\"Bert\",\"channelPointsUsed\":800,\"channelPointsWon\":0}]}]}";

        [Fact]
        public void ResolvedPredictionIsReportedOnlyOnce()
        {
            using var server = new FakeServer();
            server.PredictionJson = ResolvedPrediction;
            var link = NewLink(server, betBonus: true);
            try
            {
                int total = 0;
                WaitFor(() => (total += Drain<PredictionEvent>(link).Count) > 0, "erstes PredictionEvent");
                int before = server.PredictionRequests;
                WaitFor(() => server.PredictionRequests >= before + 3, "weitere Abfragen");
                total += Drain<PredictionEvent>(link).Count;
                Assert.Equal(1, total);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void PredictionEventCarriesWinnersAndPrivateIds()
        {
            using var server = new FakeServer();
            server.OptoutsJson = "{\"userIds\":[\"7\"]}";
            server.PredictionJson = ResolvedPrediction;
            var link = NewLink(server, betBonus: true);
            try
            {
                List<PredictionEvent> events = null;
                WaitFor(() => (events = Drain<PredictionEvent>(link)).Count > 0, "PredictionEvent");
                PredictionEvent ev = Assert.Single(events);
                Assert.Equal("p1", ev.Result.Id);
                Assert.Equal(2, ev.Result.Winners.Count);
                Assert.Contains("7", ev.PrivateUserIds);
            }
            finally { link.Stop(); }
        }

        [Fact]
        public void NoPredictionEventWithoutPrivacyListOrOutsideRimWorld()
        {
            using var server = new FakeServer();
            server.PredictionJson = ResolvedPrediction;
            server.OptoutsStatus = 500;
            var link = NewLink(server, betBonus: true);
            try
            {
                WaitFor(() => server.PredictionRequests >= 3, "Abfragen");
                Assert.Empty(Drain<PredictionEvent>(link));
            }
            finally { link.Stop(); }

            using var server2 = new FakeServer();
            server2.PredictionJson = ResolvedPrediction;
            server2.ChannelJson = "{\"category\":\"Just Chatting\",\"categoryChangedAt\":null,\"live\":true,\"updatedAt\":null}";
            var link2 = NewLink(server2, betBonus: true);
            try
            {
                WaitFor(() => server2.PredictionRequests >= 3, "Abfragen");
                Assert.Empty(Drain<PredictionEvent>(link2));
            }
            finally { link2.Stop(); }
        }

        [Fact]
        public void NoPredictionPollingWhenBetBonusOff()
        {
            using var server = new FakeServer();
            var link = NewLink(server);
            try
            {
                WaitFor(() => server.ActiveRequests >= 2, "Abfragen");
                Assert.Equal(0, server.PredictionRequests);
            }
            finally { link.Stop(); }
        }
    }
}
