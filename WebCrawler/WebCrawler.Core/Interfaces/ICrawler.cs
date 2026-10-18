using System;
using WebCrawler.Core.Models;

namespace WebCrawler.Core.Interfaces
{
    public interface ICrawler
    {
        Task<CrawlStatistics> CrawlAsync(Uri rootUrl, CancellationToken cancellationToken = default);
    }
}
