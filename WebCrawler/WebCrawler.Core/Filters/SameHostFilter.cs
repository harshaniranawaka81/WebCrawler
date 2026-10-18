using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Filters;

/// <summary>
/// Filters URLs to only allow those on the same host as the base/root URL.
/// This is a pure domain logic filter with no external dependencies.
/// </summary>
public class SameHostFilter : IUrlFilter
{
    private readonly ILogger<SameHostFilter>? _logger;

    public string FilterName => "SameHost";

    public SameHostFilter(ILogger<SameHostFilter>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri)
    {
        var isSameHost = uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase);

        if (!isSameHost)
        {
            _logger?.LogDebug("URL rejected by SameHost filter: {Url} (base: {BaseUrl})", uri, baseUri);
        }

        return Task.FromResult(isSameHost);
    }
}
