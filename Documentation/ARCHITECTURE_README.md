# WebCrawler Architecture Documentation

This documentation matches the current solution state.

## Solution
- **Path:** `WebCrawler/WebCrawler.sln`
- **Target framework:** `.NET 10`
- **Projects loaded (5):**
  1. `WebCrawler.Core`
  2. `WebCrawler.Application`
  3. `WebCrawler.Infrastructure`
  4. `WebCrawler`
  5. `WebCrawler.Tests`

## Current Layering
- **Composition Root (`WebCrawler`)**: `Program.cs`, `Extensions/ServiceCollectionExtensions.cs`, `appsettings.json`
- **Application (`WebCrawler.Application`)**: `Services/ApplicationOrchestrator.cs`
- **Domain (`WebCrawler.Core`)**: contracts, domain models, filters, crawler logic, exceptions
- **Infrastructure (`WebCrawler.Infrastructure`)**: adapters, HTTP, parsing, storage, rate limiting, robots, policy factory
- **Tests (`WebCrawler.Tests`)**: NUnit unit tests

## Canonical Rules
- Contracts are centralized in `WebCrawler.Core.Interfaces`.
- Infrastructure implementations depend on Core contracts.
- `WebCrawler.Application` depends on Core only.
- `WebCrawler` composes dependencies via DI.

## Validation Snapshot
- Build: ✅ Success
- Tests: ✅ 40/40 passing
