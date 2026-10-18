namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when page fetching fails.
/// </summary>
public class PageFetchException : WebCrawlerException
{
    public Uri? PageUri { get; }
    public int? StatusCode { get; }

    public PageFetchException(string message, Uri? pageUri = null, int? statusCode = null) 
        : base(message)
    {
        PageUri = pageUri;
        StatusCode = statusCode;
    }

    public PageFetchException(string message, Exception innerException, Uri? pageUri = null, int? statusCode = null)
        : base(message, innerException)
    {
        PageUri = pageUri;
        StatusCode = statusCode;
    }
}
