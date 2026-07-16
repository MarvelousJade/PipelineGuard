# Pipeline Guard Unreal plugin

Pipeline Guard is an editor-only Unreal Engine 5.6 plugin. It validates generic
content conventions without depending on any game module.

## Verification

Verified locally against Unreal Engine 5.6.1:

- `PipelineGuardHostEditor Win64 Development` builds successfully with UHT
  warnings treated as errors.
- All four synthetic `PipelineGuard.Rules.*` automation tests pass.
- The headless commandlet returns `0` for the empty host and emits a valid
  portable schema `1.0` report.

## Install and use

Copy this directory to `<Project>/Plugins/PipelineGuard`, enable it, and rebuild
the editor target. Configure rules under **Project Settings > Plugins > Pipeline
Guard**. Open the panel from **Tools > Pipeline Guard**; double-click a finding
to focus it in the Content Browser and open its asset editor.

Headless validation:

```text
UnrealEditor-Cmd.exe <Project.uproject> -run=PipelineGuard -Report=<report.json> -unattended -nop4 -nullrhi
```

If `-Report` is omitted, output defaults to
`<Project>/Saved/PipelineGuard/PipelineGuardReport.json`.

Raw Unreal commandlet exit codes are deterministic:

- `0`: clean
- `1`: validation findings
- `2`: invalid arguments or configuration
- `3`: validation execution failure
- `4`: report write failure

The report uses portable schema `1.0`, shared with the companion .NET CLI.
Invalid configuration exits before writing a portable report, so a contract-invalid
scope cannot be mistaken for completed validation output.

Automation tests use synthetic rule inputs and require no project assets:

```text
UnrealEditor-Cmd.exe <Project.uproject> -ExecCmds="Automation RunTests PipelineGuard" -TestExit="Automation Test Queue Empty" -unattended -nop4 -nullrhi
```
