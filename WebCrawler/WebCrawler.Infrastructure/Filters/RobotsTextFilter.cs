using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.Filters;

/// <summary>
/// Filters URLs by consulting robots.txt rules.
/// This filter depends on infrastructure concerns (robots checking),
/// so it belongs in the infrastructure layer.
/// </summary>
public class RobotsTextFilter : IUrlFilter
{
    private readonly IRobotsChecker _robotsChecker;
    private readonly ILogger<RobotsTextFilter>? _logger;

    public string FilterName => "RobotsText";

    public RobotsTextFilter(IRobotsChecker robotsChecker, ILogger<RobotsTextFilter>? logger = null)
    {
        _robotsChecker = robotsChecker ?? throw new ArgumentNullException(nameof(robotsChecker));
        _logger = logger;
    }

    public async Task<bool> ShouldIncludeAsync(Uri uri, Uri baseUri)
    {
        try
        {
            var allowed = await _robotsChecker.IsAllowedAsync(uri);

            if (!allowed)
            {
                _logger?.LogDebug("URL rejected by RobotsText filter: {Url}", uri);
            }

            return allowed;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "RobotsText filter encountered an error for URL {Url}. Allowing by default.", uri);
            return true;
        }
    }
}
