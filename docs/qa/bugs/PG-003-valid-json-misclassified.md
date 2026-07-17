# PG-003 — CLI labels a syntactically valid contract failure as invalid JSON

| Field | Value |
| --- | --- |
| Status | Open |
| Severity / priority | Low / P2 |
| Component / version | CLI diagnostics / apphost tested with revision `641cf8b` product sources |
| Source case / run | PG-CLI-003 / PG-RUN-2026-07-16-01 |
| Environment | Windows 10.0.26200 x64; .NET 8 CLI apphost |
| Reproducibility | 2/2, including the recorded manual run |

## Preconditions

Use tracked `samples/malformed-report.json`. The repository documentation explicitly states that this fixture is syntactically valid JSON but invalid as a PipelineGuard report.

## Steps to reproduce

1. Run `PipelineGuard.Cli summary samples/malformed-report.json --minimum info --fail-on error`.
2. Read standard error and record the exit code.

## Expected result

The CLI identifies a report-contract/type validation failure (for example, `invalid report`) and exits `2`.

## Actual result

The CLI exits `2` but prints `invalid JSON: The JSON value could not be converted to System.Guid...`. The JSON text itself is valid; deserialization failed while mapping a field to the strict report type.

## Impact, evidence, and workaround

- Impact: a tester may investigate JSON syntax instead of the invalid report fields, increasing triage time and weakening the advertised distinction between syntax and contract failures.
- Evidence: PG-RUN-2026-07-16-01 bounded output and the `JsonException` catch label in `src/PipelineGuard.Cli/Program.cs`.
- Workaround: validate JSON syntax separately, then treat conversion-path diagnostics as report-contract failures.

## Resolution and QA retest

- Root cause: one broad `JsonException` handler labels both parser syntax errors and JSON-to-contract conversion errors as `invalid JSON`.
- Fix revision: none.
- Focused retest: PG-CLI-003 with one syntactically broken payload and one syntactically valid/type-invalid payload.
- Regression impact: PG-API-004 because the API uses the same serializer and similarly broad diagnostic categories.
