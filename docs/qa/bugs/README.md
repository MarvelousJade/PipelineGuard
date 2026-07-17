# Defect register

| ID | Summary | Status | Severity | Priority | Source |
| --- | --- | --- | --- | --- | --- |
| [PG-001](PG-001-editor-filter-export-gap.md) | Editor panel lacks documented filtering and JSON export controls | Open | Medium | P2 | Source review; runtime retest pending |
| [PG-002](PG-002-incomplete-openapi-document.md) | Advertised OpenAPI 3.1 operations omit required response definitions | Open | Medium | P2 | Source-confirmed; validator/runtime retest pending |
| [PG-003](PG-003-valid-json-misclassified.md) | CLI labels a syntactically valid contract failure as invalid JSON | Open | Low | P2 | Reproduced in PG-RUN-2026-07-16-01 |
| [PG-004](PG-004-unreal-warning-threshold.md) | Raw Unreal commandlet cannot honor a warning failure threshold | Open | High | P1 | Source-confirmed; warning-only asset retest pending |
| [PG-005](PG-005-cli-telemetry-gap.md) | CLI lacks the traces and metrics claimed by architecture | Open | Medium | P2 | Source-confirmed; runtime collector retest pending |

`Open` means the observed contract/implementation gap has not been fixed. None claims a Jira key or a production occurrence. Source-confirmed reports explicitly retain their runtime-reproduction gap. Use the [template](_template.md) for new execution findings, and update the original report with fix and retest evidence rather than creating an unlinked “fixed” note.
