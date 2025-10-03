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

public sealed class ReutersRssProvider(ReutersRssOptions options) : INewsProvider
{
    public string Name => "ReutersRSS";
    public TimeSpan MinPollInterval => TimeSpan.FromSeconds(Math.Max(180, options.PollIntervalSeconds));

    public async Task<IReadOnlyList<Article>> FetchAsync(NewsQuery query, CancellationToken ct)
    {
        if (!options.Enabled) return Array.Empty<Article>();

        var results = new List<Article>();
        foreach (var feed in options.Feeds)
        {
            var items = await RssReader.LoadAsync(feed, ct);
            foreach (var it in items)
            {
                var title = it.Title?.Text?.Trim();
                var link  = it.Links.FirstOrDefault()?.Uri?.ToString();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;

                // Filter by tickers/search if provided
                if (query.Tickers is { Length: >0 } && !query.Tickers.Any(t => title!.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (!string.IsNullOrWhiteSpace(query.Search) && (title!.Contains(query.Search!, StringComparison.OrdinalIgnoreCase) == false))
                    continue;

                var pub = it.PublishDate != default ? it.PublishDate : (it.LastUpdatedTime != default ? it.LastUpdatedTime : DateTimeOffset.UtcNow);
                results.Add(new Article
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
        }
        return results;
    }
}