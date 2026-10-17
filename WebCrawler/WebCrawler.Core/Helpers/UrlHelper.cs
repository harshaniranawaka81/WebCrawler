using System;

namespace WebCrawler.Core.Helpers
{
    public static class UrlHelper
    {
        /// <summary>
        /// Normalizes a URL for comparison purposes
        /// </summary>
        public static string Normalize(Uri uri)
        {
            return uri.GetLeftPart(UriPartial.Path).ToLower();
        }

        /// <summary>
        /// Checks if two URLs belong to the same host
        /// </summary>
        public static bool IsSameHost(Uri baseUri, Uri targetUri)
        {
            return baseUri.Host.Equals(targetUri.Host, StringComparison.OrdinalIgnoreCase);
        }
    }
}
