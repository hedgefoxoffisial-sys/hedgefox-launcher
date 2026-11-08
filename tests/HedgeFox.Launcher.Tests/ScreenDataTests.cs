using System.Reactive.Linq;
using Gml.Dto.Profile;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.Views.HedgeFox.Controls;
using ReactiveUI;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>What the HedgeFox screens show, worked out without a window.</summary>
public class ScreenDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 16, 0, 0, TimeSpan.FromHours(3));

    private static ProfileReadDto Gml(string name, string display = "", int ram = 0) =>
        new() { Name = name, DisplayName = display, RecommendedRam = ram };

    // ----- status -----

    [Theory]
    [InlineData(HfStatusKind.Online, HfDotState.Online)]
    [InlineData(HfStatusKind.Offline, HfDotState.Offline)]
    [InlineData(HfStatusKind.Loading, HfDotState.Waiting)]
    [InlineData(HfStatusKind.NoData, HfDotState.Unknown)]
    public void StatusKindPicksTheDot(HfStatusKind kind, HfDotState dot) =>
        Assert.Equal(dot, HfServerInfoModel.DotFor(kind));

    [Fact]
    public void NoDataNeverShowsZero()
    {
        var info = new HfServerInfoModel(null, null, () => Now);
        info.Apply(HfStatusSnapshot.NoData);
        Assert.Equal("Нет данных", info.StatusLine);
        Assert.DoesNotContain("0", info.StatusLine);
        Assert.False(info.HasPelena);
    }

    [Fact]
    public void OnlineShowsPlayersAndThePelena()
    {
        var info = new HfServerInfoModel(null, null, () => Now);
        info.Apply(new HfStatusSnapshot(HfStatusKind.Online, 7, 45, "1.21.1", Now, new HfPelena(80, 96)));
        Assert.Equal("Онлайн: 7 из 45", info.StatusLine);
        Assert.Equal(HfDotState.Online, info.Dot);
        Assert.True(info.HasPelena);
        Assert.Equal("ПЕЛЕНА: Y 80", info.PelenaTitle);
        Assert.Equal("поднимается", info.PelenaTrend);
        Assert.True(info.PelenaShowBar);
        Assert.Equal(100 * (80 + 64) / 264.0, info.PelenaPercent, 6);
    }

    [Fact]
    public void SleepingPelenaHasNoBar()
    {
        var info = new HfServerInfoModel(null, null, () => Now);
        info.Apply(new HfStatusSnapshot(HfStatusKind.Online, 0, 45, "1.21.1", Now, new HfPelena(-64, -64)));
        Assert.True(info.HasPelena);
        Assert.Equal("ПЕЛЕНА СПИТ", info.PelenaTitle);
        Assert.False(info.PelenaShowBar);
        Assert.Null(info.PelenaTrend);
        Assert.False(info.HasPelenaTrend);
    }

    [Fact]
    public void ServerAsleepHidesThePelena()
    {
        var info = new HfServerInfoModel(null, null, () => Now);
        info.Apply(new HfStatusSnapshot(HfStatusKind.Online, 1, 45, null, Now, new HfPelena(10, 10)));
        info.Apply(new HfStatusSnapshot(HfStatusKind.Offline));
        Assert.Equal("Сервер спит", info.StatusLine);
        Assert.False(info.HasPelena);
    }

    // ----- news -----

    [Fact]
    public void NewsCardsUseRussianLabelsAndDates()
    {
        var item = new HfNewsItem("tg:14", "tg", "Лаунчер", "Текст\n\nвторой абзац", "https://t.me/hedgefox_ru/14",
            new DateTimeOffset(2026, 11, 7, 21, 0, 0, TimeSpan.FromHours(3)), null);
        var card = HfNewsCard.From(item, Now);
        Assert.Equal("TELEGRAM", card.Source);
        Assert.Equal("вчера", card.Date);
        Assert.Equal("Лаунчер", card.Title);
        Assert.Equal("Текст\nвторой абзац", card.Text);
        Assert.Equal("https://t.me/hedgefox_ru/14", card.Url);
        Assert.True(card.IsTelegram);
        Assert.Equal("САЙТ", HfNewsCard.From(item with { Source = "site" }, Now).Source);
    }

    [Fact]
    public void NewsMessageExplainsAnEmptyOrFailedFeed()
    {
        Assert.Equal(HfStrings.NewsFailed, HfNewsCard.Message(HfNewsResult.Error));
        Assert.Equal(HfStrings.NewsEmpty, HfNewsCard.Message(new HfNewsResult([], false)));
        var one = new HfNewsItem("site:a", "site", "А", "", "https://www.hedgefox.ru/novosti/#a", Now, null);
        Assert.Null(HfNewsCard.Message(new HfNewsResult([one], false)));
    }

    [Fact]
    public async Task ServerInfoLoadsAtMostItsLimitOfNews()
    {
        var items = Enumerable.Range(1, 5).Select(i => new HfNewsItem($"site:{i}", "site", $"Новость {i}", "", $"https://www.hedgefox.ru/novosti/#{i}",
            Now.AddDays(-i), null)).ToList();
        var info = new HfServerInfoModel(null, new ListNews(items), () => Now, newsLimit: 3);
        await info.LoadNewsAsync();
        Assert.Equal(new[] { "Новость 1", "Новость 2", "Новость 3" }, info.News.Select(n => n.Title));
        Assert.False(info.HasNewsMessage);
    }

    private sealed class ListNews(IReadOnlyList<HfNewsItem> items) : INewsService
    {
        public Task<HfNewsResult> LoadAsync(int limit = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HfNewsResult(items.Take(limit).ToList(), false));
    }

    // ----- profile cards -----

    [Fact]
    public void CardsComeInTheCatalogOrderWithTheAdviceOnOne()
    {
        var cards = HfProfileCards.Build(
            [Gml("HedgeFox-Shaders"), Gml("HedgeFox"), Gml("HedgeFox-LowPC")],
            new HfAdvice(ProfileCatalog.LowPc, false), "HedgeFox");
        Assert.Equal(new[] { "Обычный", "Слабый ПК", "Шейдеры" }, cards.Select(c => c.Title));
        Assert.Equal(new[] { false, true, false }, cards.Select(c => c.Recommended));
        Assert.Equal(new[] { true, false, false }, cards.Select(c => c.Selected));
        Assert.Equal(new[] { "6 ГБ", "4 ГБ", "8 ГБ" }, cards.Select(c => c.Ram));
        Assert.All(cards, c => Assert.False(c.ShadersAlsoFit));
    }

    [Fact]
    public void StrongPcGetsNormalAndTheShadersNote()
    {
        var cards = HfProfileCards.Build([Gml("HedgeFox"), Gml("HedgeFox-LowPC"), Gml("HedgeFox-Shaders")],
            new HfAdvice(ProfileCatalog.Normal, true), null);
        Assert.True(cards.Single(c => c.GmlName == ProfileCatalog.Normal).Recommended);
        Assert.True(cards.Single(c => c.GmlName == ProfileCatalog.Shaders).ShadersAlsoFit);
        Assert.Single(cards, c => c.Recommended);
        Assert.All(cards, c => Assert.False(c.Selected));
    }

    [Fact]
    public void UnknownAdviceShowsNoBadge()
    {
        var cards = HfProfileCards.Build([Gml("HedgeFox"), Gml("HedgeFox-LowPC")], new HfAdvice(null, false), "HedgeFox-LowPC");
        Assert.All(cards, c => Assert.False(c.Recommended));
        Assert.True(cards[1].Selected);
    }

    [Fact]
    public void UnknownProfileShowsGmlNameAndMemory()
    {
        var card = Assert.Single(HfProfileCards.Build([Gml("Event-2027", "Ивент", 3072)], new HfAdvice(null, false), null));
        Assert.Equal("Ивент", card.Title);
        Assert.Equal("", card.Hint);
        Assert.False(card.HasHint);
        Assert.Equal("3 ГБ", card.Ram);
    }

    // ----- play button -----

    [Theory]
    [InlineData("Обновление", "Осталось: 120 файлов", "Осталось: 120 файлов")]
    [InlineData("Обновление", "", "Обновление")]
    [InlineData(null, null, "")]
    public void StepTextPrefersTheDetail(string? headline, string? description, string expected) =>
        Assert.Equal(expected, HfPlayProgress.Step(headline, description));

    // the stock line is «Осталось: {N} файлов» for every N (copy-lore-6)
    [Theory]
    [InlineData(1, "Осталось: 1 файл")]
    [InlineData(2, "Осталось: 2 файла")]
    [InlineData(5, "Осталось: 5 файлов")]
    [InlineData(11, "Осталось: 11 файлов")]
    [InlineData(21, "Осталось: 21 файл")]
    [InlineData(122, "Осталось: 122 файла")]
    [InlineData(0, "Осталось: 0 файлов")]
    public void FilesLeftAgreeWithTheNumber(int n, string expected) =>
        Assert.Equal(expected, HfPlayProgress.Step("Обновление", $"Осталось: {n} файлов"));

    [Theory]
    [InlineData(42, "42 %")]
    [InlineData(0, "0 %")]
    [InlineData(null, "")]
    [InlineData(140, "100 %")]
    public void PercentIsWholeAndCapped(int? value, string expected) => Assert.Equal(expected, HfPlayProgress.Percent(value));

    [Theory]
    [InlineData("3.5 MB/s", "3,5 МБ/с")]
    [InlineData("812 KB/s", "812 КБ/с")]
    [InlineData("1.25 GB/s", "1,25 ГБ/с")]
    [InlineData("900 B/s", "900 Б/с")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SpeedIsInRussianUnits(string? speed, string expected) => Assert.Equal(expected, HfPlayProgress.Speed(speed));

    // ----- settings: the memory slider's «советуем» mark -----

    [Fact]
    public void MarkSitsAtTheAdvisedProfilesMemory()
    {
        Assert.Equal(4096, HfRamMark.RecommendedMb(new HfAdvice(ProfileCatalog.LowPc, false), 8192));
        Assert.Equal(6144, HfRamMark.RecommendedMb(new HfAdvice(ProfileCatalog.Normal, true), 32768));
    }

    [Fact]
    public void NoMarkWithoutAdviceOrWhenThePcHasLessMemory()
    {
        Assert.Null(HfRamMark.RecommendedMb(new HfAdvice(null, false), 16384));
        Assert.Null(HfRamMark.RecommendedMb(new HfAdvice(ProfileCatalog.Normal, false), 4096));
    }

    [Theory]
    [InlineData(4096, 512, 8192, 0.466666)]
    [InlineData(512, 512, 8192, 0)]
    [InlineData(9000, 512, 8192, 1)]
    [InlineData(4096, 8192, 8192, 0)]
    public void MarkFractionIsClamped(double value, double min, double max, double expected) =>
        Assert.Equal(expected, HfRamMark.Fraction(value, min, max), 5);

    [Theory]
    [InlineData(0.5, 178)]
    [InlineData(0.226, 63)]
    [InlineData(0, 0)]
    [InlineData(1, 356)]
    public void MarkLabelIsCentredOnTheThumbAndStaysInside(double fraction, double left) =>
        Assert.Equal(left, HfMarkPanel.ChildLeft(436, 9, fraction, 80));

    // ----- logout -----

    [Fact]
    public async Task LogoutForgetsTheUserAndLeavesOnlyTheLoginPage()
    {
        var screen = new TestScreen();
        var storage = new MemoryStorage();
        await storage.SetAsync(StorageConstants.User, "someone");
        screen.Router.Navigate.Execute(new Page(screen, "login")).Subscribe();
        screen.Router.Navigate.Execute(new Page(screen, "overview")).Subscribe();
        screen.Router.Navigate.Execute(new Page(screen, "settings")).Subscribe();

        await HfSession.LogoutAsync(storage, screen.Router, () => new Page(screen, "login2"));

        Assert.Null(await storage.GetAsync<string>(StorageConstants.User));
        var page = Assert.Single(screen.Router.NavigationStack);
        Assert.Equal("login2", page.UrlPathSegment);
    }

    private sealed class TestScreen : IScreen
    {
        public RoutingState Router { get; } = new();
    }

    private sealed class Page(IScreen screen, string name) : ReactiveObject, IRoutableViewModel
    {
        public string UrlPathSegment => name;
        public IScreen HostScreen { get; } = screen;
    }

    private sealed class MemoryStorage : IStorageService
    {
        private readonly Dictionary<string, object?> _values = new();

        public Task SetAsync<T>(string key, T value, CancellationToken? token = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key) =>
            Task.FromResult(_values.TryGetValue(key, out var v) && v is T t ? t : default);

        public Task<int> SaveRecord<T>(T value) => Task.FromResult(0);
        public Task<string> GetLogsAsync(int rowCount) => Task.FromResult(string.Empty);
    }
}
