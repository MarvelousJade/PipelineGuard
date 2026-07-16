[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $EngineRoot = $env:UE_ROOT,

    [Parameter(Mandatory = $false)]
    [string] $Project,

    [Parameter(Mandatory = $false)]
    [string] $Report
)

$ErrorActionPreference = 'Stop'

$RepositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Project)) {
    $Project = Join-Path $RepositoryRoot 'unreal/PipelineGuardHost.uproject'
}
if ([string]::IsNullOrWhiteSpace($Report)) {
    $Report = Join-Path $RepositoryRoot 'artifacts/pipelineguard-report.json'
}
if ([string]::IsNullOrWhiteSpace($EngineRoot)) {
    throw 'Pass -EngineRoot or set UE_ROOT to an Unreal Engine 5.6 installation.'
}

$ResolvedProject = (Resolve-Path -LiteralPath $Project).Path
$ResolvedEngineRoot = (Resolve-Path -LiteralPath $EngineRoot).Path
$RunUat = Join-Path $ResolvedEngineRoot 'Engine/Build/BatchFiles/RunUAT.bat'
$Graph = Join-Path $RepositoryRoot 'unreal/Build/PipelineGuard.BuildGraph.xml'

if (-not (Test-Path -LiteralPath $RunUat -PathType Leaf)) {
    throw "RunUAT.bat was not found under '$ResolvedEngineRoot'."
}
if (-not (Test-Path -LiteralPath $Graph -PathType Leaf)) {
    throw "BuildGraph script was not found at '$Graph'."
}

$ReportDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($Report))
New-Item -ItemType Directory -Force -Path $ReportDirectory | Out-Null
$ResolvedReport = [IO.Path]::GetFullPath($Report)

& $RunUat `
    'BuildGraph' `
    "-Script=$Graph" `
    '-Target=PipelineGuard' `
    "-set:Project=$ResolvedProject" `
    "-set:Report=$ResolvedReport" `
    '-NoP4'

exit $LASTEXITCODE
