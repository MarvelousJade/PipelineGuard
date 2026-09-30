# Agent investigation: incident replay

This is the agent's autonomous completion of **practice exercise 2**, not the user's personal investigation or a production incident. Separate solutions were consulted. Fresh local Linux checks ran on 2026-09-30 UTC (.NET 8.0.423, Node 22.14.0).

## Symptom and reproduction

Checkpoint `4359e3d` deliberately changed create-only persistence into a normal document PUT. Simulated report: replay keeps one document ID but changes audit content. The actual reproduction uses a synthetic HTTP handler, not a live cluster.

After merging the history solution into `shaoyu/learning`, source was identical to `4359e3d` (confirmed with `git diff 4359e3d -- src`). Ran:

```console
dotnet test tests/PipelineGuard.CrashIntelligence.Tests -c Release --filter Replay_PreservesOriginalDocumentThroughHttpAdapter
dotnet test tests/PipelineGuard.CrashIntelligence.Tests -c Release --no-build
```

Actual result: focused test **1 failed / 0 passed**; full crash suite **1 failed / 27 passed**. Stored processing time changed from `12:05Z` to `12:06Z`; the test also checks preservation of the original error message and the duplicate outcome. The older in-memory processor duplicate test passed, localizing the gap to HTTP persistence semantics rather than the processor's identity selection.

## Root cause, alternatives and fix

The URL lacked `?op_type=create`. Elasticsearch's normal document PUT can replace an existing document. A stable ID prevents a second ID, but does not prevent mutation. The handler models that difference using the actual adapter URI/body.

- **Chosen:** restore create-only writes and retain `409 Conflict → Duplicate`. The first indexed document wins atomically at the storage boundary.
- **Alternative:** GET-before-PUT; adds latency and a race between competing workers unless coupled with a storage precondition.
- **Alternative:** optimistic versioning or payload-hash conflict detection; useful for intentional updates or detecting reused IDs, but more machinery than immutable ingestion requires.
- **Rejected:** return “duplicate” after a normal PUT or deduplicate only in memory; neither protects the stored effect across replay/restart.

The fix changes only the write URL. Index creation only after 404, timeout/transient classification, caller cancellation and HEAD disposal are unchanged. Existing committed regression tests already cover the behavioral contract; no equivalent fix commit was cherry-picked or recreated twice.

## Passing-after evidence

```console
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore --warnaserror
dotnet test tests/PipelineGuard.CrashIntelligence.Tests -c Release --no-build --filter Replay_PreservesOriginalDocumentThroughHttpAdapter
dotnet test PipelineGuard.CrashIntelligence.sln -c Release --no-build
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.sln -c Release --no-build
node scripts/verify-validation.mjs
```

Fresh results: focused regression **1 passed**; all **28 crash + 55 validation tests passed**, none skipped; both Release builds zero warnings/errors; HTTP/SQLite smoke passed. Logs are ignored under `artifacts/learning-completion/replay-*.log`.

Limits: synthetic HTTP evidence does not verify real Elasticsearch mappings, broker acknowledgments or recovery. First-write-wins does not compare conflicting payloads or alert on ID reuse. A worker dying after indexing but before acknowledgment is why create-only replay matters; that operational scenario was not freshly exercised here.
