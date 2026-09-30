# Small rework guide

PipelineGuard validates Unreal content reports and, independently, groups crashes. This rework began at `3a37303`, preserves the existing history/features, and improves two persistence boundaries rather than rebuilding the system.

The verified rework at `4c2b37e` and corrected learning branch are now integrated into local `main` without squashing. Both dedicated exercise fix commits are preserved in its ancestry. See [agent investigations and fresh integration checks](../learning-investigations/README.md); original faulty checkpoints remain available for practice.

## One workflow to understand first

```text
samples/clean-report.json
  → POST /api/v1/runs (Program.cs: bounded body + strict deserialize)
  → JsonReportSerializer + ValidationReportValidator (shape + invariants)
  → SqliteReportStore.TryAddAsync (unique run ID, original JSON)
  → GET /api/v1/runs (UTC metadata, stable ties)
  → GET /api/v1/runs/{id} (original JSON survives restart)
```

No generic repository, event bus or new runtime dependency was added. The new API test project reuses the repository's existing xUnit/Test SDK versions.

### Reproduce all portable gates

Requires .NET 8 and Node 22; run from repository root:

```console
dotnet restore PipelineGuard.sln
dotnet build PipelineGuard.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.sln -c Release --no-build
node scripts/verify-validation.mjs

dotnet restore PipelineGuard.CrashIntelligence.sln
dotnet build PipelineGuard.CrashIntelligence.sln -c Release --no-restore --warnaserror
dotnet test PipelineGuard.CrashIntelligence.sln -c Release --no-build

npm ci --prefix dashboard
npm audit --prefix dashboard --audit-level=high
npm run build --prefix dashboard
node --check scripts/crash-intelligence/verify.mjs

dotnet list PipelineGuard.sln package --vulnerable --include-transitive
dotnet list PipelineGuard.CrashIntelligence.sln package --vulnerable --include-transitive
```

For persistent local history: `dotnet run --project src/PipelineGuard.Api`; defaults to loopback port 5078. Override `PipelineGuard__DatabasePath` to choose storage. Upload with `curl -X POST http://127.0.0.1:5078/api/v1/runs -H "Content-Type: application/json" --data-binary @samples/clean-report.json`, then GET `/api/v1/runs` and the fixture's run ID. Re-upload returns 409, not replacement. Run the CLI summary separately as documented in the root README.

The crash adapter tests exercise HTTP request/response semantics without infrastructure. For a real broker/search/dashboard deployment, use the [existing Compose guide](../crash-intelligence/README.md).

## Changes and evidence

- UTC chronology, stable page ties and transactional legacy timestamp normalization; original report bytes preserved.
- Elasticsearch index creation only after 404, explicit timeout classification, caller cancellation preserved and HEAD request disposed.
- Real API/SQLite smoke in portable CI; distinct test result files avoid evidence overwrites.
- Documentation now distinguishes intended engine/CLI capabilities from actual code.

See [decisions](decisions.md), [verification evidence](evidence.md), [interview walkthrough](interview.md), and [learner exercises](../learning/README.md). Tests are correctness evidence, not a throughput comparison. The rework makes no performance improvement claim.

## Remaining limitations

- APIs are local-development tools without production authentication/authorization; do not expose them publicly, especially the resolution write endpoint.
- Legacy timestamp normalization scans at startup; offset pages are not a concurrent-insert snapshot.
- Crash status filtering considers a bounded set of 500 recent groups; affected-build cardinality is approximate and only 20 builds are listed. Fingerprint/resolution identity is not project-scoped. No multi-tenant guarantee.
- Synthetic HTTP handlers do not verify RabbitMQ acknowledgments/dead-letter routing, Elasticsearch mappings or multi-node recovery. Compose unavailable during this rework; historical benchmark evidence is retained.
- Unreal was not rebuilt here. Existing [QA issues](../qa/known-issues.csv) remain: editor filter/export controls (PG-001), incomplete validation OpenAPI (PG-002), JSON/type diagnostic distinction (PG-003), raw commandlet warning threshold (PG-004), CLI telemetry (PG-005).
- Dashboard production build was checked, but no browser interaction suite or production deployment was run. Hosted Windows/Linux CI was not triggered.

The agreed reliability rework is complete; these are preserved/pre-existing capabilities or future scope, not silently removed functionality.
