namespace WebCrawler.Core.Exceptions;

/// <summary>
/// Thrown when configuration validation fails at startup.
/// </summary>
public class ConfigurationException : WebCrawlerException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
