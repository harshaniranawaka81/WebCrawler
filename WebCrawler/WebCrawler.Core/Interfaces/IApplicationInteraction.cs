using WebCrawler.Core.Models;

namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for interacting with the user through the application interface.
/// Abstracts UI implementation details (console, web, desktop, etc.).
/// </summary>
public interface IApplicationInteraction
{
    /// <summary>
    /// Displays welcome message to the user.
    /// </summary>
    void DisplayWelcome();

    /// <summary>
    /// Asynchronously gets a URL from the user for crawling.
    /// </summary>
    /// <param name="defaultUrl">The default URL to use if the user provides no input.</param>
    /// <returns>The URL entered or the default URL.</returns>
    Task<string> GetUrlAsync(string defaultUrl);

    /// <summary>
    /// Displays an error message indicating the provided URL is invalid.
    /// </summary>
    /// <param name="url">The invalid URL provided by the user.</param>
    void DisplayInvalidUrlError(string url);

    /// <summary>
    /// Displays a message indicating the crawl has started.
    /// </summary>
    /// <param name="url">The URL being crawled.</param>
    void DisplayCrawlStart(string url);

    /// <summary>
    /// Displays the crawl results to the user.
    /// </summary>
    /// <param name="statistics">The crawl statistics; may be null if crawl failed.</param>
    /// <param name="storage">The storage provider for accessing output file paths.</param>
    void DisplayResults(CrawlStatistics? statistics, ILinkStorage storage);

    /// <summary>
    /// Displays a completion message to the user.
    /// </summary>
    void DisplayCompletion();

    /// <summary>
    /// Displays an error message to the user.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    void DisplayError(string message);
}
