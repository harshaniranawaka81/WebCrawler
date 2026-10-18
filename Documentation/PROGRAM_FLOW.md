# WebCrawler Program Flow

This document describes how the application runs from startup to exit. For the component structure, see [ARCHITECTURE.md](ARCHITECTURE.md).

## 1. Startup (`WebCrawler.Console/Program.cs`)

1. **Build configuration.** `BuildConfiguration` loads `appsettings.json` from the current directory, then overlays environment variables.
2. **Configure logging.** `ConfigureLogging` creates a Serilog logger from the `Serilog` section (console and rolling file sinks under `./logs`).
3. **Build the service provider.** `ConfigureServices`:
   - Adds logging through Serilog.
   - Binds `WebCrawlerSettings` and `HttpClientPolicies` from configuration.
   - Calls `AddCrawlerInfrastructure` to register storage, rate limiting, HTTP fetching, link extraction, and robots.txt checking.
   - Calls `AddCrawlerCore` to register output, exception handling, the application orchestrator, the URL filter chain, and `ICrawler`.
2. **Run.** `Program` resolves `IApplicationOrchestrator`, creates a `CancellationTokenSource` cancelled by Ctrl+C (`Console.CancelKeyPress`, with the default termination suppressed), and calls `RunAsync(cts.Token)`.
The process returns

## 2. Orchestration (`ApplicationOrchestrator.RunAsync`)

1. **Validate configuration.** `ConfigurationValidator.Validate` checks the settings. A `ConfigurationException` displays an error and returns `1`.
2. **Show welcome.** `IApplicationInteraction.DisplayWelcome` prints the banner.
3. **Read the URL.** `GetUrlAsync` prompts the user. An empty entry uses `DefaultUrl` from settings.
4. **Validate the URL.** If `Uri.TryCreate` fails, the application shows an error and returns `1`.
5. **Crawl.** The orchestrator runs the crawl inside `IExceptionHandler.ExecuteAsync("Web Crawling")`, which
wraps `ICrawler.CrawlAsync(rootUri, cancellationToken)`.
6. **Show results.** `DisplayResults` prints the statistics report, the visited-links file path, and the latest log file path. If the user cancelled with Ctrl+C, the partial statistics are shown, then "Crawl cancelled" is displayed and the method returns `130`.
7. **Complete.** `DisplayCompletion` logs success and the method returns `0`.

Error handling:

- `ConfigurationException` displays a configuration error and returns `1`.
- Ctrl+C: the crawler stops its workers, finalizes storage with statistics, and returns partial statistics instead of throwing.
- `WebCrawlerException` and other exceptions are reported through `IExceptionHandler` and return `1`.
- The `finally` block always calls `ILinkStorage.FinalizeAsync()`. Finalization errors are logged and do not change the return value.

## 3. Crawling (`Crawler.CrawlAsync`)

The crawl uses an unbounded `Channel<Uri>` and `MaxConcurrentRequests` worker tasks (minimum 1). A pending counter closes the channel when queued and in-flight work reaches zero. Statistics, visited/max-page admission, and output/storage writes are synchronized, so output order is nondeterministic.

1. Create `CrawlStatistics` with a start time, write the root URL to the channel, and start the workers.
2. Each worker reads URIs until the channel completes:
   1. **Check the page limit.** If `MaxPagesToCrawl` is set and the visited count has reached it, stop the loop.
   2. **Check for duplicates.** `UrlHelper.Normalize` produces a key for `_visited`. If the key already exists, increment `AlreadyVisited` and `LinksSkipped` and continue.
   3. **Fetch the page.** Increment `PagesProcessed`, then call `IPageFetcher.GetContentAsync`. The fetcher applies `IRateLimiter` and the HTTP resilience policies. On success, increment `PagesSuccessful` and add the body length to `TotalBytesDownloaded`.
   4. **Process each link.** `ILinkExtractor.ExtractLinks` returns the `href` values. For each link:
	  - Resolve it against the current page. Invalid URIs increment `InvalidFormat` and `LinksSkipped`.
	  - Run `IUrlFilterChain.FilterAsync(link, root)`. `SameHostFilter` rejections increment `DifferentHost`; `RobotsTextFilter` rejections (only registered when `RespectRobotsTxt` is enabled) increment `DisallowedByRobotsTxt`. Both also increment `LinksSkipped`.
	  - Otherwise, add the link to `discoveredLinks`, increment `LinksDiscovered` and `LinksQueued`, and write it to the channel.
   5. **Write output.** `IOutputWriter.Write` prints the page and its discovered links.
   6. **Record the page.** `ILinkStorage.RecordLinkAsync` writes the page URL to the visited-links file.
   The worker continues with the next URL.
3. **Clean up in `finally`.** `_visited` is cleared after all workers finish.
4. **Finalize storage.** `ILinkStorage.FinalizeAsync(stats)` appends the statistics and flushes the file.
5. **Finish.** Set `EndTime` and return the statistics.

## 4. Storage and Output

- **`FileLinkStorage`** creates the configured output directory and writes `visited_links_<timestamp>.txt` in it. Writes are buffered and flushed every `StorageFlushIntervalLinks` links. The file is a singleton and must be disposed or finalized to flush the final content.
- **`ConsoleOutputWriter`** prints each page with its discovered links.

## 5. Robots.txt and Rate Limiting

- **`RobotsChecker`** fetches `/robots.txt` for each host through `IPageFetcher`, parses it with `RobotsParser`, and caches the rules per host for `RobotsCacheExpirationMinutes`. The robots URL is built from the page URI, which keeps any non-default port. If the fetch fails, links are allowed.
enforces `DelayBetweenRequestsMs` between requests (thread-safe, reserving spaced time slots for concurrent callers) and limits

## 6. Configuration Reference

| Setting | Used by | Effect |
|---|---|---|
| `DefaultUrl` | Orchestrator | URL used when the user enters nothing |
| `MaxPagesToCrawl` | Crawler | Stops the crawl after this many pages |
| `RespectRobotsTxt` | DI chain setup | Adds `RobotsTextFilter` to the filter chain |
| `DelayBetweenRequestsMs` | Rate limiter | Minimum delay between requests |
| `MaxConcurrentRequests` | Crawler, rate limiter | Number of workers and concurrent request slots |
| `RobotsCacheExpirationMinutes` | RobotsChecker | How long robots rules are cached |
| `RobotsFetchTimeoutSeconds` | RobotsChecker | Timeout for fetching robots.txt |
| `VisitedPagesOutputFilePath` | FileLinkStorage | Output directory for visited links |
| `StorageFlushIntervalLinks` | FileLinkStorage | Links written between flushes |
| `StorageWriterBufferSize` | FileLinkStorage | Writer buffer size |
| `HttpClientPolicies:*` | HTTP policies | Retry and circuit-breaker behavior |

## 7. Sequence Diagram

```mermaid
sequenceDiagram
	participant P as Program
	participant O as ApplicationOrchestrator
	participant I as ConsoleApplicationInteraction
	participant C as Crawler
	participant F as IPageFetcher
	participant L as ILinkExtractor
	participant R as IUrlFilterChain
	participant S as ILinkStorage

	P->>O: RunAsync()
	O->>O: ValidateConfiguration()
	O->>I: DisplayWelcome(), GetUrlAsync()
	I-->>O: url
	O->>I: DisplayCrawlStart(url)
	O->>C: CrawlAsync(rootUri)
	par Each worker (MaxConcurrentRequests)
		C->>F: GetContentAsync(url)
		F-->>C: html
		C->>L: ExtractLinks(html)
		L-->>C: hrefs
		loop Each link
			C->>R: FilterAsync(link, root)
			R-->>C: passed / rejection reason
		end
		C->>S: RecordLinkAsync(url)
	end
	C->>S: FinalizeAsync(stats)
	C-->>O: CrawlStatistics
	O->>I: DisplayResults(stats), DisplayCompletion()
	O->>S: FinalizeAsync() (in finally)
	O-->>P: exit code
```
