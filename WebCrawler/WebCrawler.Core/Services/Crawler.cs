using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebCrawler.Configuration;
using WebCrawler.Core.Helpers;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;

namespace WebCrawler.Core.Services
{
    /// <summary>
    /// Main web crawler service that performs breadth-first search crawling.
    /// Fetches pages, extracts links, respects robots.txt, and tracks statistics.
    /// </summary>
    public class Crawler : ICrawler
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly ILinkExtractor _linkExtractor;
        private readonly IOutputWriter _outputWriter;
        private readonly ILinkStorage? _linkStorage;
        private readonly IExceptionHandler? _exceptionHandler;
        private readonly IRobotsChecker? _robotsChecker;
        private readonly WebCrawlerSettings _config;
        private readonly ILogger<Crawler> _logger;

        private readonly ConcurrentDictionary<string, byte> _visited = new();

        /// <summary>
        /// Initializes a new instance of the Crawler class.
        /// </summary>
        /// <param name="pageFetcher">Required service to fetch page content</param>
        /// <param name="linkExtractor">Required service to extract links from HTML</param>
        /// <param name="outputWriter">Required service to write crawl results</param>
        /// <param name="linkStorage">Optional service to persist visited links</param>
        /// <param name="exceptionHandler">Optional service to handle exceptions</param>
        /// <param name="robotsChecker">Optional service to check robots.txt rules</param>
        /// <param name="config">Crawler configuration settings</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        public Crawler(
            IPageFetcher pageFetcher,
            ILinkExtractor linkExtractor,
            IOutputWriter outputWriter,
            ILinkStorage? linkStorage,
            IExceptionHandler? exceptionHandler,
            IRobotsChecker? robotsChecker,
            WebCrawlerSettings config,
            ILogger<Crawler>? logger = null)
        {
            _pageFetcher = pageFetcher ?? throw new ArgumentNullException(nameof(pageFetcher));
            _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
            _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
            _linkStorage = linkStorage;
            _exceptionHandler = exceptionHandler;
            _robotsChecker = robotsChecker;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<Crawler>();
        }

        /// <summary>
        /// Performs a breadth-first search crawl starting from the given root URL.
        /// </summary>
        /// <param name="root">The root URL to start crawling from</param>
        /// <returns>Crawl statistics including pages processed, links found, and errors</returns>
        public async Task<CrawlStatistics> CrawlAsync(Uri root)
        {
            var stats = new CrawlStatistics { StartTime = DateTime.UtcNow };
            var queue = new ConcurrentQueue<Uri>();

            queue.Enqueue(root);
            _logger.LogInformation("Starting crawl from root URL: {RootUrl}", root);

            try
            {
                while (queue.TryDequeue(out var current))
                {
                    // Check if max pages limit is reached
                    if (_config.MaxPagesToCrawl.HasValue && _visited.Count >= _config.MaxPagesToCrawl.Value)
                    {
                        _logger.LogInformation("Crawl limit reached ({MaxPages} pages). Stopping crawl.", _config.MaxPagesToCrawl);
                        break;
                    }

                    // Check if already visited
                    if (!_visited.TryAdd(UrlHelper.Normalize(current), 0))
                    {
                        _logger.LogDebug("URL already visited, skipping: {Url}", current);
                        stats.AlreadyVisited++;
                        stats.LinksSkipped++;
                        continue;
                    }

                    stats.PagesProcessed++;

                    try
                    {
                        _logger.LogInformation("Processing page {PageNumber}: {Url}", stats.PagesProcessed, current);
                        var html = await _pageFetcher.GetContentAsync(current);

                        // Track successful page
                        stats.PagesSuccessful++;
                        stats.TotalBytesDownloaded += html.Length;

                        var discoveredLinks = new List<string>();

                        foreach (var link in _linkExtractor.ExtractLinks(html))
                        {
                            // Check if link is valid URI format
                            if (!Uri.TryCreate(current, link, out var uri))
                            {
                                _logger.LogDebug("Invalid URI format for link: {Link}", link);
                                stats.InvalidFormat++;
                                stats.LinksSkipped++;
                                continue;
                            }

                            // Check if same host
                            if (!UrlHelper.IsSameHost(root, uri))
                            {
                                _logger.LogDebug("Link points to different host, skipping: {Url}", uri);
                                stats.DifferentHost++;
                                stats.LinksSkipped++;
                                continue;
                            }

                            // Check robots.txt if enabled
                            if (_config.RespectRobotsTxt && _robotsChecker != null)
                            {
                                var isAllowed = await _robotsChecker.IsAllowedAsync(uri);
                                if (!isAllowed)
                                {
                                    _logger.LogDebug("URL disallowed by robots.txt, skipping: {Url}", uri);
                                    stats.DisallowedByRobotsTxt++;
                                    stats.LinksSkipped++;
                                    continue;
                                }
                            }

                            // Link will be queued
                            discoveredLinks.Add(uri.AbsoluteUri);
                            stats.LinksDiscovered++;
                            stats.LinksQueued++;

                            queue.Enqueue(uri);
                        }

                        _logger.LogInformation("Found {LinkCount} valid links on page: {Url}", discoveredLinks.Count, current);

                        // Use IOutputWriter to display results
                        _outputWriter.Write(current, discoveredLinks.Select(l => new Uri(l)).ToList());

                        // Record visited link to storage if available
                        if (_linkStorage != null)
                        {
                            await _linkStorage.RecordLinkAsync(current.AbsoluteUri);
                            _logger.LogDebug("Recorded visited link to storage: {Url}", current);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "HTTP request failed for URL: {Url}", current);
                        stats.PagesFailed++;
                        stats.HttpErrors++;
                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.LogWarning(ex, "Request timeout for URL: {Url}", current);
                        stats.PagesFailed++;
                        stats.TimeoutErrors++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to crawl page: {Url}", current);
                        stats.PagesFailed++;
                        stats.OtherErrors++;
                    }
                }
            }
            finally
            {
                // IMPORTANT: Clean up queue to prevent memory leaks
                // If max pages was reached, remaining URLs stay in queue
                int discardedCount = 0;
                while (queue.TryDequeue(out _))
                {
                    discardedCount++;
                }

                if (discardedCount > 0)
                {
                    _logger.LogInformation("Cleared {DiscardedCount} remaining queued URLs from memory", discardedCount);
                }

                // Clear visited dictionary for next crawl if instance is reused
                _visited.Clear();
            }

            // Finalize storage
            if (_linkStorage != null)
            {
                await _linkStorage.FinalizeAsync(stats);
                _logger.LogInformation(
                    "Link storage finalized. Total pages processed: {PageCount}, Total links discovered: {LinkCount}",
                    stats.PagesProcessed, stats.LinksDiscovered);
            }

            stats.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Crawl completed. Statistics: {Stats}", stats.GenerateReport());

            return stats;
        }
    }
}
