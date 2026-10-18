namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when storage operations fail.
/// </summary>
public class StorageException : WebCrawlerException
{
    public StorageException(string message) : base(message) { }
    public StorageException(string message, Exception innerException) : base(message, innerException) { }
}
