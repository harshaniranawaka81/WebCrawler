namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Represents a URL filter in the filtering chain.
/// Filters are applied sequentially using the chain-of-responsibility pattern.
/// </summary>
public interface IUrlFilter
{
    /// <summary>
    /// Gets the name of this filter for logging purposes.
    /// </summary>
    string FilterName { get; }

    /// <summary>
    /// Determines whether the URL should be included in the crawl.
    /// </summary>
    /// <param name="uri">The URL to evaluate.</param>
    /// <param name="baseUri">The base/root URL for context (for same-host checks, etc.).</param>
    /// <returns>True if the URL passes this filter and should be processed, false otherwise.</returns>
    /// <remarks>
    /// If this method returns false, no further filters in the chain will be called for this URL.
    /// </remarks>
    Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri, CancellationToken cancellationToken = default);
}
