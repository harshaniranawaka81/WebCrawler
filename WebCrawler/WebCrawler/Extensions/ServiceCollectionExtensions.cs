using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Services;
using WebCrawler.Infrastructure.Interfaces;
using WebCrawler.Infrastructure.Services;

namespace WebCrawler.Extensions
{
    /// <summary>
    /// Extension methods for configuring web crawler services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all crawler infrastructure services including HTTP client, rate limiting, and storage.
        /// </summary>
        public static IServiceCollection AddCrawlerInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var httpPoliciesSection = configuration.GetSection("HttpClientPolicies");
            var httpPolicies = httpPoliciesSection.Get<HttpClientPolicies>() ?? new HttpClientPolicies();

            RegisterLinkStorage(services, configuration);
            RegisterRateLimiting(services);
            RegisterHttpClient(services, httpPolicies);
            RegisterLinkExtraction(services);

            return services;
        }

        /// <summary>
        /// Configures all crawler core services including output writer, exception handler, and crawler.
        /// </summary>
        public static IServiceCollection AddCrawlerCore(this IServiceCollection services)
        {
            services.AddScoped<IOutputWriter, ConsoleOutputWriter>();
            services.AddScoped<IExceptionHandler, GlobalExceptionHandler>();
            services.AddScoped<ICrawler>(sp => CreateCrawler(sp));

            return services;
        }

        private static void RegisterLinkStorage(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ILinkStorage>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
                var logger = sp.GetRequiredService<ILogger<FileLinkStorage>>();
                return new FileLinkStorage(options.Value.VisitedPagesOutputFilePath, logger: logger);
            });
        }

        private static void RegisterRateLimiting(IServiceCollection services)
        {
            services.AddScoped<IRateLimiter>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<WebCrawlerSettings>>().Value;
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var composite = new CompositeRateLimiter(loggerFactory.CreateLogger<CompositeRateLimiter>());

                if (settings.DelayBetweenRequestsMs > 0)
                {
                    composite.Add(new DelayRateLimiter(
                        settings.DelayBetweenRequestsMs,
                        loggerFactory.CreateLogger<DelayRateLimiter>()));
                }

                if (settings.MaxConcurrentRequests.HasValue && settings.MaxConcurrentRequests > 0)
                {
                    composite.Add(new ConcurrentRequestLimiter(
                        settings.MaxConcurrentRequests.Value,
                        loggerFactory.CreateLogger<ConcurrentRequestLimiter>()));
                }

                return composite;
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
                    return WebCrawler.Core.Policies.HttpPolicyFactory.CreateCombinedPolicy(httpPolicies, logger);
                });

            // Override IPageFetcher with rate limiter injection
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
            services.AddScoped<IRobotsChecker, RobotsChecker>();
        }

        private static Crawler CreateCrawler(IServiceProvider sp)
        {
            var pageFetcher = sp.GetRequiredService<IPageFetcher>();
            var linkExtractor = sp.GetRequiredService<ILinkExtractor>();
            var outputWriter = sp.GetRequiredService<IOutputWriter>();
            var linkStorage = sp.GetRequiredService<ILinkStorage>();
            var exceptionHandler = sp.GetRequiredService<IExceptionHandler>();
            var robotsChecker = sp.GetRequiredService<IRobotsChecker>();
            var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
            var logger = sp.GetRequiredService<ILogger<Crawler>>();

            return new Crawler(
                pageFetcher,
                linkExtractor,
                outputWriter,
                linkStorage,
                options.Value.MaxPagesToCrawl,
                exceptionHandler,
                robotsChecker,
                options.Value.RespectRobotsTxt,
                logger);
        }
    }
}
