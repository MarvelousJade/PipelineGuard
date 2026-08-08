# Crash Intelligence Compose benchmark — 2026-08-08

Status: **passed** for the recorded local Linux/Compose environment.

## Environment

- Linux 6.18.33.2-microsoft-standard-WSL2 x86_64
- .NET SDK 8.0.423
- Node.js 22.14.0
- Docker Engine 27.5.1 using overlay2 and cgroup v2
- Docker Compose 2.33.1
- Elasticsearch 8.17.4, RabbitMQ 3.13, one crash worker
- Benchmark client concurrency: 32

This is one local observation, not a production SLA or a cross-machine baseline.

## Command

```console
docker compose up --build -d
node scripts/crash-intelligence/verify.mjs --count 10000
```

Raw evidence: [`benchmark-2026-08-08t13-44-55-844z.json`](benchmark-2026-08-08t13-44-55-844z.json).

## Results

| Measure | Observed result |
| --- | ---: |
| Initial reports accepted | 10,000 |
| Ingestion latency p50 / p95 / p99 | 2,055.14 / 3,454.11 / 4,478.52 ms |
| Expected / actual fingerprint groups | 100 / 100 |
| Equivalent stack traces correctly grouped | 100% (10,000 / 10,000) |
| Messages replayed | 10,000 |
| Duplicate Elasticsearch effects | 0 |
| Search latency p50 / p95 / p99 | 12.04 / 15.33 / 18.94 ms |
| Reports queued while worker stopped | 100 |
| Worker restart recovery time | 9.43 seconds |
| Final benchmark-project documents | 10,100 |

After completion, the API and worker readiness endpoints were healthy and RabbitMQ reported one consumer with zero ready or unacknowledged processing messages.

## Interpretation and limits

- Elasticsearch `op_type=create` and the crash UUID document key prevented duplicate effects during a complete 10,000-message replay.
- All address, source-path, line-number, platform-module, offset, and parameter variants collapsed into their 100 intended semantic groups.
- The worker was stopped through Docker Compose, 100 reports were accepted while it was absent, and processing reached the expected count 9.43 seconds after restart.
- Search p95 was 15.33 ms for the generated project filters and corpus.
- Ingestion p95 was 3.45 seconds at concurrency 32. Publisher-confirm serialization is therefore a measured optimization target; this result must not be represented as a low-latency ingestion claim.
- The run used a single local RabbitMQ, Elasticsearch, API, and worker instance. It does not establish multi-node availability, production capacity, or networked-cloud latency.
- Dead-letter routing was not deliberately triggered by this harness and remains outside this benchmark result.
