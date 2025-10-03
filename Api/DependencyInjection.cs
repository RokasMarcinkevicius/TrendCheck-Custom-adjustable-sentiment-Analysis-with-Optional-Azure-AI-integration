using Application.Abstractions;
using Application.Services;
using Infrastructure.Background;
using Infrastructure.Options;
using Infrastructure.Providers;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Api;

public static class DependencyInjection
{
    public static IServiceCollection AddNewsIngestion(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<NewsOptions>(cfg.GetSection("News"));

        // Application layer
        services.AddSingleton<INewsAggregator, NewsAggregator>();

        // Storage (swap with DB later)
        services.AddSingleton<IArticleStore, InMemoryArticleStore>();

        // Providers
        var gn = cfg.GetSection("News:GoogleNews").Get<GoogleNewsOptions>() ?? new();
        var re = cfg.GetSection("News:Reuters").Get<ReutersRssOptions>() ?? new();
        var se = cfg.GetSection("News:Sec").Get<SecOptions>() ?? new();
        var fmp= cfg.GetSection("News:Fmp").Get<FmpOptions>() ?? new();

        services.AddSingleton<INewsProvider>(new GoogleNewsRssProvider(gn));
        services.AddSingleton<INewsProvider>(new ReutersRssProvider(re));
        services.AddSingleton<INewsProvider>(new SecAtomProvider(se));
        services.AddHttpClient<FmpNewsProvider>();
        services.AddSingleton<INewsProvider>(sp => new FmpNewsProvider(sp.GetRequiredService<System.Net.Http.HttpClient>(), fmp));

        // Background polling
        services.AddHostedService<NewsPollingService>();
        return services;
    }
}