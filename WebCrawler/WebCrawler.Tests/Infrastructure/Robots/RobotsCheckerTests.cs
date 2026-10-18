using WebCrawler.Core.Interfaces;
using WebCrawler.Infrastructure.Robots;

namespace WebCrawler.Tests.Infrastructure.Robots;

[TestFixture]
public class RobotsCheckerTests
{
    [Test]
    public async Task IsAllowedAsync_BlocksDisallowedPath()
    {
        var fetcher = new FakePageFetcher("User-agent: *\nDisallow: /private\n");
        var checker = new RobotsChecker(fetcher);

        var allowed = await checker.IsAllowedAsync(new Uri("https://example.com/private/page"));

        Assert.That(allowed, Is.False);
    }

    [Test]
    public async Task IsAllowedAsync_AllowsUnlistedPath()
    {
        var fetcher = new FakePageFetcher("User-agent: *\nDisallow: /private\n");
        var checker = new RobotsChecker(fetcher);

        var allowed = await checker.IsAllowedAsync(new Uri("https://example.com/public"));

        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task IsAllowedAsync_AllowsAll_WhenRobotsIsEmpty()
    {
        var checker = new RobotsChecker(new FakePageFetcher(""));

        var allowed = await checker.IsAllowedAsync(new Uri("https://example.com/anything"));

        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task IsAllowedAsync_AllowsAll_WhenFetchFails()
    {
        var checker = new RobotsChecker(new FakePageFetcher(null, throwOnFetch: true));

        var allowed = await checker.IsAllowedAsync(new Uri("https://example.com/anything"));

        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task IsAllowedAsync_FetchesRobotsOnlyOncePerDomain()
    {
        var fetcher = new FakePageFetcher("User-agent: *\nDisallow: /private\n");
        var checker = new RobotsChecker(fetcher);

        await checker.IsAllowedAsync(new Uri("https://example.com/a"));
        await checker.IsAllowedAsync(new Uri("https://example.com/b"));

        Assert.That(fetcher.FetchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRulesAsync_ReturnsRules_ForDomainWithRobots()
    {
        var checker = new RobotsChecker(new FakePageFetcher("User-agent: *\nDisallow: /private\n"));

        var rules = await checker.GetRulesAsync(new Uri("https://example.com/"));

        Assert.That(rules, Is.Not.Null);
    }

    [Test]
    public void ClearCache_ForcesRefetch()
    {
        var fetcher = new FakePageFetcher("User-agent: *\nDisallow: /private\n");
        var checker = new RobotsChecker(fetcher);
        var uri = new Uri("https://example.com/a");

        checker.IsAllowedAsync(uri).GetAwaiter().GetResult();
        checker.ClearCache();
        checker.IsAllowedAsync(uri).GetAwaiter().GetResult();

        Assert.That(fetcher.FetchCount, Is.EqualTo(2));
    }

    [Test]
    public void Constructor_ThrowsWhenPageFetcherIsNull()
    {
        Assert.That(() => new RobotsChecker(null!), Throws.ArgumentNullException);
    }

    private sealed class FakePageFetcher : IPageFetcher
    {
        private readonly string? _content;
        private readonly bool _throwOnFetch;

        public FakePageFetcher(string? content, bool throwOnFetch = false)
        {
            _content = content;
            _throwOnFetch = throwOnFetch;
        }

        public int FetchCount { get; private set; }

        public Task<string> GetContentAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            FetchCount++;
            if (_throwOnFetch)
            {
                throw new HttpRequestException("robots unavailable");
            }

            return Task.FromResult(_content ?? string.Empty);
        }
    }
}
