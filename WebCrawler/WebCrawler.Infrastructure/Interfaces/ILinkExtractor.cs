using System;
using System.Collections.Generic;

namespace WebCrawler.Infrastructure.Interfaces
{
    public interface ILinkExtractor
    {
        IEnumerable<string> ExtractLinks(string html);
    }
}
