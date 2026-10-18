using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Services;

/// <summary>
/// Chains multiple URL filters together, applying each in sequence.
/// </summary>
public class UrlFilterChain : IUrlFilterChain
{
private readonly List<IUrlFilter> _filters = new List<IUrlFilter>();
    private readonly ILogger<UrlFilterChain>? _logger;

    public UrlFilterChain(ILogger<UrlFilterChain>? logger = null)
    {
        _logger = logger;
    }

    public IUrlFilterChain AddFilter(IUrlFilter filter)
    {
        _filters.Add(filter ?? throw new ArgumentNullException(nameof(filter)));
        _logger?.LogDebug("Filter {FilterName} added to chain.", filter.FilterName);
        return this;
    }

    public async Task<(bool Passed, string? RejectionReason)> FilterAsync(Uri uri, Uri baseUri)
    {
        foreach (var filter in _filters)
        {
            try
            {
                var shouldInclude = await filter.ShouldIncludeAsync(uri, baseUri);

                if (!shouldInclude)
                {
                    return (false, filter.FilterName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Filter {FilterName} threw an exception for URL {Url}", 
                    filter.FilterName, uri);
                // Continue processing other filters on error
            }
        }

        return (true, null);
    }
}
