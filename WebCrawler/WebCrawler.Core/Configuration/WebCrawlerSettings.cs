namespace WebCrawler.Configuration;

public class WebCrawlerSettings
{
    public string DefaultUrl { get; set; } = "https://webscraper.io/test-sites";
    public int ConnectionTimeout { get; set; } = 30;
    public int? MaxPagesToCrawl { get; set; } = null;  // null = no limit
    public string VisitedPagesOutputFilePath { get; set; } = "./visited_links";  // Directory where visited links file will be saved
    public bool RespectRobotsTxt { get; set; } = true;  // Whether to respect robots.txt rules
    public int DelayBetweenRequestsMs { get; set; } = 1000;  // Delay in milliseconds between HTTP requests
    public int? MaxConcurrentRequests { get; set; } = 3;  // null = unlimited, number limits concurrent requests

    /// <summary>
    /// robots.txt cache expiration time in minutes.
    /// After this duration, cached robots.txt entries are refetched.
    /// </summary>
    public int RobotsCacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Timeout in seconds for fetching robots.txt.
    /// Prevents hanging on slow or unresponsive robots.txt endpoints.
    /// </summary>
    public int RobotsFetchTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Number of links to buffer before flushing to disk.
    /// Larger values improve performance (fewer I/O operations)
    /// but increase data loss risk in crashes.
    /// Recommended: 50-200 for most use cases.
    /// </summary>
    public int StorageFlushIntervalLinks { get; set; } = 100;

    /// <summary>
    /// StreamWriter buffer size in bytes.
    /// Larger values reduce I/O syscalls but use more memory.
    /// Default 4096 is reasonable; rarely changed.
    /// </summary>
    public int StorageWriterBufferSize { get; set; } = 4096;

    /// <summary>
    /// URL normalization mode: whether to include query parameters.
    /// true: URLs with different query parameters are treated as different (e.g., ?sort=asc vs ?sort=desc)
    /// false: URLs are matched by path only, query parameters ignored (legacy behavior)
    /// </summary>
    public bool NormalizeUrlsIncludeQueryParameters { get; set; } = true;
}

