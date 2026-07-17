[CmdletBinding()]
param(
    [ValidateRange(1, 50)]
    [int]$WarmupCount = 5,

    [ValidateRange(5, 500)]
    [int]$SampleCount = 30,

    [string]$OutputDirectory,

    [string]$CliPath,

    [string]$CleanFixturePath,

    [string]$ErrorFixturePath,

    [ValidatePattern('^[a-z0-9-]{1,32}$')]
    [string]$MachineLabel = 'local-windows'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptVersion = '1.0'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($CliPath)) {
    $CliPath = Join-Path $repositoryRoot 'src/PipelineGuard.Cli/bin/Release/net8.0/PipelineGuard.Cli.exe'
}
if ([string]::IsNullOrWhiteSpace($CleanFixturePath)) {
    $CleanFixturePath = Join-Path $repositoryRoot 'samples/clean-report.json'
}
if ([string]::IsNullOrWhiteSpace($ErrorFixturePath)) {
    $ErrorFixturePath = Join-Path $repositoryRoot 'samples/error-report.json'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $captureName = 'cli-{0}' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/performance/$captureName"
}

$CliPath = (Resolve-Path -LiteralPath $CliPath).Path
$CleanFixturePath = (Resolve-Path -LiteralPath $CleanFixturePath).Path
$ErrorFixturePath = (Resolve-Path -LiteralPath $ErrorFixturePath).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if (Test-Path -LiteralPath $OutputDirectory) {
    if (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1) {
        throw "Output directory must be new or empty: $OutputDirectory"
    }
}
else {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$samples = [Collections.Generic.List[object]]::new()

function Get-DurationMilliseconds {
    param(
        [long]$StartedTicks,
        [long]$CompletedTicks
    )

    return [Math]::Round(
        (($CompletedTicks - $StartedTicks) * 1000.0) / [Diagnostics.Stopwatch]::Frequency,
        3)
}

function Invoke-CliSample {
    param(
        [string]$Scenario,
        [string]$SampleKind,
        [int]$Iteration,
        [string]$FixturePath,
        [int]$ExpectedExitCode,
        [string]$ExpectedOutput
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $CliPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('summary')
    $startInfo.ArgumentList.Add($FixturePath)
    $startInfo.ArgumentList.Add('--minimum')
    $startInfo.ArgumentList.Add('info')
    $startInfo.ArgumentList.Add('--fail-on')
    $startInfo.ArgumentList.Add('error')

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $startedUtc = [DateTimeOffset]::UtcNow
    $startedTicks = [Diagnostics.Stopwatch]::GetTimestamp()

    try {
        if (-not $process.Start()) {
            throw 'The CLI process did not start.'
        }

        $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
        $standardErrorTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
        $standardError = $standardErrorTask.GetAwaiter().GetResult()
        $completedTicks = [Diagnostics.Stopwatch]::GetTimestamp()
        $combinedOutput = $standardOutput + $standardError
        $responseBytes = [Text.Encoding]::UTF8.GetByteCount($combinedOutput)
        $success = $process.ExitCode -eq $ExpectedExitCode -and
            $standardOutput.Contains($ExpectedOutput, [StringComparison]::Ordinal)

        return [pscustomobject][ordered]@{
            capture_id = $script:captureId
            scenario = $Scenario
            sample_kind = $SampleKind
            iteration = $Iteration
            started_utc = $startedUtc.ToString('O')
            duration_ticks = $completedTicks - $startedTicks
            duration_ms = Get-DurationMilliseconds $startedTicks $completedTicks
            success = $success
            exit_or_http_status = $process.ExitCode
            response_bytes = $responseBytes
            peak_working_set_bytes = $process.PeakWorkingSet64
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-NearestRank {
    param(
        [double[]]$SortedValues,
        [ValidateRange(0.01, 1.0)]
        [double]$Percentile
    )

    $rank = [Math]::Ceiling($Percentile * $SortedValues.Count)
    return $SortedValues[[Math]::Max(0, $rank - 1)]
}

function Get-ScenarioSummary {
    param(
        [string]$Scenario,
        [int]$ExpectedCount
    )

    $scenarioSamples = @($samples | Where-Object {
        $_.scenario -eq $Scenario -and $_.sample_kind -eq 'measured'
    })
    $failureCount = @($scenarioSamples | Where-Object { -not $_.success }).Count
    $valid = $scenarioSamples.Count -eq $ExpectedCount -and $failureCount -eq 0
    $values = [double[]]@($scenarioSamples.duration_ms | Sort-Object)
    $mean = if ($values.Count -gt 0) {
        ($values | Measure-Object -Average).Average
    }
    else {
        $null
    }
    $standardDeviation = if ($values.Count -gt 1) {
        $sumSquares = 0.0
        foreach ($value in $values) {
            $sumSquares += [Math]::Pow($value - $mean, 2)
        }
        [Math]::Sqrt($sumSquares / ($values.Count - 1))
    }
    else {
        $null
    }

    return [pscustomobject][ordered]@{
        scenario = $Scenario
        valid = $valid
        n = $scenarioSamples.Count
        failures = $failureCount
        minimum_ms = if ($values.Count -gt 0) { [Math]::Round($values[0], 3) } else { $null }
        p50_ms = if ($values.Count -gt 1) { [Math]::Round((Get-NearestRank $values 0.50), 3) } else { $null }
        p95_ms = if ($values.Count -ge 20) { [Math]::Round((Get-NearestRank $values 0.95), 3) } else { $null }
        maximum_ms = if ($values.Count -gt 0) { [Math]::Round($values[-1], 3) } else { $null }
        mean_ms = if ($null -ne $mean) { [Math]::Round($mean, 3) } else { $null }
        sample_stddev_ms = if ($null -ne $standardDeviation) {
            [Math]::Round($standardDeviation, 3)
        }
        else {
            $null
        }
    }
}

$captureId = [Guid]::NewGuid().ToString('D')
$captureStartedUtc = [DateTimeOffset]::UtcNow

# This first invocation is session-first, not a guaranteed cold-cache measurement.
$samples.Add((Invoke-CliSample `
    -Scenario 'cli.summary.clean.session_first' `
    -SampleKind 'measured' `
    -Iteration 1 `
    -FixturePath $CleanFixturePath `
    -ExpectedExitCode 0 `
    -ExpectedOutput 'Errors 0 | Warnings 0 | Info 0 | Suppressed 0'))

for ($iteration = 1; $iteration -le $WarmupCount; $iteration++) {
    $samples.Add((Invoke-CliSample `
        -Scenario 'cli.summary.clean.warm_cache_process' `
        -SampleKind 'warmup' `
        -Iteration $iteration `
        -FixturePath $CleanFixturePath `
        -ExpectedExitCode 0 `
        -ExpectedOutput 'Errors 0 | Warnings 0 | Info 0 | Suppressed 0'))
}
for ($iteration = 1; $iteration -le $SampleCount; $iteration++) {
    $samples.Add((Invoke-CliSample `
        -Scenario 'cli.summary.clean.warm_cache_process' `
        -SampleKind 'measured' `
        -Iteration $iteration `
        -FixturePath $CleanFixturePath `
        -ExpectedExitCode 0 `
        -ExpectedOutput 'Errors 0 | Warnings 0 | Info 0 | Suppressed 0'))
}

for ($iteration = 1; $iteration -le $WarmupCount; $iteration++) {
    $samples.Add((Invoke-CliSample `
        -Scenario 'cli.summary.error.warm_cache_process' `
        -SampleKind 'warmup' `
        -Iteration $iteration `
        -FixturePath $ErrorFixturePath `
        -ExpectedExitCode 1 `
        -ExpectedOutput 'PG.Naming.Prefix'))
}
for ($iteration = 1; $iteration -le $SampleCount; $iteration++) {
    $samples.Add((Invoke-CliSample `
        -Scenario 'cli.summary.error.warm_cache_process' `
        -SampleKind 'measured' `
        -Iteration $iteration `
        -FixturePath $ErrorFixturePath `
        -ExpectedExitCode 1 `
        -ExpectedOutput 'PG.Naming.Prefix'))
}

$summaries = @(
    Get-ScenarioSummary 'cli.summary.clean.session_first' 1
    Get-ScenarioSummary 'cli.summary.clean.warm_cache_process' $SampleCount
    Get-ScenarioSummary 'cli.summary.error.warm_cache_process' $SampleCount
)

$runtimeConfigPath = [IO.Path]::ChangeExtension($CliPath, '.runtimeconfig.json')
$runtimeConfig = if (Test-Path -LiteralPath $runtimeConfigPath) {
    Get-Content -Raw -LiteralPath $runtimeConfigPath | ConvertFrom-Json
}
else {
    $null
}

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceBranch = (& git -C $repositoryRoot branch --show-current).Trim()
$dirtyLines = @(& git -C $repositoryRoot status --porcelain)

$environment = [pscustomobject][ordered]@{
    schema_version = '1.0'
    script_version = $scriptVersion
    capture_id = $captureId
    captured_utc = $captureStartedUtc.ToString('O')
    machine_label = $MachineLabel
    source_commit = $sourceCommit
    source_branch = $sourceBranch
    source_dirty = $dirtyLines.Count -gt 0
    os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    process_architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    logical_processor_count = [Environment]::ProcessorCount
    powershell_version = $PSVersionTable.PSVersion.ToString()
    host_framework = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    time_zone = [TimeZoneInfo]::Local.Id
    warmup_count = $WarmupCount
    measured_samples_per_warm_scenario = $SampleCount
    scenario_order = @(
        'cli.summary.clean.session_first',
        'cli.summary.clean.warm_cache_process',
        'cli.summary.error.warm_cache_process'
    )
    cli_target_framework = if ($null -ne $runtimeConfig) { $runtimeConfig.runtimeOptions.tfm } else { $null }
    cli_framework = if ($null -ne $runtimeConfig) { $runtimeConfig.runtimeOptions.framework } else { $null }
    cli_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $CliPath).Hash.ToLowerInvariant()
    cli_bytes = (Get-Item -LiteralPath $CliPath).Length
    cli_last_write_utc = (Get-Item -LiteralPath $CliPath).LastWriteTimeUtc.ToString('O')
    clean_fixture_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $CleanFixturePath).Hash.ToLowerInvariant()
    clean_fixture_bytes = (Get-Item -LiteralPath $CleanFixturePath).Length
    error_fixture_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $ErrorFixturePath).Hash.ToLowerInvariant()
    error_fixture_bytes = (Get-Item -LiteralPath $ErrorFixturePath).Length
    timing_boundary = 'Before Process.Start through exit and complete stdout/stderr drain.'
    percentile_method = 'Nearest rank: rank = ceil(percentile * n). No trimming.'
    binary_provenance = 'Caller-supplied Release apphost; this capture script does not build or publish.'
}

$samplesPath = Join-Path $OutputDirectory 'samples.csv'
$summaryPath = Join-Path $OutputDirectory 'summary.csv'
$environmentPath = Join-Path $OutputDirectory 'environment.json'
$summaryMarkdownPath = Join-Path $OutputDirectory 'summary.md'

$samples | Export-Csv -LiteralPath $samplesPath -NoTypeInformation -Encoding utf8
$summaries | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding utf8
$environment | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $environmentPath -Encoding utf8

$markdown = [Collections.Generic.List[string]]::new()
$markdown.Add('# PipelineGuard CLI performance capture')
$markdown.Add('')
$markdown.Add("- Capture ID: ``$captureId``")
$markdown.Add("- Captured UTC: ``$($captureStartedUtc.ToString('O'))``")
$markdown.Add("- Source: ``$sourceCommit``; dirty: ``$($environment.source_dirty)``")
$markdown.Add("- Samples: $SampleCount measured after $WarmupCount warm-ups per warm-cache scenario")
$markdown.Add('- Timing: process start through exit and complete stdout/stderr drain')
$markdown.Add('- Percentiles: nearest rank; no samples trimmed')
$markdown.Add('')
$markdown.Add('| Scenario | Valid | n | Failures | Min ms | p50 ms | p95 ms | Max ms | Mean ms | Stddev ms |')
$markdown.Add('| --- | :---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($summary in $summaries) {
    $markdown.Add("| $($summary.scenario) | $($summary.valid) | $($summary.n) | $($summary.failures) | $($summary.minimum_ms) | $($summary.p50_ms) | $($summary.p95_ms) | $($summary.maximum_ms) | $($summary.mean_ms) | $($summary.sample_stddev_ms) |")
}
$markdown.Add('')
$markdown.Add('The session-first row is a single observation, so p50/p95 are intentionally blank. It is not labelled a cold-cache benchmark. This capture is not an SLA; establish a baseline from at least three clean captures on the same reference host.')
$markdown | Set-Content -LiteralPath $summaryMarkdownPath -Encoding utf8

Write-Host "Performance evidence written to $OutputDirectory"
if (@($summaries | Where-Object { -not $_.valid }).Count -gt 0) {
    throw 'One or more scenarios failed correctness or sample-count validation. Inspect samples.csv.'
}
