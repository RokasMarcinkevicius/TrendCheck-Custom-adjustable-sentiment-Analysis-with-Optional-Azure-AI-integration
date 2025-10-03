using System; // for Uri, TimeSpan
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Application.Abstractions;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.Translation.Text;
using Infrastructure.Nlp;

namespace Infrastructure.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArticleAnalyzer(this IServiceCollection services, IConfiguration configuration)
    {
        // Always register local so "engine":"local" works even when Azure is enabled
        services.AddSingleton<IArticleAnalyzer, LocalArticleAnalyzer>();

        var taEndpoint = configuration["Azure:TextAnalytics:Endpoint"];
        var taKey      = configuration["Azure:TextAnalytics:Key"];

        // Throttle seconds: try Processing:..., then Azure:..., default 30
        var minSec =
            configuration.GetValue<int?>("Processing:MinSecondsBetweenAzureCalls")
            ?? configuration.GetValue<int?>("Azure:MinSecondsBetweenCalls")
            ?? 30;

        if (!string.IsNullOrWhiteSpace(taEndpoint) && !string.IsNullOrWhiteSpace(taKey))
        {
            var trKey    = configuration["Azure:Translator:Key"];
            var trRegion = configuration["Azure:Translator:Region"];

            services.AddSingleton<IArticleAnalyzer>(_ =>
            {
                // Required: Text Analytics client
                var taClient = new TextAnalyticsClient(new Uri(taEndpoint), new AzureKeyCredential(taKey));

                // Optional: Translator client
                TextTranslationClient? ttClient = null;
                if (!string.IsNullOrWhiteSpace(trKey) && !string.IsNullOrWhiteSpace(trRegion))
                {
                    ttClient = new TextTranslationClient(new AzureKeyCredential(trKey), trRegion);
                }

                // Use positional arguments to match your constructor signature
                return new AzureArticleAnalyzer(
                    taClient,
                    ttClient,
                    TimeSpan.FromSeconds(Math.Max(1, minSec))
                );
            });
        }

        return services;
    }
}