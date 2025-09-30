
using Application.Abstractions;
using Application.Models;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.Translation.Text;
using Domain.ValueObjects;

namespace Infrastructure.Azure;

public sealed class AzureArticleAnalyzer : IArticleAnalyzer, IAsyncDisposable
{
    public string EngineName => "azure";

    private readonly TextAnalyticsClient _ta;
    private readonly TextTranslationClient? _tt;
    private readonly AzureThrottledQueue<(string text, bool translate, CompanyDirectory directory, TaskCompletionSource<ArticleAnalysisResult> tcs)> _queue;

    public AzureArticleAnalyzer(TextAnalyticsClient ta, TextTranslationClient? tt, TimeSpan minIntervalBetweenCalls)
    {
        _ta = ta;
        _tt = tt;
        _queue = new AzureThrottledQueue<(string, bool, CompanyDirectory, TaskCompletionSource<ArticleAnalysisResult>)>(
            minIntervalBetweenCalls, HandleAsync);
    }

    public async Task<ArticleAnalysisResult> AnalyzeAsync(string text, CompanyDirectory directory, bool translate, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<ArticleAnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.EnqueueAsync((text, translate, directory, tcs));
        return await tcs.Task.WaitAsync(ct);
    }

    private async Task HandleAsync((string text, bool translate, CompanyDirectory directory, TaskCompletionSource<ArticleAnalysisResult> tcs) job, CancellationToken ct)
    {
        try
        {
            var doc = job.text;

            // Entities
            var entResp = await _ta.RecognizeEntitiesAsync(doc, cancellationToken: ct);
            string? company = null;
            foreach (var ent in entResp.Value)
            {
                if (ent.Category == EntityCategory.Organization)
                {
                    company = ent.Text;
                    break;
                }
            }

            // Sentiment
            var sentResp = await _ta.AnalyzeSentimentAsync(doc, cancellationToken: ct);
            var s = sentResp.Value;
            double pos = s.ConfidenceScores.Positive;
            double neg = s.ConfidenceScores.Negative;
            double score = pos - neg;
            string direction = Math.Abs(score) < 0.1 ? "Neutral" : (score > 0 ? "Up" : "Down");
            double magnitude = Math.Min(1.0, Math.Abs(score) * 1.2);

            var bestSentence = s.Sentences
                                .OrderByDescending(x => Math.Abs(x.ConfidenceScores.Positive - x.ConfidenceScores.Negative))
                                .Select(x => x.Text)
                                .FirstOrDefault() ?? doc.Substring(0,Math.Min(doc.Length, 160));

            string? translation = null;
            if (job.translate && _tt is not null)
            {
                var tr = await _tt.TranslateAsync("lt", bestSentence, cancellationToken: ct);
                translation = tr.Value.FirstOrDefault()?.Translations.FirstOrDefault()?.Text;
            }

            var (resolved, ticker) = (company is not null) ? job.directory.Resolve(company) : (null, null);
            var result = new ArticleAnalysisResult(resolved ?? company ?? "Unknown", ticker, direction,
                                                   Math.Round(magnitude,3), Math.Round(score,3), bestSentence, translation);
            job.tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            job.tcs.TrySetException(ex);
        }
    }

    public async ValueTask DisposeAsync() => await _queue.DisposeAsync();
}
