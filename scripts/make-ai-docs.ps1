# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.8.2

<#
.SYNOPSIS
Renders Mermaid diagrams and validates local markdown links for the AI rules docs.

.DESCRIPTION
- Renders docs/diagrams/ai_codex_workflow.mmd -> docs/diagrams/ai_codex_workflow.png using mermaid-cli (mmdc).
- Optional: also render SVG.
- Checks that committed artifacts are up to date (check mode).
- Validates that local markdown links in README.md and all markdown files linked transitively from it resolve to existing files.

.PARAMETER Mode
"render" (default): render and overwrite committed artifacts.
"check": render to temp and FAIL if output differs from committed artifacts.

.PARAMETER Scale
Mermaid render scale factor (default 2).

.PARAMETER Format
png | svg | both (default png)

.EXAMPLE
pwsh -File .\scripts\make-ai-docs.ps1 -Mode render -Format png

.EXAMPLE
pwsh -File .\scripts\make-ai-docs.ps1 -Mode check -Format both
#>

[CmdletBinding()]
param(
  [ValidateSet("render","check")]
  [string]$Mode = "render",

  [ValidateRange(1,6)]
  [int]$Scale = 2,

  [ValidateSet("png","svg","both")]
  [string]$Format = "png"
)

function Warn-UnquotedMermaidLabels {
  param(
    [Parameter(Mandatory)][string]$MmdPath
  )
  if (-not (Test-Path -LiteralPath $MmdPath)) { return }

  $i = 0
  Get-Content -LiteralPath $MmdPath | ForEach-Object {
    $i++
    $line = $_

    if ($line -match '^\s*%%' -or $line -match '^\s*$') { return }

    foreach ($m in ([regex]::Matches($line, '(\[[^\]]*\]|\{[^\}]*\})'))) {
      $tok = $m.Value
      $inner = $tok.Substring(1, $tok.Length - 2)

      if ($inner.StartsWith('"')) { continue }

      if ($inner.Contains('(') -or $inner.Contains(')') -or $inner.Contains('"')) {
        Write-Warning ("Mermaid label may need quotes: {0}:{1}: {2}" -f $MmdPath, $i, $line.Trim())
        Write-Warning 'Hint: wrap label text in quotes, e.g. B["text (with parens)"] or G{"User explicit ''go''?"}'
      }
    }
  }
}

$ErrorActionPreference = "Stop"

function Write-DebugLine([string]$Message) {
  Write-Host ("DEBUG: " + $Message)
}

function Get-RepoRoot {
  return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Ensure-MermaidCli {
  $mmdc = Get-Command mmdc -ErrorAction SilentlyContinue
  if ($null -ne $mmdc) { return }

  Write-Host "mmdc not found. Installing @mermaid-js/mermaid-cli globally..."
  $npm = Get-Command npm -ErrorAction SilentlyContinue
  if ($null -eq $npm) {
    throw "npm is required to install mermaid-cli. Install Node.js + npm and rerun."
  }

  npm install -g @mermaid-js/mermaid-cli | Out-Host

  $mmdc = Get-Command mmdc -ErrorAction SilentlyContinue
  if ($null -eq $mmdc) {
    throw "Failed to install mmdc. Ensure Node/npm are working and retry."
  }
}

function Get-FileHashHex([string]$Path) {
  $bytes = [System.IO.File]::ReadAllBytes($Path)
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    return ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
  } finally {
    $sha.Dispose()
  }
}

function Invoke-MermaidRender([string]$InMmd, [string]$OutPath, [int]$Scale) {
  Warn-UnquotedMermaidLabels -MmdPath $InMmd
  mmdc -i $InMmd -o $OutPath -b transparent -s $Scale | Out-Host
}

function Render-Or-Check([string]$Mode, [string]$InMmd, [string]$CommittedOut, [string]$Label, [int]$Scale) {
  if ($Mode -eq "render") {
    Write-Host "Rendering ${Label}: $InMmd -> $CommittedOut"
    Invoke-MermaidRender -InMmd $InMmd -OutPath $CommittedOut -Scale $Scale
    return
  }

  if (-not (Test-Path $CommittedOut)) {
    throw "Committed ${Label} missing: $CommittedOut (run Mode=render and commit the result)."
  }

  $tmp = Join-Path $env:TEMP ("ai_codex_workflow_" + [Guid]::NewGuid().ToString("N") + "." + $Label)
  try {
    Write-Host "Rendering (check) ${Label}: $InMmd -> $tmp"
    Invoke-MermaidRender -InMmd $InMmd -OutPath $tmp -Scale $Scale

    $h1 = Get-FileHashHex $tmp
    $h2 = Get-FileHashHex $CommittedOut
    if ($h1 -ne $h2) {
      throw "Diagram ${Label} out of date. Re-render with: pwsh -File .\scripts\make-ai-docs.ps1 -Mode render -Format $Format"
    }

    Write-Host "Diagram ${Label} up to date."
  } finally {
    if (Test-Path $tmp) { Remove-Item -Force $tmp }
  }
}

function Render-WorkflowDiagrams([string]$RepoRoot, [string]$Mode, [int]$Scale, [string]$Format) {
  $mmdIn = Join-Path $RepoRoot "docs\diagrams\ai_codex_workflow.mmd"
  if (-not (Test-Path $mmdIn)) {
    Write-Host "Skipping workflow diagram render (missing): $mmdIn"
    return
  }

  Ensure-MermaidCli

  $wantPng = ($Format -eq "png" -or $Format -eq "both")
  $wantSvg = ($Format -eq "svg" -or $Format -eq "both")

  if ($wantPng) {
    $pngOut = Join-Path $RepoRoot "docs\diagrams\ai_codex_workflow.png"
    Render-Or-Check -Mode $Mode -InMmd $mmdIn -CommittedOut $pngOut -Label "png" -Scale $Scale
  }

  if ($wantSvg) {
    $svgOut = Join-Path $RepoRoot "docs\diagrams\ai_codex_workflow.svg"
    Render-Or-Check -Mode $Mode -InMmd $mmdIn -CommittedOut $svgOut -Label "svg" -Scale $Scale
  }
}

function Get-RootReadme([string]$RepoRoot) {
  $readme = Join-Path $RepoRoot "README.md"
  if (Test-Path $readme) { return (Resolve-Path $readme).Path }
  return $null
}

function Get-HumanReadableMarkdownFiles([string]$RepoRoot) {
  # Entry point: README.md
  $readme = Get-RootReadme $RepoRoot
  if ($null -eq $readme) {
    Write-Host "No README.md found at repo root; skipping link graph expansion."
    return @()
  }

  $queue = New-Object System.Collections.Generic.Queue[string]
  $visited = New-Object System.Collections.Generic.HashSet[string]
  $result = New-Object System.Collections.Generic.List[string]

  $queue.Enqueue($readme)

  while ($queue.Count -gt 0) {
    $current = $queue.Dequeue()
    if (-not (Test-Path $current)) { continue }

    $resolvedCurrent = (Resolve-Path $current).Path
    if ($visited.Contains($resolvedCurrent)) { continue }

    $visited.Add($resolvedCurrent) | Out-Null
    $result.Add($resolvedCurrent) | Out-Null

    $text = Get-Content -Path $resolvedCurrent -Raw
    $links = Get-LocalMarkdownLinks $text
    foreach ($lnk in $links) {
      $resolved = Resolve-LinkTarget $RepoRoot $resolvedCurrent $lnk
      if ($null -eq $resolved) { continue }

      # Only expand into markdown files; directories and non-md targets are still validated in Check-LocalLinks.
      if ((Test-Path $resolved) -and (-not (Get-Item $resolved).PSIsContainer) -and ([System.IO.Path]::GetExtension($resolved) -ieq ".md")) {
        $queue.Enqueue($resolved)
      }
    }
  }

  return $result
}


function Get-LocalMarkdownLinks([string]$MarkdownText) {
  $pattern = '\[[^\]]+\]\(([^)]+)\)'
  $matches = [regex]::Matches($MarkdownText, $pattern)
  $links = @()
  foreach ($m in $matches) {
    $raw = $m.Groups[1].Value.Trim()
    if ($raw.StartsWith("http://") -or $raw.StartsWith("https://") -or $raw.StartsWith("mailto:")) { continue }
    if ($raw.StartsWith("#")) { continue }
    if ($raw -match '(?i)#ai-docs-ignore') { continue }
    $path = $raw.Split('#')[0].Trim().Trim('"').Trim("'")
    if ($path -eq "") { continue }
    $links += $path
  }
  return $links
}

function Resolve-LinkTarget([string]$RepoRoot, [string]$SourceFilePath, [string]$LinkPath) {
  $ExternalSentinel = "__EXTERNAL__"

  $rootResolved = $RepoRoot
  try { $rootResolved = (Resolve-Path -LiteralPath $RepoRoot).Path } catch { }

  $clean = $LinkPath.Split('#')[0].Split('?')[0].Trim().Trim('"').Trim("'")
  if ($clean -eq "") { return $null }

  $decoded = [System.Uri]::UnescapeDataString($clean)

  $isInsideRepo = {
    param([string]$PathToCheck)
    try {
      $resolved = (Resolve-Path -LiteralPath $PathToCheck).Path
      return $resolved.StartsWith($rootResolved, [System.StringComparison]::OrdinalIgnoreCase)
    } catch {
      return $false
    }
  }

  if ($decoded -match '^(?i)file:///') {
    try {
      $uri = [uri]$decoded
      $localPath = $uri.LocalPath
    } catch {
      return $ExternalSentinel
    }

    if (-not (Test-Path -LiteralPath $localPath)) { return $ExternalSentinel }
    if (-not (& $isInsideRepo $localPath)) { return $ExternalSentinel }
    return (Resolve-Path -LiteralPath $localPath).Path
  }

  if ([System.IO.Path]::IsPathRooted($decoded)) {
    if (-not (Test-Path -LiteralPath $decoded)) { return $null }
    if (-not (& $isInsideRepo $decoded)) { return $ExternalSentinel }
    return (Resolve-Path -LiteralPath $decoded).Path
  }

  $sourceDir = Split-Path -Parent $SourceFilePath
  $candidate1 = Join-Path $sourceDir $decoded
  if (Test-Path -LiteralPath $candidate1) { return (Resolve-Path -LiteralPath $candidate1).Path }

  $candidate2 = Join-Path $RepoRoot $decoded
  if (Test-Path -LiteralPath $candidate2) { return (Resolve-Path -LiteralPath $candidate2).Path }

  return $null
}

function Check-LocalLinks([string]$RepoRoot) {
  $ExternalSentinel = "__EXTERNAL__"
  $files = Get-HumanReadableMarkdownFiles $RepoRoot
  if ($files.Count -eq 0) {
    Write-Host "No human-readable markdown files found from README graph; skipping link check."
    return
  }

  $errors = New-Object System.Collections.Generic.List[string]
  $external = New-Object System.Collections.Generic.List[string]
  foreach ($filePath in $files) {
    $text = Get-Content -Path $filePath -Raw
    $links = Get-LocalMarkdownLinks $text
    foreach ($lnk in $links) {
      $resolved = Resolve-LinkTarget $RepoRoot $filePath $lnk
      if ($resolved -eq $ExternalSentinel) { $external.Add($lnk); continue }
      if ($null -eq $resolved) {
        $errors.Add("Broken link in ${filePath}: $lnk")
        continue
      }
      # If it resolves, it must exist; directories are acceptable targets for navigation links like docs/
      if (-not (Test-Path $resolved)) {
        $errors.Add("Broken link in ${filePath}: $lnk")
      }
    }
  }

  if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Broken local links found:"
    $errors | ForEach-Object { Write-Host ("- " + $_) }
    throw "Local link validation failed."
  }

  if ($external.Count -gt 0) {
  $examples = $external | Select-Object -First 3
  Write-Host ("WARN: Skipped external targets: {0} (examples: {1})" -f $external.Count, ($examples -join ", "))
}

Write-Host "Local link validation passed."
}


# ---- Main
$repo = Get-RepoRoot
Write-DebugLine "Repo root: $repo"
Render-WorkflowDiagrams -RepoRoot $repo -Mode $Mode -Scale $Scale -Format $Format
Check-LocalLinks -RepoRoot $repo
Write-Host "Done."
