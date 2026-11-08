using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Views.HedgeFox.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The status line of the login and main screens (online, the Pelena) and the latest news from our API
///     (spec «Пелена на виджете», «Статус»). Never shows a made-up «0»: without an answer the line is hidden.
///     <see cref="Start" /> polls while a screen shows it; dispose the result when the screen goes.
/// </summary>
public sealed class HfServerInfoModel : ReactiveObject
{
    public static readonly TimeSpan NewsEvery = TimeSpan.FromMinutes(2);

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private readonly IStatusService? _status;
    private readonly INewsService? _news;
    private readonly Func<DateTimeOffset> _clock;
    private readonly int _newsLimit;

    public HfServerInfoModel(IStatusService? status, INewsService? news, Func<DateTimeOffset>? clock = null, int newsLimit = 10)
    {
        _status = status;
        _news = news;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _newsLimit = newsLimit;
        OpenNewsCommand = ReactiveCommand.Create<HfNewsCard>(card => HfSession.OpenLink(card.Url));
        Apply(status?.Current ?? HfStatusSnapshot.Loading);
    }

    /// <summary>«Now» for the news dates; the screenshot harness fixes it so its pictures do not change.</summary>
    public static Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.Now;

    /// <summary>With the app's services (Splat); <paramref name="newsLimit" /> 0 reads no news at all.</summary>
    public static HfServerInfoModel FromServices(int newsLimit) =>
        new(Locator.Current.GetService<IStatusService>(),
            newsLimit > 0 ? Locator.Current.GetService<INewsService>() : null, Clock, newsLimit);

    [Reactive] public string StatusLine { get; private set; } = HfStrings.StatusLoading;

    /// <summary>«Онлайн 7 из 45 · Пелена поднимается» or «Сервер спит»; empty (and hidden) without data.</summary>
    [Reactive] public string ShortLine { get; private set; } = string.Empty;
    [Reactive] public bool HasShortLine { get; private set; }

    /// <summary>The same without the Pelena (the sign-in screen): «Онлайн 7 из 45» or «Сервер спит».</summary>
    [Reactive] public string OnlineLine { get; private set; } = string.Empty;
    [Reactive] public HfDotState Dot { get; private set; } = HfDotState.Waiting;
    [Reactive] public bool HasPelena { get; private set; }
    [Reactive] public string PelenaTitle { get; private set; } = string.Empty;
    [Reactive] public string? PelenaTrend { get; private set; }
    [Reactive] public bool HasPelenaTrend { get; private set; }
    [Reactive] public bool PelenaShowBar { get; private set; }
    [Reactive] public double PelenaPercent { get; private set; }

    public ObservableCollection<HfNewsCard> News { get; } = [];
    [Reactive] public string? NewsMessage { get; private set; }
    [Reactive] public bool HasNewsMessage { get; private set; }

    /// <summary>The newest post (the one strip on the main screen), or null.</summary>
    [Reactive] public HfNewsCard? Latest { get; private set; }
    [Reactive] public bool HasLatest { get; private set; }

    public ReactiveCommand<HfNewsCard, Unit> OpenNewsCommand { get; }

    public static HfDotState DotFor(HfStatusKind kind) => kind switch
    {
        HfStatusKind.Online => HfDotState.Online,
        HfStatusKind.Offline => HfDotState.Offline,
        HfStatusKind.Loading => HfDotState.Waiting,
        _ => HfDotState.Unknown
    };

    public void Apply(HfStatusSnapshot snapshot)
    {
        StatusLine = HfStrings.StatusLine(snapshot);
        ShortLine = HfStrings.StatusShort(snapshot) ?? string.Empty;
        HasShortLine = ShortLine.Length > 0;
        OnlineLine = HfStrings.StatusShort(snapshot, withPelena: false) ?? string.Empty;
        Dot = DotFor(snapshot.Kind);
        // the Pelena only means something while the server is up
        var pelena = snapshot.Kind == HfStatusKind.Online ? HfPelenaView.From(snapshot.Pelena) : null;
        HasPelena = pelena is not null;
        // Tiny5 reads well only in capitals
        PelenaTitle = pelena?.Title.ToUpper(Ru) ?? string.Empty;
        PelenaTrend = pelena?.Trend;
        HasPelenaTrend = PelenaTrend is not null;
        PelenaShowBar = pelena?.ShowBar == true;
        PelenaPercent = (pelena?.Fraction ?? 0) * 100;
    }

    public async Task LoadNewsAsync()
    {
        if (_news is null)
            return;
        var result = await _news.LoadAsync(_newsLimit);
        var now = _clock();
        News.Clear();
        foreach (var item in result.Items)
            News.Add(HfNewsCard.From(item, now));
        NewsMessage = HfNewsCard.Message(result);
        HasNewsMessage = NewsMessage is not null;
        Latest = News.Count > 0 ? News[0] : null;
        HasLatest = Latest is not null;
    }

    /// <summary>Follows the status (our API is polled every 30 s while this runs) and reloads the news now and then.</summary>
    public IDisposable Start()
    {
        var d = new CompositeDisposable();
        if (_status is not null)
            d.Add(_status.Updates.ObserveOn(RxApp.MainThreadScheduler).Subscribe(Apply, _ => { }));
        if (_news is not null)
            d.Add(Observable.Timer(TimeSpan.Zero, NewsEvery, RxApp.MainThreadScheduler)
                .Select(_ => Observable.FromAsync(LoadNewsAsync))
                .Concat()
                .Subscribe(_ => { }, _ => { }));
        return d;
    }
}
