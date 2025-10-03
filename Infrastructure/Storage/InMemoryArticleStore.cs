using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.Storage;

public sealed class InMemoryArticleStore : IArticleStore
{
    private readonly ConcurrentDictionary<string, Article> _byId = new();

    public Task UpsertAsync(IEnumerable<Article> articles, CancellationToken ct)
    {
        foreach (var a in articles)
            _byId[a.Id] = a;
        // Trim older than 72h
        var cutoff = DateTimeOffset.UtcNow.AddHours(-72);
        foreach (var kv in _byId.ToArray())
            if (kv.Value.PublishedAt < cutoff) _byId.TryRemove(kv.Key, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Article>> GetAsync(NewsQuery query, CancellationToken ct)
    {
        IEnumerable<Article> q = _byId.Values;
        if (query.Tickers is { Length: >0 })
            q = q.Where(a => a.Tickers != null && a.Tickers.Any(t => query.Tickers.Contains(t, StringComparer.OrdinalIgnoreCase))
                             || query.Tickers.Any(t => a.Title.Contains(t, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(a => a.Title.Contains(query.Search!, StringComparison.OrdinalIgnoreCase)
                             || (a.Summary?.Contains(query.Search!, StringComparison.OrdinalIgnoreCase) ?? false));
        q = q.OrderByDescending(a => a.PublishedAt).Take(query.Limit);
        return Task.FromResult<IReadOnlyList<Article>>(q.ToList());
    }

    public Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken ct)
    {
        var sources = _byId.Values.Select(v => v.Source).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        return Task.FromResult<IReadOnlyList<string>>(sources);
    }
}