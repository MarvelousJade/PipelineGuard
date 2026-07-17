# PG-005 — CLI lacks the traces and metrics claimed by architecture

| Field | Value |
| --- | --- |
| Status | Open |
| Severity / priority | Medium / P2 |
| Component / version | CLI observability / revision `641cf8b` product sources |
| Source case / run | PG-OBS-001 / source review; collector/runtime retest pending |
| Environment | Source-confirmed; API runtime dependency prevented a full correlated workflow retest |
| Reproducibility | Static inspection: 1/1 |

## Preconditions

Treat `docs/architecture.md` as the current observability requirement: portable components emit traces around CLI execution and Unreal process invocation, plus metrics and structured logs.

## Steps to reproduce

1. Inspect `src/PipelineGuard.Cli/PipelineGuard.Cli.csproj`, `Program.cs`, and `UnrealValidationRunner.cs` for OpenTelemetry/activity/meter/logging instrumentation.
2. Compare with the explicit telemetry setup in the API and MCP projects.
3. When a collector is available, run a CLI summary and `validate` operation and query for CLI spans/metrics.

## Expected result

CLI execution and Unreal process orchestration emit the documented bounded traces/metrics/logs with usable correlation.

## Actual result

The CLI project has no OpenTelemetry dependencies or instrumentation, and the Unreal validation runner creates no activity, meter, or structured logger. API and MCP telemetry cannot create the missing CLI/process spans.

## Impact, evidence, and workaround

- Impact: the documented end-to-end trace cannot correlate CLI orchestration with Unreal execution; CLI latency/failure metrics are unavailable.
- Evidence: CLI/Core project and source inspection compared with `PipelineGuardTelemetry` and `McpTelemetry` implementations.
- Workaround: retain bounded CLI stdout/stderr and process exit evidence separately.

## Resolution and QA retest

- Root cause: observability was implemented for API/MCP but the architecture statement includes unimplemented CLI/Core signals.
- Fix revision: none.
- Focused retest: PG-OBS-001 with console or OTLP export after adding instrumentation.
- Regression impact: PG-CLI-001 through PG-CLI-005 and Unreal orchestration timeout/error paths.
