using System;

namespace WebCrawler.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for rate limiting HTTP requests to respect server resources
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Waits if necessary to respect the configured rate limit before proceeding.
        /// For limiters that track resources (like concurrent requests), 
        /// call Release() when the operation completes.
        /// </summary>
        /// <returns>A task that completes when it's safe to proceed with a request</returns>
        Task WaitAsync();

        /// <summary>
        /// Releases any resources held by the limiter (e.g., semaphore slots).
        /// Should be called after the operation completes.
        /// </summary>
        void Release();

        /// <summary>
        /// Resets the rate limiter state
        /// </summary>
        void Reset();
    }
}

