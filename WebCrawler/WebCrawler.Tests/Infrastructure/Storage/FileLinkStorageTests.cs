using WebCrawler.Infrastructure.Storage;
using WebCrawler.Core.Models;

namespace WebCrawler.Tests.Infrastructure.Storage;

[TestFixture]
public class FileLinkStorageTests
{
    private string _outputDir = null!;

    [SetUp]
    public void Setup()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "WebCrawlerTests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Test]
    public async Task RecordLinkAsync_WritesLinkToFile()
    {
        var storage = new FileLinkStorage(_outputDir, "links.txt");

        await storage.RecordLinkAsync("https://example.com/page");
        await storage.FinalizeAsync();
        await storage.DisposeAsync();

        var content = await File.ReadAllTextAsync(storage.GetStoragePath());
        Assert.That(content, Does.Contain("https://example.com/page"));
    }

    [Test]
    public async Task RecordLinkAsync_IgnoresBlankUrl()
    {
        var storage = new FileLinkStorage(_outputDir, "links.txt");

        await storage.RecordLinkAsync("   ");
        await storage.FinalizeAsync();
        await storage.DisposeAsync();

        var content = await File.ReadAllTextAsync(storage.GetStoragePath());
        Assert.That(content, Does.Contain("Total links recorded: 0"));
    }

    [Test]
    public async Task FinalizeAsync_AppendsCrawlStatistics()
    {
        var storage = new FileLinkStorage(_outputDir, "links.txt");
        var stats = new CrawlStatistics { PagesProcessed = 7 };

        await storage.FinalizeAsync(stats);
        await storage.DisposeAsync();

        var content = await File.ReadAllTextAsync(storage.GetStoragePath());
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("=== CRAWL STATISTICS ==="));
            Assert.That(content, Does.Contain("Pages Processed: 7"));
        });
    }

    [Test]
    public void Constructor_CreatesMissingOutputDirectory()
    {
        var storage = new FileLinkStorage(_outputDir, "links.txt");

        Assert.That(Directory.Exists(_outputDir), Is.True);
        storage.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
