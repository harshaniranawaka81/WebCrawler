using System;
using System.Collections.Generic;

namespace WebCrawler.Core.Interfaces
{
    public interface IOutputWriter
    {
        void Write(
            Uri pageUrl,
            IEnumerable<Uri> links);
    }
}
