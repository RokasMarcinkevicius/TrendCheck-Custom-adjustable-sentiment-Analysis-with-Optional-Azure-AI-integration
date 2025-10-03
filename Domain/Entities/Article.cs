namespace Domain.Entities;

public sealed record Article
{
    public required string Id { get; init; }               // stable hash (e.g., URL + published)
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Source { get; init; }           // provider name (e.g., GoogleNews, Reuters, SEC, FMP)
    public DateTimeOffset PublishedAt { get; init; }
    public string? Summary { get; init; }
    public string[]? Tickers { get; init; }                // extracted/assigned symbols
    public decimal? Sentiment { get; init; }               // optional (if provider supplies)
}



// =========================
// TrendCheck — News ingestion (controllers + services)
// Target: .NET 9, Clean Architecture projects: Domain / Application / Infrastructure / Api
// Drop each section into the indicated file path in your solution.
// Packages you may need:
//   Infrastructure.csproj:
//     <PackageReference Include="Microsoft.Windows.Compatibility" Version="8.0.6" />
//     <PackageReference Include="System.Threading.RateLimiting" Version="8.0.0" />
//   Api.csproj:
//     (no extra packages strictly required)
//
// Notes:
// - Providers included: Google News RSS (query/tickers), Reuters RSS (static feed list),
//   SEC current filings Atom (8-K/10-Q/10-K headlines), FMP Stock News (optional, API key).
// - Polling is enforced per-provider via MinPollInterval + internal last-run tracking.
// - Background service aggregates, normalizes and stores in an in-memory store (swap to DB later).
// - Controllers expose: GET /api/news, GET /api/news/sources, POST /api/news/poll.
//







// =========================
// FILE: Api/Program.cs (add inside builder.Services section)
// =========================
// builder.Services.AddNewsIngestion(builder.Configuration);

// =========================
// HTTP test (optional): tests/news.http
// =========================
/*

*/
