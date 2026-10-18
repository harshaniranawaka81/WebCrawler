using System;

namespace WebCrawler.Core.Models
{
    /// <summary>
    /// Tracks detailed statistics about a crawling session
    /// </summary>
    public class CrawlStatistics
    {
        // Pages processed
        public int PagesProcessed { get; set; } = 0;
        public int PagesSuccessful { get; set; } = 0;
        public int PagesFailed { get; set; } = 0;

        // Links tracking
        public int LinksDiscovered { get; set; } = 0;
        public int LinksQueued { get; set; } = 0;
        public int LinksSkipped { get; set; } = 0;

        // Skip reasons
        public int AlreadyVisited { get; set; } = 0;
        public int InvalidFormat { get; set; } = 0;
        public int DifferentHost { get; set; } = 0;
        public int DisallowedByRobotsTxt { get; set; } = 0;

        // Timing
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        // Content statistics
        public long TotalBytesDownloaded { get; set; } = 0;
        public int AveragePageSizeBytes => PagesSuccessful > 0 ? (int)(TotalBytesDownloaded / PagesSuccessful) : 0;

        // Error statistics
        public int HttpErrors { get; set; } = 0;
        public int TimeoutErrors { get; set; } = 0;
        public int OtherErrors { get; set; } = 0;

        /// <summary>
        /// Gets the overall success rate as a percentage
        /// </summary>
        public double SuccessRate => PagesProcessed > 0 
            ? Math.Round((double)PagesSuccessful / PagesProcessed * 100, 2) 
            : 0;

        /// <summary>
        /// Gets crawl speed in pages per second
        /// </summary>
        public double PagesPerSecond => Duration.TotalSeconds > 0
            ? Math.Round(PagesProcessed / Duration.TotalSeconds, 2)
            : 0;

        /// <summary>
        /// Gets crawl speed in bytes per second (throughput)
        /// </summary>
        public double BytesPerSecond => Duration.TotalSeconds > 0
            ? Math.Round(TotalBytesDownloaded / Duration.TotalSeconds, 2)
            : 0;

        /// <summary>
        /// Formats bytes as human-readable string
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Generates a formatted statistics report
        /// </summary>
        public string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("\n========================================================================");
            sb.AppendLine("                    CRAWL STATISTICS REPORT");
            sb.AppendLine("========================================================================\n");

            // Duration and Speed
            sb.AppendLine("[TIMING] TIMING & PERFORMANCE");
            sb.AppendLine($"   Duration:              {Duration:hh\\:mm\\:ss}");
            sb.AppendLine($"   Pages/Second:          {PagesPerSecond} pages/sec");
            sb.AppendLine($"   Throughput:            {FormatBytes((long)BytesPerSecond)}/sec\n");

            // Pages Summary
            sb.AppendLine("[PAGES] PAGES PROCESSED");
            sb.AppendLine($"   Total Processed:       {PagesProcessed}");
            sb.AppendLine($"   [OK] Successful:       {PagesSuccessful} ({SuccessRate}%)");
            sb.AppendLine($"   [FAIL] Failed:         {PagesFailed}\n");

            // Links Summary
            sb.AppendLine("[LINKS] LINKS ANALYSIS");
            sb.AppendLine($"   Total Discovered:      {LinksDiscovered}");
            sb.AppendLine($"   Queued for Crawling:   {LinksQueued}");
            sb.AppendLine($"   Skipped:               {LinksSkipped}\n");

            // Skip Breakdown
            sb.AppendLine("[SKIP] SKIP REASONS");
            sb.AppendLine($"   Already Visited:       {AlreadyVisited}");
            sb.AppendLine($"   Invalid Format:        {InvalidFormat}");
            sb.AppendLine($"   Different Host:        {DifferentHost}");
            sb.AppendLine($"   Robots.txt Blocked:    {DisallowedByRobotsTxt}\n");

            // Error Breakdown
            sb.AppendLine("[ERROR] ERRORS");
            sb.AppendLine($"   HTTP Errors:           {HttpErrors}");
            sb.AppendLine($"   Timeout Errors:        {TimeoutErrors}");
            sb.AppendLine($"   Other Errors:          {OtherErrors}");
            sb.AppendLine($"   Total Errors:          {HttpErrors + TimeoutErrors + OtherErrors}\n");

            // Data Transfer
            sb.AppendLine("[DATA] DATA TRANSFER");
            sb.AppendLine($"   Total Downloaded:      {FormatBytes(TotalBytesDownloaded)}");
            sb.AppendLine($"   Average Page Size:     {FormatBytes(AveragePageSizeBytes)}\n");

            sb.AppendLine("========================================================================");

            return sb.ToString();
        }
    }
}
