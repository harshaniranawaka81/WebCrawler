# WebCrawler Architecture

## Overview

WebCrawler is a .NET 10 console application that crawls pages starting from a configured URL. It fetches pages over HTTP, extracts links, applies URL filters and robots.txt rules, writes results to the console, and records visited links to file storage.

The solution follows a layered design. Dependencies point inward: the Console composition root depends on Infrastructure, Application, and Core, and Core depends on nothing else in the solution.

## Diagrams

### Project dependencies

```mermaid
flowchart TD
    Console[WebCrawler.Console<br/>Composition root] --> Application[WebCrawler.Application]
    Console --> Infrastructure[WebCrawler.Infrastructure]
    Console --> Core[WebCrawler.Core]
    Application --> Core
    Infrastructure --> Core
    Tests[WebCrawler.Tests] --> Core
    Tests --> Application
    Tests --> Infrastructure
    Tests --> Console
    IntegrationTests[WebCrawler.IntegrationTests] --> Core
    IntegrationTests --> Application
    IntegrationTests --> Infrastructure
    IntegrationTests --> Console
    IntegrationTests --> Application
    IntegrationTests --> Infrastructure
    IntegrationTests --> Console
```

### Crawl pipeline

```mermaid
flowchart TD
    Start([Root URL]) --> Queue[Workers read Channel of Uri<br/>MaxConcurrentRequests workers]
    Queue --> MaxCheck{MaxPagesToCrawl<br/>reached?}
    MaxCheck -- Yes --> Done([Return CrawlStatistics])
    MaxCheck -- No --> Visited{Already visited?}
    Visited -- Yes --> Queue
    Visited -- No --> Fetch[IPageFetcher.GetContentAsync<br/>via IRateLimiter]
    Fetch -- Failure --> Failed[Record failure] --> Queue
    Fetch -- Success --> Extract[ILinkExtractor.ExtractLinks]
    Extract --> LinkCheck{Valid URI?}
    LinkCheck -- No --> Skip[Record skipped] --> Extract
    LinkCheck -- Yes --> Chain{IUrlFilterChain:<br/>SameHostFilter,<br/>RobotsTextFilter if enabled}
    Chain -- Rejected --> Skip
    Chain -- Passed --> Enqueue[Write to Channel] --> Extract
    Fetch -. page recorded .-> Storage[ILinkStorage / IOutputWriter]
    Queue -. queue empty .-> Done
```

### Layered architecture

```mermaid
flowchart TB
    subgraph Composition
        Console[Console: Program, adapters, DI extensions]
    end
    subgraph Application Layer
        Orchestrator[ApplicationOrchestrator]
    end
    subgraph Core Domain
        Crawler[Crawler, filters, interfaces, models]
    end
    subgraph Infrastructure
        Http[HttpPageFetcher, retry policies]
        Parsing[HtmlLinkExtractor]
        Robots[RobotsChecker, RobotsParser]
        RateLimit[CompositeRateLimiter]
        Storage[FileLinkStorage]
    end
    Console --> Orchestrator
    Console --> Http
    Console --> Storage
    Orchestrator --> Crawler
    Crawler --> Http
    Crawler --> Parsing
    Crawler --> Filters[IUrlFilterChain]
    Filters --> Robots
    Crawler --> Storage
    Http --> RateLimit
    Robots --> Http
```

## Projects

| Project |
|---|---|---|
| `WebCrawler.Core` | Domain | Interfaces, models, settings, exceptions, URL filters, and the `Crawler` orchestration service. |
| `WebCrawler.Application` | Application | `ApplicationOrchestrator`, which coordinates the run lifecycle. |
| `WebCrawler.Infrastructure` | Infrastructure | Concrete implementations: HTTP fetching and resilience policies, HTML link extraction, robots.txt handling, rate limiting, and file storage. |
| `WebCrawler.Console` | Composition root | Program entry point, console adapters, and DI registration extensions. |
| `WebCrawler.Tests` | Tests | NUnit unit tests for Core, Application, Infrastructure, and Console. |
| `WebCrawler.IntegrationTests` | Tests | NUnit end-to-end tests that run the real DI graph against a local Kestrel test server. |

## Core Components

### Crawl pipeline

It crawls concurrently. Root and discovered URLs go through an unbounded `Channel<Uri>` read by `MaxConcurrentRequests` workers. A pending counter completes the channel when no work is queued or in flight. Statistics updates, visited/max-page admission, and output/storage writes are synchronized. Page output order is therefore nondeterministic. For each URL a worker:

1. Atomically admits the URL: it stops if `MaxPagesToCrawl` has been reached, and skips URLs already visited (normalized with `UrlHelper`).
2. Fetches the page with `IPageFetcher`. The fetcher applies `IRateLimiter` before each request.
Each link is resolved against the current page and checked for a valid URI format.
4. Runs each link through `IUrlFilterChain`, which holds `SameHostFilter` and, when `RespectRobotsTxt` is enabled, `RobotsTextFilter`. The rejection reason is mapped to `DifferentHost` or `DisallowedByRobotsTxt` (`UrlFilterNames`).
5. Enqueues eligible links and records visited links through `ILinkStorage`. Output goes through `IOutputWriter`.
6. Tracks progress in `CrawlStatistics`.

URI parsing and atomic visited/max-page admission remain in `Crawler`. `CompositeRateLimiter`

### Interfaces (Core)

- `IPageFetcher`, `ILinkExtractor`: fetching and parsing.
- `IRobotsChecker`, `IUrlFilter`, `IUrlFilterChain`: URL eligibility.
- `IRateLimiter`: request pacing.
- `ILinkStorage`, `IOutputWriter`: persistence and output.
- `IApplicationOrchestrator`, `IApplicationInteraction`, `IExceptionHandler`: application flow and error handling.

### Filters

- `SameHostFilter`: restricts crawling to the starting host.
- `RobotsTextFilter`

### Exceptions

Specific types include `ConfigurationException`.

## Infrastructure Components

- **HTTP** (`Http/`): `HttpPageFetcher` uses an `HttpClient` wrapped by `HttpPolicyFactory` and `HttpClientPolicies` for retry and circuit-breaker behavior.
- **Parsing** (`Parsing/`): `HtmlLinkExtractor` pulls `href` values from anchor tags.
- **Robots** (`Robots/`): `RobotsChecker` fetches and caches robots.txt per host. `RobotsParser` parses the rules. Robots URLs are built relative to the page URI so non-default ports are preserved.
- **Rate limiting** (`RateLimiting/`): `CompositeRateLimiter` combines the delay and concurrency limits.
- **Storage** (`Storage/`): `FileLinkStorage` writes visited links to a timestamped `visited_links_*.txt` file in the configured output directory. It is a singleton and must be disposed to flush output.

## Application Flow

1. `Program` (Console) loads `appsettings.json`, configures Serilog, and builds a `ServiceCollection` with the settings, `AddCrawlerInfrastructure`, and `AddCrawlerCore`.
2. `Program` creates a `CancellationTokenSource` tied to Ctrl+C (`Console.CancelKeyPress`), resolves `IApplicationOrchestrator`, and calls `RunAsync(token)`. The token flows through the crawler, fetcher, rate limiter, filter chain, and robots checker.
3. `ApplicationOrchestrator` validates configuration through `ConfigurationValidator`, prompts for a URL through `IApplicationInteraction`, and validates the URL format.
4. `ApplicationOrchestrator` runs `Crawler` inside `IExceptionHandler`, displays results, and finalizes storage in a `finally` block.
Exit code is `0` on success, `1` on failure, and `130` when the user cancels with Ctrl+C.

## Configuration

Settings are bound from `appsettings.json` into `WebCrawlerSettings` (Core). Key options include the default URL, `MaxPagesToCrawl`, delay, maximum concurrent requests, robots fetch timeout and cache expiration, connection timeout, storage flush interval, buffer size, and HTTP client policies.

`ConfigurationValidator` checks these values at startup and collects all errors before failing.

## Dependency Injection

- `CrawlerCoreServiceExtensions.AddCrawlerCore` registers core services and the `ICrawler` implementation. `Crawler` is constructed explicitly from `IOptions<WebCrawlerSettings>.Value`.
- `CrawlerInfrastructureServiceExtensions.AddCrawlerInfrastructure` registers the Infrastructure implementations and HTTP clients.

## Testing Strategy

They cover filters, parsers, storage, rate limiting (including concurrent callers), `HttpPageFetcher`, crawler concurrency, statistics and error handling, exception handling, and configuration validation.
- **Integration tests** (`WebCrawler.IntegrationTests`): start a local ASP.NET Core server on `127.0.0.1:0` with fixed pages and a `robots.txt`. They use the real DI registrations, so they run offline and deterministically.

## Build and Run

```powershell
dotnet build WebCrawler.slnx
dotnet test WebCrawler.slnx
dotnet run --project WebCrawler/WebCrawler.Console
```
