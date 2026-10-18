using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Configuration;
using WebCrawler.Core.Exceptions;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Services;

namespace WebCrawler.Application.Services;

/// <summary>
/// Orchestrates the main application flow:
/// 1. Validates configuration
/// 2. Presents UI for URL input via IApplicationInteraction
/// 3. Performs crawl via ICrawler
/// 4. Displays results via IApplicationInteraction
///
/// Separates orchestration logic from presentation and core services.
/// </summary>
public class ApplicationOrchestrator : IApplicationOrchestrator
{
    private readonly IApplicationInteraction _interaction;
    private readonly IExceptionHandler _exceptionHandler;
    private readonly ICrawler _crawler;
    private readonly ILinkStorage _storage;
    private readonly IOptions<WebCrawlerSettings> _settings;
    private readonly ConfigurationValidator _configValidator;
    private readonly ILogger<ApplicationOrchestrator> _logger;

    public ApplicationOrchestrator(
        IApplicationInteraction interaction,
        IExceptionHandler exceptionHandler,
        ICrawler crawler,
        ILinkStorage storage,
        IOptions<WebCrawlerSettings> settings,
        ILogger<ApplicationOrchestrator> logger)
    {
        _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configValidator = new ConfigurationValidator(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                .CreateLogger<ConfigurationValidator>());
    }

    /// <summary>
    /// Runs the complete application flow.
    /// </summary>
    public async Task<int> RunAsync()
    {
        try
        {
            // Step 1: Validate configuration
            ValidateConfiguration();

            // Step 2: Display welcome and get URL from user
            _interaction.DisplayWelcome();
            var urlInput = await _interaction.GetUrlAsync(_settings.Value.DefaultUrl);

            // Step 3: Validate URL format
            if (!Uri.TryCreate(urlInput, UriKind.Absolute, out var rootUri))
            {
                _interaction.DisplayInvalidUrlError(urlInput);
                _logger.LogWarning("Invalid URL provided by user: {Url}", urlInput);
                return 1;
            }

            // Step 4: Display crawl start
            _interaction.DisplayCrawlStart(urlInput);

            // Step 5: Execute crawl
            var stats = await _exceptionHandler.ExecuteAsync(
                async () => await _crawler.CrawlAsync(rootUri),
                "Web Crawling");

            // Step 6: Display results
            _interaction.DisplayResults(stats, _storage);

            // Step 7: Display completion
            _interaction.DisplayCompletion();

            return 0;
        }
        catch (ConfigurationException ex)
        {
            _logger.LogCritical(ex, "Configuration error");
            _interaction.DisplayError($"Configuration Error: {ex.Message}");
            return 1;
        }
        catch (WebCrawlerException ex)
        {
            _logger.LogError(ex, "Web crawler error");
            _exceptionHandler.Handle(ex);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during application execution");
            _exceptionHandler.Handle(ex);
            return 1;
        }
        finally
        {
            // Ensure storage is finalized
            try
            {
                await _storage.FinalizeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing storage");
            }
        }
    }

    private void ValidateConfiguration()
    {
        try
        {
            _configValidator.Validate(_settings.Value);
            _logger.LogInformation("Application configuration validated successfully");
        }
        catch (ConfigurationException ex)
        {
            _logger.LogCritical(ex, "Configuration validation failed");
            throw;
        }
    }
}
