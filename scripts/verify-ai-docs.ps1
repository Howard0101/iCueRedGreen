<#
.SYNOPSIS
Validates local markdown links for AI docs.

.DESCRIPTION
Docs-check-only utility. It validates local markdown links in README.md and in
all markdown files reachable transitively from README.md.
Mermaid rendering was removed from this script.

.PARAMETER Mode
Validation mode. "check" is the canonical mode.
"render" is accepted only as a compatibility alias and performs the same checks.

.PARAMETER RepoRoot
Optional path to the repository root that contains README.md.
Defaults to the parent folder of this script directory.

.EXAMPLE
pwsh -NoProfile -File .\scripts\verify-ai-docs.ps1 -Mode check

.EXAMPLE
pwsh -NoProfile -File .\scripts\verify-ai-docs.ps1 -Mode check -RepoRoot D:\Source\ai\Template
#>

[CmdletBinding()]
param(
  [ValidateSet("check","render")]
  [string]$Mode = "check",

  [string]$RepoRoot
)

$ErrorActionPreference = "Stop"

function Get-ResolvedRepoRoot {
  param([string]$RepoRoot)

  if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
    if (-not (Test-Path -LiteralPath $RepoRoot)) {
      throw "Repo root not found: ${RepoRoot}"
    }
    $resolved = (Resolve-Path -LiteralPath $RepoRoot).Path
    $item = Get-Item -LiteralPath $resolved
    if (-not $item.PSIsContainer) {
      throw "Repo root is not a directory: ${RepoRoot}"
    }
    return $resolved
  }

  return (Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath "..")).Path
}
# End: Get-ResolvedRepoRoot

function Get-RootReadme {
  param([Parameter(Mandatory)][string]$RepoRoot)

  $readme = Join-Path -Path $RepoRoot -ChildPath "README.md"
  if (Test-Path -LiteralPath $readme) {
    return (Resolve-Path -LiteralPath $readme).Path
  }
  return $null
}
# End: Get-RootReadme

function Get-LocalMarkdownLinks {
  param([Parameter(Mandatory)][string]$MarkdownText)

  $pattern = '\[[^\]]+\]\(([^)]+)\)'
  $matches = [regex]::Matches($MarkdownText, $pattern)
  $links = New-Object System.Collections.Generic.List[string]

  foreach ($m in $matches) {
    $raw = $m.Groups[1].Value.Trim()
    if ($raw.StartsWith("http://") -or $raw.StartsWith("https://") -or $raw.StartsWith("mailto:")) { continue }
    if ($raw.StartsWith("#")) { continue }
    if ($raw -match '(?i)#ai-docs-ignore') { continue }
    $path = $raw.Split('#')[0].Trim().Trim('"').Trim("'")
    if ($path -eq "") { continue }
    $links.Add($path) | Out-Null
  }

  return $links
}
# End: Get-LocalMarkdownLinks

function Resolve-LinkTarget {
  param(
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$SourceFilePath,
    [Parameter(Mandatory)][string]$LinkPath
  )

  $externalSentinel = "__EXTERNAL__"
  $rootResolved = (Resolve-Path -LiteralPath $RepoRoot).Path
  $clean = $LinkPath.Split('#')[0].Split('?')[0].Trim().Trim('"').Trim("'")
  if ($clean -eq "") { return $null }

  $decoded = [System.Uri]::UnescapeDataString($clean)

  $isInsideRepo = {
    param([Parameter(Mandatory)][string]$PathToCheck)
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
      return $externalSentinel
    }

    if (-not (Test-Path -LiteralPath $localPath)) { return $externalSentinel }
    if (-not (& $isInsideRepo $localPath)) { return $externalSentinel }
    return (Resolve-Path -LiteralPath $localPath).Path
  }

  if ([System.IO.Path]::IsPathRooted($decoded)) {
    if (-not (Test-Path -LiteralPath $decoded)) { return $null }
    if (-not (& $isInsideRepo $decoded)) { return $externalSentinel }
    return (Resolve-Path -LiteralPath $decoded).Path
  }

  $sourceDir = Split-Path -Parent $SourceFilePath
  $candidate1 = Join-Path -Path $sourceDir -ChildPath $decoded
  if (Test-Path -LiteralPath $candidate1) {
    return (Resolve-Path -LiteralPath $candidate1).Path
  }

  $candidate2 = Join-Path -Path $RepoRoot -ChildPath $decoded
  if (Test-Path -LiteralPath $candidate2) {
    return (Resolve-Path -LiteralPath $candidate2).Path
  }

  return $null
}
# End: Resolve-LinkTarget

function Get-HumanReadableMarkdownFiles {
  param([Parameter(Mandatory)][string]$RepoRoot)

  $readme = Get-RootReadme -RepoRoot $RepoRoot
  if ($null -eq $readme) { return @() }

  $queue = New-Object System.Collections.Generic.Queue[string]
  $visited = New-Object System.Collections.Generic.HashSet[string]
  $result = New-Object System.Collections.Generic.List[string]
  $queue.Enqueue($readme)

  while ($queue.Count -gt 0) {
    $current = $queue.Dequeue()
    if (-not (Test-Path -LiteralPath $current)) { continue }

    $resolvedCurrent = (Resolve-Path -LiteralPath $current).Path
    if ($visited.Contains($resolvedCurrent)) { continue }

    $visited.Add($resolvedCurrent) | Out-Null
    $result.Add($resolvedCurrent) | Out-Null

    $text = Get-Content -LiteralPath $resolvedCurrent -Raw
    $links = Get-LocalMarkdownLinks -MarkdownText $text
    foreach ($lnk in $links) {
      $resolved = Resolve-LinkTarget -RepoRoot $RepoRoot -SourceFilePath $resolvedCurrent -LinkPath $lnk
      if ($null -eq $resolved -or $resolved -eq "__EXTERNAL__") { continue }

      if ((Test-Path -LiteralPath $resolved) -and (-not (Get-Item -LiteralPath $resolved).PSIsContainer) -and ([System.IO.Path]::GetExtension($resolved) -ieq ".md")) {
        $queue.Enqueue($resolved)
      }
    }
  }

  return $result
}
# End: Get-HumanReadableMarkdownFiles

function Check-LocalLinks {
  param([Parameter(Mandatory)][string]$RepoRoot)

  $externalSentinel = "__EXTERNAL__"
  $files = Get-HumanReadableMarkdownFiles -RepoRoot $RepoRoot
  if ($files.Count -eq 0) {
    throw "README.md not found at repo root, or no markdown files reachable from README."
  }

  $errors = New-Object System.Collections.Generic.List[string]
  $external = New-Object System.Collections.Generic.List[string]
  $linkCount = 0

  foreach ($filePath in $files) {
    $text = Get-Content -LiteralPath $filePath -Raw
    $links = Get-LocalMarkdownLinks -MarkdownText $text
    foreach ($lnk in $links) {
      $linkCount++
      $resolved = Resolve-LinkTarget -RepoRoot $RepoRoot -SourceFilePath $filePath -LinkPath $lnk
      if ($resolved -eq $externalSentinel) {
        $external.Add($lnk) | Out-Null
        continue
      }
      if ($null -eq $resolved) {
        $errors.Add("Broken link in ${filePath}: $lnk") | Out-Null
        continue
      }
      if (-not (Test-Path -LiteralPath $resolved)) {
        $errors.Add("Broken link in ${filePath}: $lnk") | Out-Null
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
    Write-Host ("WARN: skipped external targets: {0} (examples: {1})" -f $external.Count, ($examples -join ", "))
  }

  return [ordered]@{
    filesChecked    = $files.Count
    linksChecked    = $linkCount
    externalSkipped = $external.Count
  }
}
# End: Check-LocalLinks

# ---- Main
if ($Mode -eq "render") {
  Write-Warning "Mode 'render' is deprecated; this script is docs-check-only and now runs link checks only."
}

$repo = Get-ResolvedRepoRoot -RepoRoot $RepoRoot
$result = Check-LocalLinks -RepoRoot $repo

Write-Host ("Docs check passed. Files checked: {0}, links checked: {1}, external skipped: {2}" -f $result.filesChecked, $result.linksChecked, $result.externalSkipped)
