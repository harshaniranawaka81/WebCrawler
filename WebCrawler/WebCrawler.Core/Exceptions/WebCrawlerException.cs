namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Base exception for all WebCrawler-specific exceptions.
/// Allows catching all application exceptions while distinguishing them from framework exceptions.
/// </summary>
public class WebCrawlerException : Exception
{
    public WebCrawlerException(string message) : base(message) { }
    public WebCrawlerException(string message, Exception innerException) : base(message, innerException) { }
}
