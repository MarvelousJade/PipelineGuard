# Troubleshooting

Start with the portable build. Unreal and BlackWood issues should not prevent report-contract, CLI, API, MCP, or telemetry tests from running.

## The .NET SDK is missing or the wrong version

Run:

```console
dotnet --info
dotnet --list-sdks
```

Install a .NET 8 SDK if no `8.0.x` entry appears. A newer runtime alone is not sufficient; building requires the SDK.

## Restore fails

Confirm network access to the configured NuGet sources and inspect them with:

```console
dotnet nuget list source
dotnet restore PipelineGuard.sln --verbosity normal
```

If the environment deliberately blocks the network, restore once in an approved networked environment or use an approved package cache. Do not disable package signature or TLS checks.

## The CLI command is not recognized

Confirm the current command surface rather than guessing:

```console
dotnet run --project src/PipelineGuard.Cli -- --help
```

Run commands from the repository root so relative sample and schema paths resolve consistently.

## A report parses but is rejected

Syntactically valid JSON may still violate the PipelineGuard contract. Check, in order:

1. `schemaVersion` is `1.0`.
2. Required fields and formats satisfy the JSON Schema.
3. Timestamps are chronological.
4. Summary totals match the findings.
5. Finding IDs are unique.
6. Suppressed findings include a reason.

Compare with `samples/clean-report.json`. `samples/malformed-report.json` is intentionally contract-invalid.

## Exit code 1 appears as a failed process

Exit `1` means validation completed correctly and found a non-suppressed issue at or above the configured threshold. Read the report and fix or explicitly suppress the finding. Exit `3` represents an execution failure; raw Unreal exit `4` means validation ran but its report could not be written.

## SQLite is locked

Stop duplicate local API processes and confirm only one service instance is using the configured database during development. Do not delete a database merely to clear a lock. If the process crashed, preserve the file for diagnosis, restart once, and inspect logs for the active trace ID.

## No OpenTelemetry output appears

- Use the console exporter first to remove collector/network variables.
- Confirm telemetry is enabled for the current environment.
- For OTLP, verify the endpoint, protocol, and collector listener.
- Look for exporter errors in structured logs.
- Trigger a real CLI/API/MCP operation; startup alone may not create an application span.

Never place credentials directly in command arguments or committed configuration.

## The MCP client connects but returns no runs

Verify that the API is healthy, at least one schema-valid report has been ingested, and the MCP server points to the same service/database environment. Use a bounded `validation_list_runs` request before requesting a specific ID.

If the client asks for a filesystem path or raw command, it is using the wrong tool contract. The default MCP surface is report-query-only.

## Unreal executable or commandlet cannot be found

This is expected before the generic Unreal phase is installed. Once available:

- Confirm the configured executable exists and matches UE 5.6.
- Confirm the `.uproject` opens and its C++ editor target builds normally.
- Confirm the plugin is enabled for the editor target.
- Confirm the direct invocation uses `-run=PipelineGuard -Report=<path>`.
- Ask the CLI for its configured profile rather than passing a raw executable through MCP.
- Inspect bounded Unreal stdout/stderr captured under the run's trace ID.

Do not mark Unreal behavior as verified based only on generated source files.

## Editor and commandlet findings differ

Use the exact same project revision, profile, scope, and failure threshold. Then check for:

- Unsaved editor assets
- Rules implemented in an adapter rather than the shared runner
- Different asset-registry readiness or content filters
- Non-deterministic ordering mistaken for different membership
- Editor-only transient state used by a rule

The parity test should compare stable finding IDs after deterministic sorting.

## BlackWood rules do not compile

Keep project-specific rules out of the generic module. Inspect the actual BlackWood row structs, module dependencies, content roots, and engine/plugin versions before writing adapters. Do not invent field names from secondary project descriptions.

## CI passes .NET but does not test Unreal

That is the expected first-phase boundary. GitHub-hosted runners do not prove the UE 5.6 plugin compiled unless the workflow actually provisions a licensed engine/toolchain and executes the Unreal build. Preserve a local Unreal build transcript until a legitimate engine runner exists.
