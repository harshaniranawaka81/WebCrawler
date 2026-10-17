using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Helpers;
using WebCrawler.Core.Interfaces;
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

        public async Task CrawlAsync(Uri root)
        {
            var queue = new ConcurrentQueue<Uri>();

            queue.Enqueue(root);
            _logger.LogInformation("Starting crawl from root URL: {RootUrl}", root);

            int pagesProcessed = 0;
            int linksDiscovered = 0;

            while (queue.TryDequeue(out var current))
            {
                // Check if max pages limit is reached
                if (_maxPagesToCrawl.HasValue && _visited.Count >= _maxPagesToCrawl.Value)
                {
                    _logger.LogInformation("Crawl limit reached ({MaxPages} pages). Stopping crawl.", _maxPagesToCrawl);
                    Console.WriteLine($"\n⚠️  Crawl limit reached ({_maxPagesToCrawl} pages)");
                    break;
                }

                if (!_visited.TryAdd(
                    UrlHelper.Normalize(current), 0))
                {
                    _logger.LogDebug("URL already visited, skipping: {Url}", current);
                    continue;
                }

                pagesProcessed++;

                try
                {
                    _logger.LogInformation("Processing page {PageNumber}: {Url}", pagesProcessed, current);
                    var html =
                        await _pageFetcher.GetContentAsync(current);

                    var discoveredLinks = new List<string>();

                    foreach (var link in _linkExtractor.ExtractLinks(html))
                    {
                        if (!Uri.TryCreate(current, link, out var uri))
                        {
                            _logger.LogDebug("Invalid URI format for link: {Link}", link);
                            continue;
                        }

                        if (!UrlHelper.IsSameHost(root, uri))
                        {
                            _logger.LogDebug("Link points to different host, skipping: {Url}", uri);
                            continue;
                        }

                        // Check robots.txt if enabled
                        if (_respectRobotsTxt && _robotsChecker != null)
                        {
                            var isAllowed = await _robotsChecker.IsAllowedAsync(uri);
                            if (!isAllowed)
                            {
                                _logger.LogDebug("URL disallowed by robots.txt, skipping: {Url}", uri);
                                continue;
                            }
                        }

                        discoveredLinks.Add(uri.AbsoluteUri);
                        linksDiscovered++;

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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to crawl page: {Url}", current);
                    // Log page crawl failure - will be caught by global handler if needed
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠️  Failed to crawl: {current}");
                    Console.ResetColor();

                    // Let exception handler log if available
                    if (_exceptionHandler != null)
                    {
                        _exceptionHandler.Handle(ex);
                    }
                    else
                    {
                        Console.WriteLine($"   └─ {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // Finalize storage
            if (_linkStorage != null)
            {
                await _linkStorage.FinalizeAsync();
                _logger.LogInformation("Link storage finalized. Total pages processed: {PageCount}, Total links discovered: {LinkCount}", pagesProcessed, linksDiscovered);
            }
            else
            {
                _logger.LogInformation("Crawl completed. Total pages processed: {PageCount}, Total links discovered: {LinkCount}", pagesProcessed, linksDiscovered);
            }
        }
    }
}

