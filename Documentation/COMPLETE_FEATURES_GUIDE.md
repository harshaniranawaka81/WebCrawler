# Complete Features Guide (Current State)

## Solution snapshot
- Framework: `.NET 10`
- Projects: Core, Application, Infrastructure, Host, Tests
- Test framework: NUnit

## End-to-end flow
1. `Program.cs` builds configuration and logging.
2. DI registrations in `ServiceCollectionExtensions`.
3. `ApplicationOrchestrator.RunAsync()` drives execution.
4. `Crawler.CrawlAsync()` performs crawl.
5. Infrastructure services fetch/extract/store/check robots.
6. Output is displayed and storage is finalized.

## Main extension points
- Add new URL filters via `IUrlFilter` and `UrlFilterChain`.
- Swap storage by implementing `ILinkStorage`.
- Swap HTTP fetch strategy via `IPageFetcher`.
- Extend rate limiting via `IRateLimiter`.

## Current implementation map
- Domain contracts: `WebCrawler.Core/Interfaces`
- Orchestration: `WebCrawler.Application/Services/ApplicationOrchestrator.cs`
- Adapters/integrations: `WebCrawler.Infrastructure/*`
- Composition: `WebCrawler/Program.cs` + `Extensions/ServiceCollectionExtensions.cs`

## Validation
- Build succeeds.
- Unit tests pass: 40/40.
