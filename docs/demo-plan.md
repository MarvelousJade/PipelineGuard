# Demo plan

The demo should prove one coherent production-tools story, not tour every file. Keep the primary recording under six minutes and capture evidence from the exact revision being presented.

## Evidence preflight

Before recording:

- Build the Release configuration from a clean checkout.
- Run the complete portable test suite and save the result file.
- Verify all three sample reports behave as documented.
- Start with console OpenTelemetry export so traces are visible without external infrastructure.
- Record the commit ID and tool version.
- If demonstrating Unreal, build the plugin and save the commandlet transcript.
- Remove personal paths, credentials, private assets, and unrelated notifications from the capture.

## Portable demo: available without Unreal or BlackWood

### 1. Frame the user problem — 20 seconds

Explain that artists and designers need actionable feedback in their normal workflow, while CI and AI agents need the same facts in a deterministic, bounded format.

### 2. Prove the contract — 45 seconds

Run report verification against:

1. `clean-report.json` and show exit `0`.
2. `error-report.json` and show the two actionable findings plus exit `1`.
3. `malformed-report.json` and show a contract error plus exit `2`.

Point out that exit `1` means the validator worked and found a blocking issue; it is not a crash.

### 3. Prove service history — 45 seconds

Ingest the schema-valid reports into the local API. Show `/health`, a paginated run list, and one run detail. Attempt to ingest the malformed report and show that it is rejected rather than persisted.

### 4. Prove observability — 45 seconds

Show correlated trace IDs across ingestion and persistence, the run-duration histogram/counter, and severity/rule metrics. Confirm that metric attributes do not contain asset paths or run IDs.

### 5. Prove safe agent access — 60 seconds

From an MCP-capable client:

- List recent validation runs.
- Request one summary.
- Filter findings by severity.
- Request remediation for one stable finding ID.

Then show one rejected request that attempts an unsupported path or raw command argument. Do not imply that PipelineGuard itself calls an LLM; MCP exposes deterministic tool data to the client.

## Generic Unreal demo: add only after UE 5.6 verification

### 6. Show the same runner in two workflows — 90 seconds

1. Open the editor tab and validate the fixture content root.
2. Filter to one intentional naming or texture failure and navigate to the asset.
3. Run the headless commandlet with the same profile and scope.
4. Compare stable finding IDs to prove editor/commandlet parity.
5. Fix the fixture asset and rerun to show the commandlet transition from exit `1` to `0`.

Capture the JSON report and commandlet transcript as release evidence.

## BlackWood integration demo: add only after repository inspection

Choose one project-specific rule derived from a real data contract, such as a dialogue link or soft-reference type check. Show:

1. The real authoring mistake in a safe/non-private project view.
2. Actionable editor remediation.
3. The same stable finding in the headless report.
4. The corrected content passing.

End with the measured run summary: actual assets scanned, actual enabled rules, actual findings by severity, and measured duration. Do not extrapolate those values into a percentage of time saved.

## Recording acceptance checklist

- All commands and exit codes are readable.
- The report shown validates against schema v1.
- The same revision produced code, tests, report, and transcript.
- No unsupported feature is described in the present tense.
- Private BlackWood assets are excluded or anonymized as appropriate.
- The safety limitation of read-only/default-off execution is stated.
- The final frame links to the repository and names the exact release tag.

## Evidence-backed release claims

After the generic UE proof exists:

> Built a UE 5.6 editor validation plugin with four configurable content rules, a shared headless commandlet, versioned JSON reports, and deterministic CI exit codes.

After the portable .NET/MCP proof exists:

> Developed a .NET 8 CLI and ASP.NET service for validation orchestration and report history; instrumented runs with OpenTelemetry and exposed results through four allow-listed, read-only MCP tools.

After the real project run, replace placeholders with generated evidence:

> Validated **[assets scanned]** assets in **[duration]**, executing **[rules]** rules and identifying **[findings]** actionable issues.

Use the final test count reported by the test runner. Do not use source-line counts, estimated productivity gains, or claims of Unreal CI unless a real runner executed it.
