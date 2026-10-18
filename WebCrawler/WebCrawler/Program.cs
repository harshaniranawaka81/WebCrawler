using Serilog;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Extensions;
using WebCrawler.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// Initialize application
var configuration = BuildConfiguration();
ConfigureLogging(configuration);

try
{
    var serviceProvider = ConfigureServices(configuration);
    await RunApplicationAsync(serviceProvider);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

return;

#region Setup

IConfiguration BuildConfiguration()
{
    return new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();
}

void ConfigureLogging(IConfiguration configuration)
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration.GetSection("Serilog"))
        .Enrich.FromLogContext()
        .CreateLogger();
}

IServiceProvider ConfigureServices(IConfiguration configuration)
{
    var services = new ServiceCollection();

    // Configure logging
    services.AddLogging(config => config.AddSerilog(Log.Logger));

    // Configure application options
    services.Configure<WebCrawlerSettings>(configuration.GetSection("WebCrawlerSettings"));
    services.Configure<HttpClientPolicies>(configuration.GetSection("HttpClientPolicies"));

    // Add crawler services
    services.AddCrawlerInfrastructure(configuration);
    services.AddCrawlerCore();

    return services.BuildServiceProvider();
}

#endregion

#region Execution

async Task RunApplicationAsync(IServiceProvider serviceProvider)
{
    try
    {
        var exceptionHandler = serviceProvider.GetRequiredService<IExceptionHandler>();
        var crawler = serviceProvider.GetRequiredService<ICrawler>();
        var storage = serviceProvider.GetRequiredService<ILinkStorage>();
        var settings = serviceProvider.GetRequiredService<IOptions<WebCrawlerSettings>>();

        DisplayWelcome();

        var url = await GetUrlAsync(settings.Value.DefaultUrl);
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? validUri))
        {
            Console.WriteLine("❌ Invalid URL format.");
            Environment.Exit(1);
        }

        Console.WriteLine($"\n🚀 Starting crawl from: {url}\n");
        var stats = await exceptionHandler.ExecuteAsync(
            async () => await crawler.CrawlAsync(validUri),
            "Web Crawling");

        DisplayResults(stats, storage);
    }
    catch (Exception ex)
    {
        var exceptionHandler = serviceProvider.GetRequiredService<IExceptionHandler>();
        exceptionHandler.Handle(ex);
        Log.Fatal(ex, "Application failed with an unhandled exception");
        Environment.Exit(1);
    }
}

#endregion

#region User Interface

void DisplayWelcome()
{
    Console.WriteLine("🕷️  Web Crawler Application");
    Console.WriteLine("════════════════════════════\n");
}

async Task<string> GetUrlAsync(string defaultUrl)
{
    Console.WriteLine("Enter the URL of the website you want to crawl: ");
    string? url = Console.ReadLine();

    if (string.IsNullOrEmpty(url))
    {
        url = defaultUrl;
        Console.WriteLine($"📌 Using default URL: {url}");
    }

    return url;
}

void DisplayResults(WebCrawler.Core.Models.CrawlStatistics? stats, ILinkStorage storage)
{
    if (stats != null)
    {
        Console.WriteLine(stats.GenerateReport());
    }

    DisplayVisitedLinksPath(storage);
    DisplayLogFilePath();
    LogCompletion();
}

void DisplayVisitedLinksPath(ILinkStorage storage)
{
    Console.WriteLine($"✓ Visited links saved to: {storage.GetStoragePath()}\n");
}

void DisplayLogFilePath()
{
    var logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    if (!Directory.Exists(logPath))
        return;

    var latestLog = Directory.GetFiles(logPath, "*.txt")
        .OrderByDescending(f => File.GetLastWriteTime(f))
        .FirstOrDefault();

    if (!string.IsNullOrEmpty(latestLog))
    {
        Console.WriteLine($"📋 Crawl logs saved to: {latestLog}\n");
    }
}

void LogCompletion()
{
    Log.Information("Application completed successfully");
}

#endregion
