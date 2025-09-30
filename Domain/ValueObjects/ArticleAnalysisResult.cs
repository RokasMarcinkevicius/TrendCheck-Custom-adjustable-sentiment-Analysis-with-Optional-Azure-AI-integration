
namespace Domain.ValueObjects;

public sealed record ArticleAnalysisResult(
    string Company,
    string? Ticker,
    string Direction,
    double Magnitude,
    double SentimentScore,
    string? EvidenceSnippet,
    string? Translation = null
);
