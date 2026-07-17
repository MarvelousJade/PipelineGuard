# Test summary — PG-RUN-2026-07-16-01

## Outcome

| Result | Count | Percent of 19 |
| --- | ---: | ---: |
| Passed | 5 | 26.3% |
| Failed | 0 | 0.0% |
| Blocked | 11 | 57.9% |
| Not Run | 3 | 15.8% |

All five fully executed CLI cases passed their functional exit/output expectations. The run is not a release pass: the required .NET 8 SDK and ASP.NET Core 8 runtime were absent, so the current revision could not be rebuilt and API-dependent cases could not run.

## Open defect snapshot

| Severity | Count | IDs |
| --- | ---: | --- |
| Critical | 0 | — |
| High | 1 | PG-004 |
| Medium | 3 | PG-001, PG-002, PG-005 |
| Low | 1 | PG-003 |

PG-003 was dynamically reproduced. PG-001, PG-002, PG-004, and PG-005 are source-confirmed gaps whose reports explicitly require runtime retest; they are not presented as production occurrences.

## Blockers and risks

- Exact source-to-binary provenance is missing for this run; the existing apphost hash is recorded.
- Six API, three MCP, and the observability case are blocked.
- The OpenAPI document has not been submitted to a runtime validator.
- The fixture host still provides no executed real game-content evidence in this run.
- No defect has a fix build and completed focused/regression retest lifecycle.
- The single CLI performance capture is a local observation, not a three-run baseline or a client/GPU/server/network game profile.
- No live Jira/TestRail project, sanitized export, multiplayer playtest, console/mobile QA, or BlackWood integration is claimed.

## Recommendation

**Blocked for a new release claim; acceptable as a transparent portfolio QA-work sample.** The existing verified release remains the product evidence. For the game-QA target, next run 8–12 real UE fixture cases, fix and retest at least one defect, prove editor-commandlet parity, and retain a representative Unreal Insights or equivalent game-performance capture.
