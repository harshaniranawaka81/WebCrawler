using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Services;

namespace WebCrawler.Tests.Core.Services;

[TestFixture]
public class CrawlerTests
{
    private static readonly Uri Root = new("https://example.com/");

    [Test]
    public async Task CrawlAsync_StopsAtMaxPagesToCrawl()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://example.com/"] = "<a href=\"/a\">a</a><a href=\"/b\">b</a>",
            ["https://example.com/a"] = "",
            ["https://example.com/b"] = ""
        };
        var fetcher = new FakePageFetcher(pages);
        var crawler = CreateCrawler(fetcher, new WebCrawlerSettings { MaxPagesToCrawl = 1, RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.That(stats.PagesProcessed, Is.EqualTo(1));
    }

    [Test]
    public async Task CrawlAsync_DoesNotVisitSamePageTwice()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://example.com/"] = "<a href=\"/\">home</a><a href=\"/\">home</a>"
        };
        var fetcher = new FakePageFetcher(pages);
        var crawler = CreateCrawler(fetcher, new WebCrawlerSettings { RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(1));
            Assert.That(fetcher.RequestedUrls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_SkipsLinksToOtherHosts()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://example.com/"] = "<a href=\"https://other.com/x\">x</a>"
        };
        var crawler = CreateCrawler(new FakePageFetcher(pages), new WebCrawlerSettings { RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.That(stats.DifferentHost, Is.EqualTo(1));
    }

    [Test]
    public async Task CrawlAsync_SkipsLinksDisallowedByRobots()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://example.com/"] = "<a href=\"/private\">p</a>"
        };
        var robots = new FakeRobotsChecker(disallowed: "/private");
        var crawler = CreateCrawler(new FakePageFetcher(pages), new WebCrawlerSettings { RespectRobotsTxt = true }, robots);

        var stats = await crawler.CrawlAsync(Root);

        Assert.That(stats.DisallowedByRobotsTxt, Is.EqualTo(1));
    }

    [Test]
    public async Task CrawlAsync_CountsFailedPages()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>());
        var crawler = CreateCrawler(fetcher, new WebCrawlerSettings { RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesFailed, Is.EqualTo(1));
            Assert.That(stats.HttpErrors, Is.EqualTo(1));
        });
    }

    private static Crawler CreateCrawler(
        FakePageFetcher fetcher,
        WebCrawlerSettings settings,
        IRobotsChecker? robotsChecker = null)
    {
        return new Crawler(
            fetcher,
            new SimpleLinkExtractor(),
            new NullOutputWriter(),
            linkStorage: null,
            exceptionHandler: null,
            robotsChecker: robotsChecker,
            config: settings);
    }

    private sealed class FakePageFetcher : IPageFetcher
    {
        private readonly Dictionary<string, string> _pages;

        public FakePageFetcher(Dictionary<string, string> pages) => _pages = pages;

        public List<string> RequestedUrls { get; } = new();

        public Task<string> GetContentAsync(Uri uri)
        {
            RequestedUrls.Add(uri.AbsoluteUri);
            if (_pages.TryGetValue(uri.AbsoluteUri, out var html))
            {
                return Task.FromResult(html);
            }

            throw new HttpRequestException("Not found");
        }
    }

    private sealed class SimpleLinkExtractor : ILinkExtractor
    {
        public IEnumerable<string> ExtractLinks(string html)
        {
            var parts = html.Split("href=\"", StringSplitOptions.RemoveEmptyEntries);
            return parts.Skip(1).Select(p => p[..p.IndexOf('"')]);
        }
    }

    private sealed class NullOutputWriter : IOutputWriter
    {
        public void Write(Uri pageUrl, IEnumerable<Uri> links)
        {
        }
    }

    private sealed class FakeRobotsChecker : IRobotsChecker
    {
        private readonly string _disallowed;

        public FakeRobotsChecker(string disallowed) => _disallowed = disallowed;

        public Task<bool> IsAllowedAsync(Uri uri) => Task.FromResult(!uri.AbsolutePath.StartsWith(_disallowed));

        public Task<object?> GetRulesAsync(Uri uri) => Task.FromResult<object?>(null);

        public void ClearCache()
        {
        }
    }
}
