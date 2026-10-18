# WebCrawler Design Review: Architecture, Patterns, Trade-offs, Limitations, and Improvements

This document reviews the design of the solution. For the component overview see [ARCHITECTURE.md](ARCHITECTURE.md). For runtime behavior see [PROGRAM_FLOW.md](PROGRAM_FLOW.md). For user-facing capabilities see [FEATURES.md](FEATURES.md).

## 1. Architecture Summary

The solution is a layered .NET 10 console application:

| Layer | Project | Role |
|---|---|---|
| Domain | `WebCrawler.Core` | Interfaces, models, settings, exceptions, filters, `Crawler` |
| Application | `WebCrawler.Application` | `ApplicationOrchestrator` (run lifecycle) |
| Infrastructure | `WebCrawler.Infrastructure` | HTTP, parsing, robots.txt, rate limiting, file storage |
| Composition root | `WebCrawler.Console` | `Program`, console adapters, DI extensions |

Dependencies point inward. Core has no project references. Application and Infrastructure depend on Core. Console wires everything together.

## 2. Design Patterns and Techniques in Use

| Pattern / technique | Where | Purpose |
|---|---|---|
| Layered / clean architecture | Project structure | Keeps domain logic independent of I/O and UI |
| Dependency injection | `CrawlerCoreServiceExtensions`, `CrawlerInfrastructureServiceExtensions` | Composition at the root, testable constructors |
| Ports and adapters | `IPageFetcher`, `ILinkExtractor`, `ILinkStorage`, `IOutputWriter`, `IApplicationInteraction` and their implementations | Core defines contracts. Infrastructure and Console supply adapters |
| Options pattern | `IOptions<WebCrawlerSettings>`, `IOptions<HttpClientPolicies>` | Strongly typed configuration |
| Orchestrator / application service | `ApplicationOrchestrator` | Separates run flow from presentation and crawl logic |
| Chain of responsibility | `UrlFilterChain` with `IUrlFilter` implementations | Composable URL filtering with a fluent `AddFilter` |
| Composite | `CompositeRateLimiter` (can `Add` further `IRateLimiter`s) | Combines delay, concurrency, and custom limiters |
| Factory | `HttpPolicyFactory` | Builds retry and circuit-breaker policies |
| Resilience policies (Polly) | `HttpPolicyFactory` | Exponential-backoff retry on 5xx, `HttpRequestException`, and timeouts, plus a circuit breaker |
| Typed `HttpClient` / `IHttpClientFactory` | `RegisterHttpClient` | Managed handler lifetime and policy attachment |
| Caching with expiry | `RobotsChecker` | Per-host robots rules, with a separate cache for hosts without robots.txt |
| Semaphore throttling | `CompositeRateLimiter` | Concurrency cap with `SemaphoreSlim` |
| Custom exception hierarchy | `WebCrawlerException` and subclasses | Typed, layered error handling |
| Global exception handling | `IExceptionHandler.ExecuteAsync` and `Handle` | One place for error reporting |
| Fail-open defaults | `RobotsChecker`, `UrlFilterChain` | A failing helper does not abort the crawl |
| Structured logging | Serilog with Microsoft logging abstractions | Console and rolling-file sinks |
| Test strategy | Unit tests plus local-Kestrel integration tests | Fast isolation tests and offline end-to-end tests |

## 3. Trade-offs

| Decision | Benefit | Cost |
|---|---|---|
| Layered architecture with many small interfaces | Testability and replaceable parts | More files and indirection for a small application |
| Channel-based worker pool in `Crawler` | Real concurrency up to `MaxConcurrentRequests` | Synchronization needed for statistics, visited set, and output. Output order is nondeterministic |
| `Crawler` uses `IUrlFilterChain` for same-host and robots checks | One mechanism, extensible filters | Crawler maps rejection names to statistics, which couples it to `UrlFilterNames` |
| Fail-open on robots.txt errors | Crawl is robust | A transient robots failure can cause the crawler to fetch pages the site disallows |
| Polly retry plus circuit breaker for all requests | Resilience to transient errors | One breaker covers every host, so failures on one path can block unrelated requests |
| Rate limiter applied inside `HttpPageFetcher` | All requests, including robots.txt, are throttled | Throttling is coupled to the fetcher, so the crawler cannot apply per-host policies |
| Visited-set held in memory | Fast duplicate checks | Memory grows with the number of pages. The crawl cannot resume |
| `FileLinkStorage` as a singleton holding an open writer | Efficient buffered writes | Needs explicit finalization or disposal. The file is created at construction, even if no crawl runs |
| Settings validated by hand in `ConfigurationValidator` | Clear error messages, all errors reported at once | More code than options validation attributes |
| Console as a thin adapter | UI can be replaced | Orchestrator is still console-oriented in its interaction flow (prompt, display methods) |
| Integration tests against a local server | Deterministic and offline | Does not cover real-world HTML, redirects, or TLS behavior |

## 4. Limitations

These come from reading the current code.

### Crawling

- **Global request delay.** The rate limiter's delay is global, not per host, so `DelayBetweenRequestsMs` also serializes the start of concurrent requests.
- **Only `<a href>` links are followed.** No scripts, forms, frames, sitemaps, or JavaScript-rendered content.
- **Same host only.** `IsSameHost` compares hosts and ignores port, and subdomains are treated as different hosts.
- **No content-type check.** The crawler downloads every linked resource as a string, including binaries, and parses it as HTML.
- **No redirect, canonical-URL, or `<base>` handling** beyond what `HttpClient` does by default.
- **`MaxPagesToCrawl` counts visited URLs, not successful pages.** Failed pages use up the budget.
Storage is finalized with statistics by the crawler, which returns partial statistics on cancellation, and the orchestrator displays them.
- **No resume or persistence of the queue.**

### Robots.txt

- **Cache is keyed by host only.** Different ports on the same host share rules.
- **Only the `*` user agent is parsed.** There is no `Crawl-delay` or `Sitemap` support.
- **The configured crawl delay is not tied to robots.txt.**
- **Failures allow the URL.** The check is fail-open.
- **Disallowed links are checked at discovery.** The root URL itself is never checked.

### HTTP and resilience

- **One circuit breaker for all requests.** The breaker treats any non-success status, including 404, as a failure, so a run of dead links can open the circuit and block later requests.
- **Retry covers 5xx and timeouts only.** 429 (Too Many Requests) is not retried and does not honor `Retry-After`.
- **Fixed User-Agent string** that imitates a browser rather than identifying the crawler.
- **`HttpClient.Timeout` is hard-coded to 30 seconds** in the DI extension, separate from the `ConnectionTimeout` setting.
- **`IPageFetcher` has two registrations** (typed client and a scoped factory lambda). The second wins, which makes the first redundant.

### Rate limiting

- **The delay serializes request starts.** `CompositeRateLimiter` reserves start times under a lock, so concurrent workers are spaced by `DelayBetweenRequestsMs`, which limits the benefit of extra workers.
- **Delay is global,** not per host.

### Storage and output

- **Output is written to console and a text file only.** There is no structured format such as JSON or CSV.
- **The visited file contains URLs and a statistics block,** not link relationships or page metadata.
- **Console output is synchronous** and not abstracted beyond `IOutputWriter`.

### Code structure

- **`IRobotsChecker.GetRulesAsync` returns `object?`,** which loses type safety.
- **`Crawler` accepts optional dependencies** (storage, exception handler, robots checker) that are always supplied in practice. This adds null-checks and hides missing registrations.
- **`ApplicationOrchestrator` builds its `ConfigurationValidator` internally** with a null logger. The validator is not injectable.
- **Mixed logging approaches:** static `Log` calls in console adapters and `ILogger<T>` elsewhere.
- **Inconsistent code style** (block-scoped and file-scoped namespaces, mixed indentation).

## 5. Possible Improvements

### High value

1. **Done: concurrent crawling.** Implemented with `Channel<Uri>` workers and a thread-safe `CompositeRateLimiter`. Consider a per-host delay as a follow-up.
2. **Done: `CancellationToken` support** from `Program` through the orchestrator, crawler, fetcher, rate limiter, and robots checker, hooked to Ctrl+C.
3. **Done: one filtering mechanism.** `Crawler` calls `IUrlFilterChain` (`SameHostFilter`, plus `RobotsTextFilter` when enabled) and reports rejection reasons in statistics.
4. **Check content type before parsing.** Skip non-HTML responses, and limit response size.
5. **Tighten the circuit breaker.** Count only 5xx, timeouts, and network errors, not 4xx. Use a breaker per host (for example through a keyed policy registry).
6. **Honor 429 and `Retry-After`,** and add jitter to the retry backoff.

### Medium value

7. **Make robots handling more complete.** Key the cache by scheme, host, and port. Parse the crawler's own user agent, `Crawl-delay`, and `Sitemap`. Make fail-open versus fail-closed a setting.
8. **Check the root URL against robots.txt** as well.
9. **Return `RobotRules` from a typed interface method** in place of `object?`.
10. **Count only successful pages toward `MaxPagesToCrawl`,** or make the choice a setting.
11. **Add configurable limits:** maximum depth, maximum response size, and an allowlist or denylist of URL patterns.
12. **Switch to `IOptions` validation** (`ValidateOnStart` with `IValidateOptions<T>`) so invalid settings fail at host startup, and inject the validator.
13. **Use a descriptive User-Agent** that names the crawler and includes a contact URL, and move it to settings.
14. **Wire `ConnectionTimeout` to the `HttpClient`** instead of the hard-coded 30 seconds.
15. **Remove the duplicate `IPageFetcher` registration.**

### Lower value or longer term

16. **Add structured output** (JSON or CSV) behind `ILinkStorage` or a new exporter interface.
17. **Support pause and resume** by persisting the queue and visited set.
18. **Add metrics and tracing** through `System.Diagnostics.Metrics` and OpenTelemetry.
19. **Add a generic host** (`Host.CreateApplicationBuilder`) in place of the manual `ServiceCollection`, for lifetime management, configuration, and logging setup.
20. **Make the `Crawler` dependencies required** and register `ILinkStorage` and `IRobotsChecker` explicitly, removing the null checks.
21. **Standardize style** with an `.editorconfig`, and enable analyzers and warnings as errors in CI.
22. **Extend tests:** redirect, content-type, retry, and circuit-breaker scenarios in the integration project, and property-based tests for URL normalization.

## 6. Suggested Priorities

| Priority | Change | Reason |
|---|---|---|
| 1 | Content-type and size checks | Avoids downloading and parsing unsuitable resources |
| 2 | Circuit breaker scope and criteria | Prevents false trips from ordinary 404 responses |
| 3 | Robots cache key and `Crawl-delay` | Improves politeness and correctness |

Already done
