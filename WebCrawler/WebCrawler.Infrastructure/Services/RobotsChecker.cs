using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Helpers;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Fetches and caches robots.txt files to check URL accessibility
    /// </summary>
    public class RobotsChecker : IRobotsChecker
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly ILogger<RobotsChecker> _logger;

        // Cache: domain -> robot rules
        private readonly ConcurrentDictionary<string, RobotsParser.RobotRules?> _rulesCache
            = new();

        // Track domains that have no robots.txt
        private readonly ConcurrentDictionary<string, bool> _noRobotsCache
            = new();

        private const string RobotsPath = "/robots.txt";
        private const int CacheExpirationMinutes = 60;

        public RobotsChecker(IPageFetcher pageFetcher, ILogger<RobotsChecker>? logger = null)
        {
            _pageFetcher = pageFetcher ?? throw new ArgumentNullException(nameof(pageFetcher));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<RobotsChecker>();
        }

        /// <summary>
        /// Checks if a URL is allowed by the site's robots.txt
        /// </summary>
        public async Task<bool> IsAllowedAsync(Uri uri)
        {
            try
            {
                var rules = await GetRulesAsync(uri);
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
        /// Gets the robots.txt rules for a domain, with caching
        /// </summary>
        public async Task<RobotsParser.RobotRules?> GetRulesAsync(Uri uri)
        {
            var domain = uri.Host;

            // Check if we already know there's no robots.txt
            if (_noRobotsCache.TryGetValue(domain, out _))
            {
                return null;
            }

            // Check cache
            if (_rulesCache.TryGetValue(domain, out var cachedRules))
            {
                return cachedRules;
            }

            // Fetch robots.txt
            try
            {
                var robotsUri = new Uri($"{uri.Scheme}://{uri.Host}{RobotsPath}");
                _logger.LogDebug("Fetching robots.txt from: {RobotsUrl}", robotsUri);

                var robotsContent = await _pageFetcher.GetContentAsync(robotsUri);

                if (string.IsNullOrWhiteSpace(robotsContent))
                {
                    _noRobotsCache.TryAdd(domain, true);
                    _logger.LogDebug("robots.txt is empty for domain: {Domain}", domain);
                    return null;
                }

                var rules = RobotsParser.Parse(robotsContent, "*");
                _rulesCache.TryAdd(domain, rules);

                _logger.LogInformation(
                    "Parsed robots.txt for {Domain}: {DisallowCount} disallow rules, {AllowCount} allow rules",
                    domain,
                    rules.DisallowPaths.Count,
                    rules.AllowPaths.Count);

                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Could not fetch robots.txt for domain {Domain}, assuming no restrictions",
                    domain);

                // No robots.txt found or fetch failed - assume no restrictions
                _noRobotsCache.TryAdd(domain, true);
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
