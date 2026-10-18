using Microsoft.Extensions.Logging.Abstractions;
using WebCrawler.Adapters;

namespace WebCrawler.Tests.Console.Adapters;

[TestFixture]
public class GlobalExceptionHandlerTests
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
    public void Handle_PrintsTimeoutMessage_ForOperationCanceledException()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        handler.Handle(new OperationCanceledException());

        Assert.That(_captured.ToString(), Does.Contain("TIMEOUT ERROR"));
    }

    [Test]
    public void Handle_PrintsInvalidUrlMessage_ForUriFormatException()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        handler.Handle(new UriFormatException("bad"));

        Assert.That(_captured.ToString(), Does.Contain("INVALID URL FORMAT"));
    }

    [Test]
    public void Handle_PrintsArgumentMessage_ForArgumentException()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        handler.Handle(new ArgumentException("bad value", "param"));

        Assert.That(_captured.ToString(), Does.Contain("INVALID ARGUMENT"));
    }

    [Test]
    public void Handle_PrintsNetworkError_ForHttpRequestException()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        handler.Handle(new HttpRequestException("down"));

        Assert.That(_captured.ToString(), Does.Contain("NETWORK ERROR"));
    }

    [Test]
    public void Handle_DoesNothing_WhenExceptionIsNull()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        handler.Handle(null!);

        Assert.That(_captured.ToString(), Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_RethrowsOriginalException()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        Assert.That(
            async () => await handler.ExecuteAsync(() => throw new InvalidOperationException("boom"), "test"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task ExecuteAsync_ReturnsResult_WhenOperationSucceeds()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var result = await handler.ExecuteAsync(() => Task.FromResult(42), "test");

        Assert.That(result, Is.EqualTo(42));
    }
}
