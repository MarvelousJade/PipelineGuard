# Crash Intelligence portable verification — 2026-08-08

Status: **passed for portable build/unit/frontend scope only**. No Docker infrastructure benchmark was run in this record.

## Environment

- Linux 6.18.33.2-microsoft-standard-WSL2 x86_64
- .NET SDK 8.0.423
- Node.js 22.14.0, npm 10.9.2
- Release configuration

## Commands and observed results

```console
dotnet restore PipelineGuard.CrashIntelligence.sln -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet build PipelineGuard.CrashIntelligence.sln --configuration Release --no-restore --warnaserror
dotnet test PipelineGuard.CrashIntelligence.sln --configuration Release --no-build --collect "XPlat Code Coverage"
dotnet list PipelineGuard.CrashIntelligence.sln package --vulnerable --include-transitive
npm ci --prefix dashboard
npm audit --prefix dashboard --audit-level=moderate
npm run build --prefix dashboard
```

- Crash solution build: 4 projects, 0 warnings, 0 errors.
- Crash unit tests: 9 passed, 0 failed, including 100 normalized address/line variants forming one fingerprint, duplicate delivery, and transient index failure/retry.
- Crash-solution coverage capture: 59.00% line and 57.59% branch. This is an initial unit-test baseline, not a coverage target or infrastructure coverage claim.
- NuGet vulnerability report: no known vulnerable direct or transitive packages from the configured source.
- Dashboard: TypeScript and Vite production build passed; npm audit reported 0 vulnerabilities.
- Existing portable suite was rerun separately: 49 passed, 0 failed. Repository portable total observed in this environment: 58 tests.
- Crash API process smoke: `/health/live` returned healthy and generated `/openapi/v1.json` contained all four crash paths while RabbitMQ was intentionally absent.

## Not established by this record

10,000-report throughput, live Elasticsearch search latency, live RabbitMQ replay behavior, dead-letter routing, and worker restart recovery remain unverified until a generated `benchmark-*.json` capture is committed.
