namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for outputting crawl results and status information.
/// Abstracts presentation layer output (console, file, API response, etc.).
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Writes discovered links for a page to the output.
    /// </summary>
    /// <param name="pageUrl">The URL of the page being crawled.</param>
    /// <param name="links">The links discovered on the page.</param>
    void Write(Uri pageUrl, IEnumerable<Uri> links);
}
