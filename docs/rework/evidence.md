# Rework verification evidence

Local Linux session, 2026-09-30 UTC; .NET SDK 8.0.423, runtime 8.0.29. All results here were actually run in this rework. Original Windows/Unreal and Compose results in `docs/verification.md` are historical, not reruns.

## Baseline at 3a37303

- Both solutions restored and built Release with `--warnaserror`: zero warnings/errors.
- Validation tests: 49 passed. Crash tests: 9 passed.
- `npm ci --prefix dashboard` and `npm run build --prefix dashboard`: passed, 28 transformed modules.
- `docker info`: blocked, cannot connect to `/var/run/docker.sock`.

## Increment 1: history ordering and end-to-end smoke

Reproduction: `dotnet test tests/PipelineGuard.Api.Tests -c Release` before the fix: **3 failed / 3 passed**. Offset order, tied pages and legacy-offset startup failed. A test-fixture compilation error (missing required summary fields) was corrected before collecting this behavioral result.

After fix:

```console
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.sln -c Release --no-build
node scripts/verify-validation.mjs
```

Observed: zero build warnings/errors; **49 core + 6 SQLite tests passed**, none skipped. HTTP smoke passed: two 201 uploads, 409 duplicate without replacement, 400 invalid contract, 404 missing run, process restart, unchanged retrieved report, chronological offset pages and only two persisted runs.

The script builds no components; run the Release build first. It uses an ephemeral loopback port, temporary SQLite file, its own API child, and cleans up. Requires Node 22 and .NET 8. Logs from this session are ignored under `artifacts/rework/`; recorded outcomes above are concise, not production measurements. CI now runs the same smoke on Windows/Linux, but hosted CI was not executed in this session.

## Increment 2: Elasticsearch HTTP adapter

Before fix: the crash suite had **6 failures / 19 passes**. The failing new cases showed non-404 HEAD still caused PUT, timeout escaped the transient contract, and pre-cancelled calls still reached the handler. A local-name compilation conflict introduced while fixing HEAD handling was corrected in the same uncommitted increment.

```console
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore --warnaserror
dotnet test tests/PipelineGuard.CrashIntelligence.Tests -c Release
```

Observed: zero build warnings/errors; **28 crash tests passed**, none skipped (9 original plus 19 adapter cases). Synthetic HTTP checks cover immutable replay, request path/body, 408/429/500/503, permanent 400, network failure, timeout, pre/in-flight caller cancellation, existing/missing indexes and concurrent index creation. No real Elasticsearch server or RabbitMQ was contacted.

## Follow-up fix: distinct CI test evidence

The final verification reproduced `WARNING: Overwriting results file` when the expanded validation solution used one fixed TRX filename for both test assemblies. This was introduced by adding a second test project, not an old product defect. CI now tests the two validation projects with distinct `core.trx` and `api.trx` filenames. Local execution of those exact commands passed 49 and 6 tests and produced both files (70,130 and 9,860 bytes in this run), with no overwrite warning. Hosted CI remains unrun.

## Working baseline final gates

Both solutions restored with `NuGetAudit=true` / `NuGetAuditMode=all` and built Release with `--warnaserror`: zero warnings/errors. Full suites: **49 + 6 + 28 = 83 passed**, none skipped. HTTP restart smoke passed again. CLI fixtures returned expected exits: clean 0, error 1, malformed/type-invalid 2, clean→error comparison 1.

Both `dotnet list ... package --vulnerable --include-transitive` commands reported no vulnerable packages for the current source; `npm audit --prefix dashboard --audit-level=high` reported zero vulnerabilities. Dashboard production build and `node --check` for both verification scripts passed. These are local/current-feed results, not future security guarantees.

No Unreal build, broker/search integration, dashboard browser interaction, production deployment or hosted CI was performed. The solution's existing CRLF line endings were retained; diff whitespace checks use `git -c core.whitespace=cr-at-eol diff --check`.

## Learning checkpoints (intentional failures)

`shaoyu/learning` branches from verified baseline `0ff1881`. Checkpoints are preserved; no existing history was rewritten.

| Checkpoint | Observed faulty result | Temporarily repaired result |
| --- | --- | --- |
| `0062219` history incident | 3/6 SQLite cases failed; HTTP smoke failed order after restart | all 55 validation tests + HTTP smoke passed |
| `4359e3d` replay incident | 1/28 crash cases failed; 27 passed | all 28 crash tests + both zero-warning builds + HTTP smoke passed |

The second checkpoint restores the first defect before introducing its own. Repair verification was actually run with targeted edits, then faulty source restored and the working branch reselected. Simulated reports/consequences are learner practice; these failure counts are actual local observations. Diagnosis and hints live separately under `docs/learning-solutions/`, which is absent from the original learning checkpoints. The learner has not completed an investigation, so no personal debugging story is claimed.

After returning to `shaoyu/rework`, both Release builds, all **83 tests**, and the HTTP restart smoke were rerun and passed. Final diff and documentation links were inspected. Only agent-owned source/tests/docs/CI changes were committed; generated logs/results remain ignored. No push or history rewrite was performed.
