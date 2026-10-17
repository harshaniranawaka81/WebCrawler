using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Combines multiple rate limiting strategies (delay + concurrency limits).
    /// Applies all rate limiters in sequence to ensure compliance with all policies.
    /// </summary>
    public class CompositeRateLimiter : IRateLimiter
    {
        private readonly List<IRateLimiter> _limiters = new();
        private readonly ILogger<CompositeRateLimiter> _logger;

        public CompositeRateLimiter(ILogger<CompositeRateLimiter>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<CompositeRateLimiter>();
        }

        /// <summary>
        /// Adds a rate limiter to the composite
        /// </summary>
        public CompositeRateLimiter Add(IRateLimiter limiter)
        {
            if (limiter != null)
            {
                _limiters.Add(limiter);
                _logger.LogDebug("Added rate limiter: {Type}", limiter.GetType().Name);
            }
            return this;
        }

        /// <summary>
        /// Waits for all configured rate limiters
        /// </summary>
        public async Task WaitAsync()
        {
            if (_limiters.Count == 0)
            {
                return; // No rate limiting configured
            }

            foreach (var limiter in _limiters)
            {
                await limiter.WaitAsync();
            }
        }

        /// <summary>
        /// Resets all configured rate limiters
        /// </summary>
        public void Reset()
        {
            foreach (var limiter in _limiters)
            {
                limiter.Reset();
            }
            _logger.LogDebug("All rate limiters reset");
        }

        /// <summary>
        /// Gets the number of configured limiters
        /// </summary>
        public int LimiterCount => _limiters.Count;
    }
}
