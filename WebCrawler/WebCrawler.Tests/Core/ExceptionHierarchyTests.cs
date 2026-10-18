using NUnit.Framework;
using WebCrawler.Core.Exceptions;

namespace WebCrawler.Tests.Core;

[TestFixture]
public class ExceptionHierarchyTests
{
    [Test]
    public void WebCrawlerException_IsBase_ForAllCustomExceptions()
    {
        var ex = new WebCrawlerException("Test message");
        Assert.That(ex, Is.InstanceOf<Exception>());
        Assert.That(ex.Message, Is.EqualTo("Test message"));
    }

    [Test]
    public void WebCrawlerException_SupportsInnerException()
    {
        var inner = new InvalidOperationException("Inner");
        var ex = new WebCrawlerException("Test message", inner);
        Assert.That(ex.InnerException, Is.EqualTo(inner));
    }

    [Test]
    public void ConfigurationException_IsWebCrawlerException()
    {
        var ex = new ConfigurationException("Config error");
        Assert.That(ex, Is.InstanceOf<WebCrawlerException>());
    }

    [Test]
    public void UrlFilterException_HasUrlProperty()
    {
        var url = "https://example.com";
        var ex = new UrlFilterException("URL filter error", url);
        Assert.That(ex.Url, Is.EqualTo(url));
    }

    [Test]
    public void RobotsException_HasRobotsUriProperty()
    {
        var robotsUri = new Uri("https://example.com/robots.txt");
        var ex = new RobotsException("Robots error", robotsUri);
        Assert.That(ex.RobotsUri, Is.EqualTo(robotsUri));
    }

    [Test]
    public void PageFetchException_HasPageUriAndStatusCode()
    {
        var pageUri = new Uri("https://example.com/page");
        var ex = new PageFetchException("Fetch failed", pageUri, 404);
        Assert.That(ex.PageUri, Is.EqualTo(pageUri));
        Assert.That(ex.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void LinkExtractionException_HasPageUri()
    {
        var pageUri = new Uri("https://example.com/page");
        var ex = new LinkExtractionException("Extraction failed", pageUri);
        Assert.That(ex.PageUri, Is.EqualTo(pageUri));
    }

    [Test]
    public void StorageException_IsWebCrawlerException()
    {
        var ex = new StorageException("Storage error");
        Assert.That(ex, Is.InstanceOf<WebCrawlerException>());
    }
}
