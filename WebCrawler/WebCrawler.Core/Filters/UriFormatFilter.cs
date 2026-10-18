using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Filters;

/// <summary>
/// Filters URLs to only allow paths that are valid URI format.
/// This is a pure domain logic filter with no external dependencies.
/// </summary>
public class UriFormatFilter : IUrlFilter
{
    private readonly ILogger<UriFormatFilter>? _logger;

    public string FilterName => "UriFormat";

    public UriFormatFilter(ILogger<UriFormatFilter>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri)
    {
        // If we got here, the URI is already valid (Uri.TryCreate succeeded in the caller)
        _logger?.LogDebug("URI format filter passed for: {Url}", uri);
        return Task.FromResult(true);
    }
}
