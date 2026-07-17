# Regression report — PG-RUN-2026-07-16-01

## Build and scope

| Field | Result |
| --- | --- |
| Source revision | `641cf8ba0cda5a3e5750efd9ce979f789c4be550` |
| Executed binary | Existing .NET 8 CLI apphost SHA-256 `fc7c088380e5e0d3c4dcab35504498d3acc0e25cb300589d97edce3636a4ed1a` |
| Exact-revision rebuild | Blocked: repository-requested .NET 8 SDK unavailable |
| Focus | Complete CLI functional/negative cases; local CLI timing observation |
| Excluded/blocked | API, SQLite, MCP, telemetry runtime, Unreal game content, playtest, client/GPU/server/network profiling |

## Original failure and fix lifecycle

No product fix was included in this execution, so there is no legitimate original-failing-build → fixed-build → passed-retest chain to claim. Five open defects are recorded; only PG-003 was reproduced dynamically in this run, while the other four are source-confirmed with their missing runtime retest stated.

| Defect | Original observation | Fixed build | Focused retest | Status |
| --- | --- | --- | --- | --- |
| PG-001 | Documented editor filter/export absent from panel source | None | Pending UE 5.6 fixture run | Open |
| PG-002 | Hand-built OpenAPI operations omit required responses | None | Pending compatible API runtime and validator | Open |
| PG-003 | Valid JSON/type-invalid fixture labelled `invalid JSON` | None | Reproduced 2/2; no fix to retest | Open |
| PG-004 | Raw commandlet exit hard-coded to error count | None | Pending warning-only Unreal fixture | Open |
| PG-005 | CLI/Core contains no claimed telemetry instrumentation | None | Pending collector run after fix | Open |

## Adjacent regression exercised

- Clean summary and all supported failure thresholds retained correct process exits.
- Forward/reverse report comparison retained added/resolved finding identity.
- Usage failures, file failures, and timeout validation remained distinguishable.
- The performance harness executed both clean and finding-bearing summaries 30 times with zero correctness failures.

API/MCP/observability adjacency was not exercised because the host lacks the required ASP.NET Core 8 runtime. Unreal adjacency was outside this portable run.

## Disposition

- Focused retest result: **Not applicable—no fix build supplied**.
- Regression decision: **Blocked** for release acceptance; five CLI cases provide useful behavioral evidence only.
- Residual risk: all five open issues, exact-revision build provenance, stateful/API/agent paths, real game-content parity, and representative game performance remain unresolved.
- Next qualifying report: fix at least one real defect, build it from a clean compatible environment, retest the source case, execute named adjacent cases, and link both commits and evidence.
