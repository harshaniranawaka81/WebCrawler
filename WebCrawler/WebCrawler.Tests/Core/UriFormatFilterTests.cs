using WebCrawler.Core.Filters;

namespace WebCrawler.Tests.Core;

[TestFixture]
public class UriFormatFilterTests
{
    [Test]
    public async Task ShouldIncludeAsync_ReturnsTrue_ForValidUri()
    {
        var filter = new UriFormatFilter();

        var result = await filter.ShouldIncludeAsync(new Uri("https://example.com/page"), new Uri("https://example.com/"));

        Assert.That(result, Is.True);
    }

    [Test]
    public void FilterName_IsUriFormat()
    {
        Assert.That(new UriFormatFilter().FilterName, Is.EqualTo("UriFormat"));
    }
}
