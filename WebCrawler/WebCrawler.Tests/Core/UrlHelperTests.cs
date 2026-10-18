using NUnit.Framework;
using WebCrawler.Core.Helpers;

namespace WebCrawler.Tests.Core;

[TestFixture]
public class UrlHelperTests
{
    [Test]
    public void Normalize_IncludesPathAndQuery()
    {
        var uri = new Uri("https://example.com/page?sort=asc#section");
        var normalized = UrlHelper.Normalize(uri);

        Assert.That(normalized, Does.Contain("/page"));
        Assert.That(normalized, Does.Contain("sort=asc"));
        Assert.That(normalized, Does.Not.Contain("section")); // Fragment excluded
    }

    [Test]
    public void Normalize_DistinguishesDifferentQueryParameters()
    {
        var uri1 = new Uri("https://example.com/page?sort=asc");
        var uri2 = new Uri("https://example.com/page?sort=desc");

        var norm1 = UrlHelper.Normalize(uri1);
        var norm2 = UrlHelper.Normalize(uri2);

        Assert.That(norm1, Is.Not.EqualTo(norm2));
    }

    [Test]
    public void Normalize_IsLowercased()
    {
        var uri = new Uri("HTTPS://EXAMPLE.COM/Page");
        var normalized = UrlHelper.Normalize(uri);

        Assert.That(normalized, Does.Contain("https://"));
        Assert.That(normalized, Does.Contain("example.com"));
        Assert.That(normalized, Does.Contain("/page"));
    }

    [Test]
    public void Normalize_IgnoresFragmentDifferences()
    {
        var uri1 = new Uri("https://example.com/page#top");
        var uri2 = new Uri("https://example.com/page#bottom");

        var norm1 = UrlHelper.Normalize(uri1);
        var norm2 = UrlHelper.Normalize(uri2);

        Assert.That(norm1, Is.EqualTo(norm2)); // Fragments should be ignored
    }

    [Test]
    public void IsSameHost_ReturnsTrueForSameHost()
    {
        var uri1 = new Uri("https://example.com/page1");
        var uri2 = new Uri("https://example.com/page2");

        var same = UrlHelper.IsSameHost(uri1, uri2);

        Assert.That(same, Is.True);
    }

    [Test]
    public void IsSameHost_ReturnsFalseForDifferentHosts()
    {
        var uri1 = new Uri("https://example.com/page1");
        var uri2 = new Uri("https://different.com/page1");

        var same = UrlHelper.IsSameHost(uri1, uri2);

        Assert.That(same, Is.False);
    }

    [Test]
    public void IsSameHost_IsCaseInsensitive()
    {
        var uri1 = new Uri("https://Example.com/page");
        var uri2 = new Uri("https://example.com/page");

        var same = UrlHelper.IsSameHost(uri1, uri2);

        Assert.That(same, Is.True);
    }

    [Test]
    public void IsSameHost_IgnoresPort()
    {
        var uri1 = new Uri("https://example.com:443/page");
        var uri2 = new Uri("https://example.com/page");

        var same = UrlHelper.IsSameHost(uri1, uri2);

        Assert.That(same, Is.True);
    }
}
