using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

public class StatusParserTests
{
    [Fact]
    public void OnlineWithPelena()
    {
        var s = HfStatusParser.Parse("""
            { "online": true, "players": 7, "max": 45, "version": "1.21.1", "checked_at": "2026-11-08T13:00:00Z",
              "pelena": { "y": 80.0, "target": 96 } }
            """);
        Assert.Equal(HfStatusKind.Online, s.Kind);
        Assert.Equal(7, s.Players);
        Assert.Equal(45, s.Max);
        Assert.Equal("1.21.1", s.Version);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 13, 0, 0, TimeSpan.Zero), s.CheckedAt);
        Assert.Equal(new HfPelena(80, 96), s.Pelena);
    }

    [Fact]
    public void ServerDownHasNoNumbers()
    {
        var s = HfStatusParser.Parse("""
            { "online": false, "players": null, "max": null, "version": null, "checked_at": "2026-11-08T13:00:00Z", "pelena": null }
            """);
        Assert.Equal(HfStatusKind.Offline, s.Kind);
        Assert.Null(s.Players);
        Assert.Null(s.Pelena);
    }

    [Fact]
    public void PelenaNullWhenRconIsDown()
    {
        var s = HfStatusParser.Parse("""{ "online": true, "players": 0, "max": 45, "pelena": null }""");
        Assert.Equal(HfStatusKind.Online, s.Kind);
        Assert.Equal(0, s.Players);
        Assert.Null(s.Pelena);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("""{ "error": "not_found" }""")]
    [InlineData("[]")]
    [InlineData("""{ "online": "yes" }""")]
    [InlineData("""{ "online": true }""")]
    [InlineData("""{ "online": true, "players": 3, "max": 0 }""")]
    [InlineData("""{ "online": true, "players": -1, "max": 45 }""")]
    [InlineData("""{ "online": true, "players": "7", "max": 45 }""")]
    [InlineData(null)]
    public void AnythingElseIsNoData(string? body) => Assert.Equal(HfStatusKind.NoData, HfStatusParser.Parse(body).Kind);

    [Theory]
    [InlineData("""{ "online": true, "players": 1, "max": 45, "pelena": { "y": "high" } }""")]
    [InlineData("""{ "online": true, "players": 1, "max": 45, "pelena": { "y": 10 } }""")]
    [InlineData("""{ "online": true, "players": 1, "max": 45, "pelena": [] }""")]
    public void BrokenPelenaIsDroppedNotTheStatus(string body)
    {
        var s = HfStatusParser.Parse(body);
        Assert.Equal(HfStatusKind.Online, s.Kind);
        Assert.Null(s.Pelena);
    }

    [Fact]
    public void ScheduleIsNeverRead()
    {
        // the API keeps the schedule private (no dates until the opening is announced); even if it leaked, the
        // launcher's model has nowhere to put it
        var s = HfStatusParser.Parse("""
            { "online": true, "players": 1, "max": 45, "pelena": { "y": 10, "target": 20, "schedule": "2026-11-28T15:30:00Z:72" } }
            """);
        Assert.Equal(new HfPelena(10, 20), s.Pelena);
    }
}

public class StatusWordingTests
{
    [Fact]
    public void Online() =>
        Assert.Equal("Онлайн 7 из 45", HfStrings.StatusLine(HfStatusParser.Parse("""{"online":true,"players":7,"max":45}""")));

    [Fact]
    public void OnlineWithNobodyStillSaysZeroBecauseTheServerAnswered() =>
        Assert.Equal("Онлайн 0 из 45", HfStrings.StatusLine(HfStatusParser.Parse("""{"online":true,"players":0,"max":45}""")));

    [Fact]
    public void Offline() => Assert.Equal("Сервер спит", HfStrings.StatusLine(HfStatusParser.Parse("""{"online":false}""")));

    [Fact]
    public void NoData() => Assert.Equal("Нет данных", HfStrings.StatusLine(HfStatusSnapshot.NoData));

    [Fact]
    public void Loading() => Assert.Equal("Проверяем…", HfStrings.StatusLine(HfStatusSnapshot.Loading));

    [Theory]
    [InlineData(80, 96, "Онлайн 7 из 45 · Пелена поднимается")]
    [InlineData(96, 80, "Онлайн 7 из 45 · Пелена опускается")]
    [InlineData(80, 80, "Онлайн 7 из 45 · Пелена: Y 80")]
    [InlineData(-64, -64, "Онлайн 7 из 45 · Пелена спит")]
    [InlineData(-64, 10, "Онлайн 7 из 45 · Пелена просыпается")]
    public void ShortLineAddsThePelenaInAFewWords(double y, double target, string expected) =>
        Assert.Equal(expected, HfStrings.StatusShort(new HfStatusSnapshot(HfStatusKind.Online, 7, 45, Pelena: new HfPelena(y, target))));

    [Fact]
    public void ShortLineWithoutThePelenaOrWithoutAnyIsJustTheOnline()
    {
        var online = new HfStatusSnapshot(HfStatusKind.Online, 0, 45, Pelena: new HfPelena(80, 96));
        Assert.Equal("Онлайн 0 из 45", HfStrings.StatusShort(online, withPelena: false));
        Assert.Equal("Онлайн 0 из 45", HfStrings.StatusShort(online with { Pelena = null }));
    }

    [Fact]
    public void ShortLineIsHiddenWithoutAnAnswerAndSaysTheServerSleeps()
    {
        Assert.Null(HfStrings.StatusShort(HfStatusSnapshot.NoData));
        Assert.Null(HfStrings.StatusShort(HfStatusSnapshot.Loading));
        Assert.Equal("Сервер спит", HfStrings.StatusShort(HfStatusParser.Parse("""{"online":false}""")));
    }
}

public class PelenaWordingTests
{
    [Theory]
    [InlineData(-64, -64)]
    [InlineData(-70, -64)]
    public void AtTheBottomThePelenaSleeps(double y, double target)
    {
        var v = HfPelenaView.From(new HfPelena(y, target))!;
        Assert.True(v.Sleeping);
        Assert.Equal("Пелена спит", v.Title);
        Assert.False(v.ShowBar);
    }

    [Fact]
    public void AboveTheBottomItShowsTheLevelAndABar()
    {
        var v = HfPelenaView.From(new HfPelena(80, 80))!;
        Assert.False(v.Sleeping);
        Assert.Equal("Пелена: Y 80", v.Title);
        Assert.True(v.ShowBar);
        Assert.Equal((80.0 + 64) / 264, v.Fraction, 6);
        Assert.Null(v.Trend);
    }

    [Theory]
    [InlineData(80, 96, "поднимается")]
    [InlineData(96, 80, "опускается")]
    [InlineData(80.02, 80, null)]
    public void TargetGivesTheTrend(double y, double target, string? trend) =>
        Assert.Equal(trend, HfPelenaView.From(new HfPelena(y, target))!.Trend);

    // never «Пелена спит» next to «поднимается» (copy-lore-11): at the bottom with a higher target it wakes up
    [Fact]
    public void AtTheBottomWithAHigherTargetItWakesUp()
    {
        var v = HfPelenaView.From(new HfPelena(-64, 72))!;
        Assert.True(v.Sleeping);
        Assert.Equal("Пелена просыпается", v.Title);
        Assert.Null(v.Trend);
        Assert.False(v.ShowBar);
    }

    [Theory]
    [InlineData(-64, -64)]
    [InlineData(-64, -80)]
    public void ASleepingPelenaHasNoTrend(double y, double target) =>
        Assert.Null(HfPelenaView.From(new HfPelena(y, target))!.Trend);

    [Theory]
    [InlineData(80.4, "Пелена: Y 80")]
    [InlineData(80.6, "Пелена: Y 81")]
    [InlineData(-12, "Пелена: Y −12")]
    public void LevelIsAWholeBlock(double y, string title) => Assert.Equal(title, HfPelenaView.From(new HfPelena(y, y))!.Title);

    [Theory]
    [InlineData(200, 1)]
    [InlineData(260, 1)]
    [InlineData(-63, 1.0 / 264)]
    public void BarRunsFromMinus64To200(double y, double fraction) =>
        Assert.Equal(fraction, HfPelenaView.From(new HfPelena(y, y))!.Fraction, 6);

    [Fact]
    public void NoPelenaNoWidget() => Assert.Null(HfPelenaView.From(null));

    [Fact]
    public void WordingHasNoDates()
    {
        foreach (var p in new[] { new HfPelena(-64, -64), new HfPelena(10, 72), new HfPelena(150, 120) })
        {
            var v = HfPelenaView.From(p)!;
            var text = v.Title + " " + v.Trend;
            Assert.DoesNotMatch(@"\d{4}|\d{1,2}[.:/]\d{1,2}|январ|феврал|ноябр|декабр|через|дн(я|ей)", text);
        }
    }
}
