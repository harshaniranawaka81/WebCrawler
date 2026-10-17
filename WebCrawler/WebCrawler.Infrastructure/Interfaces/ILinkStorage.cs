using System;

namespace WebCrawler.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for persisting visited links to storage.
    /// </summary>
    public interface ILinkStorage
    {
        /// <summary>
        /// Records a visited link.
        /// </summary>
        /// <param name="url">The URL that was visited</param>
        Task RecordLinkAsync(string url);

        /// <summary>
        /// Finalizes the storage and writes any pending data.
        /// </summary>
        Task FinalizeAsync();

        /// <summary>
        /// Gets the path where links are being stored.
        /// </summary>
        string GetStoragePath();
    }
}
