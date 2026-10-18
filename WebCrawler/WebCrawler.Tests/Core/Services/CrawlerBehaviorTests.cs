using WebCrawler.Configuration;
using WebCrawler.Core.Filters;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;
using WebCrawler.Core.Services;

namespace WebCrawler.Tests.Core.Services;

[TestFixture]
public class CrawlerBehaviorTests
{
    private static readonly Uri Root = new("https://example.com/");

    [Test]
    public void Constructor_ThrowsWhenFilterChainIsNull()
    {
        Assert.That(
            () => new Crawler(new StubFetcher(), new StubExtractor(), new StubWriter(), null, null, null!, new WebCrawlerSettings()),
            Throws.ArgumentNullException);
    }

    [Test]
    public async Task CrawlAsync_RecordsVisitedPagesAndFinalizesStorageWithStatistics()
    {
        var storage = new RecordingStorage();
        var fetcher = new StubFetcher(new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "/a",
            ["https://example.com/a"] = string.Empty
        });
        var crawler = CreateCrawler(fetcher, storage: storage);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Recorded, Is.EquivalentTo(new[] { Root.AbsoluteUri, "https://example.com/a" }));
            Assert.That(storage.FinalizedWith, Is.SameAs(stats));
        });
    }

    [Test]
    public async Task CrawlAsync_DoesNotRecordFailedPages()
    {
        var storage = new RecordingStorage();
        var crawler = CreateCrawler(new StubFetcher(), storage: storage);

        await crawler.CrawlAsync(Root);

        Assert.That(storage.Recorded, Is.Empty);
    }

    [Test]
    public async Task CrawlAsync_WritesDiscoveredLinksToOutputWriter()
    {
        var writer = new StubWriter();
        var fetcher = new StubFetcher(new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "/a,/b",
            ["https://example.com/a"] = string.Empty,
            ["https://example.com/b"] = string.Empty
        });
        var crawler = CreateCrawler(fetcher, writer: writer);

        await crawler.CrawlAsync(Root);

        var rootWrite = writer.Writes.Single(w => w.Page == Root);
        Assert.That(rootWrite.Links.Select(l => l.AbsoluteUri),
            Is.EquivalentTo(new[] { "https://example.com/a", "https://example.com/b" }));
    }

    [Test]
    public async Task CrawlAsync_CountsTimeoutsSeparately()
    {
        var fetcher = new StubFetcher { ExceptionFactory = _ => new TaskCanceledException("timeout") };
        var crawler = CreateCrawler(fetcher);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesFailed, Is.EqualTo(1));
            Assert.That(stats.TimeoutErrors, Is.EqualTo(1));
            Assert.That(stats.HttpErrors, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task CrawlAsync_CountsUnexpectedErrorsAsOther()
    {
        var fetcher = new StubFetcher { ExceptionFactory = _ => new InvalidOperationException("boom") };
        var crawler = CreateCrawler(fetcher);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesFailed, Is.EqualTo(1));
            Assert.That(stats.OtherErrors, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_ContinuesAfterOnePageFails()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "/bad,/good",
            ["https://example.com/good"] = string.Empty
        });
        var crawler = CreateCrawler(fetcher);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(3));
            Assert.That(stats.PagesSuccessful, Is.EqualTo(2));
            Assert.That(stats.PagesFailed, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_TracksBytesAndLinkCounters()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "/a,/a,https://other.com/",
            ["https://example.com/a"] = string.Empty
        });
        var crawler = CreateCrawler(fetcher);

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.TotalBytesDownloaded, Is.EqualTo("/a,/a,https://other.com/".Length));
            Assert.That(stats.DifferentHost, Is.EqualTo(1));
            Assert.That(stats.LinksDiscovered, Is.EqualTo(2));
            Assert.That(stats.AlreadyVisited, Is.EqualTo(1));
            Assert.That(stats.LinksSkipped, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CrawlAsync_CanBeReusedForASecondCrawl()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = string.Empty });
        var crawler = CreateCrawler(fetcher);

        var first = await crawler.CrawlAsync(Root);
        var second = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(first.PagesProcessed, Is.EqualTo(1));
            Assert.That(second.PagesProcessed, Is.EqualTo(1));
            Assert.That(second.AlreadyVisited, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task CrawlAsync_SetsStartAndEndTimes()
    {
        var crawler = CreateCrawler(new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = string.Empty }));

        var stats = await crawler.CrawlAsync(Root);

        Assert.That(stats.EndTime, Is.GreaterThanOrEqualTo(stats.StartTime));
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-1)]
    public async Task CrawlAsync_FallsBackToSingleWorker_ForNonPositiveConcurrency(int? maxConcurrent)
    {
        var fetcher = new StubFetcher(new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = "/a,/b,/c",
            ["https://example.com/a"] = string.Empty,
            ["https://example.com/b"] = string.Empty,
            ["https://example.com/c"] = string.Empty
        })
        { Latency = TimeSpan.FromMilliseconds(20) };
        var crawler = CreateCrawler(fetcher, new WebCrawlerSettings { MaxConcurrentRequests = maxConcurrent, RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(4));
            Assert.That(fetcher.MaxInFlight, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_ManyWorkers_ProducesConsistentStatistics()
    {
        const int pageCount = 40;
        var pages = new Dictionary<string, string>
        {
            [Root.AbsoluteUri] = string.Join(',', Enumerable.Range(0, pageCount).Select(i => $"/p{i}"))
        };
        for (var i = 0; i < pageCount; i++)
        {
            pages[$"https://example.com/p{i}"] = "/,/p0,/p1";
        }

        var fetcher = new StubFetcher(pages) { Latency = TimeSpan.FromMilliseconds(2) };
        var crawler = CreateCrawler(fetcher, new WebCrawlerSettings { MaxConcurrentRequests = 8, RespectRobotsTxt = false });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.PagesProcessed, Is.EqualTo(pageCount + 1));
            Assert.That(stats.PagesSuccessful, Is.EqualTo(pageCount + 1));
            Assert.That(fetcher.Requested.Distinct().Count(), Is.EqualTo(pageCount + 1));
            Assert.That(stats.LinksQueued, Is.EqualTo(stats.LinksDiscovered));
        });
    }

    [Test]
    public async Task CrawlAsync_UsesRejectionReasonFromFilterChain()
    {
        var chain = new UrlFilterChain();
        chain.AddFilter(new RejectEverythingFilter(UrlFilterNames.RobotsText));
        var fetcher = new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = "/a,/b" });
        var crawler = new Crawler(fetcher, new StubExtractor(), new StubWriter(), null, null, chain,
            new WebCrawlerSettings { RespectRobotsTxt = true });

        var stats = await crawler.CrawlAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(stats.DisallowedByRobotsTxt, Is.EqualTo(2));
            Assert.That(stats.DifferentHost, Is.EqualTo(0));
            Assert.That(stats.LinksSkipped, Is.EqualTo(2));
            Assert.That(stats.PagesProcessed, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_ReturnsEmptyStatsWhenTokenAlreadyCancelled()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = string.Empty });
        var crawler = CreateCrawler(fetcher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var stats = await crawler.CrawlAsync(Root, cts.Token);

        Assert.That(stats.PagesSuccessful, Is.EqualTo(0));
        Assert.That(fetcher.Requested, Is.Empty);
    }

    [Test]
    public async Task CrawlAsync_ReturnsPartialStatsAndFinalizesStorageWhenCancelledDuringCrawl()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = "/a" })
        { Latency = TimeSpan.FromSeconds(30) };
        var crawler = CreateCrawler(fetcher);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var stats = await crawler.CrawlAsync(Root, cts.Token);

        Assert.That(stats.PagesSuccessful, Is.EqualTo(0));
        Assert.That(stats.EndTime, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task CrawlAsync_CanRunAgainAfterCancellation()
    {
        var fetcher = new StubFetcher(new Dictionary<string, string> { [Root.AbsoluteUri] = string.Empty });
        var crawler = CreateCrawler(fetcher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await crawler.CrawlAsync(Root, cts.Token);

        var stats = await crawler.CrawlAsync(Root);

        Assert.That(stats.PagesSuccessful, Is.EqualTo(1));
    }

    private static Crawler CreateCrawler(
        StubFetcher fetcher,
        WebCrawlerSettings? settings = null,
        RecordingStorage? storage = null,
        StubWriter? writer = null)
    {
        var chain = new UrlFilterChain();
        chain.AddFilter(new SameHostFilter());

        return new Crawler(
            fetcher,
            new StubExtractor(),
            writer ?? new StubWriter(),
            storage,
            null,
            chain,
            settings ?? new WebCrawlerSettings { RespectRobotsTxt = false });
    }

    private sealed class StubFetcher : IPageFetcher
    {
        private readonly Dictionary<string, string> _pages;
        private int _inFlight;
        private int _maxInFlight;

        public StubFetcher(Dictionary<string, string>? pages = null) => _pages = pages ?? new();

        public Func<Uri, Exception>? ExceptionFactory { get; init; }

        public TimeSpan Latency { get; init; } = TimeSpan.Zero;

        public List<string> Requested { get; } = new();

        public int MaxInFlight => Volatile.Read(ref _maxInFlight);

        public async Task<string> GetContentAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            lock (Requested)
            {
                Requested.Add(uri.AbsoluteUri);
            }

            var current = Interlocked.Increment(ref _inFlight);
            int observed;
            while (current > (observed = Volatile.Read(ref _maxInFlight)))
            {
                Interlocked.CompareExchange(ref _maxInFlight, current, observed);
            }

            try
            {
                if (Latency > TimeSpan.Zero)
                {
                    await Task.Delay(Latency, cancellationToken);
                }

                if (ExceptionFactory != null)
                {
                    throw ExceptionFactory(uri);
                }

                return _pages.TryGetValue(uri.AbsoluteUri, out var html)
                    ? html
                    : throw new HttpRequestException("Not found");
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    // Pages are described as comma-separated hrefs to keep the tests compact.
    private sealed class StubExtractor : ILinkExtractor
    {
        public IEnumerable<string> ExtractLinks(string html) =>
            html.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed class StubWriter : IOutputWriter
    {
        public List<(Uri Page, List<Uri> Links)> Writes { get; } = new();

        public void Write(Uri pageUrl, IEnumerable<Uri> links) => Writes.Add((pageUrl, links.ToList()));
    }

    private sealed class RecordingStorage : ILinkStorage
    {
        public List<string> Recorded { get; } = new();

        public object? FinalizedWith { get; private set; }

        public Task RecordLinkAsync(string url)
        {
            lock (Recorded)
            {
                Recorded.Add(url);
            }

            return Task.CompletedTask;
        }

        public Task FinalizeAsync(object? statistics = null)
        {
            FinalizedWith = statistics;
            return Task.CompletedTask;
        }

        public string GetStoragePath() => "memory";
    }

    private sealed class RejectEverythingFilter : IUrlFilter
    {
        public RejectEverythingFilter(string name) => FilterName = name;

        public string FilterName { get; }

        public Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
