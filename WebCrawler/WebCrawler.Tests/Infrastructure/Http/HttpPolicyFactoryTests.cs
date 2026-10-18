using System.Net;
using Polly;
using WebCrawler.Infrastructure.Http;

namespace WebCrawler.Tests.Infrastructure.Http;

[TestFixture]
public class HttpPolicyFactoryTests
{
    private static HttpClientPolicies CreateSettings(int retryCount = 2) => new()
    {
        RetryCount = retryCount,
        RetryDelaySeconds = 0,
        CircuitBreakerFailureThreshold = 5,
        CircuitBreakerTimeoutSeconds = 1
    };

    [Test]
    public async Task RetryPolicy_RetriesServerErrorsUntilSuccess()
    {
        var policy = HttpPolicyFactory.CreateRetryPolicy(CreateSettings());
        var attempts = 0;

        var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            var status = attempts < 3 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(attempts, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task RetryPolicy_DoesNotRetryClientErrors()
    {
        var policy = HttpPolicyFactory.CreateRetryPolicy(CreateSettings());
        var attempts = 0;

        var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(attempts, Is.EqualTo(1));
        });
    }

    [Test]
    public void CombinedPolicy_IsCreated()
    {
        var policy = HttpPolicyFactory.CreateCombinedPolicy(CreateSettings());

        Assert.That(policy, Is.Not.Null);
    }
}
