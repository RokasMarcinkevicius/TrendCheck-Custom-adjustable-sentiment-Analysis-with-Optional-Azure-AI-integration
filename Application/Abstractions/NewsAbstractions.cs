using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Abstractions;

public sealed record NewsQuery(string? Search, string[]? Tickers, int Limit = 50);

public interface INewsProvider
{
    string Name { get; }
    TimeSpan MinPollInterval { get; }
    Task<IReadOnlyList<Article>> FetchAsync(NewsQuery query, CancellationToken ct);
}

public interface IArticleStore
{
    Task UpsertAsync(IEnumerable<Article> articles, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetAsync(NewsQuery query, CancellationToken ct);
    Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken ct);
}

public interface INewsAggregator
{
    Task<int> PollAllAsync(NewsQuery query, CancellationToken ct);
}