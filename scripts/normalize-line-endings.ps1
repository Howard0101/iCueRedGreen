# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.8.0
<#
.SYNOPSIS
  Normalizes line endings for tracked text files in this repository.

.DESCRIPTION
  - Operates on files returned by: git ls-files
  - Skips binary files (git attributes: binary/-text, or NUL byte detection as fallback)
  - Respects .gitattributes EOL policy (eol=lf / eol=crlf)
  - Forces LF for GitHub Actions workflows: .github/workflows/**/*.yml|yaml
#>

[CmdletBinding()]
param(
  [switch]$DryRun,
  [string]$File,
  [switch]$AllFiles
)

function Get-FileContainsNulByte {
  param([string]$Path)
  try {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return ($bytes -contains 0)
  } catch {
    return $false
  }
}

function Get-GitAttrs {
  param([string]$RepoRelPath)
  # Returns hashtable with keys: text, binary, eol
  $attrs = @{
    text   = $null  # "set", "unset", or $null
    binary = $null  # "set", "unset", or $null
    eol    = $null  # "lf" / "crlf" / $null
  }

  try {
    $out = git check-attr -a -- "$RepoRelPath" 2>$null
    foreach ($line in $out) {
      # format: path: attr: value
      if ($line -match '^[^:]+:\s*([^:]+):\s*(.*)$') {
        $attr = $Matches[1].Trim()
        $val  = $Matches[2].Trim()

        switch ($attr) {
          "text" {
            if ($val -eq "set")   { $attrs.text = "set" }
            elseif ($val -eq "unset") { $attrs.text = "unset" }
          }
          "binary" {
            if ($val -eq "set")   { $attrs.binary = "set" }
            elseif ($val -eq "unset") { $attrs.binary = "unset" }
          }
          "eol" {
            if ($val -eq "lf" -or $val -eq "crlf") { $attrs.eol = $val }
          }
        }
      }
    }
  } catch {
    # ignore and fall back
  }

  return $attrs
}

function Normalize-FileEol {
  param(
    [string]$FullPath,
    [ValidateSet("lf","crlf")][string]$Eol
  )

  # Use UTF-8 without BOM to avoid introducing BOMs during normalization.
  $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
  $raw = [System.IO.File]::ReadAllText($FullPath, $utf8NoBom)

  # Normalize to LF first
  $normalized = $raw -replace '\r\n', "`n"
  $normalized = $normalized -replace '\r', "`n"

  if ($Eol -eq "crlf") {
    $normalized = $normalized -replace '\n', "`r`n"
  }

  if ($normalized -ne $raw) {
    if ($DryRun) {
      Write-Host "[DRY] Would normalize: $FullPath -> $Eol"
    } else {
      [System.IO.File]::WriteAllText($FullPath, $normalized, $utf8NoBom)
      Write-Host "Normalized: $FullPath -> $Eol"
    }
  }
}

# Ensure we run in repo root (where .git is accessible)
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
  throw "Not a git repository (git rev-parse failed)."
}
Set-Location $repoRoot

function Get-RepoRelativePath([string]$RepoRoot, [string]$FullPath) {
  $rootResolved = (Resolve-Path -LiteralPath $RepoRoot).Path.TrimEnd('\','/')
  $pResolved = (Resolve-Path -LiteralPath $FullPath).Path
  if (-not $pResolved.StartsWith($rootResolved, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Path is outside repo root: $FullPath"
  }
  $rel = $pResolved.Substring($rootResolved.Length).TrimStart('\','/')
  return $rel -replace '\\','/'
}

function Is-TrackedFile([string]$RepoRelPath) {
  $p = $RepoRelPath -replace '/','\'
  git ls-files --error-unmatch -- "$p" 2>$null | Out-Null
  return ($LASTEXITCODE -eq 0)
}

$defaultExcludeDirs = @(".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea")

$targets = New-Object System.Collections.Generic.List[string]
if ($File) {
  $fullFile = $File
  if (-not (Test-Path -LiteralPath $fullFile)) {
    $fullFile = Join-Path $repoRoot $File
  }
  if (-not (Test-Path -LiteralPath $fullFile)) { throw "File not found: $File" }
  $repoRel = Get-RepoRelativePath -RepoRoot $repoRoot -FullPath $fullFile
  $targets.Add($repoRel)
} elseif ($AllFiles) {
  $all = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force
  foreach ($it in $all) {
    $full = $it.FullName
    # Always exclude .git and common build/artifact dirs
    $rel = Get-RepoRelativePath -RepoRoot $repoRoot -FullPath $full
    $segments = $rel.Split('/')
    $skip = $false
    foreach ($seg in $segments) {
      if ($defaultExcludeDirs -contains $seg) { $skip = $true; break }
    }
    if ($skip) { continue }
    $targets.Add($rel)
  }
} else {
  git ls-files | ForEach-Object {
    if (-not [string]::IsNullOrWhiteSpace($_)) { $targets.Add(($_ -replace '\\','/')) }
  }
}

foreach ($f in $targets) {
  if ([string]::IsNullOrWhiteSpace($f)) { continue }

  # Force LF for GitHub workflows (Linux runners)
  $isWorkflow = ($f -like ".github/workflows/*.yml") -or ($f -like ".github/workflows/*.yaml") -or ($f -like ".github/workflows/*/*.yml") -or ($f -like ".github/workflows/*/*.yaml")

  $full = Join-Path $repoRoot ($f -replace '/','\')
  if (-not (Test-Path -LiteralPath $full)) { continue }

  $isTracked = Is-TrackedFile -RepoRelPath $f

  if ($isTracked) {
    $attrs = Get-GitAttrs -RepoRelPath $f

    # Skip binaries via attributes
    if ($attrs.binary -eq "set" -or $attrs.text -eq "unset") { continue }

    # Fallback binary detection (NUL byte)
    if (Get-FileContainsNulByte -Path $full) { continue }

    # Determine EOL
    $eol = $attrs.eol
    if ($isWorkflow) { $eol = "lf" }
    if (-not $eol) { $eol = "crlf" } # Windows-target default
  } else {
    # Untracked files (AllFiles mode): use heuristic binary detection only
    if (Get-FileContainsNulByte -Path $full) { continue }
    $eol = "crlf"
    if ($isWorkflow) { $eol = "lf" }
  }

  Normalize-FileEol -FullPath $full -Eol $eol
}
