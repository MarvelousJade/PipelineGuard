# Manual test cases

Case revision: 1.0. Commands are run from the repository root. Unless a case says otherwise, use Release binaries built from the recorded commit. Evidence should include the command/request, exit or HTTP status, relevant bounded output, timestamp, and execution ID.

## CLI and report contract

### PG-CLI-001 — Clean report returns a clean summary

- Priority/type: P0, functional positive
- Risk: a conforming clean run blocks CI or reports invented findings
- Preconditions/data: Release CLI; `samples/clean-report.json`

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run `PipelineGuard.Cli summary samples/clean-report.json --minimum info --fail-on error`. | Header identifies run `6b64374b-78be-4f5a-8e30-8658c7ef4f78`; totals are 8 assets, 4 rules, and zero findings. |
| 2 | Record the process exit code. | Exit is `0`. |

- Automation reference: CLI behavior is indirectly covered by analyzer/serializer unit tests; retain this end-to-end check.

### PG-CLI-002 — Blocking and filtering thresholds are independent

- Priority/type: P0, decision table/boundary
- Risk: filtering output changes the CI decision or hides the error threshold
- Preconditions/data: Release CLI; `samples/error-report.json`

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run `summary` with `--minimum error --fail-on error`. | Only `PG.Naming.Prefix` is printed; totals still show one error and one warning; exit is `1`. |
| 2 | Run with `--minimum error --fail-on warning`. | Only the error is printed; exit remains `1`. |
| 3 | Run with `--minimum error --fail-on info`. | Only the error is printed; exit remains `1`. |

- Automation reference: `ReportAnalyzerTests` covers severity semantics; this case covers the CLI surface.

### PG-CLI-003 — Invalid contract fails closed

- Priority/type: P0, negative contract
- Risk: unsupported or malformed reports are treated as clean
- Preconditions/data: Release CLI; `samples/malformed-report.json`

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run `summary` on the malformed fixture. | Standard error identifies invalid JSON/report data without a stack trace. |
| 2 | Record the exit code. | Exit is `2`; no validation summary is printed. |

- Automation reference: `StrictJsonContractTests`, `JsonReportSerializerTests`, and `ValidationReportValidatorTests`.

### PG-CLI-004 — Comparison uses stable finding identity

- Priority/type: P1, state comparison
- Risk: regressions cannot distinguish new, resolved, and persistent findings
- Preconditions/data: clean and error fixtures

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Compare clean baseline to error current at the error threshold. | Added 2, resolved 0, persisting 0, changed 0; both stable rule/path pairs are printed; exit is `1`. |
| 2 | Reverse the comparison. | Added 0, resolved 2, persisting 0, changed 0; exit is `0`. |

- Automation reference: `ReportAnalyzerTests`.

### PG-CLI-005 — Usage and execution errors are distinguishable

- Priority/type: P1, negative/error handling
- Risk: operators cannot distinguish bad arguments from runtime failures
- Preconditions/data: Release CLI

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run an unknown command and an invalid severity. | Each prints bounded usage guidance and exits `2`. |
| 2 | Run `summary` with a nonexistent report path. | It prints `execution failed` and exits `3`. |
| 3 | Run `validate` with a timeout outside `1..7200`. | It rejects the value before launching Unreal and exits `2`. |

- Automation reference: argument parsing is currently manual-only; process construction has `UnrealProcessStartInfoBuilderTests`.

## Local API

### PG-API-001 — Health is loopback-ready

- Priority/type: P1, deployment smoke
- Risk: local clients cannot determine readiness or the service binds unexpectedly
- Preconditions/data: API started with a fresh database and default URL configuration

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Request `GET http://127.0.0.1:5078/health`. | Status `200`; JSON contains `status: ok`, service name, and a UTC timestamp. |
| 2 | Inspect startup evidence. | Listener is loopback by default; no non-loopback endpoint is claimed. |

### PG-API-002 — A valid run can be created and retrieved

- Priority/type: P0, stateful functional
- Risk: an accepted report is lost or changed
- Preconditions/data: fresh API database; clean fixture

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | POST the clean fixture as `application/json`. | Status `201`; `Location` and body name the submitted run ID. |
| 2 | GET that run ID. | Status `200`; returned contract matches the submitted run, including summary and findings. |
| 3 | List runs with `limit=1&offset=0`. | Status `200`; bounds are echoed and the created run is present. |

### PG-API-003 — Duplicate run IDs are rejected without mutation

- Priority/type: P0, state transition/idempotency
- Risk: run history silently overwrites or duplicates evidence
- Preconditions/data: PG-API-002 completed

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | POST the identical clean fixture again. | Status `409`; error is `duplicate_run_id`. |
| 2 | Retrieve and list the run. | Original data remains unchanged and only one row exists for the ID. |

### PG-API-004 — Invalid and malformed input is not persisted

- Priority/type: P0, negative/security
- Risk: untrusted input corrupts the evidence store
- Preconditions/data: fresh database; malformed fixture; syntactically broken JSON

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | POST the contract-invalid fixture. | Status `400` with bounded `invalid_report` or `invalid_json` detail. |
| 2 | POST syntactically broken JSON. | Status `400` with `invalid_json`. |
| 3 | List runs. | Neither rejected payload appears. |

### PG-API-005 — Pagination and body limits are bounded

- Priority/type: P0, boundary/security
- Risk: an agent or client can request unbounded memory/disk work
- Preconditions/data: running API; generated bodies at the documented boundary

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | List with limits `1`, `100`, `101`, `0` and offsets `0`, `100000`, `100001`, `-1`. | API clamps limit to `1..100` and offset to `0..100000` predictably. |
| 2 | POST a body larger than 1,048,576 bytes. | Status `413`; payload is not persisted. |
| 3 | Request an unknown GUID. | Status `404` with `run_not_found`. |

### PG-API-006 — Advertised OpenAPI is structurally valid

- Priority/type: P2, contract/tooling compatibility
- Risk: generated clients and validators reject the advertised API document
- Preconditions/data: running API; OpenAPI 3.1 validator

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | GET `/openapi/v1.json`. | Status `200`; media is JSON and declares OpenAPI 3.1. |
| 2 | Validate the full document. | Zero structural errors; every operation defines required responses and path parameters. |

- Known defect: [PG-002](../defects/PG-002-incomplete-openapi-document.md).

## MCP and safety

### PG-MCP-001 — Tool discovery is an exact read-only allowlist

- Priority/type: P0, security/protocol
- Risk: an agent discovers an unintended execution or filesystem capability
- Preconditions/data: API running with one accepted run; official SDK smoke client

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Initialize MCP over stdio and list tools. | Exactly four names are returned: `validation_list_runs`, `validation_get_summary`, `validation_list_findings`, and `validation_get_remediation`. |
| 2 | Inspect tool schemas. | No executable, URL, filesystem path, SQL, environment, or raw-command argument is accepted. |

- Automation reference: `PipelineGuard.Mcp.Smoke` asserts the exact names.

### PG-MCP-002 — Findings and remediation stay bounded and deterministic

- Priority/type: P1, functional/data parity
- Risk: MCP changes report meaning or exposes an entire unbounded report
- Preconditions/data: error fixture ingested; official SDK client

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Get the run summary. | Metadata and totals match the API report; full findings are omitted. |
| 2 | List findings at minimum `error`, limit 20. | One naming finding is returned in deterministic order. |
| 3 | Request remediation by stable finding ID. | Returned ID/rule/path/message/remediation match the accepted report exactly. |

### PG-MCP-003 — Invalid tool inputs fail closed

- Priority/type: P0, negative/security boundaries
- Risk: malformed agent input bypasses limits or triggers arbitrary work
- Preconditions/data: running API/MCP; protocol-capable client

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Call an unknown tool and pass unknown path/raw-command fields. | Protocol/schema rejects the request; no process or file action occurs. |
| 2 | Test limit `0`/`101`, offset `-1`/`100001`, invalid severity, invalid GUID, missing run, and missing finding. | Each returns a bounded tool error with no server crash. |
| 3 | Configure a non-loopback API URL without the explicit override. | MCP refuses startup/connection. |

## Observability

### PG-OBS-001 — Accepted and rejected operations are correlated without sensitive metric tags

- Priority/type: P1, observability/security inspection
- Risk: failures cannot be traced, or telemetry leaks paths/IDs and creates high cardinality
- Preconditions/data: console telemetry; one accepted, duplicate, and invalid ingest; one MCP call

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Correlate each request's structured log and trace. | Accepted/rejected outcomes have usable trace correlation and bounded error status. |
| 2 | Inspect counters and duration histogram. | Run, finding, rejection, and duration instruments reflect the actions. |
| 3 | Search metric attributes. | No asset path, run ID, report payload, credential, personal path, or free-form message is present. |

## Unreal conditional suite

### PG-UE-001 — Editor workflow exposes promised actions

- Priority/type: P2, usability/requirements
- Risk: an artist cannot filter findings or retain a report from the documented panel
- Preconditions/data: `UE56-WIN`; fixture assets with at least one error and warning

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Open the PipelineGuard tab and run validation. | Summary and actionable findings appear; settings opens. |
| 2 | Filter to one severity/rule and clear the filter. | List and totals update predictably without rerunning validation. |
| 3 | Export JSON and validate it with the CLI. | A schema v1 report is written and accepted. |
| 4 | Double-click a finding. | Content Browser focuses and the asset editor opens the named asset. |

- Known defect: [PG-001](../defects/PG-001-editor-filter-export-gap.md).

### PG-UE-002 — Editor and commandlet produce stable parity

- Priority/type: P1, cross-surface/metamorphic
- Risk: local users and CI receive different findings
- Preconditions/data: `UE56-WIN`; same saved revision, profile, scope, and threshold

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run the editor twice without content changes. | Stable IDs, membership, counts, and deterministic order match. |
| 2 | Run the headless commandlet on the same inputs. | Finding IDs and sorted contents match the editor; report validates as schema `1.0`. |
| 3 | Fix one fixture asset and rerun both surfaces. | The same stable finding is resolved; unaffected findings persist. |

### PG-UE-003 — Headless failures map to deterministic evidence

- Priority/type: P0, CI/error handling
- Risk: CI cannot distinguish findings, bad configuration, execution failure, and report-write failure
- Preconditions/data: `UE56-WIN`; writable and intentionally unwritable report targets

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run clean, finding-bearing, and invalid-configuration profiles. | Raw exits are `0`, `1`, and `2` with bounded diagnostics. |
| 2 | Force timeout/process failure through the .NET CLI. | CLI terminates the child, leaves no orphan, and exits `3`. |
| 3 | Use an unwritable raw commandlet report target. | Raw commandlet exits `4`; .NET orchestration maps it to `3`. |
| 4 | Run the two-node BuildGraph. | Compile and commandlet nodes succeed and retain the report/transcript. |

## Performance

### PG-PERF-001 — Fixed-fixture capture is reproducible and correct

- Priority/type: P2, non-functional
- Risk: latency claims mix build time, invalid responses, or incomparable machines
- Preconditions/data: compatible Release binaries; fixed fixture hashes; quiet reference host

| Step | Action | Expected result |
| ---: | --- | --- |
| 1 | Run `scripts/Measure-PipelineGuardPerformance.ps1` with recorded warm-up/sample counts. | Raw samples, summary, and environment metadata are generated in a new directory. |
| 2 | Inspect correctness fields. | Every CLI/API sample has its expected exit/status and bounded response; failures invalidate the scenario. |
| 3 | Recalculate nearest-rank p50/p95 from raw samples. | Values match the summary; no outlier was trimmed. |
| 4 | Compare with a compatible three-run baseline. | Any budget breach is repeated before a performance defect is filed. |
