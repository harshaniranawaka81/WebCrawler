namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when link extraction fails.
/// </summary>
public class LinkExtractionException : WebCrawlerException
{
    public Uri? PageUri { get; }

    public LinkExtractionException(string message, Uri? pageUri = null) : base(message)
    {
        PageUri = pageUri;
    }

    public LinkExtractionException(string message, Uri? pageUri, Exception innerException)
        : base(message, innerException)
    {
        PageUri = pageUri;
    }
}
