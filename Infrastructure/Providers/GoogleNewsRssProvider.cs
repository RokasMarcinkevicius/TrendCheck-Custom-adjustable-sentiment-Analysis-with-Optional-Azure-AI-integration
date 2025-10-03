using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Options;
using Infrastructure.Utils;
using Infrastructure.Providers.Base;

namespace Infrastructure.Providers;

public sealed class GoogleNewsRssProvider(GoogleNewsOptions options) : INewsProvider
{
    public string Name => "GoogleNews";
    public TimeSpan MinPollInterval => TimeSpan.FromSeconds(Math.Max(60, options.PollIntervalSeconds));

    public async Task<IReadOnlyList<Article>> FetchAsync(NewsQuery query, CancellationToken ct)
    {
        if (!options.Enabled) return Array.Empty<Article>();

        var q = BuildQuery(query);
        var url = $"https://news.google.com/rss/search?q={HttpUtility.UrlEncode(q)}&hl={options.Language}&gl={CountryToGl(options.Country)}&ceid={options.Country}:{LangToCeid(options.Language)}";

        var items = await RssReader.LoadAsync(url, ct);
        var list = new List<Article>(items.Count);
        foreach (var it in items.Take(options.PerQueryItems))
        {
            var title = it.Title?.Text?.Trim() ?? string.Empty;
            var link  = it.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty;
            var pub   = it.PublishDate != default ? it.PublishDate : (it.LastUpdatedTime != default ? it.LastUpdatedTime : DateTimeOffset.UtcNow);
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;

            list.Add(new Article
            {
                Id = HashHelper.Sha1($"{link}|{pub.UtcDateTime:o}"),
                Title = title,
                Url = link,
                Source = Name,
                PublishedAt = pub,
                Summary = it.Summary?.Text,
                Tickers = query.Tickers
            });
        }
        return list;
    }

    private static string BuildQuery(NewsQuery query)
    {
        var parts = new List<string>();
        if (query.Tickers is { Length: >0 })
        {
            // Google News often matches tickers with a $ prefix or plain symbol; include both
            var sym = query.Tickers.Select(t => $"(\"{t}\" OR ${t})");
            parts.Add(string.Join(" OR ", sym));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
            parts.Add(query.Search!.Trim());
        return parts.Count > 0 ? string.Join(" ", parts) : "stocks";
    }

    private static string CountryToGl(string country) => country; // keep raw (e.g., US)
    private static string LangToCeid(string lang) => lang;        // keep raw (e.g., en-US → use as-is)
}