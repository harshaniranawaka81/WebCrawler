namespace WebCrawler.Core.Interfaces;

/// <summary>
/// Contract for orchestrating the entire web crawling application workflow.
/// Coordinates configuration, UI interaction, crawler execution, and result handling.
/// </summary>
public interface IApplicationOrchestrator
{
    /// <summary>
    /// Runs the entire application workflow asynchronously.
    /// </summary>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
