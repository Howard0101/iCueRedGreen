<#
.SYNOPSIS
Payload wrapper that builds release staging via the meta-plane script.

.DESCRIPTION
This payload entrypoint is intentionally strict and depends on:
`D:\Source\ai\scripts\release\build-release-staging.ps1` by default.

If that path is unavailable, this script fails.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot,

    [Parameter()]
    [string]$ProjectsFilePath,

    [Parameter()]
    [string]$TargetReleaseVersion,

    [Parameter()]
    [string]$StagingRoot,

    [Parameter()]
    [string]$PublishProfileName = "FolderProfile",

    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string]$FallbackRuntimeIdentifier = "win-x64",

    [Parameter()]
    [switch]$IncludeComponentProjects,

    [Parameter()]
    [string[]]$RequiredFolderPath,

    [Parameter()]
    [ValidateSet("auto", "all-files", "collision-candidates")]
    [string]$HashMode = "auto",

    [Parameter()]
    [ValidateRange(200, 20000)]
    [int]$HashAutoFileThreshold = 5000,

    [Parameter()]
    [ValidateRange(256, 65536)]
    [int]$HashAutoSizeMiBThreshold = 3072,

    [Parameter()]
    [switch]$AllowSameVersionCollision,

    [Parameter()]
    [switch]$AsJson,

    [Parameter()]
    [string]$MetaScriptPath = "D:\Source\ai\scripts\release\build-release-staging.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetaScriptPath)) {
    throw "Meta build-release-staging script not found: $MetaScriptPath"
}

$repoRootDefault = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path

$forward = @{}
if ($PSBoundParameters.ContainsKey('RepoRoot')) {
    $forward['RepoRoot'] = $RepoRoot
} else {
    $forward['RepoRoot'] = $repoRootDefault
}
if ($PSBoundParameters.ContainsKey('ProjectsFilePath')) { $forward['ProjectsFilePath'] = $ProjectsFilePath }
if ($PSBoundParameters.ContainsKey('TargetReleaseVersion')) { $forward['TargetReleaseVersion'] = $TargetReleaseVersion }
if ($PSBoundParameters.ContainsKey('StagingRoot')) { $forward['StagingRoot'] = $StagingRoot }
if ($PSBoundParameters.ContainsKey('PublishProfileName')) { $forward['PublishProfileName'] = $PublishProfileName }
if ($PSBoundParameters.ContainsKey('Configuration')) { $forward['Configuration'] = $Configuration }
if ($PSBoundParameters.ContainsKey('FallbackRuntimeIdentifier')) { $forward['FallbackRuntimeIdentifier'] = $FallbackRuntimeIdentifier }
if ($PSBoundParameters.ContainsKey('IncludeComponentProjects')) { $forward['IncludeComponentProjects'] = $IncludeComponentProjects.IsPresent }
if ($PSBoundParameters.ContainsKey('RequiredFolderPath')) { $forward['RequiredFolderPath'] = $RequiredFolderPath }
if ($PSBoundParameters.ContainsKey('HashMode')) { $forward['HashMode'] = $HashMode }
if ($PSBoundParameters.ContainsKey('HashAutoFileThreshold')) { $forward['HashAutoFileThreshold'] = $HashAutoFileThreshold }
if ($PSBoundParameters.ContainsKey('HashAutoSizeMiBThreshold')) { $forward['HashAutoSizeMiBThreshold'] = $HashAutoSizeMiBThreshold }
if ($PSBoundParameters.ContainsKey('AllowSameVersionCollision')) { $forward['AllowSameVersionCollision'] = $AllowSameVersionCollision.IsPresent }
if ($PSBoundParameters.ContainsKey('AsJson')) { $forward['AsJson'] = $AsJson.IsPresent }

& $MetaScriptPath @forward
