# Architecture Visual Summary

```text
[ WebCrawler ]  (entry + DI)
	  |
	  v
[ WebCrawler.Application ]  (run flow)
	  |
	  v
[ WebCrawler.Core ]  (domain + contracts)
	  ^
	  |
[ WebCrawler.Infrastructure ]  (adapters + integrations)
```

## Core facts
- Target framework: `net10.0`
- Projects: 5
- Tests: 40 (all passing)
- Contracts: centralized in Core
- Orchestrator: Application layer
- Console + HTTP policy implementations: Infrastructure layer
