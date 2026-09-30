# Agent investigation: unreliable history

This is the agent's autonomous completion of **practice exercise 1**, not the user's personal investigation or a production incident. Separate solutions were consulted; this was not blind discovery. Fresh local Linux checks ran on 2026-09-30 UTC (.NET 8.0.423, Node 22.14.0).

## Symptom and reproduction

Checkpoint `0062219` deliberately removed UTC conversion from the timestamp helper. An older run appears first after restart although report bodies remain intact.

```console
git switch -c shaoyu/solve-history 0062219
dotnet test tests/PipelineGuard.Api.Tests -c Release
node scripts/verify-validation.mjs
```

Actual result: **3 failed / 3 passed** in the SQLite suite (instant ordering, tied pages, legacy normalization). HTTP smoke exited 1: expected run ID ending `002`, got `001` at the post-restart list assertion. Upload/retrieve steps had succeeded before that assertion.

Independent instant comparison with Python `datetime.fromisoformat(...).astimezone(timezone.utc)` confirmed `12:00+03:00` is `09:00Z`, earlier than `10:00Z`. This supports a representation/order diagnosis, not a UI or cache explanation.

## Root cause, alternatives and fix

`FormatUtc` used `ToString("O")`, which retains an offset rather than converting to UTC. SQLite's lexical sort therefore compared local clocks. The same helper is used by legacy metadata normalization, so restart could not repair it.

- **Chosen:** restore `ToUniversalTime()` before invariant round-trip formatting. One helper repairs new writes and legacy metadata while retaining seven fractional digits.
- **Alternative:** convert dates inside SQL; usual SQLite date functions lose submillisecond precision and add query work.
- **Alternative:** integer ticks plus versioned schema migration; reasonable at larger scale, unnecessary for this focused defect.
- **Rejected:** reverse UI sorting or rewrite uploaded JSON; neither establishes correct metadata chronology while preserving the original contract.

The fix preserves `completed_utc DESC, run_id ASC`, transactional legacy normalization, original report JSON and duplicate-ID behavior. Existing committed regression tests already demonstrate the defect; no equivalent test or fix commit was copied from another branch.

## Passing-after evidence

```console
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.sln -c Release --no-build
node scripts/verify-validation.mjs
```

Fresh results: build zero warnings/errors; **49 core + 6 SQLite tests passed**, none skipped; real loopback HTTP/SQLite smoke passed upload, duplicate preservation, rejection, missing run, restart and chronological pagination. Logs are ignored under `artifacts/learning-completion/history-*.log`.

Limits: stable order is for a fixed dataset, not snapshot pagination under concurrent inserts. Startup metadata scanning has not been benchmarked at large scale; original report offsets remain intentionally unchanged.
