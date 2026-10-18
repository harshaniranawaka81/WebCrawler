using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;
using WebCrawler.Extensions;

namespace WebCrawler.IntegrationTests;

[TestFixture]
public class CrawlerIntegrationTests
{
    private const string RobotsTxt = "User-agent: *\nDisallow: /private\n";

    private static readonly Dictionary<string, string> Pages = new()
    {
        ["/"] = """
            <html><body>
              <a href="/about">About</a>
              <a href="/contact">Contact</a>
              <a href="/private/secret">Secret</a>
              <a href="https://external.example.com/">External</a>
            </body></html>
            """,
        ["/about"] = """
            <html><body>
              <a href="/">Home</a>
              <a href="/team">Team</a>
            </body></html>
            """,
        ["/contact"] = "<html><body><a href=\"/about\">About</a></body></html>",
        ["/team"] = "<html><body>Team page</body></html>",
    };

    private WebApplication? _app;
    private string _baseUrl = string.Empty;
    private string _tempDirectory = string.Empty;

    [SetUp]
    public async Task SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WebCrawler.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.Use(async (HttpContext context, Func<Task> _) =>
        {
            var path = context.Request.Path.Value ?? "/";

            if (path == "/robots.txt")
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(RobotsTxt);
                return;
            }

            if (Pages.TryGetValue(path, out var html))
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(html);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Not found");
        });

        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _baseUrl = address.TrimEnd('/');
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task CrawlAsync_WithRealServices_CrawlsSameHostPagesAndRespectsRobotsTxt()
    {
        var settings = CreateSettings(_tempDirectory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClientPolicies:RetryCount"] = "0",
                ["HttpClientPolicies:CircuitBreakerFailureThreshold"] = "100",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(settings));
        services.AddCrawlerInfrastructure(configuration);
        services.AddCrawlerCore();

        CrawlStatistics stats;
        await using (var provider = services.BuildServiceProvider())
        {
            await using var scope = provider.CreateAsyncScope();
            var crawler = scope.ServiceProvider.GetRequiredService<ICrawler>();
            stats = await crawler.CrawlAsync(new Uri(_baseUrl + "/"));
        }

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesSuccessful, Is.EqualTo(4), "Root, about, contact and team should be fetched.");
            Assert.That(stats.DisallowedByRobotsTxt, Is.EqualTo(1), "/private/secret should be blocked by robots.txt.");
            Assert.That(stats.DifferentHost, Is.EqualTo(1), "External link should be skipped.");
            Assert.That(stats.PagesFailed, Is.Zero);
        });

        var generatedFile = Directory.GetFiles(_tempDirectory, "visited_links_*.txt").Single();
        var visited = await File.ReadAllLinesAsync(generatedFile);
        Assert.Multiple(() =>
        {
            Assert.That(visited, Has.Some.Contains("/about"));
            Assert.That(visited, Has.Some.Contains("/team"));
            Assert.That(visited, Has.None.Contains("/private"));
            Assert.That(visited, Has.None.Contains("external.example.com"));
        });
    }

    [Test]
    public async Task CrawlAsync_WithMaxPagesLimit_StopsAtConfiguredLimit()
    {
        var settings = CreateSettings(_tempDirectory);
        settings.MaxPagesToCrawl = 2;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(settings));
        services.AddCrawlerInfrastructure(new ConfigurationBuilder().Build());
        services.AddCrawlerCore();

        await using var provider = services.BuildServiceProvider();
        CrawlStatistics stats;

        await using (var scope = provider.CreateAsyncScope())
        {
            var crawler = scope.ServiceProvider.GetRequiredService<ICrawler>();
            stats = await crawler.CrawlAsync(new Uri(_baseUrl + "/"));
        }

        Assert.That(stats.PagesSuccessful, Is.LessThanOrEqualTo(2));
    }

    private WebCrawlerSettings CreateSettings(string outputFile) => new()
    {
        DefaultUrl = _baseUrl + "/",
        VisitedPagesOutputFilePath = outputFile,
        RespectRobotsTxt = true,
        DelayBetweenRequestsMs = 0,
        MaxConcurrentRequests = 1,
        RobotsCacheExpirationMinutes = 60,
        RobotsFetchTimeoutSeconds = 5,
        StorageFlushIntervalLinks = 1,
        StorageWriterBufferSize = 4096,
        NormalizeUrlsIncludeQueryParameters = true,
    };
}
