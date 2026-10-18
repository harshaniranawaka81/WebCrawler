using System;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    public class HttpPageFetcher : IPageFetcher
    {
        private readonly HttpClient _client;
        private readonly IRateLimiter? _rateLimiter;
        private readonly ILogger<HttpPageFetcher> _logger;

        public HttpPageFetcher(
            HttpClient client,
            ILogger<HttpPageFetcher> logger,
            IRateLimiter? rateLimiter = null)
        {
            _client = client;
            _logger = logger;
            _rateLimiter = rateLimiter;
        }

        public async Task<string> GetContentAsync(Uri uri)
        {
            // Apply rate limiting before making the request
            if (_rateLimiter != null)
            {
                await _rateLimiter.WaitAsync();
            }

            try
            {
                _logger.LogDebug("Fetching content from: {Url}", uri);
                var content = await _client.GetStringAsync(uri);
                _logger.LogDebug("Successfully fetched {ContentLength} bytes from {Url}", content.Length, uri);
                return content;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for URL: {Url}", uri);
                throw;
            }
            finally
            {
                // Release rate limiter slot to allow other requests to proceed
                // This is critical for concurrent limiters to free up slots
                if (_rateLimiter != null)
                {
                    _rateLimiter.Release();
                }
            }
        }
    }
}
