namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for persisting visited links to storage.
/// </summary>
public interface ILinkStorage
{
    /// <summary>
    /// Records a visited link.
    /// </summary>
    /// <param name="url">The URL that was visited.</param>
    Task RecordLinkAsync(string url);

    /// <summary>
    /// Finalizes the storage and writes any pending data, optionally including crawl statistics.
    /// </summary>
    /// <param name="statistics">Optional crawl statistics to append to the file.</param>
    Task FinalizeAsync(object? statistics = null);

    /// <summary>
    /// Gets the path where links are being stored.
    /// </summary>
    string GetStoragePath();
}
