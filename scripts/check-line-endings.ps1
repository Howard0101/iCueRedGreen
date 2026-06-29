<#
.SYNOPSIS
Payload stub that forwards line-ending checks to the meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on the meta script:
`D:\Source\ai\scripts\check-line-endings.ps1`.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$metaScriptPath = "D:\Source\ai\scripts\check-line-endings.ps1"
if (-not (Test-Path -LiteralPath $metaScriptPath)) {
    throw "Meta check-line-endings script not found: $metaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

$forward = @{}
if ($PSBoundParameters.ContainsKey('RepoRoot')) {
    $forward['RepoRoot'] = $RepoRoot
} else {
    $forward['RepoRoot'] = $repoRootDefault
}
if ($PSBoundParameters.ContainsKey('Fix')) { $forward['Fix'] = $Fix }
if ($PSBoundParameters.ContainsKey('AsJson')) { $forward['AsJson'] = $AsJson }

& $metaScriptPath @forward
