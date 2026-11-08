using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>Answers requests from a function; records what was asked.</summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public readonly List<Uri> Requests = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(answer(request));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public class StatusServiceTests
{
    private const string Online = """{"online":true,"players":7,"max":45,"pelena":{"y":80,"target":96}}""";

    [Fact]
    public async Task ReadsOurApi()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(Online));
        var service = new StatusService(new HttpClient(handler));
        var s = await service.RefreshAsync();
        Assert.Equal(HfStatusKind.Online, s.Kind);
        Assert.Equal(new Uri("https://www.hedgefox.ru/api/launcher/status"), handler.Requests.Single());
        Assert.Equal(s, service.Current);
    }

    [Fact]
    public async Task StartsAsLoading() =>
        Assert.Equal(HfStatusKind.Loading, new StatusService(new HttpClient(new FakeHandler(_ => FakeHandler.Json(Online)))).Current.Kind);

    [Fact]
    public async Task ServerErrorIsNoData()
    {
        var service = new StatusService(new HttpClient(new FakeHandler(_ => FakeHandler.Json("""{"error":"internal"}""", HttpStatusCode.InternalServerError))));
        Assert.Equal(HfStatusKind.NoData, (await service.RefreshAsync()).Kind);
    }

    [Fact]
    public async Task NetworkFailureIsNoData()
    {
        var service = new StatusService(new HttpClient(new FakeHandler(_ => throw new HttpRequestException("no route"))));
        Assert.Equal(HfStatusKind.NoData, (await service.RefreshAsync()).Kind);
    }

    [Fact]
    public async Task SlowAnswerTimesOutAsNoData()
    {
        var slow = new SlowHandler();
        var service = new StatusService(new HttpClient(slow), timeout: TimeSpan.FromMilliseconds(50));
        Assert.Equal(HfStatusKind.NoData, (await service.RefreshAsync()).Kind);
    }

    [Fact]
    public async Task OneFailedPollKeepsAFreshGoodAnswer()
    {
        var fail = false;
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var service = new StatusService(
            new HttpClient(new FakeHandler(_ => fail ? throw new HttpRequestException("blip") : FakeHandler.Json(Online))),
            clock: () => now);
        await service.RefreshAsync();
        fail = true;
        now = now.AddSeconds(31);
        Assert.Equal(HfStatusKind.Online, (await service.RefreshAsync()).Kind);
        now = now.AddSeconds(90);
        Assert.Equal(HfStatusKind.NoData, (await service.RefreshAsync()).Kind);
    }

    [Fact]
    public async Task UpdatesPushEveryAnswer()
    {
        var service = new StatusService(new HttpClient(new FakeHandler(_ => FakeHandler.Json(Online))));
        var seen = new System.Collections.Concurrent.ConcurrentQueue<HfStatusKind>();
        using var sub = service.Updates.Subscribe(s => seen.Enqueue(s.Kind)); // also starts the 30 s poll
        await service.RefreshAsync();
        Assert.Equal(new[] { HfStatusKind.Loading, HfStatusKind.Online }, seen.ToArray().Take(2));
    }

    [Fact]
    public void RefreshesEveryThirtySeconds() => Assert.Equal(TimeSpan.FromSeconds(30), StatusService.RefreshEvery);

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return FakeHandler.Json(Online);
        }
    }
}

public class NewsServiceTests
{
    [Fact]
    public async Task AsksForTenAndParses()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json("""
            [{ "id": "tg:1", "source": "tg", "title": "T", "text": "x", "url": "https://t.me/hedgefox_ru/1", "date": "2026-11-08T10:00:00Z" }]
            """));
        var result = await new NewsService(new HttpClient(handler)).LoadAsync();
        Assert.False(result.Failed);
        Assert.Single(result.Items);
        Assert.Equal(new Uri("https://www.hedgefox.ru/api/launcher/news?limit=10"), handler.Requests.Single());
    }

    [Fact]
    public async Task ErrorsAreFailedNotEmpty()
    {
        var result = await new NewsService(new HttpClient(new FakeHandler(_ => FakeHandler.Json("""{"error":"x"}""", HttpStatusCode.BadGateway)))).LoadAsync();
        Assert.True(result.Failed);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CachesForAMinute()
    {
        var now = DateTimeOffset.UnixEpoch;
        var handler = new FakeHandler(_ => FakeHandler.Json("[]"));
        var service = new NewsService(new HttpClient(handler), clock: () => now);
        await service.LoadAsync();
        await service.LoadAsync();
        Assert.Single(handler.Requests);
        now = now.AddSeconds(61);
        await service.LoadAsync();
        Assert.Equal(2, handler.Requests.Count);
    }
}

public class SkinServiceTests
{
    internal static byte[] SkinPng(int width = 64, int height = 64)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                    // the second layer (hat, x ≥ 32 in the top 16 rows) is transparent, like most skins
                    row[x] = x >= 32 && y < 16 ? new Rgba32(0, 0, 0, 0) : new Rgba32((byte)x, (byte)y, 100, 255);
            }
        });
        // a hat pixel over the face at (1, 1) of the head
        if (width >= 48 && height >= 16)
            image[40 + 1, 8 + 1] = new Rgba32(255, 0, 0, 255);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static HttpResponseMessage Png(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new("image/png") } } };

    [Fact]
    public async Task LoadsTheSiteSkinByUuid()
    {
        var png = SkinPng();
        var handler = new FakeHandler(_ => Png(png));
        var skin = await new SkinService(new HttpClient(handler)).GetAsync("0b3c5d2e-8f1a-4c6b-9d7e-2a4f6b8c0d1e");
        Assert.False(skin.IsFallback);
        Assert.Equal(png, skin.Png);
        Assert.Equal(new Uri("https://www.hedgefox.ru/skins/0b3c5d2e-8f1a-4c6b-9d7e-2a4f6b8c0d1e.png"), handler.Requests.Single());
    }

    [Fact]
    public async Task MissingSkinIsTheBundledSilhouette()
    {
        // the site answers a missing skin with its HTML 404 page
        var skin = await new SkinService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("<!doctype html><title>404</title>") })),
            fallback: () => SkinPng()).GetAsync("00000000-0000-0000-0000-000000000000");
        Assert.True(skin.IsFallback);
    }

    [Fact]
    public async Task HtmlWithStatus200IsNotASkin()
    {
        var skin = await new SkinService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<!doctype html>") })), fallback: () => SkinPng()).GetAsync("x");
        Assert.True(skin.IsFallback);
    }

    [Fact]
    public async Task WrongSizedPngIsNotASkin()
    {
        var skin = await new SkinService(new HttpClient(new FakeHandler(_ => Png(SkinPng(40, 40)))), fallback: () => SkinPng())
            .GetAsync("x");
        Assert.True(skin.IsFallback);
    }

    [Fact]
    public async Task NoUuidMeansSilhouetteWithoutAsking()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException());
        var skin = await new SkinService(new HttpClient(handler), fallback: () => SkinPng()).GetAsync(" ");
        Assert.True(skin.IsFallback);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void FaceIsTheHeadFrontWithTheHatOnTopScaledWithoutBlur()
    {
        using var face = Image.Load<Rgba32>(HfSkinImages.Face(SkinPng(), 4));
        Assert.Equal(32, face.Width);
        Assert.Equal(new Rgba32(8, 8, 100, 255), face[0, 0]);
        Assert.Equal(new Rgba32(8, 8, 100, 255), face[3, 3]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), face[4, 4]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), face[7, 7]);
        Assert.Equal(new Rgba32(10, 9, 100, 255), face[8, 4]);
    }

    [Fact]
    public void BundledSilhouetteIsAValidSkin()
    {
        var bytes = SkinService.BundledSilhouette();
        using var image = Image.Load<Rgba32>(bytes);
        Assert.Equal(64, image.Width);
        Assert.Equal(64, image.Height);
    }
}

public class CopyRulesTests
{
    private static IEnumerable<string> AllStrings() =>
        typeof(HfStrings).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)!)
            .Concat(ProfileCatalog.Known.SelectMany(p => new[] { p.Title, p.Hint }))
            .Concat(HfStrings.SplashHints);

    [Fact]
    public void NoBannedWordsInUiText()
    {
        foreach (var s in AllStrings())
            Assert.DoesNotMatch(@"(?i)5 лет|пять лет|GML|Recloud|GamerVII|Весёлый Роджер", s);
    }

    [Fact]
    public void NoOpeningDatesOrCountdowns()
    {
        foreach (var s in AllStrings())
            Assert.DoesNotMatch(@"28 ноября|\d{1,2}\.\d{1,2}\.\d{2,4}|до открытия|осталось \d", s);
    }

    [Fact]
    public void LinksAreOurs()
    {
        var links = typeof(HfLinks).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)!).ToList();
        Assert.Contains("https://www.hedgefox.ru/register/", links);
        Assert.Contains("https://www.hedgefox.ru/forgot/", links);
        Assert.Contains("https://www.hedgefox.ru/rules/", links);
        Assert.Contains("https://www.hedgefox.ru/wiki/", links);
        Assert.Contains("https://www.hedgefox.ru/cabinet/", links);
        Assert.Contains("https://t.me/hedgefox_ru", links);
        foreach (var link in links)
            Assert.Matches(@"^https://(www\.hedgefox\.ru(/|$)|t\.me/hedgefox_ru$)", link);
    }

    [Fact]
    public void HelpLinkFollowsTheOs()
    {
        Assert.Equal("https://www.hedgefox.ru/download/windows/", HfLinks.InstallFor(windows: true, macOs: false));
        Assert.Equal("https://www.hedgefox.ru/download/macos/", HfLinks.InstallFor(windows: false, macOs: true));
        Assert.Equal("https://www.hedgefox.ru/download/linux/", HfLinks.InstallFor(windows: false, macOs: false));
    }

    // spec «Настройки»: Russian even when the OS speaks another language; the HedgeFox settings have no picker,
    // so a language saved by v0.9 (it stored the OS language when Settings was opened) must not stick
    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    [InlineData("ru-RU")]
    public void ASavedLanguageBecomesRussian(string saved)
    {
        var storage = new TestStorage();
        storage.SetAsync(StorageConstants.Settings, new SettingsInfo(900, 600, false, true, 1024, saved)).Wait();
        Gml.Launcher.Assets.Resources.Resources.Culture = CultureInfo.GetCultureInfo(saved);

        HedgeFoxServices.UseRussian(storage);

        var settings = storage.GetAsync<SettingsInfo>(StorageConstants.Settings).Result!;
        Assert.Equal("ru-RU", settings.LanguageCode);
        Assert.Equal(1024, settings.RamValue);
        Assert.Equal("ru-RU", Gml.Launcher.Assets.Resources.Resources.Culture?.Name);
        Assert.Equal("ru-RU", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void NothingSavedStaysUnsavedButRussian()
    {
        var storage = new TestStorage();
        Gml.Launcher.Assets.Resources.Resources.Culture = CultureInfo.GetCultureInfo("en-US");

        HedgeFoxServices.UseRussian(storage);

        Assert.Null(storage.GetAsync<SettingsInfo>(StorageConstants.Settings).Result);
        Assert.Equal("ru-RU", Gml.Launcher.Assets.Resources.Resources.Culture?.Name);
        // the stock default settings take the UI culture
        Assert.Equal("ru-RU", SettingsInfo.Default.LanguageCode);
    }
}
