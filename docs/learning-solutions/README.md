# Facilitator material — spoilers

**Do not open this during initial investigation.** Learner instructions are in `docs/learning/README.md`. These diagnoses describe intentionally injected practice defects, not production incidents or learner experience. Example hypotheses below are teaching material, not the learner's actual reasoning.

An assistant should provide only the requested hint level, not this entire file. Ask for investigation notes before preparing interview stories.

## Exercise 1 — checkpoint 0062219

### Progressive hints (release one at a time)

1. Compare the two report completion instants independently of the API. Is the returned payload wrong, or only the list order?
2. A formatted date can preserve information without being safe to compare lexically. Check whether stored metadata has one consistent representation.
3. Trace the timestamp formatting helper used by both writes and legacy normalization. Does formatting itself convert the instant to UTC?

### Diagnosis and fix

The injected edit removed `ToUniversalTime()` from `FormatUtc`. Round-trip format `O` preserves the original offset; it does not normalize it. SQLite then compares local-clock strings. Legacy normalization calls the same helper, so restart cannot repair the representation. This is an ordinary “formatted date means UTC” implementation mistake.

Restore the helper in `src/PipelineGuard.Api/SqliteReportStore.cs`:

```csharp
timestamp.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
```

Don't reverse the order in the UI or rewrite report JSON. Keep `completed_utc DESC, run_id ASC`; the tie-breaker is needed independently of offsets.

### Verified repair

Faulty checkpoint: 3 failed / 3 passed in the SQLite suite; the real HTTP smoke failed expected run ID 2 versus actual run ID 1 after restart. A temporary repair was applied and tested, not committed over the checkpoint:

```console
dotnet test PipelineGuard.sln -c Release
node scripts/verify-validation.mjs
```

Observed: all 55 validation tests and smoke passed. The checkpoint remains reproducible. This proves local UTC ordering, stable ties, tick precision, duplicate preservation and legacy repair; it does not prove snapshot pagination during concurrent insertion.

## Exercise 2 — checkpoint 4359e3d

### Progressive hints (release one at a time)

1. Count stability and document immutability are different properties. Which one does the older in-memory test verify?
2. Compare the persistence request with the intended create-if-absent semantics, not just the chosen document ID.
3. Elasticsearch's document PUT is normally an upsert. Which request option changes it into create-only and makes a replay a conflict?

### Diagnosis and fix

The injected edit removed `?op_type=create` from the URL in `ElasticsearchCrashIndex.TryCreateAsync`. A stable ID now targets the same document but replaces it, so count stays constant while data changes. Returning “duplicate” locally would hide the mutation, not fix it.

Restore:

```csharp
$"{_options.CrashIndex}/_doc/{document.CrashId:D}?op_type=create"
```

Keep the existing `409 Conflict → Duplicate` handling. The first indexed effect is immutable; a crash between successful indexing and RabbitMQ acknowledgment can safely redeliver. Different payloads with one ID use first-write-wins; the system does not compare payload hashes or alert on ID reuse.

### Verified repair

Faulty checkpoint: 1 failed / 27 passed in the crash suite; stored processed time changed from 12:05 to 12:06 in the synthetic replay. The in-memory processor duplicate test still passed. Temporary repair:

```console
dotnet test PipelineGuard.CrashIntelligence.sln -c Release
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore --warnaserror
node scripts/verify-validation.mjs
```

Observed: all 28 crash tests, both zero-warning builds and validation smoke passed. The temporary repair was removed before returning to `shaoyu/rework`, preserving the faulty branch head. The handler models create/conflict semantics; it does not prove real broker acknowledgments or cluster behavior. Docker was unavailable.

## Discussion after investigation

Ask the learner what evidence eliminated a cache/UI hypothesis, why a unique key was insufficient for immutable replay, and what real infrastructure experiment would strengthen each result. Build stories only from their recorded commands, diff and reasoning, not the examples or facilitator diagnosis.
