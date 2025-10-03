using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Options;
using Infrastructure.Utils;

namespace Infrastructure.Providers;

public sealed class FmpNewsProvider(HttpClient http, FmpOptions options) : INewsProvider
{
    public string Name => "FMP";
    public TimeSpan MinPollInterval => TimeSpan.FromSeconds(Math.Max(120, options.PollIntervalSeconds));

    public async Task<IReadOnlyList<Article>> FetchAsync(NewsQuery query, CancellationToken ct)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
            return Array.Empty<Article>();

        // FMP endpoint: https://financialmodelingprep.com/api/v3/stock_news?tickers=AAPL,MSFT&limit=50&apikey=KEY
        var tickers = (query.Tickers is { Length: > 0 }) ? string.Join(',', query.Tickers) : string.Empty;
        var url = $"https://financialmodelingprep.com/api/v3/stock_news?limit={options.Limit}&apikey={options.ApiKey}";
        if (!string.IsNullOrWhiteSpace(tickers)) url += $"&tickers={tickers}";

        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<Article>();

        var list = new List<Article>();
        foreach (var el in json.RootElement.EnumerateArray())
        {
            var title = el.GetProperty("title").GetString();
            var link  = el.GetProperty("url").GetString();
            var pubStr= el.TryGetProperty("publishedDate", out var p) ? p.GetString() : null;
            var site  = el.TryGetProperty("site", out var s) ? s.GetString() : "FMP";
            var text  = el.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;
            var pub = DateTimeOffset.TryParse(pubStr, out var dto) ? dto : DateTimeOffset.UtcNow;

            list.Add(new Article
            {
                Id = HashHelper.Sha1($"{link}|{pub.UtcDateTime:o}"),
                Title = title!,
                Url = link!,
                Source = site ?? Name,
                PublishedAt = pub,
                Summary = text,
                Tickers = query.Tickers
            });
        }
        return list;
    }
}
