# Regression execution `PG-RUN-2026-07-16-01`

## Configuration

| Field | Value |
| --- | --- |
| Revision/tag | `641cf8ba0cda5a3e5750efd9ce979f789c4be550`; clean before evidence generation |
| Suite revision | `1.0` |
| Executor | Codex agent, with commands/results visible in the shared session |
| Started/completed UTC | 2026-07-17 02:10 / 2026-07-17 02:38 |
| Environment | `PORTABLE-WIN` with a dependency deviation: Windows 10.0.26200, x64, PowerShell 7.5.8; .NET SDK 8.0.x and ASP.NET Core 8 shared runtime unavailable |
| Build provenance | Current build blocked: `global.json` requests SDK 8.0.100 and no compatible 8.0 SDK is installed. CLI cases used the existing Release apphost SHA-256 `fc7c088380e5e0d3c4dcab35504498d3acc0e25cb300589d97edce3636a4ed1a`; product source/data did not change from initial release through this revision. This is behavioral evidence, not a release rebuild. |
| Test data | Clean SHA-256 `b217badcca7f60f5d3948cabeec1bc239c6a4b2dcd91ed48c2fd1ec03e50491e`; error SHA-256 `c4ab67fa51a77ac916ebc3286f39274b68b2176c8b8efb065d26d24d1661cbba`; malformed tracked fixture |
| Scope/deviations | Full CLI cases; performance capture steps 1–3. API/MCP/observability blocked by runtime prerequisite. Unreal intentionally outside this portable execution. |

The API startup attempt failed before binding with the host message that `Microsoft.AspNetCore.App` 8.0.0 must be installed. This is classified as an environment block, not a product defect, because the repository explicitly requires .NET 8.

## Results

| Case | Status | Actual result | Evidence | Defect |
| --- | --- | --- | --- | --- |
| PG-CLI-001 | Passed | Clean summary reported 8 assets, 4 rules, zero findings; exit `0`. | Bounded session output; performance raw samples also assert clean output/exit. | None |
| PG-CLI-002 | Passed | At minimum `error`, only `PG.Naming.Prefix` printed for fail thresholds error/warning/info; totals retained the warning; every exit was `1`. | Bounded session output; error performance samples assert finding/exit. | None |
| PG-CLI-003 | Passed | Contract-invalid fixture failed closed with exit `2` and no summary; its valid JSON was misleadingly labelled `invalid JSON`. | Bounded session output. | PG-003 |
| PG-CLI-004 | Passed | Forward comparison: added 2, exit `1`; reverse: resolved 2, exit `0`; stable rule/path pairs matched. | Bounded session output. | None |
| PG-CLI-005 | Passed | Unknown command and invalid severity exited `2`; missing report exited `3`; timeout `0` was rejected before launch with exit `2`. | Bounded session output. | None |
| PG-API-001 | Blocked | API could not start because ASP.NET Core 8 shared runtime is absent. | Startup stderr under ignored local `artifacts/`. | None (environment) |
| PG-API-002 | Blocked | Same API runtime prerequisite. | Same as PG-API-001. | None (environment) |
| PG-API-003 | Blocked | Same API runtime prerequisite. | Same as PG-API-001. | None (environment) |
| PG-API-004 | Blocked | Same API runtime prerequisite. | Same as PG-API-001. | None (environment) |
| PG-API-005 | Blocked | Same API runtime prerequisite. | Same as PG-API-001. | None (environment) |
| PG-API-006 | Blocked | Runtime validator step unavailable; source inspection confirms the documented contract gap. | `src/PipelineGuard.Api/Program.cs` source review. | PG-002 |
| PG-MCP-001 | Blocked | Required seeded local API unavailable. | Dependency chain recorded above. | None |
| PG-MCP-002 | Blocked | Required seeded local API unavailable. | Dependency chain recorded above. | None |
| PG-MCP-003 | Blocked | Required API-backed protocol session unavailable. | Dependency chain recorded above. | None |
| PG-OBS-001 | Blocked | API/MCP telemetry operations unavailable; source inspection also found no CLI instrumentation. | Dependency chain and CLI/Core source review. | PG-005 |
| PG-UE-001 | Not Run | Outside this portable execution; source review retained. | Existing release verification is separate evidence. | PG-001 |
| PG-UE-002 | Not Run | Outside this portable execution. | Existing release verification is separate evidence. | None |
| PG-UE-003 | Not Run | Outside this portable execution; source review found raw warning-threshold disagreement. | Existing release verification is separate evidence. | PG-004 |
| PG-PERF-001 | Blocked | Capture generation, correctness validation, and nearest-rank summary passed; compatible three-run baseline comparison is not yet available. | [Raw local capture](../performance/evidence/2026-07-16-local-cli/) | None |

## Performance observation

Capture `478c6382-aeb1-463e-8943-2f3ae2f1d8de` contains 71 rows: one session-first sample, two sets of five warm-ups, and two sets of 30 measured samples. All correctness fields passed.

| Scenario | n | Failures | p50 ms | p95 ms | Classification |
| --- | ---: | ---: | ---: | ---: | --- |
| Clean summary, warm-cache new process | 30 | 0 | 375.139 | 594.462 | Local observation |
| Error summary, warm-cache new process | 30 | 0 | 456.500 | 578.131 | Local observation |

The single session-first observation was 389.261 ms and has no percentile. No latency result is an SLA or regression gate.

## Summary and decision

| Passed | Failed | Blocked | Not Run |
| ---: | ---: | ---: | ---: |
| 5 | 0 | 11 | 3 |

- Exit decision: **Blocked** for release acceptance; useful portable CLI behavioral evidence only.
- Residual risk: five open defects; API, SQLite, MCP, observability, OpenAPI runtime validation, exact-revision rebuild, real game-content QA, and three-run performance comparison were not completed in this environment.
- Follow-up: install the repository-required .NET 8 SDK and ASP.NET Core 8 runtime, build from a clean revision, rerun the six API, three MCP, and observability cases, then collect two additional comparable performance captures.
