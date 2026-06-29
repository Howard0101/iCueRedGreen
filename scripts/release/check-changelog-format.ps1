<#
.SYNOPSIS
Payload wrapper that validates/migrates changelog format via meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on:
`D:\Source\ai\scripts\release\check-changelog-format.ps1` by default.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [string[]]$Path,
    [string]$RepoRoot,
    [ValidateSet("Validate","Migrate")]
    [string]$Mode = "Validate",
    [string]$BackupRoot,
    [switch]$Strict,
    [switch]$RequireProjectVersionPrefix,
    [switch]$AsJson,
    [string]$MetaScriptPath = "D:\Source\ai\scripts\release\check-changelog-format.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetaScriptPath)) {
    throw "Meta changelog format script not found: $MetaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path

$forward = @{
    Mode = $Mode
}
if ($PSBoundParameters.ContainsKey('Path')) { $forward['Path'] = $Path }
if ($PSBoundParameters.ContainsKey('RepoRoot')) {
    $forward['RepoRoot'] = $RepoRoot
} else {
    $forward['RepoRoot'] = $repoRootDefault
}
if ($PSBoundParameters.ContainsKey('BackupRoot')) { $forward['BackupRoot'] = $BackupRoot }
if ($PSBoundParameters.ContainsKey('Strict')) { $forward['Strict'] = $Strict.IsPresent }
if ($PSBoundParameters.ContainsKey('RequireProjectVersionPrefix')) { $forward['RequireProjectVersionPrefix'] = $RequireProjectVersionPrefix.IsPresent }
if ($PSBoundParameters.ContainsKey('AsJson')) { $forward['AsJson'] = $AsJson.IsPresent }

& $MetaScriptPath @forward
