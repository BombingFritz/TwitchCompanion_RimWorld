// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class JsonTests
    {
        [Fact]
        public void ParsesNestedValues()
        {
            var o = (Dictionary<string, object>)Json.Parse("{\"a\":1.5,\"b\":[true,false,null],\"c\":{\"d\":\"x\\\"y\\u00e4\"}}");
            Assert.Equal(1.5, o["a"]);
            Assert.Equal(new object[] { true, false, null }, (List<object>)o["b"]);
            Assert.Equal("x\"yä", Json.GetString(Json.GetObject(o, "c"), "d"));
        }

        [Fact]
        public void QuoteRoundTrips()
        {
            string text = "Zeile1\nZeile2 \"zitat\" \\ · ✓";
            Assert.Equal(text, Json.Parse(Json.Quote(text)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,]")]
        [InlineData("{} x")]
        public void InvalidJsonGivesNullObject(string text)
        {
            Assert.Null(Json.TryParseObject(text));
        }

        [Fact]
        public void TimeIsUtc()
        {
            var o = Json.TryParseObject("{\"t\":\"2026-11-21T18:30:00.000Z\"}");
            DateTime? t = Json.GetTime(o, "t");
            Assert.Equal(new DateTime(2026, 11, 21, 18, 30, 0, DateTimeKind.Utc), t);
            Assert.Equal(DateTimeKind.Utc, t.Value.Kind);
        }
    }

    public class SseParserTests
    {
        [Fact]
        public void ParsesEventsCommentsAndMultilineData()
        {
            var p = new SseParser();
            var events = new List<SseEvent>();
            foreach (string line in new[] { ": ping", "retry: 5000", "event: hello", "data: {\"a\":1}", "", "data: x", "data: y", "", "" })
            {
                SseEvent ev = p.Feed(line);
                if (ev != null) events.Add(ev);
            }
            Assert.Equal(2, events.Count);
            Assert.Equal("hello", events[0].Name);
            Assert.Equal("{\"a\":1}", events[0].Data);
            Assert.Equal("message", events[1].Name);
            Assert.Equal("x\ny", events[1].Data);
        }
    }

    public class ServerEventsTests
    {
        private const string Chat = "{\"messageId\":\"m1\",\"userId\":\"123\",\"userLogin\":\"Anna\",\"userName\":\"ANNA\",\"text\":\"$beer 2\","
            + "\"at\":\"2026-11-21T18:00:00Z\",\"badges\":{\"broadcaster\":false,\"moderator\":true,\"vip\":false,\"subscriber\":true},"
            + "\"private\":false,\"neuesFeld\":42}";

        [Fact]
        public void ParsesChatAndIgnoresUnknownFields()
        {
            ChatLine line = ServerEvents.ParseChat(Chat);
            Assert.NotNull(line);
            Assert.Equal("m1", line.MessageId);
            Assert.Equal("123", line.UserId);
            Assert.Equal("anna", line.Login);
            Assert.Equal("ANNA", line.NameForRics);
            Assert.Equal("$beer 2", line.Text);
            Assert.True(line.IsModerator);
            Assert.True(line.IsSubscriber);
            Assert.False(line.IsVip);
            Assert.False(line.IsPrivate);
            Assert.Equal(new DateTime(2026, 11, 21, 18, 0, 0, DateTimeKind.Utc), line.ReceivedAt);
        }

        [Fact]
        public void ChatWithoutUserIdIsRejected()
        {
            Assert.Null(ServerEvents.ParseChat(Chat.Replace("\"userId\":\"123\",", string.Empty)));
            Assert.Null(ServerEvents.ParseChat("kein json"));
        }

        [Fact]
        public void ChatWithoutBadgesHasNoRoles()
        {
            ChatLine line = ServerEvents.ParseChat("{\"messageId\":\"m\",\"userId\":\"1\",\"userLogin\":\"bob\",\"text\":\"!bal\",\"private\":true}");
            Assert.NotNull(line);
            Assert.False(line.IsModerator || line.IsVip || line.IsSubscriber || line.IsBroadcaster);
            Assert.True(line.IsPrivate);
            Assert.Equal("bob", line.NameForRics);
        }

        [Fact]
        public void ParsesPrivacy()
        {
            PrivacyChange c = ServerEvents.ParsePrivacy("{\"userId\":\"9\",\"userLogin\":\"x\",\"private\":true,\"at\":\"2026-11-21T18:00:00Z\"}");
            Assert.Equal("9", c.UserId);
            Assert.True(c.Private);
            Assert.Null(ServerEvents.ParsePrivacy("{\"userId\":\"9\"}"));
        }

        [Fact]
        public void ParsesChannelWithNullCategory()
        {
            ChannelInfo c = ServerEvents.ParseChannel("{\"category\":null,\"categoryChangedAt\":null,\"live\":false,\"updatedAt\":null}");
            Assert.NotNull(c);
            Assert.Null(c.Category);
            Assert.Null(ServerEvents.ParseChannel("{\"error\":\"x\"}"));
        }

        [Fact]
        public void ParsesActiveAndSkipsIncompleteEntries()
        {
            var list = ServerEvents.ParseActive("{\"updatedAt\":null,\"windowMinutes\":60,\"chatters\":["
                + "{\"id\":\"1\",\"login\":\"anna\",\"name\":\"Anna\",\"lastChatAt\":\"2026-11-21T18:00:00Z\",\"moderator\":false,\"vip\":true,\"subscriber\":false},"
                + "{\"id\":\"2\",\"login\":\"ohnezeit\"},"
                + "{\"login\":\"ohneid\",\"lastChatAt\":\"2026-11-21T18:00:00Z\"}]}");
            Assert.Single(list);
            Assert.Equal("Anna", list[0].NameForRics);
            Assert.True(list[0].IsVip);
            Assert.Null(ServerEvents.ParseActive("{}"));
        }

        [Fact]
        public void RetryAfterIsBounded()
        {
            Assert.Equal(734, ServerEvents.ParseRetryAfterMs("{\"ok\":false,\"retryAfterMs\":733.2}", 1000));
            Assert.Equal(1000, ServerEvents.ParseRetryAfterMs("{}", 1000));
            Assert.Equal(10000, ServerEvents.ParseRetryAfterMs("{\"retryAfterMs\":999999}", 1000));
        }

        [Fact]
        public void SendBodyIsValidJson()
        {
            var o = Json.TryParseObject(ServerEvents.BuildSendBody("rimworld", "@anna \"Bier\" · 5 Coins"));
            Assert.Equal("rimworld", Json.GetString(o, "feature"));
            Assert.Equal("@anna \"Bier\" · 5 Coins", Json.GetString(o, "message"));
        }
    }

    public class CategoryGateTests
    {
        [Theory]
        [InlineData("RimWorld", true)]
        [InlineData("rimworld ", true)]
        [InlineData("Just Chatting", false)]
        [InlineData(null, false)]
        public void RecognisesRimWorld(string category, bool expected)
        {
            Assert.Equal(expected, CategoryGate.IsRimWorld(category));
        }

        [Fact]
        public void GateNeedsConnectionAndCategory()
        {
            var rim = new ChannelInfo { Category = "RimWorld" };
            var chat = new ChannelInfo { Category = "Just Chatting" };
            Assert.False(CategoryGate.IsOpen(false, true, rim));
            Assert.True(CategoryGate.IsOpen(true, true, rim));
            Assert.False(CategoryGate.IsOpen(true, true, chat));
            Assert.False(CategoryGate.IsOpen(true, true, null));
            Assert.True(CategoryGate.IsOpen(true, false, null));
        }

        [Fact]
        public void RefreshSkipsChatsBeforeCategoryChange()
        {
            var now = new DateTime(2026, 11, 21, 19, 0, 0, DateTimeKind.Utc);
            var channel = new ChannelInfo { Category = "RimWorld", CategoryChangedAt = now.AddMinutes(-10) };
            var chatters = new List<ActiveChatter>
            {
                new ActiveChatter { UserId = "1", Login = "vorher", LastChatAt = now.AddMinutes(-20) },
                new ActiveChatter { UserId = "2", Login = "nachher", LastChatAt = now.AddMinutes(-5) },
                new ActiveChatter { UserId = "3", Login = "zukunft", LastChatAt = now.AddMinutes(5) },
            };
            var selected = CategoryGate.SelectForRefresh(chatters, true, channel, now);
            Assert.Single(selected);
            Assert.Equal("nachher", selected[0].Login);
            Assert.Equal(2, CategoryGate.SelectForRefresh(chatters, false, channel, now).Count);
        }

        [Fact]
        public void RecentIdsDropsDuplicatesWithinCapacity()
        {
            var ids = new RecentIds(2);
            Assert.True(ids.TryAdd("a"));
            Assert.False(ids.TryAdd("a"));
            Assert.True(ids.TryAdd("b"));
            Assert.True(ids.TryAdd("c")); // "a" fällt heraus
            Assert.True(ids.TryAdd("a"));
            Assert.True(ids.TryAdd(null));
        }

        [Fact]
        public void PrivacyNoticeOncePerStream()
        {
            var p = new PrivacyNotices();
            Assert.True(p.ShouldNotify("1"));
            Assert.False(p.ShouldNotify("1"));
            p.Reset();
            Assert.True(p.ShouldNotify("1"));
        }
    }
}
