# Features Documentation (Current)

## Crawl Engine
- `Crawler` performs queue-based crawling and emits `CrawlStatistics`.

## URL Filtering
- Domain filters: `SameHostFilter`, `UniqueUrlFilter`, `UriFormatFilter`.
- Infrastructure filter: `RobotsTextFilter`.
- Chain coordinator: `UrlFilterChain`.

## Page Fetch + Resilience
- Fetcher: `HttpPageFetcher`.
- Policies: `HttpPolicyFactory` with `HttpClientPolicies` settings.

## Storage
- `FileLinkStorage` writes visited links and finalization metadata.

## Robots
- `RobotsChecker` fetches and caches robots rules.
- `RobotsParser` parses robots directives.

## Rate limiting
- `CompositeRateLimiter` enforces delay + concurrency behavior.

## Orchestration
- `ApplicationOrchestrator` coordinates validation, user interaction, crawl run, and output.

## Quality status
- Build: ✅ passing
- Tests: ✅ 40/40 passing
