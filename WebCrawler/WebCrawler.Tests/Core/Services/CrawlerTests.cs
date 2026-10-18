using WebCrawler.Configuration;
using WebCrawler.Core.Filters;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Services;
using WebCrawler.Infrastructure.Filters;

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
    public async Task CrawlAsync_ReportsFilterRejectionsInStatistics()
    {
        var pages = new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "<a href=\"https://other.com/x\">x</a><a href=\"/private\">p</a><a href=\"/ok\">ok</a>",
            ["https://example.com/ok"] = ""
        };
        var robots = new FakeRobotsChecker(disallowed: "/private");
        var crawler = CreateCrawler(new FakePageFetcher(pages), new WebCrawlerSettings { RespectRobotsTxt = true }, robots);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.DifferentHost, Is.EqualTo(1));
            Assert.That(stats.DisallowedByRobotsTxt, Is.EqualTo(1));
            Assert.That(stats.LinksSkipped, Is.EqualTo(2));
            Assert.That(stats.LinksQueued, Is.EqualTo(1));
            Assert.That(stats.PagesSuccessful, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CrawlAsync_WithoutRobotsFilter_DoesNotApplyRobotsRules()
    {
        var pages = new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "<a href=\"/private\">p</a>",
            ["https://example.com/private"] = ""
        };
        var crawler = CreateCrawler(new FakePageFetcher(pages), new WebCrawlerSettings { RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.DisallowedByRobotsTxt, Is.EqualTo(0));
            Assert.That(stats.PagesSuccessful, Is.EqualTo(2));
        });
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

    [Test]
    public async Task CrawlAsync_WithMultipleWorkers_FetchesPagesConcurrently()
    {
        var pages = new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "<a href=\"/a\">a</a><a href=\"/b\">b</a><a href=\"/c\">c</a>",
            ["https://example.com/a"] = "",
            ["https://example.com/b"] = "",
            ["https://example.com/c"] = ""
        };
        var fetcher = new ConcurrencyTrackingFetcher(pages, TimeSpan.FromMilliseconds(100));
        var crawler = new Crawler(
            fetcher,
            new SimpleLinkExtractor(),
            new NullOutputWriter(),
            linkStorage: null,
            exceptionHandler: null,
            filterChain: BuildChain(null),
            config: new WebCrawlerSettings { MaxConcurrentRequests = 3, RespectRobotsTxt = false });
        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(4));
            Assert.That(stats.PagesSuccessful, Is.EqualTo(4));
            Assert.That(fetcher.MaxObservedConcurrency, Is.GreaterThan(1));
            Assert.That(fetcher.MaxObservedConcurrency, Is.LessThanOrEqualTo(3));
        });
    }

    [Test]
    public async Task CrawlAsync_WithMultipleWorkers_RespectsMaxPagesAndVisitsEachPageOnce()
    {
        var pages = new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = string.Concat(Enumerable.Range(0, 20).Select(i => $"<a href=\"/p{i}\">p</a><a href=\"/p{i}\">p</a>"))
        };
        for (var i = 0; i < 20; i++)
        {
            pages[$"https://example.com/p{i}"] = "<a href=\"/\">home</a>";
        }

        var fetcher = new ConcurrencyTrackingFetcher(pages, TimeSpan.FromMilliseconds(10));
        var crawler = new Crawler(
            fetcher,
            new SimpleLinkExtractor(),
            new NullOutputWriter(),
            linkStorage: null,
            exceptionHandler: null,
            filterChain: BuildChain(null),
            config: new WebCrawlerSettings { MaxConcurrentRequests = 4, MaxPagesToCrawl = 10, RespectRobotsTxt = false });
        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(10));
            Assert.That(fetcher.RequestedUrls.Distinct().Count(), Is.EqualTo(fetcher.RequestedUrls.Count));
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
            filterChain: BuildChain(robotsChecker),
            config: settings);
    }

    private static IUrlFilterChain BuildChain(IRobotsChecker? robotsChecker)
    {
        var chain = new UrlFilterChain();
        chain.AddFilter(new SameHostFilter());
        if (robotsChecker != null)
        {
            chain.AddFilter(new RobotsTextFilter(robotsChecker));
        }

        return chain;
    }

    private sealed class FakePageFetcher : IPageFetcher
    {
        private readonly Dictionary<string, string> _pages;

        public FakePageFetcher(Dictionary<string, string> pages) => _pages = pages;

        public List<string> RequestedUrls { get; } = new();

        public Task<string> GetContentAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            lock (RequestedUrls)
            {
                RequestedUrls.Add(uri.AbsoluteUri);
            }
            if (_pages.TryGetValue(uri.AbsoluteUri, out var html))
            {
                return Task.FromResult(html);
            }

            throw new HttpRequestException("Not found");
        }
    }

    private sealed class ConcurrencyTrackingFetcher : IPageFetcher
    {
        private readonly Dictionary<string, string> _pages;
        private readonly TimeSpan _latency;
        private int _inFlight;
        private int _maxInFlight;

        public ConcurrencyTrackingFetcher(Dictionary<string, string> pages, TimeSpan latency)
        {
            _pages = pages;
            _latency = latency;
        }

        public List<string> RequestedUrls { get; } = new();

        public int MaxObservedConcurrency => Volatile.Read(ref _maxInFlight);

        public async Task<string> GetContentAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            lock (RequestedUrls)
            {
                RequestedUrls.Add(uri.AbsoluteUri);
            }

            var current = Interlocked.Increment(ref _inFlight);
            int observed;
            while (current > (observed = Volatile.Read(ref _maxInFlight)))
            {
                Interlocked.CompareExchange(ref _maxInFlight, current, observed);
            }

            try
            {
                await Task.Delay(_latency);
                if (_pages.TryGetValue(uri.AbsoluteUri, out var html))
                {
                    return html;
                }

                throw new HttpRequestException("Not found");
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
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

        public Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult(!uri.AbsolutePath.StartsWith(_disallowed));

        public Task<object?> GetRulesAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);

        public void ClearCache()
        {
        }
    }
}
