using System;

namespace Infrastructure.Options;

public sealed class NewsOptions
{
    public string[] WatchlistTickers { get; set; } = Array.Empty<string>();
    public GoogleNewsOptions GoogleNews { get; set; } = new();
    public ReutersRssOptions Reuters { get; set; } = new();
    public SecOptions Sec { get; set; } = new();
    public FmpOptions Fmp { get; set; } = new();
}

public sealed class GoogleNewsOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 120; // be gentle
    public string Language { get; set; } = "en-US";
    public string Country { get; set; } = "US";
    public int PerQueryItems { get; set; } = 25;
}

public sealed class ReutersRssOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 300;
    // A few Reuters RSS endpoints; extend as needed
    public string[] Feeds { get; set; } = new[]
    {
        "https://www.reuters.com/markets/us/rss",
        "https://www.reuters.com/markets/asia/rss",
        "https://www.reuters.com/markets/europe/rss"
    };
}

public sealed class SecOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 600;
    // Current filings Atom (headlines). You can add specific filters later.
    public string AtomUrl { get; set; } = "https://www.sec.gov/cgi-bin/browse-edgar?action=getcurrent&output=atom";
}

public sealed class FmpOptions
{
    public bool Enabled { get; set; } = false;           // set true when you have a key
    public int PollIntervalSeconds { get; set; } = 300;
    public string? ApiKey { get; set; }
    public int Limit { get; set; } = 50;                 // FMP /stock_news limit
}