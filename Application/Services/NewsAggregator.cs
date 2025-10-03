using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Entities;

namespace Application.Services;

public sealed class NewsAggregator(INewsProvider[] providers, IArticleStore store) : INewsAggregator
{
    private readonly INewsProvider[] _providers = providers;
    private readonly IArticleStore _store = store;

    public async Task<int> PollAllAsync(NewsQuery query, CancellationToken ct)
    {
        var tasks = _providers.Select(p => p.FetchAsync(query, ct));
        var results = await Task.WhenAll(tasks);
        var merged = results.SelectMany(x => x).ToList();
        if (merged.Count > 0)
            await _store.UpsertAsync(merged, ct);
        return merged.Count;
    }
}