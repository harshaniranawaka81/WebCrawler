using Microsoft.Extensions.Logging;
using WebCrawler.Configuration;
using WebCrawler.Core.Exceptions;

namespace WebCrawler.Core.Services;

/// <summary>
/// Validates application configuration at startup.
/// Throws ConfigurationException if any required settings are invalid.
/// </summary>
public class ConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator>? _logger;

    public ConfigurationValidator(ILogger<ConfigurationValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates all application settings.
    /// </summary>
    /// <param name="settings">The settings to validate.</param>
    /// <throws>ConfigurationException if validation fails.</throws>
    public void Validate(WebCrawlerSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException("WebCrawlerSettings cannot be null");

        var errors = new List<string>();

        // Validate DefaultUrl
        if (string.IsNullOrWhiteSpace(settings.DefaultUrl))
        {
            errors.Add("DefaultUrl is required and cannot be empty");
        }
        else if (!Uri.TryCreate(settings.DefaultUrl, UriKind.Absolute, out _))
        {
            errors.Add($"DefaultUrl '{settings.DefaultUrl}' is not a valid absolute URI");
        }

        // Validate ConnectionTimeout
        if (settings.ConnectionTimeout <= 0)
        {
            errors.Add($"ConnectionTimeout must be positive, got {settings.ConnectionTimeout}");
        }

        // Validate MaxPagesToCrawl
        if (settings.MaxPagesToCrawl.HasValue && settings.MaxPagesToCrawl.Value < 1)
        {
            errors.Add($"MaxPagesToCrawl must be >= 1, got {settings.MaxPagesToCrawl.Value}");
        }

        // Validate VisitedPagesOutputFilePath
        if (string.IsNullOrWhiteSpace(settings.VisitedPagesOutputFilePath))
        {
            errors.Add("VisitedPagesOutputFilePath is required and cannot be empty");
        }
        else
        {
            try
            {
                var path = Path.GetFullPath(settings.VisitedPagesOutputFilePath);
                // Check if parent directory exists or can be created
                var parentDir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir))
                {
                    // Try to infer if it's a valid path by checking if we can create the directory
                    // Don't actually create it here, just validate
                    if (!IsValidDirectoryPath(path))
                    {
                        errors.Add($"VisitedPagesOutputFilePath '{settings.VisitedPagesOutputFilePath}' is not a valid directory path");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"VisitedPagesOutputFilePath '{settings.VisitedPagesOutputFilePath}' caused an error: {ex.Message}");
            }
        }

        // Validate DelayBetweenRequestsMs
        if (settings.DelayBetweenRequestsMs < 0)
        {
            errors.Add($"DelayBetweenRequestsMs cannot be negative, got {settings.DelayBetweenRequestsMs}");
        }

        // Validate MaxConcurrentRequests
        if (settings.MaxConcurrentRequests.HasValue && settings.MaxConcurrentRequests.Value < 1)
        {
            errors.Add($"MaxConcurrentRequests must be >= 1, got {settings.MaxConcurrentRequests.Value}");
        }

        // Validate Priority-2 settings
        if (settings.RobotsCacheExpirationMinutes < 1)
        {
            errors.Add($"RobotsCacheExpirationMinutes must be >= 1, got {settings.RobotsCacheExpirationMinutes}");
        }

        if (settings.RobotsFetchTimeoutSeconds < 1)
        {
            errors.Add($"RobotsFetchTimeoutSeconds must be >= 1, got {settings.RobotsFetchTimeoutSeconds}");
        }

        if (settings.StorageFlushIntervalLinks < 1)
        {
            errors.Add($"StorageFlushIntervalLinks must be >= 1, got {settings.StorageFlushIntervalLinks}");
        }

        if (settings.StorageWriterBufferSize < 256) // Reasonable minimum
        {
            errors.Add($"StorageWriterBufferSize must be >= 256, got {settings.StorageWriterBufferSize}");
        }

        if (errors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:\r\n" + string.Join("\r\n", errors.Select(e => $"  - {e}"));
            _logger?.LogError(errorMessage);
            throw new ConfigurationException(errorMessage);
        }

        _logger?.LogInformation("Configuration validation successful");
    }

    private static bool IsValidDirectoryPath(string path)
    {
        try
        {
            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
                return false;

            // Try to get the directory name - if it works, it's likely valid
            var dir = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(dir);
        }
        catch
        {
            return false;
        }
    }
}
