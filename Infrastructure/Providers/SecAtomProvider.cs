using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Options;
using Infrastructure.Utils;
using Infrastructure.Providers.Base;

namespace Infrastructure.Providers;

public sealed class SecAtomProvider(SecOptions options) : INewsProvider
{
    public string Name => "SEC";
    public TimeSpan MinPollInterval => TimeSpan.FromSeconds(Math.Max(300, options.PollIntervalSeconds));

    public async Task<IReadOnlyList<Article>> FetchAsync(NewsQuery query, CancellationToken ct)
    {
        if (!options.Enabled) return Array.Empty<Article>();
        var items = await RssReader.LoadAsync(options.AtomUrl, ct);
        var list = new List<Article>(items.Count);
        foreach (var it in items)
        {
            var title = it.Title?.Text?.Trim();
            var link  = it.Links.FirstOrDefault()?.Uri?.ToString();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;

            // Light filter by tickers/search if provided (best effort without CIK map)
            if (query.Tickers is { Length: >0 } && !query.Tickers.Any(t => title!.Contains(t, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!string.IsNullOrWhiteSpace(query.Search) && (title!.Contains(query.Search!, StringComparison.OrdinalIgnoreCase) == false))
                continue;

            var pub = it.PublishDate != default ? it.PublishDate : (it.LastUpdatedTime != default ? it.LastUpdatedTime : DateTimeOffset.UtcNow);
            list.Add(new Article
            {
                Id = HashHelper.Sha1($"{link}|{pub.UtcDateTime:o}"),
                Title = title!,
                Url = link!,
                Source = Name,
                PublishedAt = pub,
                Summary = it.Summary?.Text,
                Tickers = query.Tickers
            });
        }
        return list;
    }
}