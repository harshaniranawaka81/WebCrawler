using System;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Limits the number of concurrent HTTP requests using a semaphore.
    /// This prevents overwhelming servers by limiting parallelism.
    /// </summary>
    public class ConcurrentRequestLimiter : IRateLimiter
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<ConcurrentRequestLimiter> _logger;

        public ConcurrentRequestLimiter(int maxConcurrentRequests, ILogger<ConcurrentRequestLimiter>? logger = null)
        {
            if (maxConcurrentRequests <= 0)
                throw new ArgumentException("Max concurrent requests must be greater than 0", nameof(maxConcurrentRequests));

            _semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<ConcurrentRequestLimiter>();

            _logger.LogInformation("Concurrent request limiter initialized with max {MaxConcurrent} requests", maxConcurrentRequests);
        }

        /// <summary>
        /// Waits if necessary until a slot is available for a new request
        /// </summary>
        public async Task WaitAsync()
        {
            var availableBefore = _semaphore.CurrentCount;

            if (availableBefore < 1)
            {
                _logger.LogDebug("Concurrent request limit reached, waiting for slot to become available");
            }

            await _semaphore.WaitAsync();

            _logger.LogDebug("Request slot acquired, {AvailableSlots} slots remaining", _semaphore.CurrentCount);
        }

        /// <summary>
        /// Releases a request slot back to the pool
        /// </summary>
        public void Release()
        {
            _semaphore.Release();
            _logger.LogDebug("Request slot released, {AvailableSlots} slots available", _semaphore.CurrentCount);
        }

        /// <summary>
        /// Resets the rate limiter to allow max concurrent requests again
        /// </summary>
        public void Reset()
        {
            // Reset is not directly applicable to semaphore-based limiting
            // but we can dispose and recreate if needed
            _logger.LogDebug("Concurrent request limiter does not support reset");
        }

        /// <summary>
        /// Gets the number of available request slots
        /// </summary>
        public int AvailableSlots => _semaphore.CurrentCount;
    }
}
