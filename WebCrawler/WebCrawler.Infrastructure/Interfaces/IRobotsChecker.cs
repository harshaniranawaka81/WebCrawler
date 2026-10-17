using System;
using WebCrawler.Infrastructure.Helpers;

namespace WebCrawler.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for checking URL compliance with robots.txt rules
    /// </summary>
    public interface IRobotsChecker
    {
        /// <summary>
        /// Checks if a URL is allowed by robots.txt
        /// </summary>
        /// <param name="uri">The URI to check</param>
        /// <returns>True if the URL is allowed, false if it should be excluded</returns>
        Task<bool> IsAllowedAsync(Uri uri);

        /// <summary>
        /// Gets the current robots.txt rules for the domain
        /// </summary>
        Task<RobotsParser.RobotRules?> GetRulesAsync(Uri uri);

        /// <summary>
        /// Clears the cached robots.txt rules
        /// </summary>
        void ClearCache();
    }
}
