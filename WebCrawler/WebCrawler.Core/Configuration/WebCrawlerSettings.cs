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
}
