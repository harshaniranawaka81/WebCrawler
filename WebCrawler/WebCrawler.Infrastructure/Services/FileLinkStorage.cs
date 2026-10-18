using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebCrawler.Infrastructure.Interfaces;

namespace WebCrawler.Infrastructure.Services
{
    /// <summary>
    /// Persists visited links to a text file.
    /// </summary>
    public class FileLinkStorage : ILinkStorage, IAsyncDisposable
    {
        private readonly string _filePath;
        private StreamWriter? _writer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ILogger<FileLinkStorage>? _logger;
        private bool _disposed = false;
        private int _linkCount = 0;

        public FileLinkStorage(string? outputPath = null, string? fileName = null, ILogger<FileLinkStorage>? logger = null)
        {
            _logger = logger;

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

            // Create the file and initialize the writer
            _writer = new StreamWriter(_filePath, append: false, encoding: System.Text.Encoding.UTF8, bufferSize: 4096)
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
            if (string.IsNullOrWhiteSpace(url) || _writer == null || _disposed)
                return;

            await _semaphore.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(url);
                _linkCount++;
                // Flush periodically to avoid losing data in case of crashes
                await _writer.FlushAsync();
                _logger?.LogDebug("Recorded link #{LinkCount}: {Url}", _linkCount, url);
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
            if (_disposed || _writer == null)
                return;

            await _semaphore.WaitAsync();
            try
            {
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
                await _writer.DisposeAsync();
                _disposed = true;
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
            if (!_disposed && _writer != null)
            {
                await _writer.DisposeAsync();
                _disposed = true;
            }

            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

