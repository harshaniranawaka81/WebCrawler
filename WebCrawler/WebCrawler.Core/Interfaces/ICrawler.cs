using System;

namespace WebCrawler.Core.Interfaces
{
    public interface ICrawler
    {
        Task CrawlAsync(Uri rootUrl);
    }
}
