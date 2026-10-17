namespace WebCrawler.Configuration;

/// <summary>
/// Configuration for HTTP client resilience policies
/// </summary>
public class HttpClientPolicies
{
    /// <summary>
    /// Number of retry attempts (default: 3)
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Initial delay in seconds between retries (default: 1)
    /// Uses exponential backoff: 1s, 2s, 4s, 8s...
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Number of consecutive failures before circuit breaks (default: 5)
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds the circuit breaker stays open (default: 10)
    /// </summary>
    public int CircuitBreakerTimeoutSeconds { get; set; } = 10;
}
