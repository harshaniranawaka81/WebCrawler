namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for fetching page content from a URL.
/// </summary>
public interface IPageFetcher
{
    /// <summary>
    /// Asynchronously retrieves the HTML content of a webpage.
    /// </summary>
    /// <param name="uri">The URI to fetch.</param>
    /// <returns>The HTML content of the page.</returns>
    Task<string> GetContentAsync(Uri uri, CancellationToken cancellationToken = default);
}
