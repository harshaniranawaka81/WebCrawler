using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.Robots
{
    /// <summary>
    /// Fetches and caches robots.txt files to check URL accessibility with expiration support.
    /// Cache entries expire to ensure fresh robots.txt files are fetched periodically.
    /// </summary>
    public class RobotsChecker : IRobotsChecker
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly ILogger<RobotsChecker> _logger;
        private readonly int _cacheExpirationMinutes;
        private readonly int _robotsFetchTimeoutSeconds;

        // Cache entry with timestamp for expiration
        private record CacheEntry
        {
            public RobotsParser.RobotRules? Rules { get; set; }
            public DateTime CachedAt { get; set; }

            public bool IsExpired(int expirationMinutes) =>
                DateTime.UtcNow.Subtract(CachedAt).TotalMinutes > expirationMinutes;
        }

        // Cache: domain - robot rules with timestamp
        private readonly ConcurrentDictionary<string, CacheEntry> _rulesCache
            = new();

        // Track domains that have no robots.txt (with timestamp)
        private readonly ConcurrentDictionary<string, CacheEntry> _noRobotsCache
            = new();

        private const string RobotsPath = "/robots.txt";

        public RobotsChecker(IPageFetcher pageFetcher, ILogger<RobotsChecker>? logger = null, int cacheExpirationMinutes = 60, int robotsFetchTimeoutSeconds = 5)
        {
            _pageFetcher = pageFetcher ?? throw new ArgumentNullException(nameof(pageFetcher));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<RobotsChecker>();
            _cacheExpirationMinutes = cacheExpirationMinutes;
            _robotsFetchTimeoutSeconds = robotsFetchTimeoutSeconds;
        }

        /// <summary>
        /// Checks if a URL is allowed by the site's robots.txt
        /// </summary>
        public async Task<bool> IsAllowedAsync(Uri uri)
        {
            try
            {
                var rules = await GetRobotRulesAsync(uri);
                if (rules == null)
                {
                    // No robots.txt or it's empty, allow crawling
                    return true;
                }

                var allowed = RobotsParser.IsPathAllowed(rules, uri.AbsolutePath);
                if (!allowed)
                {
                    _logger.LogDebug(
                        "URL disallowed by robots.txt: {Url}",
                        uri.AbsoluteUri);
                }

                return allowed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error checking robots.txt for {Host}, allowing URL by default: {Url}",
                    uri.Host,
                    uri.AbsoluteUri);

                // On error, be conservative and allow the URL
                return true;
            }
        }

        /// <summary>
        /// Gets the current robots.txt rules for the domain.
        /// </summary>
        public async Task<object?> GetRulesAsync(Uri uri)
        {
            return await GetRobotRulesAsync(uri);
        }

        /// <summary>
        /// Gets the robots.txt rules for a domain, with caching and expiration support
        /// </summary>
        private async Task<RobotsParser.RobotRules?> GetRobotRulesAsync(Uri uri)
        {
            var domain = uri.Host;

            // Check if we already know there's no robots.txt (with expiration)
            if (_noRobotsCache.TryGetValue(domain, out var noCachedEntry))
            {
                if (!noCachedEntry.IsExpired(_cacheExpirationMinutes))
                {
                    return null;  // Cache hit - no robots.txt exists
                }
                else
                {
                    _noRobotsCache.TryRemove(domain, out _);  // Expired, remove
                    _logger.LogDebug("No robots.txt cache entry expired for domain: {Domain}", domain);
                }
            }

            // Check cache with expiration
            if (_rulesCache.TryGetValue(domain, out var cachedEntry))
            {
                if (!cachedEntry.IsExpired(_cacheExpirationMinutes))
                {
                    _logger.LogDebug("Using cached robots.txt for domain: {Domain}", domain);
                    return cachedEntry.Rules;  // Valid cache hit
                }
                else
                {
                    _rulesCache.TryRemove(domain, out _);  // Expired, remove
                    _logger.LogDebug("Robots.txt cache entry expired for domain: {Domain}", domain);
                }
            }

            // Fetch robots.txt with timeout support
            try
            {
                var robotsUri = new Uri(uri, RobotsPath);
                _logger.LogDebug("Fetching robots.txt from: {RobotsUrl}", robotsUri);

                var robotsContent = await _pageFetcher.GetContentAsync(robotsUri);

                if (string.IsNullOrWhiteSpace(robotsContent))
                {
                    _noRobotsCache[domain] = new CacheEntry
                    {
                        Rules = null,
                        CachedAt = DateTime.UtcNow
                    };
                    _logger.LogDebug("robots.txt is empty for domain: {Domain}", domain);
                    return null;
                }

                var rules = RobotsParser.Parse(robotsContent, "*");
                _rulesCache[domain] = new CacheEntry
                {
                    Rules = rules,
                    CachedAt = DateTime.UtcNow
                };

                _logger.LogInformation(
                    "Parsed robots.txt for {Domain}: {DisallowCount} disallow rules, {AllowCount} allow rules",
                    domain,
                    rules.DisallowPaths.Count,
                    rules.AllowPaths.Count);

                return rules;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Timeout fetching robots.txt for domain {Domain}, assuming no restrictions",
                    domain);

                // Timeout - assume no robots.txt restrictions
                _noRobotsCache[domain] = new CacheEntry
                {
                    Rules = null,
                    CachedAt = DateTime.UtcNow
                };
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Could not fetch robots.txt for domain {Domain}, assuming no restrictions",
                    domain);

                // No robots.txt found or fetch failed - assume no restrictions
                _noRobotsCache[domain] = new CacheEntry
                {
                    Rules = null,
                    CachedAt = DateTime.UtcNow
                };
                return null;
            }
        }

        /// <summary>
        /// Clears all cached robots.txt rules
        /// </summary>
        public void ClearCache()
        {
            _rulesCache.Clear();
            _noRobotsCache.Clear();
            _logger.LogDebug("Robots.txt cache cleared");
        }
    }
}
