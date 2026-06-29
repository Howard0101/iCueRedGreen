<#
.SYNOPSIS
Payload stub that forwards TEMP lifecycle checks to the meta-plane script.

.DESCRIPTION
This script exists in the payload template as a compatibility entrypoint.
It forwards parameters to the authoritative meta-plane implementation:
`D:\Source\ai\scripts\invoke-temp-lifecycle-check.ps1` by default.

Use `-MetaScriptPath` to override the target script location.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("Clean","BootstrapReport","UpgradeReport","BothReport","LifecycleSuite","UpgradeReadmeObsoleteReport","ReleasePrecheck")]
    [string]$Action = "LifecycleSuite",

    [Parameter()]
    [string]$ScenarioName = "lifecycle_validation",

    [Parameter()]
    [string]$TempRootPath,

    [Parameter()]
    [string]$TempRepoPath,

    [Parameter()]
    [string]$FromVersion = "1.9.7",

    [Parameter()]
    [string]$ToVersion,

    [Parameter()]
    [string]$TemplateSourcePath = "D:\Source\ai\Template",

    [Parameter()]
    [switch]$RunLifecycleChecks,

    [Parameter()]
    [ValidateRange(1, 200)]
    [int]$ReleaseComponentCount = 1,

    [Parameter()]
    [string]$ReleaseComponentMatrixPath,

    [Parameter()]
    [switch]$RetainTempArtifacts,

    [Parameter()]
    [string]$MetaScriptPath = "D:\Source\ai\scripts\invoke-temp-lifecycle-check.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetaScriptPath)) {
    throw "Meta invoke-temp-lifecycle-check script not found: $MetaScriptPath"
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($TempRootPath)) {
    $TempRootPath = Join-Path $repoRoot "TEMP"
}

$forward = @{}
if ($PSBoundParameters.ContainsKey('Action')) { $forward['Action'] = $Action }
if ($PSBoundParameters.ContainsKey('ScenarioName')) { $forward['ScenarioName'] = $ScenarioName }
$forward['TempRootPath'] = $TempRootPath
if ($PSBoundParameters.ContainsKey('TempRepoPath')) { $forward['TempRepoPath'] = $TempRepoPath }
if ($PSBoundParameters.ContainsKey('FromVersion')) { $forward['FromVersion'] = $FromVersion }
if ($PSBoundParameters.ContainsKey('ToVersion')) { $forward['ToVersion'] = $ToVersion }
if ($PSBoundParameters.ContainsKey('TemplateSourcePath')) { $forward['TemplateSourcePath'] = $TemplateSourcePath }
if ($PSBoundParameters.ContainsKey('RunLifecycleChecks')) { $forward['RunLifecycleChecks'] = $RunLifecycleChecks }
if ($PSBoundParameters.ContainsKey('ReleaseComponentCount')) { $forward['ReleaseComponentCount'] = $ReleaseComponentCount }
if ($PSBoundParameters.ContainsKey('ReleaseComponentMatrixPath')) { $forward['ReleaseComponentMatrixPath'] = $ReleaseComponentMatrixPath }
if ($PSBoundParameters.ContainsKey('RetainTempArtifacts')) { $forward['RetainTempArtifacts'] = $RetainTempArtifacts }

& $MetaScriptPath @forward
