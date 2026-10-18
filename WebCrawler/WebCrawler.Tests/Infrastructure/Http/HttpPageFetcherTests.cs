using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WebCrawler.Core.Interfaces;
using WebCrawler.Infrastructure.Http;

namespace WebCrawler.Tests.Infrastructure.Http;

[TestFixture]
public class HttpPageFetcherTests
{
    private static readonly Uri PageUri = new("https://example.com/page");

    [Test]
    public async Task GetContentAsync_ReturnsResponseBody()
    {
        var fetcher = CreateFetcher(HttpStatusCode.OK, "<html>hello</html>");

        var content = await fetcher.GetContentAsync(PageUri);

        Assert.That(content, Is.EqualTo("<html>hello</html>"));
    }

    [Test]
    public async Task GetContentAsync_WaitsOnAndReleasesRateLimiter()
    {
        var limiter = new CountingRateLimiter();
        var fetcher = CreateFetcher(HttpStatusCode.OK, "ok", limiter);

        await fetcher.GetContentAsync(PageUri);

        Assert.Multiple(() =>
        {
            Assert.That(limiter.WaitCount, Is.EqualTo(1));
            Assert.That(limiter.ReleaseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void GetContentAsync_ThrowsAndStillReleasesRateLimiter_WhenRequestFails()
    {
        var limiter = new CountingRateLimiter();
        var fetcher = CreateFetcher(HttpStatusCode.NotFound, "missing", limiter);

        Assert.That(async () => await fetcher.GetContentAsync(PageUri), Throws.TypeOf<HttpRequestException>());
        Assert.Multiple(() =>
        {
            Assert.That(limiter.WaitCount, Is.EqualTo(1));
            Assert.That(limiter.ReleaseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetContentAsync_WorksWithoutRateLimiter()
    {
        var fetcher = CreateFetcher(HttpStatusCode.OK, "ok");

        Assert.That(await fetcher.GetContentAsync(PageUri), Is.EqualTo("ok"));
    }

    [Test]
    public async Task GetContentAsync_RequestsTheGivenUri()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "ok");
        var fetcher = new HttpPageFetcher(new HttpClient(handler), NullLogger<HttpPageFetcher>.Instance);

        await fetcher.GetContentAsync(PageUri);

        Assert.That(handler.LastRequestUri, Is.EqualTo(PageUri));
    }

    private static HttpPageFetcher CreateFetcher(HttpStatusCode status, string body, IRateLimiter? limiter = null) =>
        new(new HttpClient(new StubHandler(status, body)), NullLogger<HttpPageFetcher>.Instance, limiter);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }

    private sealed class CountingRateLimiter : IRateLimiter
    {
        public int WaitCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            WaitCount++;
            return Task.CompletedTask;
        }

        public void Release() => ReleaseCount++;

        public void Reset()
        {
        }
    }
}
