using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Background;

public sealed class NewsPollingService : BackgroundService
{
    private readonly ILogger<NewsPollingService> _log;
    private readonly INewsProvider[] _providers;
    private readonly IArticleStore _store;
    private readonly NewsOptions _opts;

    private readonly Dictionary<string, DateTimeOffset> _lastRun = new();

    public NewsPollingService(ILogger<NewsPollingService> log, IEnumerable<INewsProvider> providers, IArticleStore store, IOptions<NewsOptions> options)
    {
        _log = log;
        _providers = providers.ToArray();
        _store = store;
        _opts = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("NewsPollingService started with {ProviderCount} providers", _providers.Length);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var query = new NewsQuery(Search: null, Tickers: _opts.WatchlistTickers, Limit: 200);
                var now = DateTimeOffset.UtcNow;

                foreach (var p in _providers)
                {
                    var last = _lastRun.TryGetValue(p.Name, out var ts) ? ts : DateTimeOffset.MinValue;
                    if (now - last < p.MinPollInterval) continue; // respect per-provider poll interval

                    _lastRun[p.Name] = now;
                    _ = PollOneAsync(p, query, stoppingToken); // fire-and-forget; do not block the loop
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Polling loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // base tick
        }
    }

    private async Task PollOneAsync(INewsProvider provider, NewsQuery query, CancellationToken ct)
    {
        try
        {
            var list = await provider.FetchAsync(query, ct);
            if (list.Count > 0)
            {
                await _store.UpsertAsync(list, ct);
                _log.LogInformation("{Provider} fetched {Count} articles", provider.Name, list.Count);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "{Provider} fetch failed", provider.Name);
        }
    }
}
