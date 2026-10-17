using System;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    public class HttpPageFetcher : IPageFetcher
    {
        private readonly HttpClient _client;
        private readonly ILogger<HttpPageFetcher> _logger;

        public HttpPageFetcher(HttpClient client, ILogger<HttpPageFetcher> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<string> GetContentAsync(Uri uri)
        {
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
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout for URL: {Url}", uri);
                throw;
            }
        }
    }
}
