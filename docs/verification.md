# Verification record

## Release 2026-07-16

This record separates implemented and observed behavior from planned integration. It was produced on Windows 11 with Unreal Engine 5.6.1 and the repository's pinned package versions.

## Observed gates

| Surface | Verification | Result |
| --- | --- | --- |
| .NET solution | `dotnet build PipelineGuard.sln --configuration Release --no-restore` | Seven projects; zero warnings and zero errors |
| .NET tests | `dotnet test PipelineGuard.sln --configuration Release --no-build --no-restore` | 49 passed; zero failed or skipped |
| NuGet audit | SDK 8.0.300 restore audit plus `dotnet list ... --vulnerable --include-transitive` | No vulnerable direct or transitive packages |
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

The MCP allowlist observed by the client was:

1. `validation_get_remediation`
2. `validation_get_summary`
3. `validation_list_findings`
4. `validation_list_runs`

## Deliberate limits

- The standalone fixture host contains zero game-content assets. Unreal rule logic is verified with four synthetic automation tests.
- The BlackWood repository was not present in the workspace, so no BlackWood-specific validator or asset finding is claimed.
- The GitHub Actions Windows/Linux workflow is defined but was not executed on GitHub in this local verification.
- Horde deployment, Perforce, WPF, Entra, production hosting, and write-capable MCP operations are outside this release.
- No production time saving, defect count, performance improvement, or user adoption number is claimed.
