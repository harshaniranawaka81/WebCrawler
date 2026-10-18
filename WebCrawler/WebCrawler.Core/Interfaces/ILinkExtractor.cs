namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for extracting hyperlinks from HTML content.
/// </summary>
public interface ILinkExtractor
{
    /// <summary>
    /// Extracts all hyperlinks from HTML content.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>An enumeration of extracted links as strings.</returns>
    IEnumerable<string> ExtractLinks(string html);
}
