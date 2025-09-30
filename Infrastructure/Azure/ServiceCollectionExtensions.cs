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
        var taEndpoint = configuration["Azure:TextAnalytics:Endpoint"];
        var taKey = configuration["Azure:TextAnalytics:Key"];
        
        if (!string.IsNullOrWhiteSpace(taEndpoint) && !string.IsNullOrWhiteSpace(taKey))
        {
            var trKey = configuration["Azure:Translator:Key"];
            var trRegion = configuration["Azure:Translator:Region"];
            var minSec = int.Parse(configuration["Processing:MinSecondsBetweenAzureCalls"] ?? "30");
            
            services.AddSingleton<IArticleAnalyzer>(sp => 
            {
                var taClient = new TextAnalyticsClient(new Uri(taEndpoint), new AzureKeyCredential(taKey));
                TextTranslationClient? ttClient = null;
                if (!string.IsNullOrWhiteSpace(trKey) && !string.IsNullOrWhiteSpace(trRegion))
                    ttClient = new TextTranslationClient(new AzureKeyCredential(trKey), trRegion);
                
                return new AzureArticleAnalyzer(taClient, ttClient, TimeSpan.FromSeconds(minSec));
            });
        }
        else
        {
            services.AddSingleton<IArticleAnalyzer, LocalArticleAnalyzer>();
        }
        
        return services;
    }
}