<#
.SYNOPSIS
Payload wrapper that sanitizes hidden changelog parameters via meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on:
`D:\Source\ai\scripts\release\release-sanitize-changelog.ps1` by default.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version,

    [Parameter()]
    [string]$RepoRoot,

    [Parameter()]
    [string[]]$ChangelogPath,

    [Parameter()]
    [switch]$AsJson,

    [Parameter()]
    [string]$MetaScriptPath = "D:\Source\ai\scripts\release\release-sanitize-changelog.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetaScriptPath)) {
    throw "Meta changelog sanitizer script not found: $MetaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path

$forward = @{
    Version = $Version
}
if ($PSBoundParameters.ContainsKey('RepoRoot')) {
    $forward['RepoRoot'] = $RepoRoot
} else {
    $forward['RepoRoot'] = $repoRootDefault
}
if ($PSBoundParameters.ContainsKey('ChangelogPath')) { $forward['ChangelogPath'] = $ChangelogPath }
if ($PSBoundParameters.ContainsKey('AsJson')) { $forward['AsJson'] = $AsJson }

& $MetaScriptPath @forward
