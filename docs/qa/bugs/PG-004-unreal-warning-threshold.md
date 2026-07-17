# PG-004 — Raw Unreal commandlet cannot honor a warning failure threshold

| Field | Value |
| --- | --- |
| Status | Open |
| Severity / priority | High / P1 |
| Component / version | Unreal commandlet/runner / revision `641cf8b` product sources |
| Source case / run | PG-UE-003 / source review; runtime reproduction pending |
| Environment | Source-confirmed; requires `UE56-WIN` and a warning-only fixture for runtime retest |
| Reproducibility | Static inspection: 1/1 |

## Preconditions

The report contract states that the selected failure threshold must be evaluated consistently by the editor, commandlet, and CLI. Prepare a profile/run containing a warning but no errors and select `warning` as the failure threshold.

## Steps to reproduce

1. Inspect the Unreal settings/config type for a failure-threshold field.
2. Inspect `FPipelineGuardRunner::Run` and `UPipelineGuardCommandlet::Main` exit selection.
3. At runtime, execute the raw commandlet against a warning-only fixture, then run the .NET `validate` wrapper with `--fail-on warning`.

## Expected result

Both the raw commandlet and .NET wrapper return validation-failed exit `1` for the warning-only report at a warning threshold.

## Actual result

No Unreal failure-threshold configuration exists. The runner and commandlet hard-code validation failure to `GetErrorCount() > 0`, so a warning-only raw commandlet run returns `0`. The .NET wrapper separately reevaluates the report and can return `1`, creating surface disagreement.

## Impact, evidence, and workaround

- Impact: CI invoking the raw commandlet can pass content that the configured .NET workflow blocks, violating editor/commandlet/CLI parity.
- Evidence: `PipelineGuardRunner.cpp`, `PipelineGuardCommandlet.cpp`, and the absence of a threshold field under the Unreal public settings/types at revision `641cf8b`.
- Workaround: invoke through the .NET CLI with `--fail-on warning` and use its public exit code.

## Resolution and QA retest

- Root cause: Unreal exit selection is hard-coded to error count while threshold evaluation exists only in the portable consumer.
- Fix revision: none.
- Focused retest: PG-UE-003 with clean, warning-only, and error fixtures at each supported threshold.
- Regression impact: PG-CLI-002 and PG-UE-002 parity.
