using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

public class NewsParserTests
{
    private const string Two = """
        [
          { "id": "site:2026-11-07-nabor-na-betu", "source": "site", "title": "Набор на бету", "text": "Ищем пиратов.",
            "url": "https://www.hedgefox.ru/novosti/2026-11-07-nabor-na-betu/", "date": "2026-11-07T18:00:00Z", "image_url": null },
          { "id": "tg:12", "source": "tg", "title": "Меню и лаунчер", "text": "Первая строка\nвторая строка",
            "url": "https://t.me/hedgefox_ru/12", "date": "2026-11-08T10:00:00Z", "image_url": "https://www.hedgefox.ru/x.webp" }
        ]
        """;

    [Fact]
    public void ParsesAndSortsNewestFirst()
    {
        var items = HfNewsParser.Parse(Two)!;
        Assert.Equal(new[] { "tg:12", "site:2026-11-07-nabor-na-betu" }, items.Select(i => i.Id));
        var tg = items[0];
        Assert.True(tg.IsTelegram);
        Assert.Equal("Меню и лаунчер", tg.Title);
        Assert.Equal("Первая строка\nвторая строка", tg.Text);
        Assert.Equal("https://t.me/hedgefox_ru/12", tg.Url);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero), tg.Date);
        Assert.Equal("https://www.hedgefox.ru/x.webp", tg.ImageUrl);
        Assert.False(items[1].IsTelegram);
        Assert.Null(items[1].ImageUrl);
    }

    [Fact]
    public void EmptyFeedIsAnEmptyListNotAnError() => Assert.Empty(HfNewsParser.Parse("[]")!);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""{ "error": "rate_limited" }""")]
    [InlineData("<html>502</html>")]
    [InlineData("""{ "items": [] }""")]
    public void ErrorShapesAreNull(string? body) => Assert.Null(HfNewsParser.Parse(body));

    [Theory]
    [InlineData("""{ "id": "a", "source": "tg", "title": "", "text": "x", "url": "https://t.me/hedgefox_ru/1", "date": "2026-11-08T10:00:00Z" }""")]
    [InlineData("""{ "id": "a", "source": "tg", "title": "T", "text": "x", "url": "javascript:alert(1)", "date": "2026-11-08T10:00:00Z" }""")]
    [InlineData("""{ "id": "a", "source": "tg", "title": "T", "text": "x", "url": "file:///etc/passwd", "date": "2026-11-08T10:00:00Z" }""")]
    [InlineData("""{ "id": "a", "source": "tg", "title": "T", "text": "x", "url": "https://t.me/hedgefox_ru/1", "date": "вчера" }""")]
    [InlineData("""{ "source": "tg", "title": "T", "text": "x", "url": "https://t.me/hedgefox_ru/1", "date": "2026-11-08T10:00:00Z" }""")]
    [InlineData("42")]
    public void BadItemsAreSkipped(string item) => Assert.Empty(HfNewsParser.Parse($"[{item}]")!);

    [Fact]
    public void LongTextIsCutAt600WithAnEllipsis()
    {
        var text = new string('а', 700);
        var items = HfNewsParser.Parse($$"""[{ "id": "a", "source": "tg", "title": "T", "text": "{{text}}", "url": "https://t.me/hedgefox_ru/1", "date": "2026-11-08T10:00:00Z" }]""")!;
        Assert.Equal(600, items[0].Text.Length);
        Assert.EndsWith("…", items[0].Text);
    }

    [Fact]
    public void MissingTextIsEmpty()
    {
        var items = HfNewsParser.Parse("""[{ "id": "a", "source": "site", "title": "T", "url": "https://www.hedgefox.ru/novosti/a/", "date": "2026-11-08T10:00:00Z" }]""")!;
        Assert.Equal("", items[0].Text);
    }

    [Fact]
    public void KeepsAtMostTheLimit()
    {
        var many = string.Join(",", Enumerable.Range(1, 25).Select(i =>
            $$"""{ "id": "tg:{{i}}", "source": "tg", "title": "T{{i}}", "text": "", "url": "https://t.me/hedgefox_ru/{{i}}", "date": "2026-11-{{i:00}}T10:00:00Z" }"""));
        var items = HfNewsParser.Parse($"[{many}]", limit: 10)!;
        Assert.Equal(10, items.Count);
        Assert.Equal("tg:25", items[0].Id);
    }
}

public class NewsWordingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 15, 0, 0, TimeSpan.FromHours(3));

    [Theory]
    [InlineData("2026-11-08T10:00:00+03:00", "сегодня")]
    [InlineData("2026-11-07T23:59:00+03:00", "вчера")]
    [InlineData("2026-11-01T10:00:00+03:00", "1 ноября")]
    [InlineData("2026-05-09T10:00:00+03:00", "9 мая")]
    [InlineData("2025-12-31T10:00:00+03:00", "31 декабря 2025")]
    public void NewsDates(string date, string expected) =>
        Assert.Equal(expected, HfStrings.NewsDate(DateTimeOffset.Parse(date), Now));

    [Fact]
    public void SourceLabels()
    {
        Assert.Equal("Telegram", HfStrings.NewsSource("tg"));
        Assert.Equal("Сайт", HfStrings.NewsSource("site"));
    }

    [Theory]
    [InlineData(1, "игрок")]
    [InlineData(3, "игрока")]
    [InlineData(5, "игроков")]
    [InlineData(11, "игроков")]
    [InlineData(21, "игрок")]
    [InlineData(112, "игроков")]
    public void RussianPlurals(int n, string word) => Assert.Equal(word, HfStrings.Plural(n, "игрок", "игрока", "игроков"));
}
