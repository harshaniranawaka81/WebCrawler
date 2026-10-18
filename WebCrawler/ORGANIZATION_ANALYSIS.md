# Project Organization Analysis (Current)

## Current structure quality
The codebase is organized by responsibility and layer.

## Positive points
- Clear separation: Core / Application / Infrastructure / Host / Tests.
- Single source of interfaces in `WebCrawler.Core.Interfaces`.
- Infrastructure contains concrete adapters and external concerns.
- Composition is centralized in `ServiceCollectionExtensions`.

## Remaining notes
- `WebCrawler.Tests` references host + infrastructure + core; acceptable for current scope.
- Keep new interfaces in Core to avoid reintroducing ambiguity.

## Recommended guardrails
1. No interfaces in Infrastructure unless strictly internal.
2. No domain logic in host project.
3. Application project should continue to reference Core only.
4. Keep DI registrations in host extension methods.
