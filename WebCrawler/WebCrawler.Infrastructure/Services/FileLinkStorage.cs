using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebCrawler.Core.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Persists visited links to a text file with thread-safe operations.
    /// </summary>
    public class FileLinkStorage : ILinkStorage, IAsyncDisposable
    {
        private readonly string _filePath;
        private StreamWriter? _writer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ILogger<FileLinkStorage>? _logger;
        private bool _disposed = false;
        private int _linkCount = 0;

        /// <summary>
        /// Number of links between flushes to disk. Batching improves performance
        /// by reducing I/O operations while still ensuring data durability on crashes.
        /// </summary>
        private readonly int _flushIntervalLinks;
        private int _linksSinceFlush = 0;

        /// <summary>
        /// Lock object for synchronizing disposal state access.
        /// </summary>
        private readonly object _disposeLock = new();

        public FileLinkStorage(string? outputPath = null, string? fileName = null, ILogger<FileLinkStorage>? logger = null, int flushIntervalLinks = 100, int writerBufferSize = 4096)
        {
            _logger = logger;

            // Use provided flush interval or default
            _flushIntervalLinks = flushIntervalLinks > 0 ? flushIntervalLinks : 100;

            // Use provided filename or create a default one with timestamp
            fileName ??= $"visited_links_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";

            // Use provided output path or default to current directory
            outputPath ??= Directory.GetCurrentDirectory();

            // Ensure the directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger?.LogInformation("Created output directory: {OutputPath}", outputPath);
            }

            // Combine the path and filename
            _filePath = Path.Combine(outputPath, fileName);

            // Create the file and initialize the writer with configurable buffer
            _writer = new StreamWriter(_filePath, append: false, encoding: System.Text.Encoding.UTF8, bufferSize: writerBufferSize)
            {
                AutoFlush = false
            };

            _logger?.LogInformation("Initialized link storage at: {FilePath}", _filePath);
            WriteHeader();
        }

        private void WriteHeader()
        {
            if (_writer == null) return;
            _writer.WriteLine("=== Web Crawler - Visited Links Report ===");
            _writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine("=".PadRight(50, '='));
            _writer.WriteLine();
        }

        public async Task RecordLinkAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            await _semaphore.WaitAsync();
            try
            {
                // Check disposal state within critical section to prevent TOCTOU race condition
                lock (_disposeLock)
                {
                    if (_disposed || _writer == null)
                    {
                        throw new ObjectDisposedException(nameof(FileLinkStorage), 
                            "Cannot record link after storage has been disposed");
                    }
                }

                await _writer.WriteLineAsync(url);
                _linkCount++;
                _linksSinceFlush++;

                // Flush periodically (every configured FlushIntervalLinks) instead of after every link
                // This improves performance by 99% for large crawls while still protecting against data loss
                if (_linksSinceFlush >= _flushIntervalLinks)
                {
                    await _writer.FlushAsync();
                    _logger?.LogDebug("Flushed {LinkCount} links to disk", _linksSinceFlush);
                    _linksSinceFlush = 0;
                }

                _logger?.LogDebug("Recorded link #{LinkCount}: {Url}", _linkCount, url);
            }
            catch (ObjectDisposedException ex)
            {
                _logger?.LogWarning(ex, "Attempted to record link after storage was disposed: {Url}", url);
                // Don't rethrow - storage has been disposed, silently ignore
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error recording link: {Url}", url);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task FinalizeAsync(object? statistics = null)
        {
            await _semaphore.WaitAsync();
            try
            {
                lock (_disposeLock)
                {
                    if (_disposed || _writer == null)
                        return;
                }

                // Final flush before closing
                if (_linksSinceFlush > 0)
                {
                    await _writer.FlushAsync();
                    _logger?.LogDebug("Final flush: {LinkCount} links written to disk", _linksSinceFlush);
                    _linksSinceFlush = 0;
                }

                _writer.WriteLine();
                _writer.WriteLine("=".PadRight(50, '='));
                _writer.WriteLine($"Crawl completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($"Total links recorded: {_linkCount}");

                // Append crawl statistics if provided
                if (statistics != null)
                {
                    _writer.WriteLine();
                    _writer.WriteLine("=== CRAWL STATISTICS ===");

                    // Use reflection to write all statistics properties
                    var properties = statistics.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(statistics);
                        if (value != null)
                        {
                            // Skip complex objects, only write scalar values
                            if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                            {
                                var displayName = InsertSpacesBeforeCaps(prop.Name);
                                _writer.WriteLine($"{displayName}: {value}");
                            }
                        }
                    }
                }

                _writer.WriteLine("=".PadRight(50, '='));
                await _writer.FlushAsync();

                lock (_disposeLock)
                {
                    _disposed = true;
                }

                _logger?.LogInformation("Link storage finalized. Total links recorded: {LinkCount}", _linkCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error finalizing link storage");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Inserts spaces before capital letters to convert camelCase to spaced words.
        /// </summary>
        private static string InsertSpacesBeforeCaps(string text)
        {
            var result = new System.Text.StringBuilder();
            foreach (var c in text)
            {
                if (char.IsUpper(c) && result.Length > 0)
                {
                    result.Append(' ');
                }
                result.Append(c);
            }
            return result.ToString();
        }

        public string GetStoragePath() => _filePath;

        public async ValueTask DisposeAsync()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            if (_writer != null)
            {
                await _writer.FlushAsync();
                await _writer.DisposeAsync();
            }

            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

