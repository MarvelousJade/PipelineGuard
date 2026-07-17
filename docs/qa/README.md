# PipelineGuard QA evidence

This directory turns the project's verification claims into a reviewable QA lifecycle. It separates what should be tested from what was actually executed, links failures to defect reports, and keeps performance observations distinct from product SLAs.

## Evidence map

| Artifact | Purpose |
| --- | --- |
| [Manual test plan](manual-test-plan.md) | Scope, risks, environments, cadence, and release criteria |
| [Manual test cases](cases/manual-test-cases.md) | Reusable, project-specific procedures and expected results |
| [Executed-case CSV](test-cases.csv) | Latest actual result/status/build snapshot for import or review |
| [Regression suite](regression-suite.csv) | Requirement/risk-to-case traceability and execution cadence |
| [Execution records](executions/) | Immutable actual results for one revision and environment |
| [Defect register](bugs/README.md) | Open and closed observations linked to cases and retests |
| [Known issues](known-issues.csv) | Current ownership, status, target build, and last verification |
| [Regression report](regression-report.md) | Retest/regression disposition and residual risk |
| [Test summary](test-summary.md) | Totals, blockers, open defects, and recommendation |
| [Jira/TestRail mapping](tooling/jira-testrail-mapping.md) | Import fields and lifecycle conventions without pretending a live instance was used |
| [Performance plan](performance/performance-plan.md) | Repeatable scenarios, statistics, evidence, and baseline rules |

## Evidence rules

- A case definition is never evidence that the case passed.
- Execution statuses are `Passed`, `Failed`, `Blocked`, or `Not Run`. `Blocked` names the missing prerequisite.
- Automated results are linked as supporting coverage; they are not relabelled as manual executions.
- A defect records severity and delivery priority separately, plus expected/actual behavior and retest evidence.
- A performance capture records the binary hash, fixture hash, environment, raw samples, and calculation method. A single local capture is an observation, not an SLA.
- Unreal and BlackWood results remain `Blocked` or `Not Run` unless the named engine, content corpus, and exact revision were exercised.

## Release evidence flow

```text
risk or requirement -> case ID -> execution record -> defect -> fix -> retest -> regression decision
```

For a release candidate, copy the [execution template](executions/_template.md), execute the required rows in `regression-suite.csv`, attach sanitized evidence, update the append-only run ledger, and make the exit decision in the execution record.

These artifacts are Jira/TestRail-compatible workflow examples. No live Jira or TestRail instance, project key, user, or execution history is claimed. The current executed evidence is portable CLI work; it does not yet prove manual game-content regression, Unreal Insights profiling, multiplayer playtest coordination, or a production bug-tracker lifecycle.
