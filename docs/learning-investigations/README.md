# Autonomous exercise completion and local main integration

The **agent**, not the user, reproduced, diagnosed and fixed both intentional practice defects. Separate solutions were consulted and checked against fresh failures. These are not production incidents or claims about the user's personal experience.

## Checklist and history

- [x] Inspect status, instructions and branch/tag relationships; initial working tree clean.
- [x] Reproduce each faulty checkpoint, confirm regression failure, apply one focused fix, rerun checks and inspect its diff.
- [x] Preserve the rework and both individual fix commits through non-squash merges into local `main`.
- [x] Run fresh final portable checks, record blockers and update interview explanations.

Initial refs: `main` / `origin/main` at `3a37303`, `shaoyu/rework` at `4c2b37e`, `shaoyu/learning` at `4359e3d`. Rework and learning share ancestor `0ff1881`; `main` was their ancestor. No solution branches existed; only the existing release tag `v1.0.0` existed and remains unchanged.

| Defect | Faulty checkpoint | Dedicated fix commit | Agent evidence |
| --- | --- | --- | --- |
| History chronology | `0062219` | `d8a0a8f` on `shaoyu/solve-history` | [Investigation](history.md): 3/6 store failures and failing HTTP order → 55 validation tests and smoke pass |
| Immutable replay | `4359e3d` | `1d5a25c` on `shaoyu/learning` | [Investigation](replay.md): focused failure / 1 of 28 crash failures → 28 crash tests pass |

`b75c63a` merged the history fix into learning. Practice commit `4359e3d` had already restored the history helper while injecting replay; Git recognized the identical correction, so this merge added the investigation document without undoing or reapplying source changes. There was no existing dedicated solution commit to reuse; neither fix was duplicated by cherry-pick.

`132e3af` merged rework into local `main` with `--no-ff`. The final merge containing this document merges corrected learning with `--no-ff`, preserving both fix commits, both original faulty checkpoints and all rework commits in ancestry. No conflict required resolution. Final runtime source, tests, scripts, dashboard and CI match verified rework `4c2b37e`; only completion documentation is added/updated. Thus SQLite UTC chronology, stable ties, legacy normalization and Elasticsearch failure/cancellation handling are retained; no intentional defect remains in the final tree.

No reset, rebase, squash, tag rewrite, branch rewrite or push was performed. Original checkpoint hashes remain usable for practice. `shaoyu/rework` remains at `4c2b37e`; the corrected learning head is `1d5a25c`.

## Fresh final verification

Run on the integrated `main` merge tree on 2026-09-30 UTC, Linux, .NET SDK 8.0.423 / runtime 8.0.29, Node 22.14.0. These checks were run again for this completion; earlier rework/Unreal/Compose records are not substituted.

```console
dotnet restore PipelineGuard.sln -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet test tests/PipelineGuard.Core.Tests -c Release --no-build --logger "trx;LogFileName=core.trx" --results-directory artifacts/learning-completion/TestResults
dotnet test tests/PipelineGuard.Api.Tests -c Release --no-build --logger "trx;LogFileName=api.trx" --results-directory artifacts/learning-completion/TestResults
node scripts/verify-validation.mjs

dotnet restore PipelineGuard.CrashIntelligence.sln -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.CrashIntelligence.sln -c Release --no-build --logger "trx;LogFileName=crash.trx" --results-directory artifacts/learning-completion/TestResults
npm ci --prefix dashboard
npm run build --prefix dashboard
npm audit --prefix dashboard --audit-level=high
dotnet list PipelineGuard.sln package --vulnerable --include-transitive
dotnet list PipelineGuard.CrashIntelligence.sln package --vulnerable --include-transitive
node --check scripts/verify-validation.mjs
node --check scripts/crash-intelligence/verify.mjs
```

| Check | Fresh observed result |
| --- | --- |
| Both Release builds | Zero warnings/errors |
| Tests | **83 passed**: 49 core, 6 SQLite, 28 crash; none failed/skipped |
| Real loopback HTTP/SQLite smoke | Passed ingest, duplicate preservation, invalid/missing report, restart and chronological pages |
| Dashboard install/build | Passed; 28 modules transformed |
| npm audit | Zero reported vulnerabilities |
| Both NuGet transitive audits | No vulnerable packages reported by current configured source |
| Verification script syntax | Both passed |

Distinct TRX files and console logs remain ignored under `artifacts/learning-completion/`. Whitespace checked with `git -c core.whitespace=cr-at-eol diff --check`; original solution CRLF endings retained. Documentation links and final diff inspected.

## Unavailable/unrun checks and remaining limits

- Fresh `docker info --format '{{.ServerVersion}}'` failed: cannot connect to `/var/run/docker.sock`. Compose CLI 2.33.1 is installed, but no daemon is accessible. Real Elasticsearch/RabbitMQ integration, recovery and benchmark were **not run**. Synthetic adapter tests do not establish live-cluster behavior.
- Unreal checks were **not run**: no Unreal source, rule contract or commandlet was changed in this completion. Previous engine results remain historical, not fresh evidence.
- Browser interaction was **not run**: no dashboard UI/search code changed; production build was rerun. Hosted CI and production deployment were not run; nothing was pushed.
- UTC metadata startup scanning and offset pagination retain their local-development limits. Immutable replay is first-write-wins, without payload-hash conflict alerts. Production auth, multi-tenant isolation and existing QA gaps remain as documented in the [rework guide](../rework/README.md).
