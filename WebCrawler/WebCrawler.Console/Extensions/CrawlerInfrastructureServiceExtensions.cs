using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;
using WebCrawler.Infrastructure.Http;
using WebCrawler.Infrastructure.Parsing;
using WebCrawler.Infrastructure.RateLimiting;
using WebCrawler.Infrastructure.Robots;
using WebCrawler.Infrastructure.Storage;

namespace WebCrawler.Extensions;

public static class CrawlerInfrastructureServiceExtensions
{
    public static IServiceCollection AddCrawlerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var httpPoliciesSection = configuration.GetSection("HttpClientPolicies");
        var httpPolicies = httpPoliciesSection.Get<HttpClientPolicies>() ?? new HttpClientPolicies();

        RegisterLinkStorage(services);
        RegisterRateLimiting(services);
        RegisterHttpClient(services, httpPolicies);
        RegisterLinkExtraction(services);

        return services;
    }

    private static void RegisterLinkStorage(IServiceCollection services)
    {
        services.AddSingleton<ILinkStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
            var logger = sp.GetRequiredService<ILogger<FileLinkStorage>>();
            return new FileLinkStorage(
                options.Value.VisitedPagesOutputFilePath,
                logger: logger,
                flushIntervalLinks: options.Value.StorageFlushIntervalLinks,
                writerBufferSize: options.Value.StorageWriterBufferSize);
        });
    }

    private static void RegisterRateLimiting(IServiceCollection services)
    {
        services.AddScoped<IRateLimiter>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<WebCrawlerSettings>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<CompositeRateLimiter>();

            var delayMs = settings.DelayBetweenRequestsMs > 0 ? settings.DelayBetweenRequestsMs : 0;
            var maxConcurrent = settings.MaxConcurrentRequests.HasValue && settings.MaxConcurrentRequests > 0
                ? settings.MaxConcurrentRequests.Value
                : 1;

            return new CompositeRateLimiter(delayMs, maxConcurrent, logger);
        });
    }

    private static void RegisterHttpClient(IServiceCollection services, HttpClientPolicies httpPolicies)
    {
        services.AddHttpClient<IPageFetcher, HttpPageFetcher>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("HttpClientPolicies");
                return HttpPolicyFactory.CreateCombinedPolicy(httpPolicies, logger);
            });

        services.AddScoped<IPageFetcher>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(IPageFetcher));
            var logger = sp.GetRequiredService<ILogger<HttpPageFetcher>>();
            var rateLimiter = sp.GetRequiredService<IRateLimiter>();

            return new HttpPageFetcher(httpClient, logger, rateLimiter);
        });
    }

    private static void RegisterLinkExtraction(IServiceCollection services)
    {
        services.AddScoped<ILinkExtractor, HtmlLinkExtractor>();
        services.AddScoped<IRobotsChecker>(sp =>
        {
            var pageFetcher = sp.GetRequiredService<IPageFetcher>();
            var settings = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
            var logger = sp.GetRequiredService<ILogger<RobotsChecker>>();
            return new RobotsChecker(
                pageFetcher,
                logger,
                cacheExpirationMinutes: settings.Value.RobotsCacheExpirationMinutes,
                robotsFetchTimeoutSeconds: settings.Value.RobotsFetchTimeoutSeconds);
        });
    }
}
