namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for checking URL compliance with robots.txt rules.
/// </summary>
public interface IRobotsChecker
{
    /// <summary>
    /// Checks if a URL is allowed by robots.txt.
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns>True if the URL is allowed, false if it should be excluded.</returns>
    Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current robots.txt rules for the domain.
    /// Implementation-dependent return type details.
    /// </summary>
    Task<object?> GetRulesAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cached robots.txt rules.
    /// </summary>
    void ClearCache();
}
