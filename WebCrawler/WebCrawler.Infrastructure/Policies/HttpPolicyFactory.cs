using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WebCrawler.Infrastructure.Configuration;

namespace WebCrawler.Infrastructure.Policies;

/// <summary>
/// Factory for creating resilience policies for HTTP requests
/// </summary>
public static class HttpPolicyFactory
{
    /// <summary>
    /// Creates a retry policy that handles transient HTTP failures
    /// </summary>
    /// <remarks>
    /// Retries on:
    /// - HttpRequestException
    /// - OperationCanceledException (timeouts)
    /// - HTTP 5xx errors (server errors)
    ///
    /// Uses exponential backoff: 1s, 2s, 4s, 8s...
    /// </remarks>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(
        HttpClientPolicies settings,
        ILogger? logger = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<OperationCanceledException>()
            .OrResult<HttpResponseMessage>(r =>
                !r.IsSuccessStatusCode &&
                (int)r.StatusCode >= 500)  // Retry only on server errors
            .WaitAndRetryAsync(
                retryCount: settings.RetryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * settings.RetryDelaySeconds),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var message = outcome.Exception?.Message ??
                        (!outcome.Result?.IsSuccessStatusCode ?? false
                            ? $"HTTP {(int)outcome.Result!.StatusCode}"
                            : "Unknown error");

                    logger?.LogWarning(
                        outcome.Exception,
                        "⏳ Retry attempt {RetryCount}/{MaxRetries} after {DelaySeconds}s - {Reason}",
                        retryCount,
                        settings.RetryCount,
                        timespan.TotalSeconds,
                        message);
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy to prevent cascading failures
    /// </summary>
    /// <remarks>
    /// Opens circuit after specified number of consecutive failures.
    /// Prevents requests from being sent while circuit is open.
    /// Automatically attempts to close after timeout expires.
    /// </remarks>
    public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(
        HttpClientPolicies settings,
        ILogger? logger = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<OperationCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync<HttpResponseMessage>(
                handledEventsAllowedBeforeBreaking: settings.CircuitBreakerFailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(settings.CircuitBreakerTimeoutSeconds),
                onBreak: (outcome, timespan) =>
                {
                    logger?.LogCritical(
                        "Circuit breaker opened! {FailureThreshold} failures detected. " +
                        "Requests will be blocked for {TimeoutSeconds}s.",
                        settings.CircuitBreakerFailureThreshold,
                        timespan.TotalSeconds);
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit breaker reset. Resuming requests.");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit breaker half-open. Testing with next request...");
                });
    }

    /// <summary>
    /// Creates a combined policy (retry + circuit breaker) using Polly Wrap
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy(
        HttpClientPolicies settings,
        ILogger? logger = null)
    {
        var retryPolicy = CreateRetryPolicy(settings, logger);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(settings, logger);

        // Retry first, then circuit breaker
        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
}
