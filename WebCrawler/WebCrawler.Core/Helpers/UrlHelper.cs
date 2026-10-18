using System;

namespace WebCrawler.Core.Helpers
{
    public static class UrlHelper
    {
        /// <summary>
        /// Normalizes a URL for comparison purposes.
        /// Includes path and query parameters for proper deduplication.
        /// Fragment is excluded as it's typically not relevant for crawling.
        /// Uses Ordinal casing (not culture-sensitive) for consistency.
        /// </summary>
        public static string Normalize(Uri uri)
        {
            // Include scheme, host, path, and query (but not fragment)
            // Use Ordinal comparison (case-sensitive lowercase) not culture-sensitive ToLower
            var normalized = $"{uri.Scheme}://{uri.Host}{uri.LocalPath}".ToLowerInvariant();

            // Append query string if present to distinguish URLs with different parameters
            if (!string.IsNullOrEmpty(uri.Query))
            {
                normalized += uri.Query.ToLowerInvariant();
            }

            return normalized;
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
