using System;

namespace WebCrawler.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for rate limiting HTTP requests to respect server resources
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Waits if necessary to respect the configured rate limit before proceeding
        /// </summary>
        /// <returns>A task that completes when it's safe to proceed with a request</returns>
        Task WaitAsync();

        /// <summary>
        /// Resets the rate limiter state
        /// </summary>
        void Reset();
    }
}
