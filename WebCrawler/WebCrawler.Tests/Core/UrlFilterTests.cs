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
}

[TestFixture]
public class UrlFilterChainTests
{
    private sealed class ThrowingFilter : WebCrawler.Core.Interfaces.IUrlFilter
    {
        public string FilterName => "Throwing";

        public Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should not be reached");
    }

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
        chain.AddFilter(new ThrowingFilter());

        var (passed, reason) = await chain.FilterAsync(testUri, baseUri);

        Assert.That(passed, Is.False);
        Assert.That(reason, Is.EqualTo("SameHost")); // Should fail at SameHost, not reach UniqueUrl
    }

    [Test]
    public async Task UrlFilterChain_FluentBuilding()
    {
        var chain = new UrlFilterChain()
            .AddFilter(new SameHostFilter())
            .AddFilter(new SameHostFilter());

        var (passed, reason) = await chain.FilterAsync(
            new Uri("https://example.com/page"),
            new Uri("https://example.com/"));

        Assert.That(passed, Is.True);
    }
}
