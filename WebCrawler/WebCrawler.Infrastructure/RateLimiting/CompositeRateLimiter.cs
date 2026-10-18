using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.RateLimiting
{
    /// <summary>
    /// Combined rate limiter that enforces both delay-based and concurrency-based limits.
    /// Integrates the logic of DelayRateLimiter and ConcurrentRequestLimiter into a single, efficient limiter.
    /// Ensures requests are spaced out by minimum delay AND respects concurrent request limits.
    /// </summary>
    public class CompositeRateLimiter : IRateLimiter
    {
        // Delay-based rate limiting
        private readonly int _delayMs;
        private readonly object _delayLock = new();
        private DateTime _nextAllowedTime;

        // Concurrency-based rate limiting
        private readonly SemaphoreSlim _semaphore;

        // Composite management
        private readonly List<IRateLimiter> _limiters = new();
        private readonly ILogger<CompositeRateLimiter> _logger;

        /// <summary>
        /// Creates a combined rate limiter with both delay and concurrency limits.
        /// </summary>
        /// <param name="delayMs">Minimum delay in milliseconds between consecutive requests</param>
        /// <param name="maxConcurrentRequests">Maximum number of concurrent requests allowed</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        public CompositeRateLimiter(
            int delayMs = 0,
            int maxConcurrentRequests = 1,
            ILogger<CompositeRateLimiter>? logger = null)
        {
            _delayMs = Math.Max(0, delayMs);
            _nextAllowedTime = DateTime.UtcNow;

            if (maxConcurrentRequests <= 0)                throw new ArgumentException("Max concurrent requests must be greater than 0", nameof(maxConcurrentRequests));

            _semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<CompositeRateLimiter>();

            _logger.LogInformation(
                "Composite rate limiter initialized: {DelayMs}ms delay, {MaxConcurrent} max concurrent requests",
                _delayMs, maxConcurrentRequests);
        }

        /// <summary>
        /// Adds a custom rate limiter to the composite (for extensibility)
        /// </summary>
        public CompositeRateLimiter Add(IRateLimiter limiter)
        {
            if (limiter != null)
            {
                _limiters.Add(limiter);
                _logger.LogDebug("Added custom rate limiter: {Type}", limiter.GetType().Name);
            }
            return this;
        }

        /// <summary>
        /// Waits to respect both delay and concurrency rate limits.
        /// First enforces the minimum delay since the last request, then acquires a concurrency slot.
        /// </summary>
        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            // 1. Apply delay-based rate limiting
            await ApplyDelayAsync(cancellationToken);

            // 2. Apply concurrency-based rate limiting
            await AcquireConcurrencySlotAsync(cancellationToken);

            // 3. Apply any additional custom rate limiters
            foreach (var limiter in _limiters)
            {
                await limiter.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Enforces minimum delay between consecutive requests
        /// </summary>
        private async Task ApplyDelayAsync(CancellationToken cancellationToken)
        {
            if (_delayMs <= 0)
                return;

            // Reserve the next request slot under a lock so concurrent callers are spaced apart.
            TimeSpan delayNeeded;
            lock (_delayLock)
            {
                var now = DateTime.UtcNow;
                var scheduled = _nextAllowedTime > now ? _nextAllowedTime : now;
                _nextAllowedTime = scheduled.AddMilliseconds(_delayMs);
                delayNeeded = scheduled - now;
            }

            if (delayNeeded.TotalMilliseconds > 0)
            {
                var delayMs = (int)Math.Ceiling(delayNeeded.TotalMilliseconds);
                _logger.LogDebug("Rate limit: waiting {DelayMs}ms before next request", delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        /// <summary>
        /// Acquires a concurrency slot, waiting if necessary
        /// </summary>
        private async Task AcquireConcurrencySlotAsync(CancellationToken cancellationToken)
        {
            var availableBefore = _semaphore.CurrentCount;

            if (availableBefore < 1)
            {
                _logger.LogDebug("Concurrent request limit reached, waiting for slot to become available");
            }

            await _semaphore.WaitAsync(cancellationToken);
            _logger.LogDebug("Request slot acquired, {AvailableSlots} slots remaining", _semaphore.CurrentCount);
        }

        /// <summary>
        /// Releases the concurrency slot after request completes
        /// </summary>
        public void Release()
        {
            _semaphore.Release();
            _logger.LogDebug("Request slot released, {AvailableSlots} slots available", _semaphore.CurrentCount);

            // Also release from custom limiters
            foreach (var limiter in _limiters)
            {
                limiter.Release();
            }
        }

        /// <summary>
        /// Resets both delay and concurrency rate limiters
        /// </summary>
        public void Reset()
        {
            lock (_delayLock)
            {
                _nextAllowedTime = DateTime.UtcNow;
            }

            _logger.LogDebug("Rate limiter delay reset");

            // Reset custom limiters
            foreach (var limiter in _limiters)
            {
                limiter.Reset();
            }

            _logger.LogDebug("All rate limiters reset");
        }

        /// <summary>
        /// Gets the number of configured custom limiters
        /// </summary>
        public int LimiterCount => _limiters.Count;

        /// <summary>
        /// Gets the number of available concurrency slots
        /// </summary>
        public int AvailableSlots => _semaphore.CurrentCount;

        /// <summary>
        /// Gets the configured delay in milliseconds
        /// </summary>
        public int DelayMs => _delayMs;
    }
}
