using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Filters;

/// <summary>
/// Filters URLs to only allow those not already visited.
/// This is a pure domain logic filter with no external dependencies.
/// </summary>
public class UniqueUrlFilter : IUrlFilter
{
    private readonly Func<Uri, bool> _isVisited;
    private readonly ILogger<UniqueUrlFilter>? _logger;

    public string FilterName => "UniqueUrl";

    /// <summary>
    /// Creates a new UniqueUrlFilter.
    /// </summary>
    /// <param name="isVisited">A function that determines if a URL has already been visited.</param>
    /// <param name="logger">Optional logger.</param>
    public UniqueUrlFilter(Func<Uri, bool> isVisited, ILogger<UniqueUrlFilter>? logger = null)
    {
        _isVisited = isVisited ?? throw new ArgumentNullException(nameof(isVisited));
        _logger = logger;
    }

    public Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri)
    {
        var isUnique = !_isVisited(uri);

        if (!isUnique)
        {
            _logger?.LogDebug("URL rejected by UniqueUrl filter (already visited): {Url}", uri);
        }

        return Task.FromResult(isUnique);
    }
}
