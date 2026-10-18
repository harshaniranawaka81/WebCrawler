using WebCrawler.Adapters;

namespace WebCrawler.Tests.Console.Adapters;

[TestFixture]
public class ConsoleOutputWriterTests
{
    private TextWriter _originalOut = null!;
    private StringWriter _captured = null!;

    [SetUp]
    public void SetUp()
    {
        _originalOut = System.Console.Out;
        _captured = new StringWriter();
        System.Console.SetOut(_captured);
    }

    [TearDown]
    public void TearDown()
    {
        System.Console.SetOut(_originalOut);
        _captured.Dispose();
    }

    [Test]
    public void Write_PrintsNoLinksFound_WhenEmpty()
    {
        new ConsoleOutputWriter().Write(new Uri("https://example.com/"), Array.Empty<Uri>());

        Assert.That(_captured.ToString(), Does.Contain("No links found"));
    }

    [Test]
    public void Write_PrintsEachLinkAndCount()
    {
        var links = new[] { new Uri("https://example.com/a"), new Uri("https://example.com/b") };

        new ConsoleOutputWriter().Write(new Uri("https://example.com/"), links);

        var output = _captured.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Found 2 link(s)"));
            Assert.That(output, Does.Contain("https://example.com/a"));
            Assert.That(output, Does.Contain("https://example.com/b"));
        });
    }

    [Test]
    public void Write_ThrowsWhenPageUrlIsNull()
    {
        Assert.That(() => new ConsoleOutputWriter().Write(null!, Array.Empty<Uri>()), Throws.ArgumentNullException);
    }

    [Test]
    public void Write_ThrowsWhenLinksIsNull()
    {
        Assert.That(() => new ConsoleOutputWriter().Write(new Uri("https://example.com/"), null!), Throws.ArgumentNullException);
    }
}
