#requires -Version 7.0
# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.8.0
<#
.SYNOPSIS
  Generates docs/changelog/CHANGELOG.md from a plain-text changelog source.

.PARAMETER SourcePath
  Path to the legacy changelog source (history.txt or changelog.txt).

.PARAMETER TargetPath
  Path to the Markdown changelog to write.

.EXAMPLE
  pwsh -File .\.template\bootstrap\generate-changelog.ps1 -SourcePath .\history.txt -TargetPath .\docs\changelog\CHANGELOG.md
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$SourcePath,
  [Parameter(Mandatory)][string]$TargetPath
)

$ErrorActionPreference = "Stop"

function Resolve-InputPath {
  param([Parameter(Mandatory)][string]$Path)
  if ([System.IO.Path]::IsPathRooted($Path)) {
    return (Resolve-Path -LiteralPath $Path).Path
  }
  return (Resolve-Path -LiteralPath (Join-Path (Get-Location) $Path)).Path
}

function Write-AtomicUtf8NoBom {
  param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Content
  )

  $dir = Split-Path -Parent $Path
  if (-not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
  }

  $tmp = Join-Path $dir ("." + [System.IO.Path]::GetFileName($Path) + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
  $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

  try {
    [System.IO.File]::WriteAllText($tmp, $Content, $utf8NoBom)
    Move-Item -LiteralPath $tmp -Destination $Path -Force
  } finally {
    if (Test-Path -LiteralPath $tmp) {
      Remove-Item -LiteralPath $tmp -Force
    }
  }
}

$source = Resolve-InputPath -Path $SourcePath
if (-not (Test-Path -LiteralPath $source)) {
  throw "Source file not found: $source"
}

$target = $TargetPath
if (-not [System.IO.Path]::IsPathRooted($target)) {
  $target = Join-Path (Get-Location) $target
}

$raw = Get-Content -LiteralPath $source -Raw

$nl = "`r`n"
$lines = @()
$lines += "# CHANGELOG"
$lines += ""
$lines += $raw.TrimEnd()
$content = ($lines -join $nl) + $nl

Write-AtomicUtf8NoBom -Path $target -Content $content

Write-Host ("Generated: " + $target)
