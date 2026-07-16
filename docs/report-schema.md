# Validation report v1

The canonical machine-readable contract is [`../schemas/validation-report-v1.schema.json`](../schemas/validation-report-v1.schema.json). This document defines its semantics and the invariants that JSON Schema alone cannot enforce.

## Media and encoding

- UTF-8 JSON
- Recommended media type: `application/json`
- `schemaVersion` is exactly `1.0`
- Timestamps use RFC 3339/JSON Schema `date-time` format in UTC
- Durations are non-negative integer milliseconds

## Top-level fields

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Selects this versioned contract. Unsupported versions fail closed. |
| `runId` | UUID assigned once to the run and reused during ingestion. |
| `toolVersion` | Version of the producing plugin/tool. |
| `startedUtc`, `completedUtc` | Inclusive execution window. |
| `project` | Human-readable project name, not a filesystem path. |
| `engineVersion` | Producing Unreal version, or the fixture producer's declared target. |
| `profile` | Stable validation configuration name. |
| `scope` | One or more Unreal content roots or other bounded validation scopes. |
| `summary` | Counts and wall-clock duration. |
| `findings` | Deterministically ordered actionable results. |

## Summary semantics

`assetsScanned` counts unique assets presented to the runner after scope filtering. `rulesRun` counts distinct enabled rule IDs, not rule/asset invocations.

For each non-suppressed finding, increment exactly one of `errors`, `warnings`, or `infos` according to its severity. A suppressed finding increments only `suppressed`, even though it retains its original severity. Therefore:

```text
errors + warnings + infos + suppressed == findings.length
```

## Finding identity

`id` must be stable for the same logical problem across repeated runs. A suitable input is the normalized tuple:

```text
ruleId | assetPath | optional-stable-property-key
```

Hashing that tuple is acceptable. Do not include display-message text, timestamps, run IDs, machine names, or array positions. A rule that can report more than one finding for the same asset must add a stable property/subrule key. Stable IDs let a user compare reports and track suppressions even when wording improves.

`assetPath` uses an Unreal package/object path, not an absolute host filesystem path. `message` explains the observed problem. `remediation` gives the next concrete action. `metadata` is optional bounded diagnostic context and must not contain credentials or personal data.

When `suppressed` is `true`, `suppressionReason` is required. Suppression does not delete or hide the finding from the report.

## Deterministic ordering

Producers sort findings by:

1. `assetPath` using ordinal comparison
2. `ruleId` using ordinal comparison
3. `message` using ordinal comparison
4. Stable `id` using ordinal comparison as a final tie-breaker

Suppression does not change ordering. Deterministic output makes editor/commandlet parity and report diffs testable.

## Cross-field invariants

Consumers enforce these after schema validation:

- `completedUtc` is not earlier than `startedUtc`.
- Summary severity/suppression totals equal `findings.length`.
- Summary totals agree with each finding's `severity` and `suppressed` value.
- Finding IDs are unique within a report.
- A producer emits no duplicate `(ruleId, assetPath, id)` tuple.
- The report's configured failure threshold is evaluated consistently by editor, commandlet, and CLI.

## Limits

Version 1 allows at most:

- 20,000 findings per report
- 20 metadata entries per finding
- 2,048 characters in a message or remediation
- 512 characters in an asset path

The local API rejects request bodies above 1,048,576 bytes before parsing. The MCP API client rejects responses above 1,100,000 bytes; list tools cap a page at 100 items and never return an entire large report in one response.

## Exit behavior

| Code | Surface | Contract |
| ---: | --- | --- |
| `0` | CLI and Unreal | Validation completed and no non-suppressed finding met the selected threshold. |
| `1` | CLI and Unreal | Validation completed and at least one non-suppressed finding met the selected threshold. |
| `2` | CLI and Unreal | Arguments, configuration, or report contract were invalid. |
| `3` | CLI and Unreal | Unreal launch, timeout, cancellation, or another internal operation failed. The CLI also maps raw commandlet exit `4` here. |
| `4` | Raw Unreal only | The commandlet completed validation but could not write its JSON report. |

Exit `1` is a successful tool execution with a failing validation result. It should not be reported as a crash.

## Fixture intent

- [`../samples/clean-report.json`](../samples/clean-report.json) conforms and produces exit `0` at the `error` threshold.
- [`../samples/error-report.json`](../samples/error-report.json) conforms and produces exit `1` at the `error` threshold.
- [`../samples/malformed-report.json`](../samples/malformed-report.json) is syntactically valid JSON but intentionally violates this contract and produces exit `2`.

## Evolution

Version-specific schemas are strict. Adding or renaming a field requires a new schema version and migration tests. Consumers reject unsupported versions rather than guessing. A later version may define an explicit compatibility policy; version 1 does not imply one.
