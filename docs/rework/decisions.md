# Engineering decisions

## 1. Keep validation as the zero-infrastructure demonstration

**Problem:** the README leads with two workflows and an operations stack, which makes a small interview walkthrough look like it needs seven services.

**Alternatives:** remove the crash extension; merge APIs/storage; retain separate deployment boundaries and demonstrate validation first.

**Evidence:** baseline portable tests passed (49 validation, 9 crash). The validation API uses only SQLite and a single process; the crash extension already has useful immutable documents and recorded synthetic benchmark evidence. No evidence justifies deleting it or rebuilding the contracts.

**Choice:** preserve both solutions; add a one-command isolated HTTP smoke for validation. No runtime package or new service. Crash Compose and Kubernetes remain optional.

**Tradeoff/limits:** more source remains in the repository, but a learner can trace one workflow independently. The smoke covers the API/SQLite boundary, not Unreal or the MCP protocol. Existing editor/OpenAPI/telemetry gaps remain explicitly tracked rather than disguised by a refactor.

## 2. Store sortable UTC metadata; retain original report JSON

**Problem:** `ORDER BY completed_utc` compared local-clock strings, so 12:00+03:00 appeared newer than 10:00Z. Equal completion instants also lacked a deterministic page order.

**Alternatives:** SQLite date conversion at query time; new integer-ticks column and versioned schema migration; canonical UTC round-trip strings at write/startup.

**Evidence:** the new store suite initially failed 3/6 tests (offset order, tied pages, legacy startup). Fixed tests also preserve 100-nanosecond ordering, which SQLite's usual millisecond date conversion would not retain. No large-dataset speed measurement was made.

**Choice:** canonical seven-fractional-digit UTC strings and `run_id ASC` as a tie-breaker. Transactionally normalize legacy non-UTC metadata at startup. Preserve the exact uploaded JSON, IDs and summary fields. The existing completion index remains useful.

**Tradeoff/limits:** startup scans metadata and loads non-UTC legacy rows into memory before updating; appropriate for the existing local-development store, not a proven million-row migration. Invalid manually edited timestamps fail startup rather than silently inventing dates. Offset pagination is stable for a fixed dataset, not a snapshot across concurrent insertions. A cursor/migration version would be justified only by actual scale requirements.
