using System;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Enforces a minimum delay between consecutive HTTP requests.
    /// This is a simple token-bucket style rate limiter that ensures no requests
    /// happen faster than the configured interval.
    /// </summary>
    public class DelayRateLimiter : IRateLimiter
    {
        private readonly int _delayMs;
        private readonly ILogger<DelayRateLimiter> _logger;
        private DateTime _lastRequestTime = DateTime.UtcNow.AddSeconds(-1);

        public DelayRateLimiter(int delayMs, ILogger<DelayRateLimiter>? logger = null)
        {
            _delayMs = Math.Max(0, delayMs);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<DelayRateLimiter>();
        }

        /// <summary>
        /// Waits if necessary to ensure minimum delay since last request
        /// </summary>
        public async Task WaitAsync()
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRequest = now - _lastRequestTime;
            var delayNeeded = TimeSpan.FromMilliseconds(_delayMs) - timeSinceLastRequest;

            if (delayNeeded.TotalMilliseconds > 0)
            {
                var delayMs = (int)Math.Ceiling(delayNeeded.TotalMilliseconds);
                _logger.LogDebug("Rate limit: waiting {DelayMs}ms before next request", delayMs);
                await Task.Delay(delayMs);
            }

            _lastRequestTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Resets the rate limiter
        /// </summary>
        public void Reset()
        {
            _lastRequestTime = DateTime.UtcNow.AddSeconds(-1);
            _logger.LogDebug("Rate limiter reset");
        }
    }
}
