
using Application.Models;
using Domain.ValueObjects;

namespace Application.Abstractions;

public interface IArticleAnalyzer
{
    string EngineName { get; }
    Task<ArticleAnalysisResult> AnalyzeAsync(string text, CompanyDirectory directory, bool translate, CancellationToken ct = default);
}
