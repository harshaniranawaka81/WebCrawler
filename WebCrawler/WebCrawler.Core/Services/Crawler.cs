using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Helpers;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Core.Services
{
    public class Crawler : ICrawler
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly ILinkExtractor _linkExtractor;
        private readonly IOutputWriter _outputWriter;
        private readonly ILinkStorage? _linkStorage;
        private readonly IExceptionHandler? _exceptionHandler;
        private readonly IRobotsChecker? _robotsChecker;
        private readonly int? _maxPagesToCrawl;
        private readonly bool _respectRobotsTxt;
        private readonly ILogger<Crawler> _logger;

        private readonly ConcurrentDictionary<string, byte> _visited
            = new();

        public Crawler(
            IPageFetcher pageFetcher,
            ILinkExtractor linkExtractor,
            IOutputWriter outputWriter,
            ILinkStorage? linkStorage = null,
            int? maxPagesToCrawl = null,
            IExceptionHandler? exceptionHandler = null,
            IRobotsChecker? robotsChecker = null,
            bool respectRobotsTxt = true,
            ILogger<Crawler>? logger = null)
        {
            _pageFetcher = pageFetcher;
            _linkExtractor = linkExtractor;
            _outputWriter = outputWriter;
            _linkStorage = linkStorage;
            _maxPagesToCrawl = maxPagesToCrawl;
            _exceptionHandler = exceptionHandler;
            _robotsChecker = robotsChecker;
            _respectRobotsTxt = respectRobotsTxt;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<Crawler>();
        }

        public async Task<CrawlStatistics> CrawlAsync(Uri root)
        {
            var stats = new CrawlStatistics { StartTime = DateTime.UtcNow };
            var queue = new ConcurrentQueue<Uri>();

            queue.Enqueue(root);
            _logger.LogInformation("Starting crawl from root URL: {RootUrl}", root);

            while (queue.TryDequeue(out var current))
            {
                // Check if max pages limit is reached
                if (_maxPagesToCrawl.HasValue && _visited.Count >= _maxPagesToCrawl.Value)
                {
                    _logger.LogInformation("Crawl limit reached ({MaxPages} pages). Stopping crawl.", _maxPagesToCrawl);
                    Console.WriteLine($"\n⚠️  Crawl limit reached ({_maxPagesToCrawl} pages)");
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
                        if (_respectRobotsTxt && _robotsChecker != null)
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

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠️  HTTP error: {current}");
                    Console.WriteLine($"   └─ {ex.Message}");
                    Console.ResetColor();

                    if (_exceptionHandler != null)
                    {
                        _exceptionHandler.Handle(ex);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning(ex, "Request timeout for URL: {Url}", current);
                    stats.PagesFailed++;
                    stats.TimeoutErrors++;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⏱️  Timeout: {current}");
                    Console.ResetColor();

                    if (_exceptionHandler != null)
                    {
                        _exceptionHandler.Handle(ex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to crawl page: {Url}", current);
                    stats.PagesFailed++;
                    stats.OtherErrors++;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"❌ Failed to crawl: {current}");
                    Console.WriteLine($"   └─ {ex.GetType().Name}: {ex.Message}");
                    Console.ResetColor();

                    if (_exceptionHandler != null)
                    {
                        _exceptionHandler.Handle(ex);
                    }
                }
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