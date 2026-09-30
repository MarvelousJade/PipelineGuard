# Debugging practice — learner instructions only

These are **invented practice incidents**, not defects discovered in production. Consequences below are hypothetical. The working `shaoyu/rework` branch passes; `shaoyu/learning` deliberately contains faulty checkpoints. No Docker or Unreal is required.

Before switching, inspect `git status` and preserve your changes. Never reset a dirty tree. Create your own solution branches; do not change the original checkpoints or push them. Each checkpoint has only its own defect: exercise 2 restores exercise 1 before introducing a different mistake.

Do not read `docs/learning-solutions/`, `docs/rework/decisions.md`, the checkpoint commit diff or regression-test implementation before you investigate. They can reveal causes. You can run the supplied checks without opening their source. Ask for hints only when you want them; hints will be progressive.

## Exercise 1 — history seems unreliable

**Simulated report:** “After importing reports from two build agents, an older run appears above the more recent run. Refresh and API restart don't help. The run payloads themselves still look correct.”

**Hypothetical consequence:** a build engineer could compare against the wrong baseline or incorrectly assume the latest run is clean.

Start from the first checkpoint:

```console
git switch -c shaoyu/solve-history shaoyu/learning~1
dotnet restore PipelineGuard.sln
dotnet build PipelineGuard.sln -c Release --no-restore
node scripts/verify-validation.mjs
```

Expected observable symptom: the smoke fails a run-order assertion after restart, although uploads and retrieval work. The build itself should succeed.

Investigate the public inputs, upload response, stored report, list response and restart behavior. Make a small independent reproduction with two reports before changing code. Separate your observations from hypotheses. Don't assume the browser or a cache is at fault.

Once you have a diagnosis/fix, use `dotnet test tests/PipelineGuard.Api.Tests -c Release` as regression verification, then the full validation solution and HTTP smoke. Preserve original report JSON and duplicate behavior; fixing a displayed order alone is not enough.

## Exercise 2 — replay changes an existing incident

**Simulated report:** “A worker reconnect replayed a crash with the same ID. Total crash count didn't increase, but the stored message and processing timestamp changed. We expected the first accepted document to be immutable.”

**Hypothetical consequence:** audit evidence and group membership could change even while a duplicate-count dashboard looks healthy.

Start from the second checkpoint (independent of your exercise-1 work):

```console
git switch -c shaoyu/solve-replay shaoyu/learning
dotnet restore PipelineGuard.CrashIntelligence.sln
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore
dotnet test tests/PipelineGuard.CrashIntelligence.Tests -c Release --filter Replay_PreservesOriginalDocumentThroughHttpAdapter
```

Expected observable symptom: the adapter-level replay check fails because stored document content changed. The older processor/in-memory duplicate test can still pass. This reproduction uses synthetic HTTP semantics, not a running Elasticsearch cluster.

Investigate where identity is selected, what the persistence request means, and whether the response accurately represents the effect. Write down which checks prove duplicate counts versus immutability. A fix must preserve the original body, not just return a “duplicate” label.

After your fix, run the entire crash suite, both Release builds and the validation HTTP smoke. If infrastructure is available, real replay/acknowledgment testing is an additional check, not required by this synthetic exercise.

## Notes and help

For each exercise record: checkpoint → exact command/output → hypotheses → distinguishing experiment → root cause → fix diff → regression before/after → remaining uncertainty.

Share your investigation notes before requesting interview stories. Ask for “exercise 1 hint 1” or “exercise 2 hint 1” when needed. No cause, solution or invented personal reasoning is embedded in this guide.
