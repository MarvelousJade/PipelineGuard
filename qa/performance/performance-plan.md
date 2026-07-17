# Performance capture plan

## Purpose and current boundary

The performance work measures fixed, correctness-checked scenarios; it does not infer production throughput or time saved. `summary.durationMs` inside a fixture is producer-reported Unreal validation time and must not be presented as CLI or API latency.

The checked-in script currently captures portable CLI end-to-end process latency. API and Unreal scenarios remain defined below but require their compatible runtimes and fixed data sets before a baseline is recorded.

## CLI capture

Build or publish Release outside the timed interval, then run:

```powershell
./scripts/Measure-PipelineGuardPerformance.ps1 `
  -WarmupCount 5 `
  -SampleCount 30
```

The script writes a new ignored directory under `artifacts/performance/` containing:

- `samples.csv`: every session-first, warm-up, and measured sample
- `summary.csv`: validity, count, failures, min, p50, p95, max, mean, and sample standard deviation
- `environment.json`: revision/dirty state, runtime/OS, hashes, counts, scenario order, and timing/statistics definitions
- `summary.md`: human-readable review

Scenarios:

| Scenario | Boundary | Correctness gate |
| --- | --- | --- |
| `cli.summary.clean.session_first` | First observed process launch of the session through output drain | Exit `0`; clean totals present |
| `cli.summary.clean.warm_cache_process` | New CLI process per sample after warm-ups | Exit `0`; clean totals present |
| `cli.summary.error.warm_cache_process` | New CLI process per sample after warm-ups | Exit `1`; expected error finding present |

“Session-first” is intentionally not called “cold”: restarting a process does not safely or portably clear Windows filesystem caches. A single session-first row has no percentile. Aggregate at least five independent sessions before summarizing it.

## API capture protocol

When the repository-required ASP.NET Core 8 runtime is present, use a fresh SQLite database and the default console-telemetry profile. Build/publish before timing. A future automation pass should retain raw per-request samples for:

| Scenario | Fixed shape | Correctness gate |
| --- | --- | --- |
| Startup readiness | Process start to first complete `/health` response | HTTP `200`, `status: ok` |
| Health | Sequential requests through full body read | All `200`, valid body |
| List | Exactly 100 seeded runs; `limit=20&offset=0` | All `200`, exactly 20 items |
| Ingest | Pre-generated unique-ID clean fixture per request | All `201`; returned ID matches submitted ID |

Use one persistent `HttpClient`, proxy disabled, sequential concurrency, 10 warm-ups, and at least 100 measured endpoint samples. Generate payloads outside timed regions, read complete bodies inside them, retain outliers, and continuously drain API stdout/stderr so telemetry cannot block the process. Record database volume/state and telemetry profile.

## Unreal capture protocol

Use a named, immutable content corpus with nonzero assets. Record engine build, project revision, profile, scope, assets scanned, rules run, findings, raw report duration, wall-clock commandlet duration, and report hash. Run at least five repetitions after one unmeasured asset-registry warm-up. Editor responsiveness and commandlet wall time are different scenarios.

The zero-asset fixture host and the `184 ms`/`231 ms` durations embedded in portable sample JSON are functional fixtures, not production baselines.

## Statistics and validity

- Percentiles use nearest rank: `rank = ceil(percentile * sample count)`.
- Report p50 for 5+ samples and p95 for 20+ samples; do not report p99 from 100 samples.
- Retain min, max, mean, sample standard deviation, and failure count; never trim a slow sample after inspection.
- Any unexpected exit/status, invalid body, timeout, or missing sample invalidates that scenario.
- Establish a baseline from at least three clean captures on the same host/configuration. Compare the median of each run's p50 and p95 rather than pooling all samples.

## Regression budgets

Correctness gates apply immediately. Latency budgets apply only after a compatible three-run baseline exists:

```text
budget = max(baseline * (1 + relative tolerance), baseline + absolute noise floor)
```

Suggested investigation thresholds:

| Scenario | p50 | p95 |
| --- | ---: | ---: |
| CLI warm-cache process | +20% and at least +5 ms | +25% and at least +10 ms |
| API health/list | +25% and at least +1 ms | +30% and at least +2 ms |
| API ingest | +20% and at least +2 ms | +25% and at least +5 ms |

Confirm a breach with a fresh capture; file a performance defect when two of three comparable captures exceed the budget. Session-first CLI and API startup are track-only initially. Do not hard-gate noisy hosted runners; use a stable self-hosted reference machine if enforcement becomes necessary.

## Publication checklist

- Revision is clean or the dirty state and diff are retained.
- Release binary and fixture hashes are present.
- SDK/runtime, OS, architecture, power/thermal deviations, and sample order/count are recorded.
- Raw samples and correctness fields accompany the summary.
- Usernames, private assets, absolute personal paths, and credentials are removed.
- Claims say “local observation” until a representative corpus and compatible multi-run baseline exist.
