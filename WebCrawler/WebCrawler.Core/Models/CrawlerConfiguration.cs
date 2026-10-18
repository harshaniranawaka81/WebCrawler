namespace WebCrawler.Core.Models
{
    /// <summary>
    /// Encapsulates all configuration settings for the crawler.
    /// This reduces constructor complexity and provides a cohesive configuration object.
    /// </summary>
    public record CrawlerConfiguration
    {
        /// <summary>
        /// Maximum number of pages to crawl. Null means no limit.
        /// </summary>
        public int? MaxPagesToCrawl { get; set; }

        /// <summary>
        /// Whether to respect robots.txt rules. Default is true.
        /// </summary>
        public bool RespectRobotsTxt { get; set; } = true;

        /// <summary>
        /// Initializes a new CrawlerConfiguration with default settings.
        /// </summary>
        public CrawlerConfiguration()
        {
        }

        /// <summary>
        /// Initializes a new CrawlerConfiguration with specified settings.
        /// </summary>
        public CrawlerConfiguration(int? maxPagesToCrawl, bool respectRobotsTxt = true)
        {
            MaxPagesToCrawl = maxPagesToCrawl;
            RespectRobotsTxt = respectRobotsTxt;
        }
    }
}
