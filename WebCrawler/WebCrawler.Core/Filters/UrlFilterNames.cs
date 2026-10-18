namespace WebCrawler.Core.Filters;

/// <summary>
/// Well-known filter names reported as rejection reasons by the URL filter chain.
/// </summary>
public static class UrlFilterNames
{
    public const string SameHost = "SameHost";
    public const string RobotsText = "RobotsText";
}
