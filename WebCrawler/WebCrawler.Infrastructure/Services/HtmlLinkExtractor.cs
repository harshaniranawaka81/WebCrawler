using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.Services
{  
    public class HtmlLinkExtractor : ILinkExtractor
    {
        private readonly ILogger<HtmlLinkExtractor> _logger;

        public HtmlLinkExtractor(ILogger<HtmlLinkExtractor> logger)
        {
            _logger = logger;
        }

        public IEnumerable<string> ExtractLinks(string html)
        {
            try
            {
                var document = new HtmlDocument();
                document.LoadHtml(html);

                var links = document.DocumentNode
                    .SelectNodes("//a[@href]")?
                    .Select(n => n.GetAttributeValue("href", ""))
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    ?? Enumerable.Empty<string>();

                var linkList = links.ToList();
                _logger.LogDebug("Extracted {LinkCount} links from HTML document", linkList.Count);

                return linkList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting links from HTML");
                throw;
            }
        }
    }
}
