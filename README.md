# PipelineGuard

[![.NET portable checks](https://github.com/MarvelousJade/PipelineGuard/actions/workflows/dotnet.yml/badge.svg)](https://github.com/MarvelousJade/PipelineGuard/actions/workflows/dotnet.yml)

PipelineGuard is a portfolio-scale game-production tool that turns Unreal content checks into deterministic reports that people, CI jobs, and AI-assisted workflows can use safely.

The intended end-to-end flow is:

1. A shared Unreal validation runner checks content from either an editor tab or a headless commandlet.
2. The runner writes a versioned JSON report and returns a CI-safe exit code.
3. A .NET CLI verifies and summarizes the report; an ASP.NET service stores run history.
4. OpenTelemetry makes the .NET workflow observable.
5. A constrained MCP server lets an AI agent inspect results without arbitrary command or filesystem access.

The repository now also contains [PipelineGuard Crash Intelligence](docs/crash-intelligence/README.md): an event-driven subsystem that ingests Unreal crashes through ASP.NET Core, processes them through RabbitMQ workers, groups normalized stacks in Elasticsearch, and presents the results in a React/Grafana operations surface.

The portable .NET layer and sample reports do not require Unreal Engine or BlackWood. The Unreal plugin requires a compatible Unreal Engine 5.6 installation. Project-specific BlackWood rules require the BlackWood repository and must be defined only after its real data contracts have been inspected.

## Verified release

The 2026-07-16 local release is verified, with the limits stated explicitly:

- A seven-project .NET 8 solution builds in Release with zero warnings; 49 xUnit tests pass.
- SDK 8.0.300 restore audits direct and transitive NuGet dependencies; no known vulnerable package is reported.
- GitHub Actions passes the same audited build and test gates on Ubuntu and Windows.
- The UE 5.6.1 editor target builds, all four synthetic Unreal automation tests pass, and the headless commandlet emits schema `1.0`.
- The two-node BuildGraph compiles the editor target and runs the commandlet successfully.
- Fixture outcomes are `0` clean, `1` findings, and `2` invalid contract.
- The loopback API returns `201` ingest, `409` duplicate, and `400` invalid; SQLite retrieval, structured logs, traces, and three custom metrics were observed.
- An official MCP SDK client discovered exactly four read-only tools and successfully called both run-list and run-summary tools.

The fixture host intentionally has zero project assets. Generic rule behavior is covered by synthetic tests; BlackWood integration and production time-saved claims are not made. See the [verification record](docs/verification.md).

## Crash Intelligence status

The crash subsystem includes the API, RabbitMQ worker, deterministic fingerprinting, Elasticsearch search/aggregation, React dashboard, OpenTelemetry/Grafana provisioning, Docker Compose, optional Kubernetes manifests, and a 10,000-report verification harness. Its portable baseline currently adds 9 passing tests (58 repository portable tests observed locally) and 59.25% line coverage for the new crash solution. Live 10,000-report, replay, latency, and recovery numbers are deliberately not claimed until a Compose benchmark capture is committed. See the [crash evidence record](docs/crash-intelligence/evidence/2026-08-08-portable-tests.md).

## Project boundaries

| Capability | Portable .NET | Unreal 5.6 | BlackWood |
| --- | :---: | :---: | :---: |
| Validate, summarize, store, and query fixture reports | Yes | No | No |
| OpenTelemetry traces, metrics, and structured logs | Yes | No | No |
| Read-only MCP inspection tools | Yes | No | No |
| Editor validation tab and headless commandlet | No | Yes | No |
| Generic naming, path, texture, and DataTable rules | No | Yes | No |
| Item, dialogue, soft-reference, or footstep rules | No | Yes | Yes |

Not in the first release: Horde deployment, WPF, Entra/Key Vault, Perforce integration, remote production hosting, automatic AI fixes, or arbitrary MCP-triggered commands.

## Quick start

### Requirements

- .NET 8 SDK
- Git
- Unreal Engine 5.6 and its C++ build prerequisites only for the Unreal phase

Confirm the SDK and build the portable solution:

```console
dotnet --version
dotnet restore PipelineGuard.sln
dotnet build PipelineGuard.sln --configuration Release --no-restore
dotnet test PipelineGuard.sln --configuration Release --no-build
```

The exact executable commands are exposed by the CLI itself:

```console
dotnet run --project src/PipelineGuard.Cli -- --help
```

Use the supplied fixtures to exercise all three report outcomes:

```console
dotnet run --project src/PipelineGuard.Cli -- summary samples/clean-report.json --minimum info --fail-on error
dotnet run --project src/PipelineGuard.Cli -- summary samples/error-report.json --minimum info --fail-on error
dotnet run --project src/PipelineGuard.Cli -- summary samples/malformed-report.json --minimum info --fail-on error
```

Compare a prior accepted report with a current run:

```console
dotnet run --project src/PipelineGuard.Cli -- compare samples/clean-report.json samples/error-report.json --fail-on error
```

When Unreal is installed, ask the CLI for the engine-orchestration arguments:

```console
dotnet run --project src/PipelineGuard.Cli -- validate --help
```

The CLI's engine invocation is equivalent to:

```console
UnrealEditor-Cmd.exe <Project.uproject> -run=PipelineGuard -Report=<report.json> -unattended -nop4 -nullrhi
```

If `-Report` is omitted from a direct commandlet run, the plugin writes to `<Project>/Saved/PipelineGuard/PipelineGuardReport.json`. Prefer an explicit report path in automation.

Expected process results:

| Exit code | Meaning | Fixture |
| ---: | --- | --- |
| `0` | Valid report below the configured failure threshold | `clean-report.json` |
| `1` | Valid report containing findings at or above the threshold | `error-report.json` |
| `2` | Invalid arguments, configuration, or report contract | `malformed-report.json` |
| `3` | Unreal process, report-write, or internal execution failure | Not fixture-driven |

The raw Unreal commandlet additionally uses exit `4` when validation ran but its report could not be written. The .NET CLI maps that condition to its public execution-failure exit `3`.

### Crash Intelligence stack

On Linux with Docker Compose v2, start RabbitMQ, Elasticsearch, the crash API and worker, dashboard, and monitoring stack:

```console
cp .env.example .env
# Replace the development credentials in .env.
docker compose up --build -d
curl http://127.0.0.1:5080/health/ready
curl -i -X POST http://127.0.0.1:5080/api/v1/crashes -H "Content-Type: application/json" --data-binary "@samples/crash-report.json"
```

Open the crash dashboard at <http://127.0.0.1:3000>, generated OpenAPI at <http://127.0.0.1:5080/openapi/v1.json>, and Grafana at <http://127.0.0.1:3001>. Full operating, replay, worker-recovery, and 10,000-report benchmark instructions are in the [Crash Intelligence guide](docs/crash-intelligence/README.md).

### Local report service

Start the loopback-only API in one terminal:

```console
dotnet run --project src/PipelineGuard.Api
```

In another terminal, check health, ingest a conforming fixture, and list stored runs:

```console
curl http://127.0.0.1:5078/health
curl -i -X POST http://127.0.0.1:5078/api/v1/runs -H "Content-Type: application/json" --data-binary "@samples/clean-report.json"
curl "http://127.0.0.1:5078/api/v1/runs?limit=20&offset=0"
```

The default SQLite database lives beneath the API build output. Override `PipelineGuard:DatabasePath` through normal ASP.NET configuration when a stable local path is needed.

### MCP server

Configure an MCP client to launch the stdio server with executable `dotnet` and arguments `run --project src/PipelineGuard.Mcp`. It queries `http://127.0.0.1:5078/` by default. `PIPELINEGUARD_API_URL` may select another endpoint, but non-loopback endpoints are rejected unless `PIPELINEGUARD_ALLOW_REMOTE_API=true` is explicitly set.

The `tools/PipelineGuard.Mcp.Smoke` client uses the official SDK to verify initialization, the exact tool allowlist, and real tool calls against a running API.

### BuildGraph

On Windows, run the same two-node compile-and-validate graph used for the local
verification:

```powershell
./scripts/Invoke-PipelineGuardBuildGraph.ps1 -EngineRoot "C:/Program Files/Epic Games/UE_5.6"
```

`malformed-report.json` is valid JSON by design. It is malformed as a PipelineGuard report, which lets parsers load it and contract tests reject it predictably.

## Repository map

```text
PipelineGuard/
|-- src/                         validation and crash-intelligence .NET services
|-- tests/                       portable unit and integration tests
|-- dashboard/                   React crash-group operations dashboard
|-- deploy/                      observability and optional Kubernetes resources
|-- tools/                       official-SDK MCP protocol smoke client
|-- unreal/                      editor-only Unreal plugin and fixture host
|-- schemas/                     validation and crash-report JSON contracts
|-- samples/                     validation and crash-report fixtures
|-- docs/                        architecture, operations, evidence, and QA records
`-- .github/workflows/           portable Windows/Linux verification
```

## Contract first

[`schemas/validation-report-v1.schema.json`](schemas/validation-report-v1.schema.json) is the portable boundary between Unreal and .NET. Producers must validate their output against it. Consumers must additionally enforce the cross-field invariants documented in [`docs/report-schema.md`](docs/report-schema.md), such as matching summary counts and chronological timestamps.

## Safety model

MCP inspection is read-only. An optional run tool, if implemented later, is disabled by default and accepts a preconfigured profile ID rather than an executable, project path, or raw arguments. See [`docs/threat-model.md`](docs/threat-model.md) for trust boundaries, abuse cases, and required negative tests.

## Documentation

- [Architecture](docs/architecture.md)
- [Report schema](docs/report-schema.md)
- [Threat model](docs/threat-model.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Demo plan](docs/demo-plan.md)
- [Verification record](docs/verification.md)
- [Manual QA and regression evidence](docs/qa/README.md)
- [Crash Intelligence architecture, operations, and benchmark](docs/crash-intelligence/README.md)

## Security and license

Report suspected vulnerabilities privately as described in
[`SECURITY.md`](SECURITY.md). The code is published for portfolio review and
evaluation under an all-rights-reserved [`LICENSE`](LICENSE); it is not
open-source licensed.

## Evidence before claims

Only claims listed in the [verification record](docs/verification.md) or a completed [QA execution record](docs/qa/executions/) are release evidence. Case definitions and automation references are not passes. Performance captures are local observations until the [baseline rules](docs/qa/performance/performance-plan.md) are satisfied. BlackWood integration, production performance, and time saved remain unverified and must not be claimed. Future releases should update the records with their actual tests, assets, findings, and environment.
