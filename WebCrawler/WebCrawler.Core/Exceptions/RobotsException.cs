namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when robots.txt fetching or parsing fails.
/// </summary>
public class RobotsException : WebCrawlerException
{
    public Uri? RobotsUri { get; }

    public RobotsException(string message, Uri? robotsUri = null) : base(message)
    {
        RobotsUri = robotsUri;
    }

    public RobotsException(string message, Uri? robotsUri, Exception innerException)
        : base(message, innerException)
    {
        RobotsUri = robotsUri;
    }
}
