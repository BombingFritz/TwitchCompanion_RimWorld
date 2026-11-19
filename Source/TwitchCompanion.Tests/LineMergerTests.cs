// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

public class LineMergerTests
{
    private static readonly DateTime T0 = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WartetRuhezeitAbUndVerbindetZeilen()
    {
        var m = new LineMerger();
        m.Add("Neue Abstimmung", T0);
        m.Add("[1] Raid", T0.AddMilliseconds(300));
        m.Add("[2] Händler", T0.AddMilliseconds(600));

        Assert.Empty(m.Flush(T0.AddMilliseconds(1500)));          // erst 0,9 s nach der letzten Zeile
        var sent = m.Flush(T0.AddMilliseconds(1700));
        Assert.Equal(new[] { "Neue Abstimmung · [1] Raid · [2] Händler" }, sent);
        Assert.Empty(m.Flush(T0.AddSeconds(5)));                 // nichts doppelt
    }

    [Fact]
    public void TeiltBeiMaximalLaenge()
    {
        var m = new LineMerger(maxMessageLength: 30);
        m.Add("aaaaaaaaaa bbbbbbbbbb", T0);     // 21
        m.Add("cccccccccc", T0);                 // 21 + 3 + 10 = 34 > 30
        var sent = m.Flush(T0, force: true);
        Assert.Equal(new[] { "aaaaaaaaaa bbbbbbbbbb", "cccccccccc" }, sent);
        Assert.All(sent, s => Assert.True(s.Length <= 30));
    }

    [Fact]
    public void LangeZeileWirdAnWortgrenzenGeteilt()
    {
        var m = new LineMerger(maxMessageLength: 20);
        m.Add("eins zwei drei vier fünf sechs sieben acht", T0);
        var sent = m.Flush(T0, force: true);
        Assert.All(sent, s => Assert.True(s.Length <= 20, s));
        Assert.Equal("eins zwei drei vier fünf sechs sieben acht", string.Join(" ", sent.SelectMany(s => s.Split(" · "))));
    }

    [Fact]
    public void ZeilenumbruecheUndLeereZeilen()
    {
        var m = new LineMerger();
        m.Add("  ", T0);
        m.Add(null, T0);
        m.Add("Zeile\r\nzwei\tdrei  ", T0);
        Assert.Equal(new[] { "Zeile zwei drei" }, m.Flush(T0, force: true));
    }

    [Fact]
    public void ZuVieleZeilenVerwerfenDieAeltesten()
    {
        var m = new LineMerger(maxPendingLines: 3);
        for (int i = 1; i <= 5; i++) m.Add("z" + i, T0);
        Assert.Equal(2, m.TakeDroppedCount());
        Assert.Equal(0, m.TakeDroppedCount());
        Assert.Equal(new[] { "z3 · z4 · z5" }, m.Flush(T0, force: true));
    }

    [Fact]
    public void ThreadSicherBeiGleichzeitigemHinzufuegen()
    {
        var m = new LineMerger(maxPendingLines: 10000);
        System.Threading.Tasks.Parallel.For(0, 1000, i => m.Add("x" + i, T0));
        var sent = m.Flush(T0, force: true);
        Assert.Equal(1000, sent.SelectMany(s => s.Split(" · ")).Count());
    }
}

public class ChatLineTests
{
    [Theory]
    [InlineData("fritz", "Fritz", "Fritz")]      // nur Schreibweise anders → Anzeigename
    [InlineData("abc123", "ナルト", "abc123")]    // anderer Name → Login
    [InlineData("fritz", null, "fritz")]
    public void NameFuerRics(string login, string display, string expected)
    {
        Assert.Equal(expected, new ChatLine { Login = login, DisplayName = display }.NameForRics);
    }

    [Fact]
    public void UnvollstaendigOhneId()
    {
        Assert.False(new ChatLine { Login = "a", Text = "!bal" }.IsComplete);
        Assert.True(new ChatLine { UserId = "1", Login = "a", Text = "!bal" }.IsComplete);
    }
}

public class PrivacyNoticesTests
{
    [Fact]
    public void HinweisNurEinmalJeZuschauer()
    {
        var p = new PrivacyNotices();
        Assert.True(p.ShouldNotify("42"));
        Assert.False(p.ShouldNotify("42"));
        Assert.True(p.ShouldNotify("43"));
        Assert.False(p.ShouldNotify(null));
        p.Reset();
        Assert.True(p.ShouldNotify("42"));
    }
}
