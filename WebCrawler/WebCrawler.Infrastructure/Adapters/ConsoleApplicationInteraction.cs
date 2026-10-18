using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;

namespace WebCrawler.Infrastructure.Adapters;

/// <summary>
/// Console-based implementation of application interaction.
/// Handles all console input/output and logging in a single cohesive layer.
/// This is a presentation adapter that implements the core layer contract.
/// </summary>
public class ConsoleApplicationInteraction : IApplicationInteraction
{
    private readonly ILogger<ConsoleApplicationInteraction>? _logger;

    public ConsoleApplicationInteraction(ILogger<ConsoleApplicationInteraction>? logger = null)
    {
        _logger = logger;
    }

    public void DisplayWelcome()
    {
        Console.WriteLine("[SPIDER] Web Crawler Application");
        Console.WriteLine("====================================");
        Log.Information("Web Crawler application started");
    }

    public async Task<string> GetUrlAsync(string defaultUrl)
    {
        Console.WriteLine("Enter the URL of the website you want to crawl: ");
        string? url = Console.ReadLine();

        if (string.IsNullOrEmpty(url))
        {
            url = defaultUrl;
            Console.WriteLine($"[DEFAULT] Using default URL: {url}");
        }

        // Simulate async if needed (e.g., for GUI dialogs)
        return await Task.FromResult(url);
    }

    public void DisplayInvalidUrlError(string url)
    {
        Console.WriteLine("[ERROR] Invalid URL format.");
        _logger?.LogWarning("User provided invalid URL: {Url}", url);
    }

    public void DisplayCrawlStart(string url)
    {
        Console.WriteLine($"\n[START] Starting crawl from: {url}\n");
        Log.Information("Starting crawl from: {Url}", url);
    }

    public void DisplayResults(CrawlStatistics? statistics, ILinkStorage storage)
    {
        if (statistics != null)
        {
            Console.WriteLine(statistics.GenerateReport());
        }

        DisplayVisitedLinksPath(storage);
        DisplayLogFilePath();
    }

    private void DisplayVisitedLinksPath(ILinkStorage storage)
    {
        Console.WriteLine($"[SUCCESS] Visited links saved to: {storage.GetStoragePath()}\n");
    }

    private void DisplayLogFilePath()
    {
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logPath))
            return;

        var latestLog = Directory.GetFiles(logPath, "*.txt")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(latestLog))
        {
            Console.WriteLine($"[LOGS] Crawl logs saved to: {latestLog}\n");
        }
    }

    public void DisplayCompletion()
    {
        Log.Information("Application completed successfully");
    }

    public void DisplayError(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
        Log.Error("Application error: {Message}", message);
    }
}
