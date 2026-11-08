using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.HedgeFox;

public interface INewsService
{
    /// <summary>The feed (Telegram and the site, newest first), cached for a minute. Never throws.</summary>
    Task<HfNewsResult> LoadAsync(int limit = 10, CancellationToken cancellationToken = default);
}

/// <summary>GET https://www.hedgefox.ru/api/launcher/news?limit=N. The launcher never reads t.me itself.</summary>
public sealed class NewsService : INewsService
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private (int Limit, DateTimeOffset At, HfNewsResult Result)? _cache;

    public NewsService(HttpClient http, TimeSpan? timeout = null, Func<DateTimeOffset>? clock = null)
    {
        _http = http;
        _timeout = timeout ?? StatusService.DefaultTimeout;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<HfNewsResult> LoadAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cache is { } c && c.Limit == limit && _clock() - c.At < CacheFor)
                return c.Result;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);
            using var response = await _http.GetAsync(HfLinks.NewsApi(limit), cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return HfNewsResult.Error;
            var items = HfNewsParser.Parse(await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false), limit);
            if (items is null)
                return HfNewsResult.Error;
            var result = new HfNewsResult(items, false);
            lock (_gate)
                _cache = (limit, _clock(), result);
            return result;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HfNewsResult.Error;
        }
    }
}
