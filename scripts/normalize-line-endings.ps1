<#
.SYNOPSIS
Payload stub that forwards line-ending normalization to the meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on the meta script:
`D:\Source\ai\scripts\normalize-line-endings.ps1`.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot,

    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [Alias('File')]
    [string]$FilePath,

    [Parameter()]
    [string]$FilePathList,

    [Parameter()]
    [switch]$AllFiles
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$metaScriptPath = "D:\Source\ai\scripts\normalize-line-endings.ps1"
if (-not (Test-Path -LiteralPath $metaScriptPath)) {
    throw "Meta normalize-line-endings script not found: $metaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

$repoRootValue = if ($PSBoundParameters.ContainsKey('RepoRoot')) { $RepoRoot } else { $repoRootDefault }

if ($PSBoundParameters.ContainsKey('FilePath')) {
    & $metaScriptPath -RepoRoot $repoRootValue -DryRun:$DryRun -FilePathList $FilePath -AllFiles:$AllFiles
} elseif ($PSBoundParameters.ContainsKey('FilePathList')) {
    & $metaScriptPath -RepoRoot $repoRootValue -DryRun:$DryRun -FilePathList $FilePathList -AllFiles:$AllFiles
} else {
    & $metaScriptPath -RepoRoot $repoRootValue -DryRun:$DryRun -AllFiles:$AllFiles
}
