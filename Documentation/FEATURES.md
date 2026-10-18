# WebCrawler Features

WebCrawler is a .NET 10 console application that crawls a website breadth-first from a starting URL and reports what it finds. For how the code is organized, see [ARCHITECTURE.md](ARCHITECTURE.md). For the execution sequence, see [PROGRAM_FLOW.md](PROGRAM_FLOW.md).

## Core Features

### Crawling

- **Breadth-first crawl** from a user-supplied root URL.
- **Default URL.** If the user enters nothing, the crawl starts from `DefaultUrl` in `appsettings.json`.
- **Same-host scope.** Only links on the root's host are followed. Other hosts are counted as skipped.
- **Link normalization.** Duplicate pages are detected after normalization, so the same page is not crawled twice. Query parameters are included or ignored based on `NormalizeUrlsIncludeQueryParameters`.
- **Relative link resolution.** Links are resolved against the page they appear on.
- **Page limit.** `MaxPagesToCrawl` stops the crawl after a set number of pages. Leave it unset for no limit.
- **Failure tolerance.** A page that fails to load (HTTP error, timeout, or other error) is counted and the crawl continues.

### Robots.txt Compliance

- **Optional.** Controlled by `RespectRobotsTxt`.
- **Per-host rules.** `/robots.txt` is fetched once per host and cached for `RobotsCacheExpirationMinutes`.
- **Fetch timeout.** `RobotsFetchTimeoutSeconds` limits how long a robots.txt fetch can take.
- **Fail-open.** If robots.txt cannot be fetched, links are allowed.
- **Port-aware.** Hosts on non-default ports are handled correctly.

### Rate Limiting and Concurrency

- **Request delay.** `DelayBetweenRequestsMs` sets the minimum time between requests.
- **Concurrency limit.** `MaxConcurrentRequests` caps simultaneous requests.

### HTTP Resilience

- **Retries** for transient failures. Controlled by `HttpClientPolicies:RetryCount` and `RetryDelaySeconds`.
- **Circuit breaker.** Opens after `CircuitBreakerFailureThreshold` failures and stays open for `CircuitBreakerTimeoutSeconds`.
- **Timeouts.** `ConnectionTimeout` sets the per-request timeout.

## Output

### Console Output

- **Per-page results.** Each crawled page is printed with the links found on it.
- **Summary report.** After the crawl, statistics are printed, including:
  - Pages processed, successful, and failed
  - Links discovered, queued, and skipped
  - Skips by reason: invalid format, different host, already visited, disallowed by robots.txt
  - Errors by type: HTTP, timeout, other
  - Total bytes downloaded and the success rate
- **Paths.** The console prints the visited-links file location and the latest log file.

### Visited Links File

- **Written to** `VisitedPagesOutputFilePath` as `visited_links_<timestamp>.txt`. The configured path is treated as a directory and is created if it does not exist.
- **Buffered writes.** Links are flushed every `StorageFlushIntervalLinks` entries. `StorageWriterBufferSize` sets the buffer size.
- **Statistics appended.** The crawl statistics are written to the file when the crawl finishes.

### Logging

- **Console and file logs** through Serilog, configured in the `Serilog` section of `appsettings.json`.
- **Daily rolling log files** under `./logs`, with a retention limit of 30 files.
- **Log levels** are set per namespace. Microsoft and System logs default to `Warning`.

## Configuration

All behavior is set in `appsettings.json` and can be overridden with environment variables.

| Section | Purpose |
|---|---|
| `WebCrawlerSettings` | Crawl behavior, robots.txt, rate limits, and storage |
| `HttpClientPolicies` | Retry and circuit-breaker settings |
| `Serilog` | Log sinks, format, and levels |
| `Logging` | Microsoft logging defaults |

On startup, the application validates the settings and reports every error before exiting with code `1`.

## Error Handling and Exit Codes

| Exit code | Meaning |
|---|---|
| `0` | Crawl completed |
| `1` | Invalid configuration, invalid URL, unhandled error, or fatal startup error |

- Invalid URLs are rejected before the crawl starts.
- Page-level errors do not stop the crawl. They are counted in the report.
- Storage is always finalized, even if the crawl fails.

## User Interaction

- **Prompt.** The user enters a URL at the console, or presses Enter to use the default.
- **Status messages.** The application prints a banner, a start message, and a completion message.

## Limitations

- Only `<a href>` links are followed. Other resource types and JavaScript-generated links are not.
- Robots.txt is checked against the links the crawler finds, not against the pages it fetches.
- The crawl is single-run and console-based. There is no resume capability.
- Output is written to the console and files only. There is no web or API interface.
