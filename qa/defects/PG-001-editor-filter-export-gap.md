# PG-001 — Editor panel lacks documented filtering and JSON export controls

| Field | Value |
| --- | --- |
| Status | Open |
| Severity / priority | Medium / P2 |
| Component / version | Unreal editor panel / revision `b90675b` |
| Source case / run | PG-UE-001 / source review (runtime execution pending) |
| Environment | Source-confirmed; UE runtime reproduction not performed in this review |
| Reproducibility | Static inspection: 1/1 |

## Preconditions

The editor adapter behavior described in `docs/architecture.md` and the filtering step in `docs/demo-plan.md` are treated as the current UX requirement.

## Steps to reproduce

1. Read the Unreal editor adapter description in `docs/architecture.md` and the Unreal demo procedure in `docs/demo-plan.md`.
2. Inspect `SPipelineGuardPanel::Construct` and the panel header for user actions and state.

## Expected result

The panel provides controls to filter validation findings and export the current run as JSON, in addition to running validation, opening settings, displaying results, and navigating to an asset.

## Actual result

The panel constructs `Run Validation` and `Settings` buttons, summary text, and an `SListView` with double-click navigation. No filter/search widget, filter state, export button, or export handler exists in the panel source.

## Impact, evidence, and workaround

- Impact: the documented artist workflow cannot isolate a target finding or export the editor run from the panel; the planned demo step cannot be completed as written.
- Evidence: `docs/architecture.md`, `docs/demo-plan.md`, and `unreal/PipelineGuard/Source/PipelineGuard/Private/SPipelineGuardPanel.cpp` at revision `b90675b`.
- Workaround: run the commandlet with `-Report=<path>` and inspect/filter the JSON through the CLI.

## Resolution and QA retest

- Root cause: pending implementation triage.
- Fix revision: none.
- Focused retest: run PG-UE-001 on `UE56-WIN` after implementation.
- Regression impact: PG-UE-002 and PG-UE-003 should confirm that UI work does not fork runner/report behavior.
