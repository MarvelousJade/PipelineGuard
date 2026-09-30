# Verification record

## Learning completion and main integration — 2026-09-30

Both practice defects were freshly reproduced and repaired in individual commits (`d8a0a8f`, `1d5a25c`) preserved by non-squash merges into local `main`. On the integrated merge tree, both Release builds had zero warnings/errors, **83 tests passed**, real HTTP/SQLite smoke and dashboard production build passed, and current NuGet/npm audits reported no vulnerabilities. See [fresh commands, investigation records and limitations](learning-investigations/README.md).

Docker daemon access was freshly checked and unavailable; integration/recovery/benchmark were not run. Unreal and browser checks were not run because their source surfaces were unchanged. Hosted CI and production deployment were not run. Earlier results below remain historical.

## Rework portable rerun — 2026-09-30

On `shaoyu/rework`: both Release builds passed with zero warnings/errors; **83 tests passed** (49 core, 6 SQLite, 28 crash). The isolated HTTP ingest/restart/history smoke and dashboard production build passed; current NuGet/npm audits reported no vulnerabilities. See [reproducible commands and scope](rework/README.md) and [actual evidence](rework/evidence.md).

Docker daemon unavailable; Unreal, Compose recovery/benchmark, hosted CI and dashboard browser behavior were **not rerun**. The following records remain historical.

## Release 2026-07-16

This record separates implemented and observed behavior from planned integration. It was produced on Windows 11 with Unreal Engine 5.6.1 and the repository's pinned package versions.

## Observed gates

| Surface | Verification | Result |
| --- | --- | --- |
| .NET solution | `dotnet build PipelineGuard.sln --configuration Release --no-restore` | Seven projects; zero warnings and zero errors |
| .NET tests | `dotnet test PipelineGuard.sln --configuration Release --no-build --no-restore` | 49 passed; zero failed or skipped |
| NuGet audit | SDK 8.0.300 restore audit plus `dotnet list ... --vulnerable --include-transitive` | No vulnerable direct or transitive packages |
| GitHub Actions | [Run 29515094223](https://github.com/MarvelousJade/PipelineGuard/actions/runs/29515094223) | Ubuntu and Windows jobs passed; test evidence uploaded |
| Fixture contract | CLI `summary` on clean, error, and malformed fixtures | Exits `0`, `1`, and `2` respectively |
| Fixture comparison | CLI `compare` from clean to error | Two added findings; exit `1` at error threshold |
| Unreal compile | `PipelineGuardHostEditor Win64 Development` | Succeeded; UHT warnings treated as errors |
| Unreal automation | `Automation RunTests PipelineGuard` | Four discovered and four passed, including configuration limits, regex validity, UTF-8 finding IDs, and report-cap coverage |
| Unreal commandlet | `-run=PipelineGuard -Report=<path>` | Exit `0`; schema `1.0` report written |
| .NET-to-Unreal | CLI `validate` against `UnrealEditor-Cmd.exe` | Raw Unreal `0`; public CLI `0`; strict report parsed |
| BuildGraph | Windows target `PipelineGuard` | Compile node and commandlet node succeeded |
| API/SQLite | Health, ingest, duplicate, invalid, list, and get | `200`, `201`, `409`, `400`; stored report retrieved |
| OpenTelemetry | Console capture after error-fixture ingest | Correlated structured log and trace IDs; run counter, finding counter, and ingest histogram emitted |
| MCP protocol | Official C# SDK stdio client | Four tools discovered; list-runs and get-summary calls succeeded |

## Crash Intelligence extension — 2026-08-08

A Linux/Compose verification used .NET SDK 8.0.423, Docker Engine 27.5.1, Docker Compose 2.33.1, Elasticsearch 8.17.4, RabbitMQ 3.13, and one crash worker.

| Surface | Verification | Result |
| --- | --- | --- |
| Crash solution | Release build and xUnit execution | Four projects built with zero warnings/errors; 9 passed |
| Dashboard | `npm ci`, audit, and production build | Build passed; zero reported vulnerabilities |
| Ingestion | 10,000 generated reports at concurrency 32 | 10,000 accepted; p95 3,454.11 ms |
| Fingerprinting | 100 semantic groups with stack variants | 10,000 / 10,000 correctly grouped |
| Idempotency | Replay all 10,000 messages | Zero duplicate Elasticsearch effects |
| Search | 100 filtered group searches | p95 15.33 ms |
| Recovery | Stop worker, queue 100 reports, restart | Expected count reached in 9.43 seconds |
| Final health | API/worker readiness and RabbitMQ queue | Healthy; one consumer; zero ready/unacknowledged messages |

The [benchmark summary](crash-intelligence/evidence/2026-08-08-compose-benchmark.md) documents scope and limitations; the linked JSON is the raw harness output. This is a single local result, not an SLA. Dead-letter routing and multi-node availability were not exercised by this benchmark. Ingestion publisher-confirm serialization remains an observed performance target.

The MCP allowlist observed by the client was:

1. `validation_get_remediation`
2. `validation_get_summary`
3. `validation_list_findings`
4. `validation_list_runs`

## Deliberate limits

- The standalone fixture host contains zero game-content assets. Unreal rule logic is verified with four synthetic automation tests.
- The BlackWood repository was not present in the workspace, so no BlackWood-specific validator or asset finding is claimed.
- GitHub Actions verifies the portable .NET layer; Unreal compile, automation, commandlet, and BuildGraph evidence remains local to the licensed UE 5.6.1 environment.
- Horde deployment, Perforce, WPF, Entra, production hosting, and write-capable MCP operations are outside this release.
- No production time saving, defect count, performance improvement, or user adoption number is claimed.
