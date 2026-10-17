using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Policies;
using WebCrawler.Core.Services;
using WebCrawler.Infrastructure.Interfaces;
using WebCrawler.Infrastructure.Services;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Register services (Dependency Injection)
var services = new ServiceCollection();

// Configure logging
services.AddLogging(config =>
{
    config.AddConsole();
    config.AddConfiguration(configuration.GetSection("Logging"));
});

// Configuration
services.Configure<WebCrawlerSettings>(configuration.GetSection("WebCrawlerSettings"));
services.Configure<HttpClientPolicies>(configuration.GetSection("HttpClientPolicies"));

// Get policy settings for HTTP client configuration
var httpPoliciesSection = configuration.GetSection("HttpClientPolicies");
var httpPolicies = httpPoliciesSection.Get<HttpClientPolicies>() ?? new HttpClientPolicies();

// Link storage servi
services.AddSingleton<ILinkStorage>(sp =>
{
    var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
    var logger = sp.GetRequiredService<ILogger<FileLinkStorage>>();
    return new FileLinkStorage(options.Value.VisitedPagesOutputFilePath, logger: logger);
});

// Rate limiting configuration
services.AddScoped<IRateLimiter>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<WebCrawlerSettings>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var composite = new CompositeRateLimiter(loggerFactory.CreateLogger<CompositeRateLimiter>());

    // Add delay-based rate limiter
    if (settings.DelayBetweenRequestsMs > 0)
    {
        composite.Add(new DelayRateLimiter(
            settings.DelayBetweenRequestsMs,
            loggerFactory.CreateLogger<DelayRateLimiter>()));
    }

    // Add concurrent request limiter
    if (settings.MaxConcurrentRequests.HasValue && settings.MaxConcurrentRequests > 0)
    {
        composite.Add(new ConcurrentRequestLimiter(
            settings.MaxConcurrentRequests.Value,
            loggerFactory.CreateLogger<ConcurrentRequestLimiter>()));
    }

    return composite;
});

// Infrastructure services - HTTP Client with resilience policies
services.AddHttpClient<IPageFetcher, HttpPageFetcher>()
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigureHttpMessageHandlerBuilder(builder =>
    {
        // Rate limiter will be injected via constructor of HttpPageFetcher
    })
    .AddPolicyHandler((sp, _) =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("HttpClientPolicies");
        return HttpPolicyFactory.CreateCombinedPolicy(httpPolicies, logger);
    });

// Override the default IPageFetcher with our custom factory that injects rate limiter
services.AddScoped<IPageFetcher>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(IPageFetcher));
    var logger = sp.GetRequiredService<ILogger<HttpPageFetcher>>();
    var rateLimiter = sp.GetRequiredService<IRateLimiter>();

    return new HttpPageFetcher(httpClient, logger, rateLimiter);
});

services.AddScoped<ILinkExtractor, HtmlLinkExtractor>();

// Robots.txt checker
services.AddScoped<IRobotsChecker, RobotsChecker>();

// Core services
services.AddScoped<IOutputWriter, ConsoleOutputWriter>();
services.AddScoped<WebCrawler.Core.Interfaces.IExceptionHandler, GlobalExceptionHandler>();
services.AddScoped<ICrawler>(sp =>
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
        logger
    );
});

var serviceProvider = services.BuildServiceProvider();

await RunApplicationAsync();
return;

async Task RunApplicationAsync()
{
    try
    {
        var exceptionHandler = serviceProvider.GetRequiredService<IExceptionHandler>();

        // Main application logic
        Console.WriteLine("Web Crawler Application");
        Console.WriteLine("============================\n");

        Console.WriteLine("Enter the URL of the website you want to crawl: ");
        string? url = Console.ReadLine();

        var settings = serviceProvider.GetRequiredService<IOptions<WebCrawlerSettings>>();
        if (string.IsNullOrEmpty(url))
        {
            url = settings.Value.DefaultUrl;
            Console.WriteLine($"Using default URL: {url}");
        }

        // Validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? validUri))
        {
            Console.WriteLine(" Invalid URL format.");
            Environment.Exit(1);
        }

        Console.WriteLine($"\n Starting crawl from: {url}\n");

        // Get the crawler and start crawling with global exception handling
        var crawler = serviceProvider.GetRequiredService<ICrawler>();
        await exceptionHandler.ExecuteAsync(
            async () => await crawler.CrawlAsync(validUri),
            "Web Crawling"
        );

        Console.WriteLine("\n Crawl completed successfully!");

        // Display the path where links were saved
        var storage = serviceProvider.GetRequiredService<ILinkStorage>();
        Console.WriteLine($"Visited links saved to: {storage.GetStoragePath()}");
    }
    catch (Exception ex)
    {
        var exceptionHandler = serviceProvider.GetRequiredService<IExceptionHandler>();
        exceptionHandler.Handle(ex);
        Environment.Exit(1);
    }
}
