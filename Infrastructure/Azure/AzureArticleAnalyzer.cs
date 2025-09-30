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

    private readonly TextAnalyticsClient _textAnalyticsClient;
    private readonly TextTranslationClient? _textTranslationClient;
    private readonly AzureThrottledQueue<(string Text, bool Translate, CompanyDirectory CompanyDirectory, TaskCompletionSource<ArticleAnalysisResult> CompletionSource)> _analysisQueue;

    public AzureArticleAnalyzer(TextAnalyticsClient textAnalyticsClient, TextTranslationClient? textTranslationClient, TimeSpan minIntervalBetweenCalls)
    {
        _textAnalyticsClient = textAnalyticsClient;
        _textTranslationClient = textTranslationClient;
        _analysisQueue = new AzureThrottledQueue<(string, bool, CompanyDirectory, TaskCompletionSource<ArticleAnalysisResult>)>(
            minIntervalBetweenCalls, HandleAsync);
    }

    public async Task<ArticleAnalysisResult> AnalyzeAsync(string text, CompanyDirectory directory, bool translate, CancellationToken cancellationToken = default)
    {
        var completionSource = new TaskCompletionSource<ArticleAnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _analysisQueue.EnqueueAsync((text, translate, directory, completionSource));
        return await completionSource.Task.WaitAsync(cancellationToken);
    }

    private async Task HandleAsync(
        (string Text, bool Translate, CompanyDirectory CompanyDirectory, TaskCompletionSource<ArticleAnalysisResult> CompletionSource) job,
        CancellationToken cancellationToken)
    {
        try
        {
            string document = job.Text;

            // Entities (organization / company)
            Response<CategorizedEntityCollection> entityResponse =
                await _textAnalyticsClient.RecognizeEntitiesAsync(document, cancellationToken: cancellationToken);

            string? companyName = null;
            foreach (CategorizedEntity entity in entityResponse.Value)
            {
                if (entity.Category == EntityCategory.Organization)
                {
                    companyName = entity.Text;
                    break;
                }
            }

            // Sentiment
            Response<DocumentSentiment> sentimentResponse =
                await _textAnalyticsClient.AnalyzeSentimentAsync(document, cancellationToken: cancellationToken);

            DocumentSentiment overallSentiment = sentimentResponse.Value;

            double positiveScore = overallSentiment.ConfidenceScores.Positive;
            double negativeScore = overallSentiment.ConfidenceScores.Negative;
            double overallScore = positiveScore - negativeScore;

            string direction = Math.Abs(overallScore) < 0.10 ? "Neutral" : (overallScore > 0 ? "Up" : "Down");
            double magnitude = Math.Min(1.0, Math.Abs(overallScore) * 1.2);

            string bestSentence =
                overallSentiment.Sentences
                    .OrderByDescending(sentence => Math.Abs(sentence.ConfidenceScores.Positive - sentence.ConfidenceScores.Negative))
                    .Select(sentence => sentence.Text)
                    .FirstOrDefault()
                ?? document[..Math.Min(document.Length, 160)];

            // Optional translation (still uses Lithuanian "lt" if Translate=true and translator is configured)
            string? translation = null;
            if (job.Translate && _textTranslationClient is not null)
            {
                Response<IReadOnlyList<TranslatedTextItem>> translationResponse =
                    await _textTranslationClient.TranslateAsync("lt", bestSentence, cancellationToken: cancellationToken);

                translation = translationResponse.Value
                    .FirstOrDefault()?
                    .Translations
                    .FirstOrDefault()?
                    .Text;
            }

            (string? resolvedCompanyName, string? ticker) =
                companyName is not null ? job.CompanyDirectory.Resolve(companyName) : (null, null);

            var analysisResult = new ArticleAnalysisResult(
                Company: resolvedCompanyName ?? companyName ?? "Unknown",
                Ticker: ticker,
                Direction: direction,
                Magnitude: Math.Round(magnitude, 3),
                SentimentScore: Math.Round(overallScore, 3),
                EvidenceSnippet: bestSentence,
                Translation: translation
            );

            job.CompletionSource.TrySetResult(analysisResult);
        }
        catch (Exception exception)
        {
            job.CompletionSource.TrySetException(exception);
        }
    }

    public async ValueTask DisposeAsync() => await _analysisQueue.DisposeAsync();
}