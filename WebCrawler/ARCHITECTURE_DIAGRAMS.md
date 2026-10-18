# Architecture Diagrams (Current State)

## 1) Project Layers

```text
WebCrawler (Executable / Composition Root)
  ├─ Program.cs
  ├─ Extensions/ServiceCollectionExtensions.cs
  └─ appsettings.json
		   │
		   ▼
WebCrawler.Application
  └─ Services/ApplicationOrchestrator.cs
		   │
		   ▼
WebCrawler.Core
  ├─ Interfaces (contracts)
  ├─ Services (Crawler, UrlFilterChain, Validator, ExceptionHandler)
  ├─ Filters (SameHost, UniqueUrl, UriFormat)
  ├─ Models, Helpers, Exceptions, Configuration
		   ▲
		   │
WebCrawler.Infrastructure
  ├─ Adapters (ConsoleApplicationInteraction, ConsoleOutputWriter)
  ├─ Services (HttpPageFetcher, HtmlLinkExtractor, FileLinkStorage,
  │            RobotsChecker, CompositeRateLimiter)
  ├─ Filters (RobotsTextFilter)
  ├─ Configuration (HttpClientPolicies)
  ├─ Policies (HttpPolicyFactory)
  └─ Helpers (RobotsParser)
```

## 2) Runtime Flow

```text
Program -> ConfigureServices -> Resolve IApplicationOrchestrator
	   -> RunAsync
	   -> Validate settings
	   -> Read input URL
	   -> Crawler.CrawlAsync
	   -> Show results
	   -> Finalize storage
```

## 3) Test Coverage Areas
- Configuration validation
- Exception hierarchy
- URL helper behavior
- URL filter behavior and chain
