using WebCrawler.Core.Models;

namespace WebCrawler.Tests.Core.Models;

[TestFixture]
public class CrawlStatisticsTests
{
    [Test]
    public void SuccessRate_IsZero_WhenNoPagesProcessed()
    {
        var stats = new CrawlStatistics();

        Assert.That(stats.SuccessRate, Is.EqualTo(0));
    }

    [Test]
    public void SuccessRate_IsPercentageOfSuccessfulPages()
    {
        var stats = new CrawlStatistics { PagesProcessed = 4, PagesSuccessful = 3 };

        Assert.That(stats.SuccessRate, Is.EqualTo(75));
    }

    [Test]
    public void AveragePageSizeBytes_IsZero_WhenNoSuccessfulPages()
    {
        var stats = new CrawlStatistics { TotalBytesDownloaded = 1000 };

        Assert.That(stats.AveragePageSizeBytes, Is.EqualTo(0));
    }

    [Test]
    public void AveragePageSizeBytes_DividesBytesBySuccessfulPages()
    {
        var stats = new CrawlStatistics { PagesSuccessful = 4, TotalBytesDownloaded = 4000 };

        Assert.That(stats.AveragePageSizeBytes, Is.EqualTo(1000));
    }

    [Test]
    public void Duration_IsEndMinusStart()
    {
        var start = new DateTime(2024, 1, 1, 10, 0, 0);
        var stats = new CrawlStatistics { StartTime = start, EndTime = start.AddSeconds(30) };

        Assert.That(stats.Duration, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public void PagesPerSecond_IsZero_WhenDurationIsZero()
    {
        var stats = new CrawlStatistics { PagesProcessed = 10 };

        Assert.That(stats.PagesPerSecond, Is.EqualTo(0));
    }

    [Test]
    public void PagesPerSecond_IsPagesOverDuration()
    {
        var start = new DateTime(2024, 1, 1);
        var stats = new CrawlStatistics { PagesProcessed = 10, StartTime = start, EndTime = start.AddSeconds(5) };

        Assert.That(stats.PagesPerSecond, Is.EqualTo(2));
    }

    [Test]
    public void BytesPerSecond_IsBytesOverDuration()
    {
        var start = new DateTime(2024, 1, 1);
        var stats = new CrawlStatistics { TotalBytesDownloaded = 500, StartTime = start, EndTime = start.AddSeconds(5) };

        Assert.That(stats.BytesPerSecond, Is.EqualTo(100));
    }

    [TestCase(512L, "512 B")]
    [TestCase(1024L, "1 KB")]
    [TestCase(1536L, "1.5 KB")]
    [TestCase(1048576L, "1 MB")]
    public void FormatBytes_FormatsHumanReadable(long bytes, string expected)
    {
        Assert.That(CrawlStatistics.FormatBytes(bytes), Is.EqualTo(expected));
    }

    [Test]
    public void GenerateReport_IncludesTotalErrors()
    {
        var stats = new CrawlStatistics { HttpErrors = 2, TimeoutErrors = 1, OtherErrors = 3 };

        var report = stats.GenerateReport();

        Assert.That(report, Does.Contain("Total Errors:          6"));
    }
}
