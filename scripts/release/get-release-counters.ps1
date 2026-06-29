<#
.SYNOPSIS
Payload wrapper that forwards release-counter queries to the meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on:
`D:\Source\ai\scripts\release\get-release-counters.ps1` by default.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot,

    [Parameter()]
    [string]$ChangelogPath,

    [Parameter()]
    [string]$PlanPath,

    [Parameter()]
    [switch]$AsJson,

    [Parameter()]
    [string]$MetaScriptPath = "D:\Source\ai\scripts\release\get-release-counters.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetaScriptPath)) {
    throw "Meta get-release-counters script not found: $MetaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path

$forward = @{}
if ($PSBoundParameters.ContainsKey('RepoRoot')) {
    $forward['RepoRoot'] = $RepoRoot
} else {
    $forward['RepoRoot'] = $repoRootDefault
}
if ($PSBoundParameters.ContainsKey('ChangelogPath')) { $forward['ChangelogPath'] = $ChangelogPath }
if ($PSBoundParameters.ContainsKey('PlanPath')) { $forward['PlanPath'] = $PlanPath }
if ($PSBoundParameters.ContainsKey('AsJson')) { $forward['AsJson'] = $AsJson }

& $MetaScriptPath @forward
