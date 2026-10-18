namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Manages a chain of URL filters to be applied during crawling.
/// All filters must pass (return true) for a URL to be included.
/// </summary>
public interface IUrlFilterChain
{
    /// <summary>
    /// Evaluates a URL against all filters in the chain.
    /// </summary>
    /// <param name="uri">The URL to filter.</param>
    /// <param name="baseUri">The base/root URL for context.</param>
    /// <returns>
    /// A tuple containing:
    /// - bool: True if URL passes all filters, false otherwise
    /// - string: The name of the filter that rejected the URL (if rejected), null if passed or no filters
    /// </returns>
    Task<(bool Passed, string? RejectionReason)> FilterAsync(Uri uri, Uri baseUri);

    /// <summary>
    /// Adds a filter to the chain.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    /// <returns>The chain instance for fluent building.</returns>
    IUrlFilterChain AddFilter(IUrlFilter filter);
}
