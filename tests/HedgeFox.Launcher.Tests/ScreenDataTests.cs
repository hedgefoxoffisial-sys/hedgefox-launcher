using System.Reactive.Linq;
using Gml.Dto.Mods;
using Gml.Dto.Profile;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;
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
        // the screens hide their status line instead
        Assert.False(info.HasOnlineLine);
        Assert.Equal("", info.OnlineLine);
    }

    [Fact]
    public void OnlineShowsPlayersAndThePelena()
    {
        var info = new HfServerInfoModel(null, null, () => Now);
        info.Apply(new HfStatusSnapshot(HfStatusKind.Online, 7, 45, "1.21.1", Now, new HfPelena(80, 96)));
        Assert.Equal("Онлайн 7 из 45", info.StatusLine);
        // the line on the screens is the online alone, the Pelena stays out of it
        Assert.Equal("Онлайн 7 из 45", info.OnlineLine);
        Assert.True(info.HasOnlineLine);
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
        // the main screen shows only the newest one
        Assert.True(info.HasLatest);
        Assert.Equal("Новость 1", info.Latest!.Title);
    }

    [Fact]
    public async Task NoPostsNoLatestStrip()
    {
        var info = new HfServerInfoModel(null, new ListNews([]), () => Now, newsLimit: 1);
        await info.LoadNewsAsync();
        Assert.False(info.HasLatest);
        Assert.Null(info.Latest);
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
    [InlineData("Обновление", "Осталось: 120 файлов", "Осталось 120 файлов")]
    [InlineData("Обновление", "", "Обновление")]
    [InlineData(null, null, "")]
    public void StepTextPrefersTheDetail(string? headline, string? description, string expected) =>
        Assert.Equal(expected, HfPlayProgress.Step(headline, description));

    // the stock line is «Осталось: {N} файлов» for every N (copy-lore-6); under the sign it loses the colon
    [Theory]
    [InlineData(1, "Осталось 1 файл")]
    [InlineData(2, "Осталось 2 файла")]
    [InlineData(5, "Осталось 5 файлов")]
    [InlineData(11, "Осталось 11 файлов")]
    [InlineData(21, "Осталось 21 файл")]
    [InlineData(122, "Осталось 122 файла")]
    [InlineData(0, "Осталось 0 файлов")]
    public void FilesLeftAgreeWithTheNumber(int n, string expected) =>
        Assert.Equal(expected, HfPlayProgress.Step("Обновление", $"Осталось: {n} файлов"));

    [Theory]
    [InlineData(42, "42 %")]
    [InlineData(0, "0 %")]
    [InlineData(null, "")]
    [InlineData(140, "100 %")]
    public void PercentIsWholeAndCapped(int? value, string expected) => Assert.Equal(expected, HfPlayProgress.Percent(value));

    [Theory]
    [InlineData(42, "42 %")]
    [InlineData(null, "ЗАГРУЗКА…")]
    public void SignShowsThePercentOrThatItLoads(int? percent, string expected) =>
        Assert.Equal(expected, HfPlayProgress.SignLabel(percent));

    // ----- the version of the game: picked for this PC, the player's pick wins -----

    [Fact]
    public void NobodyChoosesTheVersionAdvisedForThisPcIsPicked()
    {
        var offered = new[] { Gml("HedgeFox"), Gml("HedgeFox-LowPC"), Gml("HedgeFox-Shaders") };
        Assert.Equal("HedgeFox-LowPC", HfProfilePick.Pick(offered, new HfAdvice(ProfileCatalog.LowPc, false), null)?.Name);
        Assert.Equal("HedgeFox", HfProfilePick.Pick(offered, new HfAdvice(ProfileCatalog.Normal, true), null)?.Name);
    }

    [Fact]
    public void ThePlayersPickWinsWhileItIsOffered()
    {
        var offered = new[] { Gml("HedgeFox"), Gml("HedgeFox-LowPC"), Gml("HedgeFox-Shaders") };
        var advice = new HfAdvice(ProfileCatalog.LowPc, false);
        Assert.Equal("HedgeFox-Shaders", HfProfilePick.Pick(offered, advice, "HedgeFox-Shaders")?.Name);
        // a version GML no longer offers: back to the advice
        Assert.Equal("HedgeFox-LowPC", HfProfilePick.Pick(offered, advice, "HedgeFox-Old")?.Name);
    }

    [Fact]
    public void WithoutAdviceOrAnOfferedAdvisedVersionTheSelectionStays()
    {
        Assert.Null(HfProfilePick.Pick([Gml("HedgeFox"), Gml("HedgeFox-LowPC")], new HfAdvice(null, false), null));
        Assert.Null(HfProfilePick.Pick([Gml("HedgeFox")], new HfAdvice(ProfileCatalog.LowPc, false), null));
        Assert.Null(HfProfilePick.Pick([], new HfAdvice(ProfileCatalog.LowPc, false), "HedgeFox"));
    }

    [Fact]
    public void PickingTheAdvisedVersionFollowsThisPcAgain()
    {
        var advice = new HfAdvice(ProfileCatalog.LowPc, false);
        Assert.Null(HfProfilePick.Remember("HedgeFox-LowPC", advice));
        Assert.Equal("HedgeFox-Shaders", HfProfilePick.Remember("HedgeFox-Shaders", advice));
        Assert.Equal("HedgeFox-LowPC", HfProfilePick.Remember("HedgeFox-LowPC", new HfAdvice(null, false)));
    }

    // ----- settings: the version's mods, one row each -----

    [Fact]
    public void ModRowsTakeTheTitlesGmlKeepsUnderTheJar()
    {
        var mods = new[]
        {
            new ExternalModReadDto(new ModReadDto { Name = "minimap" }, isEnabled: true),
            new ExternalModReadDto(new ModReadDto { Name = "sound-physics" })
        };
        var details = new Dictionary<string, ModsDetailsInfoDto>
        {
            ["minimap.jar"] = new() { Key = "minimap.jar", Title = "Мини-карта", Description = " Карта в углу экрана. " }
        };
        var rows = HfModRow.Build(mods, details);
        Assert.Equal(new[] { "Мини-карта", "sound-physics" }, rows.Select(r => r.Title));
        Assert.Equal("Карта в углу экрана.", rows[0].Description);
        Assert.True(rows[0].Mod.IsSelected);
        Assert.False(rows[1].HasDescription);
    }

    [Fact]
    public async Task ThePickIsStoredAndAtOnceInMemory()
    {
        var storage = new TestStorage();
        await HfVersionChoice.SetAsync(storage, "HedgeFox-Shaders");
        Assert.Equal("HedgeFox-Shaders", await storage.GetAsync<string>(HfProfilePick.StorageKey));
        // the main screen reads the memory, not a storage whose write may still be on its way
        Assert.Equal("HedgeFox-Shaders", await HfVersionChoice.GetAsync(new TestStorage()));
        await HfVersionChoice.SetAsync(storage, null);
        Assert.Null(await HfVersionChoice.GetAsync(storage));
    }

    // ----- settings: the memory slider's «рекомендуем» mark -----

    [Fact]
    public void MarkSitsAtTheVersionsMemory()
    {
        Assert.Equal(4096, HfRamMark.ProfileMb(Gml("HedgeFox-LowPC", ram: 4096)));
        Assert.Equal(8192, HfRamMark.ProfileMb(Gml("HedgeFox-Shaders", ram: 8192)));
    }

    [Fact]
    public void NoMarkWithoutAVersionOrItsMemory()
    {
        Assert.Null(HfRamMark.ProfileMb(null));
        Assert.Null(HfRamMark.ProfileMb(Gml("HedgeFox")));
    }

    [Theory]
    [InlineData(4096, 4096, true)]
    [InlineData(4096.4, 4096, true)]
    [InlineData(4608, 4096, false)]
    [InlineData(4096, null, false)]
    public void OnTheMarkMemoryIsAutomatic(double value, int? mark, bool on) =>
        Assert.Equal(on, HfRamMark.IsOnMark(value, mark));

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
