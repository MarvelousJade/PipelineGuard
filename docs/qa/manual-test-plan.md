# Manual QA and regression test plan

## Objective

Demonstrate that PipelineGuard converts Unreal validation results into a deterministic, bounded contract that behaves consistently across the CLI, local API, MCP tools, observability signals, and—when the licensed environment is available—the editor and commandlet.

## Scope

In scope:

- CLI report validation, filtering, comparison, exit codes, and process failures
- API health, strict ingestion, persistence, duplicates, pagination, and body limits
- The four-tool read-only MCP allowlist, bounded arguments, and deterministic remediation
- Trace/log correlation and low-cardinality metrics
- Unreal editor usability, generic-rule behavior, report generation, and editor/commandlet parity
- Release performance observations for fixed fixtures and data shapes

Conditional scope:

- Unreal cases require UE 5.6.x, a built fixture host, and saved fixture assets.
- BlackWood cases require an inspected, authorized project revision and anonymized representative content. No BlackWood-specific case currently meets those entry criteria.

Out of scope for this release: production hosting, authentication, Horde, Perforce, automatic fixes, write-capable MCP, and productivity/time-saved claims.

## Risk priorities

| Priority | Risk | Primary coverage |
| --- | --- | --- |
| P0 | Invalid data is accepted, a blocking finding returns a clean exit, or an agent gains an unintended capability | CLI contract/exit cases, API rejection cases, MCP allowlist/negative cases |
| P1 | Results differ by surface, stable IDs/order drift, accepted runs cannot be retrieved, or telemetry leaks sensitive/high-cardinality values | Compare/parity, API persistence, observability cases |
| P2 | User workflow is incomplete, documentation is misleading, pagination is surprising, or performance regresses materially | Editor UX, OpenAPI, bounds, performance cases |
| P3 | Cosmetic wording or evidence presentation issue with no workflow impact | Exploratory review |

## Test techniques

- Equivalence partitions: clean, finding-bearing, syntactically invalid, and contract-invalid reports
- Boundaries: severities, page sizes, offsets, timeout range, report bytes, finding/metadata caps
- State transitions: first ingest, duplicate ingest, rejected ingest, retrieval, fix and retest
- Pairwise workflow checks: editor versus commandlet; baseline versus current report; API versus MCP view
- Abuse cases: GUID/URL/path/SQL/raw-command attempts, oversized input, unsupported tool names
- Exploratory charters: editor usability, evidence redaction, failure diagnostics, and documentation accuracy

## Environments

| ID | Purpose | Required configuration |
| --- | --- | --- |
| `PORTABLE-WIN` | Local portable execution | Windows 11, repository-required .NET 8 SDK and ASP.NET Core 8 runtime, loopback networking |
| `PORTABLE-CI` | Automated regression | GitHub Actions Ubuntu and Windows matrix from the exact commit |
| `UE56-WIN` | Engine/editor/commandlet | Windows 11, UE 5.6.x C++ toolchain, built `PipelineGuardHost`, fixture content |
| `BLACKWOOD-SAFE` | Future project integration | Authorized revision, inspected data contracts, sanitized representative assets |

Record exact SDK/runtime versions, engine build, commit, dirty state, fixture hashes, and configuration deviations in every execution.

## Entry criteria

- The revision and intended release scope are identified.
- Required dependencies are present; otherwise affected cases are `Blocked`.
- Release binaries are built from the recorded revision with zero build errors.
- Test data is immutable or its hash is recorded.
- A fresh API database is used for stateful API/MCP and performance scenarios.
- Evidence paths contain no credentials, private assets, usernames, or unnecessary absolute paths.

## Exit criteria

- All in-scope P0 and P1 cases required by `regression-suite.csv` have passed.
- No open Critical/High defect remains; accepted exceptions name an owner, reason, and review date.
- Failed cases link a defect; fixed defects link both focused retest and affected regression results.
- Blocked cases and their residual risk are explicit in the release decision.
- Performance correctness checks have zero unexpected exit/status results; latency is compared only with a compatible baseline.

## Status and defect conventions

| Value | Meaning |
| --- | --- |
| Passed | Actual result matched every expected result and evidence was retained |
| Failed | Product behavior did not match an expected result; a defect is linked |
| Blocked | The procedure could not run because a named prerequisite was unavailable |
| Not Run | The case was intentionally outside this execution's scope |

Severity expresses user/system impact: `Critical`, `High`, `Medium`, `Low`. Priority expresses delivery order: `P0` through `P3`. They may differ.

## Cadence

- Pull request: impacted portable P0/P1 cases plus the automated .NET suite
- Release candidate: complete portable regression, MCP protocol smoke, observability inspection, and compatible performance capture
- Unreal plugin change: full `UE56-WIN` suite plus editor/commandlet parity
- Contract/schema change: all consumer and producer cases across CLI, API, MCP, and Unreal
- BlackWood adapter change: affected generic regression plus the authorized project suite
