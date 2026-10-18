using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WebCrawler.Configuration;
using WebCrawler.Core.Filters;
using WebCrawler.Core.Helpers;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;

namespace WebCrawler.Core.Services
{
    /// <summary>
    /// Main web crawler service that crawls pages concurrently using a pool of workers.
    /// Fetches pages, extracts links, respects robots.txt, and tracks statistics.
    /// The number of workers is MaxConcurrentRequests (minimum 1).
    /// </summary>
    public class Crawler : ICrawler
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly ILinkExtractor _linkExtractor;
        private readonly IOutputWriter _outputWriter;
        private readonly ILinkStorage? _linkStorage;
        private readonly IExceptionHandler? _exceptionHandler;
        private readonly IUrlFilterChain _filterChain;
        private readonly WebCrawlerSettings _config;
        private readonly ILogger<Crawler> _logger;

        private readonly ConcurrentDictionary<string, byte> _visited = new();
        private readonly object _visitedGate = new();

        /// <summary>
        /// Initializes a new instance of the Crawler class.
        /// </summary>
        /// <param name="pageFetcher">Required service to fetch page content</param>
        /// <param name="linkExtractor">Required service to extract links from HTML</param>
        /// <param name="outputWriter">Required service to write crawl results</param>
        /// <param name="linkStorage">Optional service to persist visited links</param>
        /// <param name="exceptionHandler">Optional service to handle exceptions</param>
        /// <param name="filterChain">Required filter chain deciding which discovered links are followed (same host, robots.txt, ...)</param>
        /// <param name="config">Crawler configuration settings</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        public Crawler(
            IPageFetcher pageFetcher,
            ILinkExtractor linkExtractor,
            IOutputWriter outputWriter,
            ILinkStorage? linkStorage,
            IExceptionHandler? exceptionHandler,
            IUrlFilterChain filterChain,
            WebCrawlerSettings config,
            ILogger<Crawler>? logger = null)
        {
            _pageFetcher = pageFetcher ?? throw new ArgumentNullException(nameof(pageFetcher));
            _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
            _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
            _linkStorage = linkStorage;
            _exceptionHandler = exceptionHandler;
            _filterChain = filterChain ?? throw new ArgumentNullException(nameof(filterChain));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<Crawler>();
        }

        /// <summary>
        /// Crawls the site starting from the given root URL using concurrent workers.
        /// </summary>
        /// <param name="root">The root URL to start crawling from</param>
        /// <returns>Crawl statistics including pages processed, links found, and errors</returns>
        public async Task<CrawlStatistics> CrawlAsync(Uri root, CancellationToken cancellationToken = default)
        {
            var stats = new CrawlStatistics { StartTime = DateTime.UtcNow };
            var statsLock = new object();
            var ioGate = new SemaphoreSlim(1, 1);
            var channel = Channel.CreateUnbounded<Uri>();

            // Number of URLs queued or being processed; the crawl is complete when it reaches zero.
            int pending = 0;
            int discarded = 0;
            bool limitReached = false;

            var workerCount = _config.MaxConcurrentRequests is > 0 ? _config.MaxConcurrentRequests.Value : 1;

            void UpdateStats(Action<CrawlStatistics> update)
            {
                lock (statsLock)
                {
                    update(stats);
                }
            }

            void Enqueue(Uri uri)
            {
                Interlocked.Increment(ref pending);
                channel.Writer.TryWrite(uri);
            }

            _logger.LogInformation("Starting crawl from root URL: {RootUrl} with {Workers} worker(s)", root, workerCount);
            Enqueue(root);

            async Task WorkerAsync()
            {
                await foreach (var current in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        if (Volatile.Read(ref limitReached))
                        {
                            Interlocked.Increment(ref discarded);
                        }
                        else
                        {
                            await ProcessPageAsync(current);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pending) == 0)
                        {
                            channel.Writer.TryComplete();
                        }
                    }
                }
            }

            async Task ProcessPageAsync(Uri current)
            {
                bool limitHit = false;
                bool alreadyVisited = false;

                lock (_visitedGate)
                {
                    if (_config.MaxPagesToCrawl.HasValue && _visited.Count >= _config.MaxPagesToCrawl.Value)
                    {
                        limitHit = true;
                    }
                    else if (!_visited.TryAdd(UrlHelper.Normalize(current), 0))
                    {
                        alreadyVisited = true;
                    }
                }

                if (limitHit)
                {
                    _logger.LogInformation("Crawl limit reached ({MaxPages} pages). Stopping crawl.", _config.MaxPagesToCrawl);
                    Volatile.Write(ref limitReached, true);
                    Interlocked.Increment(ref discarded);
                    return;
                }

                if (alreadyVisited)
                {
                    _logger.LogDebug("URL already visited, skipping: {Url}", current);
                    UpdateStats(s =>
                    {
                        s.AlreadyVisited++;
                        s.LinksSkipped++;
                    });
                    return;
                }

                int pageNumber = 0;
                UpdateStats(s => pageNumber = ++s.PagesProcessed);

                try
                {
                    _logger.LogInformation("Processing page {PageNumber}: {Url}", pageNumber, current);
                    var html = await _pageFetcher.GetContentAsync(current, cancellationToken);

                    UpdateStats(s =>
                    {
                        s.PagesSuccessful++;
                        s.TotalBytesDownloaded += html.Length;
                    });

                    var discoveredLinks = new List<string>();

                    foreach (var link in _linkExtractor.ExtractLinks(html))
                    {
                        if (!Uri.TryCreate(current, link, out var uri))
                        {
                            _logger.LogDebug("Invalid URI format for link: {Link}", link);
                            UpdateStats(s =>
                            {
                                s.InvalidFormat++;
                                s.LinksSkipped++;
                            });
                            continue;
                        }

                        var (passed, rejectedBy) = await _filterChain.FilterAsync(uri, root, cancellationToken);
                        if (!passed)
                        {
                            _logger.LogDebug("Link rejected by {Filter} filter, skipping: {Url}", rejectedBy, uri);
                            UpdateStats(s =>
                            {
                                switch (rejectedBy)
                                {
                                    case UrlFilterNames.SameHost:
                                        s.DifferentHost++;
                                        break;
                                    case UrlFilterNames.RobotsText:
                                        s.DisallowedByRobotsTxt++;
                                        break;
                                }

                                s.LinksSkipped++;
                            });
                            continue;
                        }

                        discoveredLinks.Add(uri.AbsoluteUri);
                        UpdateStats(s =>
                        {
                            s.LinksDiscovered++;
                            s.LinksQueued++;
                        });

                        Enqueue(uri);
                    }

                    _logger.LogInformation("Found {LinkCount} valid links on page: {Url}", discoveredLinks.Count, current);

                    await ioGate.WaitAsync(cancellationToken);
                    try
                    {
                        _outputWriter.Write(current, discoveredLinks.Select(l => new Uri(l)).ToList());

                        if (_linkStorage != null)
                        {
                            await _linkStorage.RecordLinkAsync(current.AbsoluteUri);
                            _logger.LogDebug("Recorded visited link to storage: {Url}", current);
                        }
                    }
                    finally
                    {
                        ioGate.Release();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "HTTP request failed for URL: {Url}", current);
                    UpdateStats(s =>
                    {
                        s.PagesFailed++;
                        s.HttpErrors++;
                    });
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning(ex, "Request timeout for URL: {Url}", current);
                    UpdateStats(s =>
                    {
                        s.PagesFailed++;
                        s.TimeoutErrors++;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to crawl page: {Url}", current);
                    UpdateStats(s =>
                    {
                        s.PagesFailed++;
                        s.OtherErrors++;
                    });
                }
            }

            try
            {
                var workers = Enumerable.Range(0, workerCount)
                    .Select(_ => Task.Run(WorkerAsync))
                    .ToArray();

                await Task.WhenAll(workers);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Crawl cancelled. Returning partial statistics");
            }
            finally
            {
                channel.Writer.TryComplete();

                if (discarded > 0)
                {
                    _logger.LogInformation("Discarded {DiscardedCount} remaining queued URLs", discarded);
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
