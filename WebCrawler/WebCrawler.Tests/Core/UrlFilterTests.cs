using NUnit.Framework;
using WebCrawler.Core.Filters;
using WebCrawler.Core.Services;

namespace WebCrawler.Tests.Core;

[TestFixture]
public class UrlFilterTests
{
    [Test]
    public async Task SameHostFilter_AllowsSameHost()
    {
        var filter = new SameHostFilter();
        var baseUri = new Uri("https://example.com/");
        var testUri = new Uri("https://example.com/page");

        var result = await filter.ShouldIncludeAsync(testUri, baseUri);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task SameHostFilter_RejectsDifferentHost()
    {
        var filter = new SameHostFilter();
        var baseUri = new Uri("https://example.com/");
        var testUri = new Uri("https://different.com/page");

        var result = await filter.ShouldIncludeAsync(testUri, baseUri);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task SameHostFilter_IsCaseInsensitive()
    {
        var filter = new SameHostFilter();
        var baseUri = new Uri("https://Example.com/");
        var testUri = new Uri("https://example.com/page");

        var result = await filter.ShouldIncludeAsync(testUri, baseUri);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task UniqueUrlFilter_AllowsUnvisitedUrl()
    {
        var visitedUrls = new HashSet<string>();
        var filter = new UniqueUrlFilter(uri => visitedUrls.Contains(uri.AbsoluteUri));
        var uri = new Uri("https://example.com/page");
        var baseUri = new Uri("https://example.com/");

        var result = await filter.ShouldIncludeAsync(uri, baseUri);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task UniqueUrlFilter_RejectsVisitedUrl()
    {
        var visitedUrls = new HashSet<string> { "https://example.com/page" };
        var filter = new UniqueUrlFilter(uri => visitedUrls.Contains(uri.AbsoluteUri));
        var uri = new Uri("https://example.com/page");
        var baseUri = new Uri("https://example.com/");

        var result = await filter.ShouldIncludeAsync(uri, baseUri);

        Assert.That(result, Is.False);
    }
}

[TestFixture]
public class UrlFilterChainTests
{
    [Test]
    public async Task UrlFilterChain_AllFiltersMustPass()
    {
        var chain = new UrlFilterChain();
        var baseUri = new Uri("https://example.com/");
        var testUri = new Uri("https://example.com/page");

        chain.AddFilter(new SameHostFilter());

        var (passed, reason) = await chain.FilterAsync(testUri, baseUri);

        Assert.That(passed, Is.True);
        Assert.That(reason, Is.Null);
    }

    [Test]
    public async Task UrlFilterChain_StopsAtFirstFailingFilter()
    {
        var chain = new UrlFilterChain();
        var baseUri = new Uri("https://example.com/");
        var testUri = new Uri("https://different.com/page");

        chain.AddFilter(new SameHostFilter());
        chain.AddFilter(new UniqueUrlFilter(_ => false)); // Always reject if reached

        var (passed, reason) = await chain.FilterAsync(testUri, baseUri);

        Assert.That(passed, Is.False);
        Assert.That(reason, Is.EqualTo("SameHost")); // Should fail at SameHost, not reach UniqueUrl
    }

    [Test]
    public async Task UrlFilterChain_FluentBuilding()
    {
        var chain = new UrlFilterChain()
            .AddFilter(new SameHostFilter())
            .AddFilter(new UniqueUrlFilter(_ => false));

        var (passed, reason) = await chain.FilterAsync(
            new Uri("https://example.com/page"),
            new Uri("https://example.com/"));

        Assert.That(passed, Is.True);
    }
}
