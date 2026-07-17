# Regression execution `<run-id>`

## Configuration

| Field | Value |
| --- | --- |
| Revision/tag | `<full commit and dirty state>` |
| Suite revision | `1.0` |
| Executor | `<person or agent; do not invent>` |
| Started/completed UTC | `<timestamps>` |
| Environment | `<environment ID and versions>` |
| Build provenance | `<command, result, artifact hashes>` |
| Test data | `<paths and hashes>` |
| Scope/deviations | `<included cases and deviations>` |

## Results

| Case | Status | Actual result | Evidence | Defect |
| --- | --- | --- | --- | --- |
| `<case ID>` | `<Passed/Failed/Blocked/Not Run>` | `<bounded observation>` | `<sanitized path/link>` | `<ID or none>` |

## Summary and decision

| Passed | Failed | Blocked | Not Run |
| ---: | ---: | ---: | ---: |
| 0 | 0 | 0 | 0 |

- Exit decision: `<Pass/Pass with risk/Blocked/Fail>`
- Residual risk: `<explicit risk>`
- Follow-up: `<owner/action or none>`
