# Jira and TestRail field mapping

These are vendor-compatible workflow examples for a portfolio repository. No live Jira or TestRail instance, project key, account, screenshot, or historical execution is claimed.

## TestRail-style case import

| Repository field | TestRail-style field | Rule |
| --- | --- | --- |
| Case heading ID | References or custom Case ID | Keep `PG-<surface>-nnn` stable across imports |
| Group heading | Section | CLI, API, MCP, Observability, Unreal, Performance |
| Case title | Title | Outcome-focused and unique |
| Priority/type | Priority, Type | Map P0/P1/P2/P3; use Functional, Security, Regression, Performance, or Usability |
| Risk | References/custom Requirement | Link risk/requirement rather than copying a vague feature name |
| Preconditions/data | Preconditions | Include environment and immutable fixture |
| Step rows | Steps (separated) | One action per row |
| Expected-result column | Expected Result | One observable result for each step |
| Automation reference | Automation Type/References | `Manual`, `Automated`, or `Partial`; never infer Pass from this field |

Suggested CSV import order: Section, Title, Type, Priority, Preconditions, Steps, Expected Result, References, Automation Status. Perform a two-case trial import first because custom field names and multiline handling vary by instance.

## Jira-style defect mapping

| Repository field | Jira-style field | Rule |
| --- | --- | --- |
| Defect heading | Summary | Prefix with the component only if the project convention requires it |
| Status | Status/Resolution | New → Triaged → In Progress → Ready for QA → Closed; failed retest → Reopened |
| Severity | Severity custom field | User/system impact; do not substitute delivery priority |
| Priority | Priority | P0 through P3 delivery order |
| Component/version | Component/s, Affects Version/s | Use the tested revision or release |
| Source case/run | Linked issue, Test, or remote link | Preserve case and execution traceability |
| Environment | Environment | Only relevant reproducibility details; sanitize local usernames/paths |
| Steps/expected/actual | Description | Keep expected and actual separate |
| Evidence | Attachment/link | Prefer bounded text/CSV plus logs; a database alone is insufficient |
| Fix/retest | Fix Version/s, development link, comment | Link the fix revision, focused retest, and affected regression run |

Suggested labels are bounded values such as `qa`, `cli`, `api`, `mcp`, `unreal`, `security`, and `performance`. Never use free-form asset paths or run IDs as labels.

## Status mapping

| Repository execution | TestRail-style result | Jira action |
| --- | --- | --- |
| Passed | Passed | Close linked defect only after focused retest and required regression pass |
| Failed | Failed | Create/link a defect or document why an existing defect applies |
| Blocked | Blocked | Record the exact prerequisite; do not open a product bug for a lab dependency gap |
| Not Run | Untested | Record scope decision; do not include in pass-rate numerator |

## Traceability example

```text
API rejects duplicate run IDs
  -> PG-API-003
  -> PG-RUN-<date>-<sequence>
  -> PG-<defect> if actual != expected
  -> fix commit / PR
  -> PG-API-003 focused retest
  -> PG-API-002, PG-API-004, PG-MCP-002 regression
```

## Triage and closure checklist

1. Reproduce against the recorded revision and data; separate environment failures from product behavior.
2. Confirm component, severity, priority, and affected scope during triage.
3. Link duplicates to one canonical defect without discarding their environments/evidence.
4. A developer records diagnosis and fix revision; QA independently records actual retest behavior.
5. Close only when focused retest and required regression pass, or use an explicit non-fixed resolution with rationale.
