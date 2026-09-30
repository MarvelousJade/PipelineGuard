# Architecture

PipelineGuard separates engine-specific validation from portable orchestration. The JSON report is the contract between those halves, so the .NET workflow can be built and tested with fixtures before an Unreal installation or BlackWood repository is available.

## System context

```mermaid
flowchart LR
    User[Artist / designer / programmer] --> Editor[Unreal editor tab]
    CI[CI or local automation] --> Cmd[Unreal commandlet]
    Editor --> Runner[Shared C++ validation runner]
    Cmd --> Runner
    Runner --> Report[Validation report v1 JSON]
    CLI[.NET CLI] --> Cmd
    Report --> CLI
    Upload[HTTP report upload] --> API[ASP.NET report API]
    Report --> Upload
    API --> DB[(SQLite run history)]
    MCP[Constrained MCP server] --> API
    Agent[AI coding agent] --> MCP
    API -. traces / metrics / logs .-> OTel[OpenTelemetry exporter]
    MCP -. traces / metrics / logs .-> OTel
```

The AI agent is not trusted merely because it is local. MCP arguments are treated as untrusted input and remain behind a narrow application interface.

## Components

### Unreal editor-only plugin

The plugin contains one validation runner and two adapters:

- The editor adapter renders findings, navigates to an asset and displays remediation. Filtering/export controls are not implemented (PG-001).
- The commandlet adapter selects a configured scope, invokes the same runner, writes JSON and fails on errors. It does not honor a warning threshold directly (PG-004); the .NET wrapper evaluates its own configured threshold against the report.

The adapters must not reimplement validation rules. Given the same project revision, profile, and scope, editor and commandlet runs should produce the same sorted finding set.

The generic profile has four bounded rules:

1. Asset naming prefix by class
2. Allowed or forbidden content path
3. Texture dimension and power-of-two budget
4. DataTable structure and row validation

BlackWood-specific rules belong in a separate project profile or module. They must not leak BlackWood dependencies into the generic core.

### Report contract

The report is a finite snapshot, not a stream. It contains run metadata, summary counters, and actionable findings. Version 1 caps findings at 20,000 and metadata entries at 20 per finding to bound memory, storage, and MCP response costs.

The producer sorts findings deterministically. The consumer validates both JSON Schema constraints and invariants that JSON Schema cannot express. See [report-schema.md](report-schema.md).

### .NET CLI

The CLI is both a report consumer and, when Unreal is installed, a safe process orchestrator. Its responsibilities are:

- Verify a report against the versioned contract.
- Summarize or filter findings for a person or CI log.
- Apply a failure threshold and return the documented exit code.
- Launch an approved Unreal executable with `ProcessStartInfo.ArgumentList`, a timeout, cancellation, and bounded output. The commandlet contract is `-run=PipelineGuard -Report=<path>`.

Report submission is a separate HTTP upload; the current CLI does not implement an upload command.

The CLI never builds a shell command string from user input.

### ASP.NET report API

The initial API is a local development service rather than a production multi-user service. It binds to loopback by default, validates uploads, enforces a body limit, and stores accepted reports in SQLite. The minimal surface is:

- `POST /api/v1/runs`
- `GET /api/v1/runs`
- `GET /api/v1/runs/{id}`
- `GET /health`

Lists are paginated by completion instant descending, then run ID ascending for stable ties. SQLite metadata timestamps are canonical UTC round-trip strings; startup transactionally normalizes legacy offset-bearing metadata without changing uploaded report JSON. Duplicate IDs never replace stored reports. Invalid or unsupported report versions receive a client error and are not persisted.

Run the isolated ingest → persist → list/get → restart workflow with `node scripts/verify-validation.mjs` after a Release build. See [rework decisions](rework/decisions.md) for alternatives and migration limits.

### MCP server

The default MCP surface is read-only:

- `validation_list_runs`
- `validation_get_summary`
- `validation_list_findings`
- `validation_get_remediation`

The server queries the application/API rather than opening arbitrary files. It applies strict input schemas, page and response limits, timeouts, and cancellation. Optional run capability is a separate, default-off feature described in [threat-model.md](threat-model.md).

### Observability

The report API and MCP server configure OpenTelemetry exporters; the current CLI does **not** implement telemetry (tracked as PG-005). The intended CLI/process instrumentation is not claimed as working here.

- Traces around report ingestion and MCP tool calls
- Metrics for run count, duration, and findings grouped only by bounded rule/severity dimensions
- Structured logs carrying the active trace ID

Console export supports a zero-infrastructure demo. OTLP export is opt-in through configuration. Asset paths, report payloads, and run IDs are excluded from metric attributes to avoid high cardinality and accidental disclosure.

## Dependency direction

The intended dependency direction is inward toward contracts and application services:

```text
CLI -------+
API -------+--> application/contracts
MCP -------+
Unreal producer --> JSON contract <-- .NET consumers
```

The contracts layer must not depend on ASP.NET, MCP, OpenTelemetry exporters, SQLite, or Unreal. This keeps fixture tests fast and portable.

## Deployment phases

### Phase 1: portable proof

Build and test contracts, fixtures, CLI behavior, API persistence, telemetry, and read-only MCP on Windows and Linux. No Unreal claim is justified by this phase.

### Phase 2: generic Unreal proof

Compile the plugin in a clean UE 5.6 C++ editor project. Add passing and failing fixture assets for each generic rule. Prove editor/commandlet parity and .NET orchestration. This remains a phase goal: the recorded Unreal host has no game-content assets, and PG-001/PG-004 track incomplete editor controls and warning-threshold behavior.

### Phase 3: BlackWood integration

Inspect actual project data contracts before defining item, dialogue, soft-reference, or physical-surface rules. Record the real assets scanned, rules run, findings, and elapsed time. Intentional exceptions require a reason and must remain visible in the report.

## Crash Intelligence extension

Crash Intelligence is a separate event-driven deployment boundary within this repository. Its ASP.NET ingestion API publishes confirmed persistent events to a RabbitMQ quorum queue; competing workers normalize call stacks and create immutable Elasticsearch documents using `crashId` as the idempotency key. Search-time aggregations derive group counts and affected builds without a mutable distributed counter. OpenTelemetry, Prometheus, Grafana alerts, health probes, a React dashboard, and dead-letter routing form the operational boundary.

The extension does not add RabbitMQ or Elasticsearch dependencies to the original validation contracts, CLI, local SQLite API, or MCP server. See [crash-intelligence/README.md](crash-intelligence/README.md) for the detailed topology and processing guarantees.

## Deliberate non-goals

The first release includes a local Windows-only BuildGraph compile-and-commandlet
target. It does not include a WPF client, remote identity, automatic content
mutation, LLM calls, Horde, Perforce, or production cloud deployment.
