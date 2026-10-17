using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
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

// Link storage servi
services.AddSingleton<ILinkStorage>(sp =>
{
    var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
    var logger = sp.GetRequiredService<ILogger<FileLinkStorage>>();
    return new FileLinkStorage(options.Value.VisitedPagesOutputFilePath, logger: logger);
});

// Infrastructure services
services.AddHttpClient<IPageFetcher, HttpPageFetcher>()
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

services.AddScoped<ILinkExtractor, HtmlLinkExtractor>();

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
    var options = sp.GetRequiredService<IOptions<WebCrawlerSettings>>();
    var logger = sp.GetRequiredService<ILogger<Crawler>>();

    return new Crawler(
        pageFetcher,
        linkExtractor,
        outputWriter,
        linkStorage,
        options.Value.MaxPagesToCrawl,
        exceptionHandler,
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
