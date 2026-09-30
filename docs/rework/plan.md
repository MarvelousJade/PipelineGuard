# Rework checklist and acceptance criteria

Base: `3a37303` (`main`). Initial working tree clean; no repository or ancestor AGENTS.md found. Existing release and extension commits are preserved, not presented as new development.

## Inspection

Already works: local .NET Release builds pass with warnings as errors; 49 validation tests and 9 crash tests pass. Dashboard `npm ci` and production build pass. Contracts, CLI and storage are small; immutable crash documents and search-time aggregation are useful choices worth preserving.

Complexity: the repository has two independent workflows. RabbitMQ, Elasticsearch, telemetry and Kubernetes are not needed to demonstrate validation history. Keep them optional rather than rebuilding/removing useful crash functionality. No additional runtime dependency or generalized repository abstraction is needed.

Gaps found by code review: SQLite orders offset-bearing timestamp strings rather than instants and lacks a tie-breaker. Existing tests don't exercise SQLite persistence or the Elasticsearch HTTP adapter. Index creation follows any failed HEAD (including authorization/outage); HTTP timeout classification is implicit. Recorded QA lists also show unfinished editor filters/export, OpenAPI and CLI telemetry. Those engine/feature changes are not prerequisites for this bounded reliability rework.

## Increments

- [ ] 1. Validation history: ingest → SQLite → list/get smoke, UTC chronology and stable ties. Acceptance: regression fails on old ordering, offsets/ties survive restart, duplicate IDs preserve original JSON; real loopback smoke verifies 201/409/400/404 and pagination.
- [ ] 2. Crash persistence boundary: test actual HTTP requests and status handling; distinguish unavailable/auth failures from absent indexes and preserve caller cancellation. Acceptance: create-only URL, immutable replay, 408/429/5xx/network/timeout transient, 400 permanent, no PUT after failed non-404 HEAD, cancellation not swallowed.
- [ ] 3. Final run guide, decisions, evidence and interview walkthrough; all portable checks pass. Claims about the earlier Unreal and Compose runs remain historical only.
- [ ] 4. Two independent faulty learning checkpoints on `shaoyu/learning`; regression demonstrably fails at each, solutions and hints separate, return to passing `shaoyu/rework`.

Docker daemon is unavailable in this session. Unreal verification requires the licensed engine environment and is not part of portable reruns. No production, multi-node, or new performance claim will be made. Finish these increments without changing the two deployment boundaries or introducing new infrastructure.
