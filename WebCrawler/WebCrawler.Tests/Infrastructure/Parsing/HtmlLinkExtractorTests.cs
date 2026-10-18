using Microsoft.Extensions.Logging.Abstractions;
using WebCrawler.Infrastructure.Parsing;

namespace WebCrawler.Tests.Infrastructure.Parsing;

[TestFixture]
public class HtmlLinkExtractorTests
{
    private HtmlLinkExtractor _extractor = null!;

    [SetUp]
    public void Setup()
    {
        _extractor = new HtmlLinkExtractor(NullLogger<HtmlLinkExtractor>.Instance);
    }

    [Test]
    public void ExtractLinks_ReturnsHrefValuesFromAnchors()
    {
        const string html = "<html><body><a href=\"/one\">1</a><a href=\"https://example.com/two\">2</a></body></html>";

        var links = _extractor.ExtractLinks(html).ToList();

        Assert.That(links, Is.EqualTo(new[] { "/one", "https://example.com/two" }));
    }

    [Test]
    public void ExtractLinks_IgnoresAnchorsWithoutHref()
    {
        const string html = "<a name=\"top\">top</a><a href=\"/kept\">kept</a>";

        var links = _extractor.ExtractLinks(html).ToList();

        Assert.That(links, Is.EqualTo(new[] { "/kept" }));
    }

    [Test]
    public void ExtractLinks_ReturnsEmptyWhenNoLinks()
    {
        var links = _extractor.ExtractLinks("<html><body>No links</body></html>");

        Assert.That(links, Is.Empty);
    }
}
