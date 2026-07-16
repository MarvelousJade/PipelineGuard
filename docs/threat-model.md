# Threat model

PipelineGuard is a local development tool, but its report uploads and MCP inputs are still untrusted. An AI agent can make mistakes, repeat calls, or pass adversarial text copied from content. Local execution is not a security boundary.

## Protected assets

- Integrity and confidentiality of project files
- Integrity of validation reports and run history
- Unreal executable and project-path configuration
- Developer credentials and inherited environment values
- Host availability and bounded CPU, memory, disk, and process use
- Logs and telemetry that may leave the machine through an OTLP exporter

## Trust boundaries

| Boundary | Untrusted input | Required control |
| --- | --- | --- |
| JSON report to CLI/API | Structure, sizes, strings, counts, version | Body limits, strict schema, cross-field validation, bounded persistence |
| MCP client to server | Tool name and every argument | Allow list, strict schemas, pagination, timeouts, cancellation |
| .NET orchestrator to Unreal | Profile-derived executable/project/arguments | Trusted configuration, canonical paths, `ArgumentList`, no shell |
| Components to telemetry exporter | User-controlled messages and paths | Structured fields, redaction, bounded attributes, no report payloads |
| API listener to host | HTTP requests | Loopback binding by default; no production exposure without authentication/TLS |

## Default MCP capability

The default server exposes only:

- `validation_list_runs`
- `validation_get_summary`
- `validation_list_findings`
- `validation_get_remediation`

These tools query accepted report data through the application layer. They do not accept filesystem paths, executable names, URLs, SQL, or raw command arguments. List calls enforce a server-side maximum page size, and responses have a byte cap.

MCP remediation text is deterministic tool output. PipelineGuard does not claim that it called or embedded an LLM.

## Optional run capability

`validation_run`, if implemented, is disabled by default. Enabling it requires trusted local configuration containing named profiles. A request supplies only a profile ID and a bounded scope selector defined by that profile.

The server must:

1. Reject unknown or disabled profiles.
2. Resolve configured executable and project paths canonically at startup.
3. Reject a path outside configured roots, including traversal and link/reparse-point escapes where applicable.
4. Use `ProcessStartInfo.ArgumentList`; never invoke a shell.
5. Apply a timeout, cancellation, single-run concurrency limit, and bounded stdout/stderr capture.
6. Avoid logging command-line secrets or the inherited environment.
7. Return a run identifier and bounded summary rather than unrestricted process output.

MCP never receives a raw executable path, `.uproject` path, environment map, working directory, command line, or arbitrary output path.

## Abuse cases and mitigations

| Abuse case | Mitigation |
| --- | --- |
| Path traversal such as `../../secret` | No path-bearing read tools; canonical allow-list checks for configured run profiles |
| Shell metacharacters in scope or profile | Strict patterns and `ArgumentList`; no shell command construction |
| Agent requests every finding repeatedly | Pagination, response cap, rate/concurrency limits, cancellation |
| Oversized report exhausts memory/disk | HTTP byte limit before parsing, schema item limits, bounded database retention |
| Forged summary hides errors | Recompute and compare summary counts before persistence |
| High-cardinality telemetry overload | Do not attach asset path, run ID, message, or project path to metrics |
| Sensitive content leaks through logs | Log rule IDs and counts by default; omit payloads and full asset messages |
| API is exposed beyond the workstation | Bind loopback; require a separate authenticated deployment design for remote use |
| SQLite query manipulation | Parameterized data access; MCP exposes typed filters rather than query text |

## Required negative tests

The release test suite must prove that:

- Unknown MCP tools and unknown profile IDs are rejected.
- Traversal, absolute-path, executable, URL, SQL, and raw-argument fields are not accepted by tool schemas.
- Page sizes over the maximum are rejected or clamped predictably.
- Oversized HTTP bodies and reports with more than 20,000 findings are rejected.
- A schema-valid report with inconsistent summary counts is rejected.
- Duplicate finding IDs are rejected.
- An Unreal process timeout is terminated and maps to exit `3`.
- Cancellation does not leave an orphaned Unreal process.
- Logs and exported metric attributes do not contain fixture secrets or asset paths.

## Residual risk and non-goals

The local API has no production authentication in the first release. Loopback binding is a deployment restriction, not a substitute for authentication on a shared or remote host. The first release does not modify assets, apply AI-generated fixes, download models, administer source control, or run arbitrary build steps.

Any future remote service, Horde or remote BuildGraph deployment, or write-capable MCP tool needs a new threat-model review rather than inheriting this approval.
