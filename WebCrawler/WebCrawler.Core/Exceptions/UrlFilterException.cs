namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when URL filtering or validation fails.
/// </summary>
public class UrlFilterException : WebCrawlerException
{
    public string? Url { get; }

    public UrlFilterException(string message, string? url = null) : base(message)
    {
        Url = url;
    }

    public UrlFilterException(string message, string? url, Exception innerException) 
        : base(message, innerException)
    {
        Url = url;
    }
}
