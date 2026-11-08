using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using Gml.Client;
using Gml.Client.Interfaces;
using Gml.Client.Models;
using Gml.Dto.Messages;
using Gml.Dto.Mods;
using Gml.Dto.News;
using Gml.Dto.Profile;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using Gml.Web.Api.Domains.System;
using GmlCore.Interfaces.Enums;
using Splat;

namespace HedgeFox.Shots;

/// <summary>
///     Fake services with fixed data, registered in Splat instead of ServiceLocator.RegisterServices: no network, no
///     disk outside the scratch folder, the same picture on every run.
/// </summary>
public static class Fakes
{
    public static readonly AuthLauncherUser User = new()
    {
        Name = "AppEzhik",
        Uuid = "0b3c5d2e-8f1a-4c6b-9d7e-2a4f6b8c0d1e",
        AccessToken = "fake-token",
        IsAuth = true,
        ExpiredDate = DateTime.Now.AddDays(7)
    };

    public static List<ProfileReadDto> Profiles() =>
    [
        Profile("HedgeFox", "HedgeFox", "Сезон 1: небо над Пеленой. Обычный профиль.", 6144, 100),
        Profile("HedgeFox-Shaders", "HedgeFox · Шейдеры", "Сезон 1: небо над Пеленой. Профиль с шейдерами для мощных ПК.", 8192, 90),
        Profile("HedgeFox-LowPC", "HedgeFox · Слабый ПК", "Сезон 1: небо над Пеленой. Лёгкий профиль для слабых ПК.", 4096, 80)
    ];

    private static ProfileReadDto Profile(string name, string display, string description, int ram, int priority) => new()
    {
        Name = name,
        DisplayName = display,
        Description = description,
        GameVersion = "1.21.1",
        LaunchVersion = "neoforge-21.1.265",
        RecommendedRam = ram,
        Priority = priority,
        IsEnabled = true,
        IconBase64 = string.Empty,
        Background = string.Empty,
        State = ProfileState.Ready,
        Servers = []
    };

    public static List<NewsReadDto> GmlNews() =>
    [
        new() { Title = "Меню и лаунчер", Content = "<p>Новое меню игры и лаунчер в стиле сайта.</p>", Date = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero), Type = NewsListenerType.Custom },
        new() { Title = "Набор на бету", Content = "<p>Ищем пиратов для беты.</p>", Date = new DateTimeOffset(2026, 11, 7, 18, 0, 0, TimeSpan.Zero), Type = NewsListenerType.Custom }
    ];

    /// <summary>Our API's answers: online 7 of 45, the Pelena at Y 80 rising to 96.</summary>
    public static readonly HfStatusSnapshot Status = new(HfStatusKind.Online, 7, 45, "1.21.1",
        new DateTimeOffset(2026, 11, 8, 13, 0, 0, TimeSpan.Zero), new HfPelena(80, 96));

    /// <summary>The owner's minimum: an 8 GB laptop without a graphics card.</summary>
    public static readonly HfHardware Hardware = new(8192, [new HfGpu("Intel Corporation", "Intel(R) UHD Graphics 620")]);

    public static readonly DateTimeOffset Now = new(2026, 11, 8, 16, 0, 0, TimeSpan.FromHours(3));

    public static IReadOnlyList<HfNewsItem> News() =>
    [
        new("tg:14", "tg", "Лаунчер в стиле сайта",
            "Новый лаунчер: ночь, пиксельные рамки и оранжевая кнопка «Играть». Профили теперь карточками.",
            "https://t.me/hedgefox_ru/14", new DateTimeOffset(2026, 11, 8, 12, 30, 0, TimeSpan.FromHours(3)), null),
        new("tg:13", "tg", "Пелена шевелится",
            "Ночью туман поднялся до Y 80 и не думает останавливаться. Капитаны, проверьте якоря и поднимите шлюпки " +
            "повыше: кто оставит склад у самой воды, утром будет искать его на ощупь. На мачтах уже видели зелёный " +
            "блеск — это не к добру. Совет от бывалых: держите пару факелов и ведро песка у трюма, а дирижабль — " +
            "на привязи. И не спорьте с Пеленой о курсе, она всё равно права. Подробности и карта уровней — на сайте, " +
            "в разделе про сезон.",
            "https://t.me/hedgefox_ru/13", new DateTimeOffset(2026, 11, 7, 21, 0, 0, TimeSpan.FromHours(3)), null),
        new("site:2026-11-08-menyu-i-launcher", "site", "Меню и лаунчер",
            "Меню игры и лаунчер переехали в стиль сайта.",
            "https://www.hedgefox.ru/novosti/2026-11-08-menyu-i-launcher/",
            new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.FromHours(3)), null),
        new("site:2026-11-07-nabor-na-betu", "site", "Набор на бету",
            "Ищем пиратов для беты. Без заявок и без лицензии.",
            "https://www.hedgefox.ru/novosti/2026-11-07-nabor-na-betu/",
            new DateTimeOffset(2026, 10, 30, 18, 0, 0, TimeSpan.FromHours(3)), null)
    ];

    public static void Register(string scratch)
    {
        var storage = new MemoryStorage();
        var system = new FakeSystem(scratch);
        var manager = FakeProxy.Create<IGmlClientManager>(GmlClient(scratch));
        var m = Locator.CurrentMutable;
        m.RegisterConstant<ILocalizationService>(new ResourceLocalizationService());
        m.RegisterConstant<ISystemService>(system);
        m.RegisterConstant(new LogHandler());
        m.RegisterConstant(manager);
        m.RegisterConstant<IStorageService>(storage);
        m.RegisterConstant<IVpnChecker>(new NoVpn());
        m.RegisterConstant<IBackendChecker>(new OnlineBackend());
        m.RegisterConstant<ISettingsService>(new SettingsService(system, storage));

        var skin = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "skin.png"));
        m.RegisterConstant<IStatusService>(new FixedStatus());
        m.RegisterConstant<INewsService>(new FixedNews());
        m.RegisterConstant<ISkinService>(new FixedSkin(skin));
        m.RegisterConstant<IHardwareProbe>(new FixedHardware());
    }

    /// <summary>What the fake status service answers now (a shot can switch to «Нет данных» or «Сервер спит»).</summary>
    public static HfStatusSnapshot StatusNow { get; set; } = Status;

    /// <summary>The fake feed fails, as our API does until it ships the endpoint.</summary>
    public static bool NewsFails { get; set; }

    /// <summary>The GML sign-in server did not answer at start: the launcher runs in offline mode.</summary>
    public static bool BackendOffline { get; set; }

    private sealed class FixedStatus : IStatusService
    {
        public HfStatusSnapshot Current => StatusNow;
        public IObservable<HfStatusSnapshot> Updates => Observable.Defer(() => Observable.Return(StatusNow));
        public Task<HfStatusSnapshot> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(StatusNow);
    }

    private sealed class FixedNews : INewsService
    {
        public Task<HfNewsResult> LoadAsync(int limit = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult(NewsFails ? HfNewsResult.Error : new HfNewsResult(News().Take(limit).ToList(), false));
    }

    private sealed class FixedSkin(byte[] png) : ISkinService
    {
        public Task<HfSkin> GetAsync(string uuid, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HfSkin(png, false));
    }

    private sealed class FixedHardware : IHardwareProbe
    {
        public Task<HfHardware> ReadAsync() => Task.FromResult(Hardware);
    }

    private static Func<MethodInfo, object?[]?, object?> GmlClient(string scratch)
    {
        var gameLoader = FakeProxy.Create<IGameLoader>((_, _) => null);
        return (method, args) => method.Name switch
        {
            "get_ProjectName" => "HedgeFox",
            "get_InstallationDirectory" => @"C:\Users\Player\AppData\Roaming\.hedgefox",
            "get_SkipUpdate" => true,
            // a closed local port: the splash's Sentry lookup fails at once instead of going to the internet
            "get_HostUri" => new Uri("http://127.0.0.1:9/"),
            "get_Gameloader" => gameLoader,
            "GetProfiles" or "GetOfflineProfiles" => Task.FromResult(new ResponseMessage<List<ProfileReadDto>> { Data = Profiles() }),
            "GetNews" => Task.FromResult(new ResponseMessage<List<NewsReadDto>> { Data = GmlNews() }),
            "GetOptionalMods" => Task.FromResult(new ResponseMessage<List<ModReadDto>>
            {
                Data = [new ModReadDto { Name = "minimap" }, new ModReadDto { Name = "sound-physics" }]
            }),
            "GetOptionalModsInfo" => Task.FromResult(new ResponseMessage<List<ModsDetailsInfoDto>>
            {
                Data =
                [
                    new ModsDetailsInfoDto { Key = "minimap.jar", Title = "Мини-карта", Description = "Карта в углу экрана." },
                    new ModsDetailsInfoDto
                    {
                        Key = "sound-physics.jar", Title = "Объёмный звук",
                        Description = "Эхо в пещерах и трюмах. Нагружает процессор."
                    }
                ]
            }),
            _ => FakeProxy.Default
        };
    }

    private sealed class NoVpn : IVpnChecker
    {
        public bool IsUseVpnTunnel() => false;
    }

    private sealed class OnlineBackend : IBackendChecker
    {
        public Task<bool> BackendIsActive() => Task.FromResult(!BackendOffline);
        public Task UpdateBackendStatus() => Task.CompletedTask;
        public bool IsOffline => BackendOffline;
    }

    private sealed class MemoryStorage : IStorageService
    {
        private readonly ConcurrentDictionary<string, object?> _values = new();

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

    private sealed class FakeSystem(string scratch) : ISystemService
    {
        private readonly SystemService _real = new();
        public string GetApplicationFolder() => scratch;
        public string GetGameFolder(string additionalPath, bool needCreate) => Path.Combine(scratch, "game", additionalPath);
        public ulong GetMaxRam() => 16384;
        public OsType GetOsType() => OsType.Windows;
        public IEnumerable<Language> GetAvailableLanguages() => _real.GetAvailableLanguages();
        public string GetHwid() => "fake-hwid";
        public Task LoadSystemData() => Task.CompletedTask;
        public bool IsDiskFull(IOException ioException) => false;
        public string GetOsArchitecture() => "64";
    }
}

/// <summary>An interface fake: the handler answers what it knows, the rest gets empty values (never null tasks).</summary>
public class FakeProxy : DispatchProxy
{
    public static readonly object Default = new();
    private Func<MethodInfo, object?[]?, object?> _handler = (_, _) => Default;

    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = Create<T, FakeProxy>();
        ((FakeProxy)(object)proxy)._handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null)
            return null;
        var answer = _handler(method, args);
        return ReferenceEquals(answer, Default) || answer is null ? Empty(method.ReturnType) : answer;
    }

    private static object? Empty(Type type)
    {
        if (type == typeof(void))
            return null;
        if (type == typeof(Task))
            return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner)
                .Invoke(null, [inner.IsValueType ? Activator.CreateInstance(inner) : null]);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IObservable<>))
            return Activator.CreateInstance(typeof(Subject<>).MakeGenericType(type.GetGenericArguments()[0]));
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
