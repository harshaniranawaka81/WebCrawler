using System;
using System.Collections.Generic;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Core.Services
{
    /// <summary>
    /// Outputs crawled links to the console
    /// </summary>
    public class ConsoleOutputWriter : IOutputWriter
    {
        public void Write(
            Uri pageUrl,
            IEnumerable<Uri> links)
        {
            if (pageUrl == null) throw new ArgumentNullException(nameof(pageUrl));
            if (links == null) throw new ArgumentNullException(nameof(links));

            Console.WriteLine($"\n Page: {pageUrl}");

            var linkList = links.ToList();
            if (!linkList.Any())
            {
                Console.WriteLine("  No links found");
                return;
            }

            Console.WriteLine($"  Found {linkList.Count} link(s):");
            foreach (var link in linkList)
            {
                Console.WriteLine($"    {link}");
            }
        }
    }
}
