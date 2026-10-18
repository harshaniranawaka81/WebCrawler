# Architecture Quick Reference

## What each project does
- **WebCrawler.Core**: domain contracts + crawling logic
- **WebCrawler.Application**: orchestration (`ApplicationOrchestrator`)
- **WebCrawler.Infrastructure**: adapters + external integrations
- **WebCrawler**: entry point + DI wiring
- **WebCrawler.Tests**: NUnit tests

## Where to look first
1. `WebCrawler/Program.cs`
2. `WebCrawler/Extensions/ServiceCollectionExtensions.cs`
3. `WebCrawler.Application/Services/ApplicationOrchestrator.cs`
4. `WebCrawler.Core/Services/Crawler.cs`

## Key contracts (Core)
- `ICrawler`
- `IPageFetcher`
- `ILinkExtractor`
- `ILinkStorage`
- `IRobotsChecker`
- `IRateLimiter`
- `IUrlFilter` / `IUrlFilterChain`

## Current health
- Build: ✅ passing
- Tests: ✅ 40/40 passing
- Target: `.NET 10`
