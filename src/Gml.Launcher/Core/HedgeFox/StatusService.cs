using System;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.HedgeFox;

public interface IStatusService
{
    /// <summary>The latest snapshot (Loading before the first answer).</summary>
    HfStatusSnapshot Current { get; }

    /// <summary>
    ///     The current snapshot, then every new one. While anyone listens, our API is polled every 30 s.
    ///     Values arrive on a background thread: <c>ObserveOn(RxApp.MainThreadScheduler)</c> before touching the UI.
    /// </summary>
    IObservable<HfStatusSnapshot> Updates { get; }

    Task<HfStatusSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     GET https://www.hedgefox.ru/api/launcher/status (cached 30 s on the server). A failed poll keeps the last good
///     answer for 90 s, so one network blip does not flash «Нет данных»; after that the window says so.
/// </summary>
public sealed class StatusService : IStatusService
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(90);

    private readonly HttpClient _http;
    private readonly string _url;
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;
    private readonly BehaviorSubject<HfStatusSnapshot> _subject = new(HfStatusSnapshot.Loading);
    private readonly IObservable<HfStatusSnapshot> _poll;
    private readonly object _gate = new();
    private HfStatusSnapshot? _lastGood;
    private DateTimeOffset _lastGoodAt;

    public StatusService(HttpClient http, string? url = null, TimeSpan? timeout = null, Func<DateTimeOffset>? clock = null)
    {
        _http = http;
        _url = url ?? HfLinks.StatusApi;
        _timeout = timeout ?? DefaultTimeout;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _poll = Observable.Timer(TimeSpan.Zero, RefreshEvery)
            .Select(_ => Observable.FromAsync(RefreshAsync))
            .Concat()
            .Publish()
            .RefCount();
        Updates = Observable.Create<HfStatusSnapshot>(observer =>
            new CompositeDisposable(_subject.Subscribe(observer), _poll.Subscribe(_ => { }, _ => { })));
    }

    public HfStatusSnapshot Current => _subject.Value;
    public IObservable<HfStatusSnapshot> Updates { get; }

    public async Task<HfStatusSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        HfStatusSnapshot answer;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);
            using var response = await _http.GetAsync(_url, cts.Token).ConfigureAwait(false);
            answer = response.IsSuccessStatusCode
                ? HfStatusParser.Parse(await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false))
                : HfStatusSnapshot.NoData;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            answer = HfStatusSnapshot.NoData; // offline, DNS, timeout, TLS: all «Нет данных»
        }

        HfStatusSnapshot result;
        lock (_gate)
        {
            var now = _clock();
            if (answer.Kind != HfStatusKind.NoData)
            {
                _lastGood = answer;
                _lastGoodAt = now;
                result = answer;
            }
            else
            {
                result = _lastGood is not null && now - _lastGoodAt <= StaleAfter ? _lastGood : HfStatusSnapshot.NoData;
            }
        }

        _subject.OnNext(result);
        return result;
    }
}
