using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebCrawler.Application.Services;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;

namespace WebCrawler.Tests.Application.Services;

[TestFixture]
public class ApplicationOrchestratorTests
{
    private FakeApplicationInteraction _interaction = null!;
    private FakeCrawler _crawler = null!;
    private FakeLinkStorage _storage = null!;

    [SetUp]
    public void Setup()
    {
        _interaction = new FakeApplicationInteraction();
        _crawler = new FakeCrawler();
        _storage = new FakeLinkStorage();
    }

    [Test]
    public async Task RunAsync_ValidUrl_CrawlsAndReturnsZero()
    {
        _interaction.UrlToReturn = "https://example.com/";
        var orchestrator = CreateOrchestrator(new WebCrawlerSettings { DefaultUrl = "https://example.com/" });

        var exitCode = await orchestrator.RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_crawler.CrawledUrls, Is.EqualTo(new[] { new Uri("https://example.com/") }));
            Assert.That(_interaction.CompletionDisplayed, Is.True);
            Assert.That(_storage.Finalized, Is.True);
        });
    }

    [Test]
    public async Task RunAsync_InvalidUrl_ReturnsOneWithoutCrawling()
    {
        _interaction.UrlToReturn = "not a valid url";
        var orchestrator = CreateOrchestrator(new WebCrawlerSettings { DefaultUrl = "https://example.com/" });

        var exitCode = await orchestrator.RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(_interaction.InvalidUrlDisplayed, Is.True);
            Assert.That(_crawler.CrawledUrls, Is.Empty);
            Assert.That(_storage.Finalized, Is.True);
        });
    }

    [Test]
    public async Task RunAsync_InvalidConfiguration_ReturnsOneWithoutCrawling()
    {
        var orchestrator = CreateOrchestrator(new WebCrawlerSettings { DefaultUrl = string.Empty });

        var exitCode = await orchestrator.RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(_interaction.ErrorDisplayed, Is.True);
            Assert.That(_crawler.CrawledUrls, Is.Empty);
        });
    }

    private ApplicationOrchestrator CreateOrchestrator(WebCrawlerSettings settings)
    {
        return new ApplicationOrchestrator(
            _interaction,
            new FakeExceptionHandler(),
            _crawler,
            _storage,
            Options.Create(settings),
            NullLogger<ApplicationOrchestrator>.Instance);
    }

    private sealed class FakeExceptionHandler : IExceptionHandler
    {
        public void Handle(Exception exception)
        {
        }

        public Task ExecuteAsync(Func<Task> action, string operationName = "Operation") => action();

        public Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string operationName = "Operation") => action()!;
    }

    private sealed class FakeApplicationInteraction : IApplicationInteraction
    {
        public string UrlToReturn { get; set; } = string.Empty;
        public bool InvalidUrlDisplayed { get; private set; }
        public bool ErrorDisplayed { get; private set; }
        public bool CompletionDisplayed { get; private set; }

        public void DisplayWelcome() { }

        public Task<string> GetUrlAsync(string defaultUrl) => Task.FromResult(UrlToReturn);

        public void DisplayInvalidUrlError(string url) => InvalidUrlDisplayed = true;

        public void DisplayCrawlStart(string url) { }

        public void DisplayResults(CrawlStatistics? statistics, ILinkStorage storage) { }

        public void DisplayCompletion() => CompletionDisplayed = true;

        public void DisplayError(string message) => ErrorDisplayed = true;
    }

    private sealed class FakeCrawler : ICrawler
    {
        public List<Uri> CrawledUrls { get; } = new();

        public Task<CrawlStatistics> CrawlAsync(Uri rootUrl)
        {
            CrawledUrls.Add(rootUrl);
            return Task.FromResult<CrawlStatistics>(null!);
        }
    }

    private sealed class FakeLinkStorage : ILinkStorage
    {
        public bool Finalized { get; private set; }

        public Task RecordLinkAsync(string url) => Task.CompletedTask;

        public Task FinalizeAsync(object? statistics = null)
        {
            Finalized = true;
            return Task.CompletedTask;
        }

        public string GetStoragePath() => string.Empty;
    }
}
