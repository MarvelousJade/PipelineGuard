# PipelineGuard CLI performance capture

- Capture ID: `478c6382-aeb1-463e-8943-2f3ae2f1d8de`
- Captured UTC: `2026-07-17T02:36:23.3062313+00:00`
- Source: `641cf8ba0cda5a3e5750efd9ce979f789c4be550`; dirty: `False`
- Samples: 30 measured after 5 warm-ups per warm-cache scenario
- Timing: process start through exit and complete stdout/stderr drain
- Percentiles: nearest rank; no samples trimmed

| Scenario | Valid | n | Failures | Min ms | p50 ms | p95 ms | Max ms | Mean ms | Stddev ms |
| --- | :---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| cli.summary.clean.session_first | True | 1 | 0 | 389.261 |  |  | 389.261 | 389.261 |  |
| cli.summary.clean.warm_cache_process | True | 30 | 0 | 226.48 | 375.139 | 594.462 | 600.856 | 392.443 | 104.037 |
| cli.summary.error.warm_cache_process | True | 30 | 0 | 342.864 | 456.5 | 578.131 | 614.291 | 463.639 | 70.966 |

The session-first row is a single observation, so p50/p95 are intentionally blank. It is not labelled a cold-cache benchmark. This capture is not an SLA; establish a baseline from at least three clean captures on the same reference host.
