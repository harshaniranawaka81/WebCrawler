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

}
