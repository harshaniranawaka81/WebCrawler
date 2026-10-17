using System;

namespace WebCrawler.Infrastructure.Interfaces
{
    public interface IPageFetcher
    {
        Task<string> GetContentAsync(Uri uri);
    }
}
