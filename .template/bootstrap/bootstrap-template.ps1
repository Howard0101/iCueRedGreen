#requires -Version 7.0
# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.8.3
<#
.SYNOPSIS
  Bootstraps template-managed files into a repository without a template.

.PARAMETER TemplateSourcePath
  Path to the canonical template source directory.

.PARAMETER TargetRepoPath
  Optional path to the target repository root.

.PARAMETER Mode
  Safe (default), Strict, or ReportOnly.

.PARAMETER DryRun
  Report-only behavior without writing template-managed files.

.PARAMETER ReportDir
  Directory to write the markdown and JSON reports.

.PARAMETER ForceChangelog
  Regenerate docs/changelog/CHANGELOG.md even if it already exists.

.PARAMETER RemoveLegacyChangelogFiles
  Remove legacy changelog source files after migration.

.PARAMETER ConfigureReadme
  Update README marker blocks after bootstrap (default: $true).

.EXAMPLE
  pwsh -File .\.template\bootstrap\bootstrap-template.ps1 -TemplateSourcePath D:\Source\ai\template -Mode Safe

.EXAMPLE
  pwsh -File D:\Source\ai\Template\.template\bootstrap\bootstrap-template.ps1 -TemplateSourcePath D:\Source\ai\Template -TargetRepoPath D:\Source\Repos\MyNewRepo -Mode ReportOnly
#>

[CmdletBinding()]
param(
  [string]$TemplateSourcePath = "D:\Source\ai\template",
  [string]$TargetRepoPath,

  [ValidateSet("Safe","Strict","ReportOnly")]
  [string]$Mode = "Safe",

  [switch]$DryRun,

  [string]$ReportDir,

  [switch]$ForceChangelog,

  [switch]$RemoveLegacyChangelogFiles,

  [bool]$ConfigureReadme = $true
)

$ErrorActionPreference = "Stop"

$CurrentTemplateVersion = "1.8.3"
$GitIgnoreBlockStart = "### BEGIN TEMPLATE-MANAGED (DO NOT EDIT)"
$GitIgnoreBlockEnd = "### END TEMPLATE-MANAGED"
$GitIgnoreManagedRel = ".template/managed/gitignore.txt"

function Get-RepoRoot {
  <#
  .SYNOPSIS
    Determines the target repository root for bootstrap operations.

  .PARAMETER TemplateSourcePath
    Path to the canonical template source directory.

  .PARAMETER TargetRepoPath
    Optional path to the target repository root.

  .EXAMPLE
    Get-RepoRoot -TemplateSourcePath D:\Source\ai\Template -TargetRepoPath D:\Source\Repos\MyNewRepo
  #>
  param(
    [Parameter(Mandatory)][string]$TemplateSourcePath,
    [string]$TargetRepoPath
  )

  if (-not [string]::IsNullOrWhiteSpace($TargetRepoPath)) {
    if (-not (Test-Path -LiteralPath $TargetRepoPath)) {
      throw "Target repo path not found: ${TargetRepoPath}"
    }
    $resolvedTarget = (Resolve-Path -LiteralPath $TargetRepoPath).Path
    $item = Get-Item -LiteralPath $resolvedTarget
    if (-not $item.PSIsContainer) {
      throw "Target repo path is not a directory: ${TargetRepoPath}"
    }
    return $resolvedTarget
  }

  $oneUp = Split-Path -Parent $PSScriptRoot
  $twoUp = Split-Path -Parent $oneUp
  $scriptRepoRoot = (Resolve-Path -LiteralPath $twoUp).Path

  $templateRoot = $null
  try {
    $sourceRoot = $TemplateSourcePath
    if (-not [System.IO.Path]::IsPathRooted($sourceRoot)) {
      $sourceRoot = Join-Path $scriptRepoRoot $sourceRoot
    }
    if (Test-Path -LiteralPath $sourceRoot) {
      $templateRoot = (Resolve-Path -LiteralPath $sourceRoot).Path
    }
  } catch {
    $templateRoot = $null
  }

  if ($null -ne $templateRoot -and -not $scriptRepoRoot.Equals($templateRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    return $scriptRepoRoot
  }

  $cwd = (Get-Location).Path
  if ($null -ne $templateRoot -and $cwd.StartsWith($templateRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Target repo root not determined (current directory is inside the template source). Use -TargetRepoPath."
  }

  return $cwd
}
# End: Get-RepoRoot

function Ensure-Directory {
  param([Parameter(Mandatory)][string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}
# End: Ensure-Directory

function Get-RelativePath {
  param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$FullPath
  )

  $rootResolved = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\','/')
  $pathResolved = (Resolve-Path -LiteralPath $FullPath).Path
  if (-not $pathResolved.StartsWith($rootResolved, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Path is outside root: $FullPath"
  }
  $rel = $pathResolved.Substring($rootResolved.Length).TrimStart('\','/')
  return ($rel -replace '\\', '/')
}
# End: Get-RelativePath

function Get-FileHashHex {
  param([Parameter(Mandatory)][string]$Path)
  $bytes = [System.IO.File]::ReadAllBytes($Path)
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    return ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
  } finally {
    $sha.Dispose()
  }
}

function Test-TemplatePlaceholder {
  param(
    [Parameter(Mandatory)][string]$TargetPath,
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$RelPath
  )

  if (-not (Test-Path -LiteralPath $TargetPath)) { return $false }

  try {
    $raw = Get-Content -LiteralPath $TargetPath -Raw
  } catch {
    return $false
  }

  # Generic placeholder marker used by template-shipped stub files.
  if ($raw -match 'Placeholder file shipped with the template\.') { return $true }

  # README placeholder is marker-based and does not include the generic placeholder sentence.
  if ($RelPath -ieq "README.md") {
    if ($raw -match '<!--\s*TEMPLATE:PROJECT_NAME\s*-->Project Name<!--\s*/TEMPLATE:PROJECT_NAME\s*-->' -and
        $raw -match '<!--\s*TEMPLATE:SHORT_DESCRIPTION\s*-->Short description of the project, its goal, and its main stakeholders\.' ) {
      return $true
    }
  }

  # If the file is identical to the template source, it is effectively a placeholder copy.
  try {
    if ((Get-FileHashHex -Path $SourcePath) -eq (Get-FileHashHex -Path $TargetPath)) { return $true }
  } catch { }

  return $false
}

# End: Get-FileHashHex

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
# End: Write-AtomicUtf8NoBom

function ConvertTo-JsonStable {
  param([Parameter(Mandatory)]$Data)
  $json = $Data | ConvertTo-Json -Depth 8
  return ($json -replace '\n', "`r`n")
}
# End: ConvertTo-JsonStable

function Get-PreferredNewLine {
  param([Parameter(Mandatory)][string]$Path)
  if (Test-Path -LiteralPath $Path) {
    $raw = Get-Content -LiteralPath $Path -Raw
    if ($raw -match '\r\n') { return "`r`n" }
    return "`n"
  }
  return "`r`n"
}
# End: Get-PreferredNewLine

function Get-ManagedScriptPaths {
  <#
  .SYNOPSIS
    Returns manifest-managed script paths under scripts/.

  .PARAMETER ManifestPath
    Path to the template manifest JSON.

  .EXAMPLE
    Get-ManagedScriptPaths -ManifestPath $manifestPath
  #>
  param([Parameter(Mandatory)][string]$ManifestPath)

  if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Template manifest not found: $ManifestPath"
  }

  $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
  $rawEntries = @()
  if ($null -ne $manifest.managedFiles) {
    $rawEntries = @($manifest.managedFiles)
  } elseif ($null -ne $manifest.managedPaths) {
    $rawEntries = @($manifest.managedPaths)
  } else {
    throw "Template manifest has no managed files list."
  }

  $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
  $paths = New-Object System.Collections.Generic.List[string]
  foreach ($entry in $rawEntries) {
    $path = $null
    if ($entry -is [string]) {
      $path = $entry
    } else {
      $path = $entry.path
    }
    if ([string]::IsNullOrWhiteSpace($path)) { continue }
    if ($path -like "scripts/*" -or $path -like "scripts\\*") {
      if ($set.Add($path)) { $paths.Add($path) | Out-Null }
    }
  }

  return ($paths | Sort-Object)
}
# End: Get-ManagedScriptPaths

function Get-ScriptSynopsis {
  <#
  .SYNOPSIS
    Extracts the .SYNOPSIS from a script's comment-based help header.

  .PARAMETER ScriptPath
    Full path to the script.

  .EXAMPLE
    Get-ScriptSynopsis -ScriptPath $scriptPath
  #>
  param([Parameter(Mandatory)][string]$ScriptPath)

  if (-not (Test-Path -LiteralPath $ScriptPath)) { return $null }
  $lines = Get-Content -LiteralPath $ScriptPath
  $inHelp = $false
  $collect = $false

  foreach ($line in $lines) {
    if (-not $inHelp) {
      if ($line -match '^\s*<#') { $inHelp = $true }
      continue
    }

    if ($line -match '^\s*#>') { break }

    if ($collect) {
      if ($line -match '^\s*\.') { break }
      if (-not [string]::IsNullOrWhiteSpace($line)) { return $line.Trim() }
      continue
    }

    $m = [regex]::Match($line, '^\s*\.SYNOPSIS\s*(.*)$')
    if ($m.Success) {
      $val = $m.Groups[1].Value.Trim()
      if (-not [string]::IsNullOrWhiteSpace($val)) { return $val }
      $collect = $true
    }
  }

  return $null
}
# End: Get-ScriptSynopsis

function Get-ReadmeToolingBlock {
  <#
  .SYNOPSIS
    Builds the README tooling list from manifest-managed scripts.

  .PARAMETER RepoRoot
    Path to the repository root.

  .PARAMETER ManifestPath
    Path to the template manifest JSON.

  .PARAMETER NewLine
    Newline sequence to use for output.

  .EXAMPLE
    Get-ReadmeToolingBlock -RepoRoot $repoRoot -ManifestPath $manifestPath -NewLine "`r`n"
  #>
  param(
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$NewLine
  )

  $scriptPaths = Get-ManagedScriptPaths -ManifestPath $ManifestPath
  $lines = New-Object System.Collections.Generic.List[string]

  foreach ($relPath in $scriptPaths) {
    $rel = $relPath -replace '\\', '/'
    $full = Join-Path $RepoRoot ($rel -replace '/', '\')
    $synopsis = Get-ScriptSynopsis -ScriptPath $full

    if ([string]::IsNullOrWhiteSpace($synopsis)) {
      $name = [System.IO.Path]::GetFileNameWithoutExtension($rel)
      $clean = $name -replace '[_-]+', ' '
      $clean = $clean.Trim()
      if (-not [string]::IsNullOrWhiteSpace($clean)) {
        $lower = $clean.ToLowerInvariant()
        $generic = @("script","tool","util","utility","helper")
        if (-not ($generic -contains $lower)) {
          $synopsis = [System.Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($lower)
        }
      }
    }

    while ([string]::IsNullOrWhiteSpace($synopsis)) {
      $synopsis = Read-Host ("Enter a one-line purpose for {0}" -f $rel)
      $synopsis = $synopsis.Trim()
    }

    $lines.Add(("- {0}: [{1}]({1})" -f $synopsis, $rel)) | Out-Null
  }

  return ($lines -join $NewLine)
}
# End: Get-ReadmeToolingBlock

function Get-ReadmeProjectName {
  <#
  .SYNOPSIS
    Derives a safe README project name from the repo folder or prompts.

  .PARAMETER RepoRoot
    Path to the repository root.

  .EXAMPLE
    Get-ReadmeProjectName -RepoRoot $repoRoot
  #>
  param([Parameter(Mandatory)][string]$RepoRoot)

  $isUnsafe = {
    param([string]$Name)

    if ([string]::IsNullOrWhiteSpace($Name)) { return $true }
    $trimmed = $Name.Trim()
    if ($trimmed -match '[<>:"/\\|?*]') { return $true }

    $base = $trimmed.TrimEnd('.')
    if ([string]::IsNullOrWhiteSpace($base)) { return $true }

    $reserved = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $reserved.Add("CON") | Out-Null
    $reserved.Add("PRN") | Out-Null
    $reserved.Add("AUX") | Out-Null
    $reserved.Add("NUL") | Out-Null
    for ($i = 1; $i -le 9; $i++) {
      $reserved.Add(("COM{0}" -f $i)) | Out-Null
      $reserved.Add(("LPT{0}" -f $i)) | Out-Null
    }
    if ($reserved.Contains($base)) { return $true }

    $placeholders = @(
      "project",
      "project name",
      "repo",
      "repository",
      "template",
      "new repo",
      "my repo",
      "user_repo"
    )
    if ($placeholders -contains $base.ToLowerInvariant()) { return $true }
    if ($base -match '(?i)\btemplate\b') { return $true }
    if ($base -match '(?i)\bproject\b') { return $true }

    return $false
  }

  $candidate = (Split-Path -Leaf $RepoRoot)
  while (& $isUnsafe $candidate) {
    $candidate = Read-Host "Enter a project name for README"
    $candidate = $candidate.Trim()
  }

  return $candidate
}
# End: Get-ReadmeProjectName

function Get-ReadmeShortDescription {
  <#
  .SYNOPSIS
    Prompts for a short README description.

  .PARAMETER Prompt
    Prompt text to display.

  .EXAMPLE
    Get-ReadmeShortDescription -Prompt "Enter a short description"
  #>
  param([Parameter(Mandatory)][string]$Prompt)

  $desc = ""
  while ([string]::IsNullOrWhiteSpace($desc)) {
    $desc = Read-Host $Prompt
    $desc = $desc.Trim()
  }

  return $desc
}
# End: Get-ReadmeShortDescription

function Update-ReadmeMarkers {
  <#
  .SYNOPSIS
    Updates README marker blocks for project name, description, and tooling.

  .PARAMETER ReadmePath
    Path to README.md.

  .PARAMETER ProjectName
    Project name to place inside the project name marker.

  .PARAMETER ShortDescription
    Short description to place inside the short description marker.

  .PARAMETER ToolingBlock
    Tooling list content to place inside the tooling marker.

  .PARAMETER ReportOnly
    If set, report intended changes without writing.

  .EXAMPLE
    Update-ReadmeMarkers -ReadmePath $readme -ProjectName $name -ShortDescription $desc -ToolingBlock $tooling -ReportOnly:$false
  #>
  param(
    [Parameter(Mandatory)][string]$ReadmePath,
    [Parameter(Mandatory)][string]$ProjectName,
    [Parameter(Mandatory)][string]$ShortDescription,
    [Parameter(Mandatory)][string]$ToolingBlock,
    [switch]$ReportOnly
  )

  if (-not (Test-Path -LiteralPath $ReadmePath)) {
    return [ordered]@{ changed = $false; reason = "README missing" }
  }

  $nl = Get-PreferredNewLine -Path $ReadmePath
  $text = Get-Content -LiteralPath $ReadmePath -Raw
  $original = $text

  $projectMarkerPattern = '(?s)<!-- TEMPLATE:PROJECT_NAME -->(.*?)<!-- /TEMPLATE:PROJECT_NAME -->'
  $shortMarkerPattern = '(?s)<!-- TEMPLATE:SHORT_DESCRIPTION -->(.*?)<!-- /TEMPLATE:SHORT_DESCRIPTION -->'
  $toolingMarkerPattern = '(?s)<!-- TEMPLATE:TOOLING -->(.*?)<!-- /TEMPLATE:TOOLING -->'

  $hasProjectMarker = [regex]::IsMatch($text, $projectMarkerPattern)
  $hasShortMarker = [regex]::IsMatch($text, $shortMarkerPattern)
  $hasToolingMarker = [regex]::IsMatch($text, $toolingMarkerPattern)
  $introduced = $false

  if (-not $hasProjectMarker) {
    $titlePlaceholderPattern = '(?m)^#\s+Project Name\s*$'
    if ([regex]::IsMatch($text, $titlePlaceholderPattern)) {
      $text = [regex]::Replace($text, $titlePlaceholderPattern, '# <!-- TEMPLATE:PROJECT_NAME -->Project Name<!-- /TEMPLATE:PROJECT_NAME -->')
      $hasProjectMarker = $true
      $introduced = $true
    }
  }

  if (-not $hasShortMarker) {
    $shortPlaceholderPattern = '(?m)^\s*Short description of the project, its goal, and its main stakeholders\.\s*$'
    if ([regex]::IsMatch($text, $shortPlaceholderPattern)) {
      $text = [regex]::Replace($text, $shortPlaceholderPattern, '<!-- TEMPLATE:SHORT_DESCRIPTION -->Short description of the project, its goal, and its main stakeholders.<!-- /TEMPLATE:SHORT_DESCRIPTION -->')
      $hasShortMarker = $true
      $introduced = $true
    }
  }

  if (-not $hasToolingMarker) {
    $lines = $text -split '\r?\n'
    $toolingIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
      if ($lines[$i].Trim() -eq "## Tooling") { $toolingIndex = $i; break }
    }

    if ($toolingIndex -ge 0) {
      $listStart = -1
      $hasUnexpectedText = $false
      for ($i = $toolingIndex + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*##\s+') { break }
        if ($lines[$i] -match '^\s*[-*]\s+') { $listStart = $i; break }
        if (-not [string]::IsNullOrWhiteSpace($lines[$i])) { $hasUnexpectedText = $true; break }
      }

      if ($listStart -ge 0 -and -not $hasUnexpectedText) {
        $listEnd = $listStart
        for ($i = $listStart; $i -lt $lines.Count; $i++) {
          if ($lines[$i] -match '^\s*##\s+') { break }
          if ($lines[$i] -match '^\s*[-*]\s+') { $listEnd = $i; continue }
        }

        $newLines = New-Object System.Collections.Generic.List[string]
        if ($toolingIndex -gt 0) { $newLines.AddRange([string[]]$lines[0..$toolingIndex]) }
        else { $newLines.Add($lines[$toolingIndex]) }

        $newLines.Add("<!-- TEMPLATE:TOOLING -->")
        $newLines.AddRange([string[]]$lines[$listStart..$listEnd])
        $newLines.Add("<!-- /TEMPLATE:TOOLING -->")

        if ($listEnd -lt ($lines.Count - 1)) {
          $newLines.AddRange([string[]]$lines[($listEnd + 1)..($lines.Count - 1)])
        }

        $text = $newLines -join $nl
        $hasToolingMarker = $true
        $introduced = $true
      }
    }
  }

  if (-not ($hasProjectMarker -or $hasShortMarker -or $hasToolingMarker) -and -not $introduced) {
    return [ordered]@{ changed = $false; reason = "no markers or placeholders" }
  }

  if ($hasProjectMarker) {
    $text = [regex]::Replace(
      $text,
      $projectMarkerPattern,
      { param($m) return "<!-- TEMPLATE:PROJECT_NAME -->${ProjectName}<!-- /TEMPLATE:PROJECT_NAME -->" },
      [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
  }

  if ($hasShortMarker) {
    $text = [regex]::Replace(
      $text,
      $shortMarkerPattern,
      { param($m) return "<!-- TEMPLATE:SHORT_DESCRIPTION -->${ShortDescription}<!-- /TEMPLATE:SHORT_DESCRIPTION -->" },
      [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
  }

  if ($hasToolingMarker) {
    $replacement = "<!-- TEMPLATE:TOOLING -->${nl}${ToolingBlock}${nl}<!-- /TEMPLATE:TOOLING -->"
    $text = [regex]::Replace(
      $text,
      $toolingMarkerPattern,
      { param($m) return $replacement },
      [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
  }

  $changed = ($text -ne $original)
  if ($changed -and -not $ReportOnly) {
    Write-AtomicUtf8NoBom -Path $ReadmePath -Content $text
  }

  return [ordered]@{ changed = $changed; reason = if ($changed) { "updated markers" } else { "no changes" } }
}
# End: Update-ReadmeMarkers

function Get-GitIgnoreTemplateLines {
  param([Parameter(Mandatory)][string]$TemplatePath)
  if (-not (Test-Path -LiteralPath $TemplatePath)) {
    throw "Gitignore template entries file not found: $TemplatePath"
  }

  $lines = New-Object System.Collections.Generic.List[string]
  $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)

  foreach ($line in Get-Content -LiteralPath $TemplatePath) {
    $trimmed = $line.TrimEnd()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
    if ($seen.Add($trimmed)) { $lines.Add($trimmed) | Out-Null }
  }

  return $lines
}
# End: Get-GitIgnoreTemplateLines

function Merge-GitIgnore {
  param(
    [Parameter(Mandatory)][string]$TargetPath,
    [Parameter(Mandatory)][string]$TemplateEntriesPath,
    [Parameter(Mandatory)][string]$BlockStart,
    [Parameter(Mandatory)][string]$BlockEnd,
    [switch]$ReportOnly
  )

  $templateLines = Get-GitIgnoreTemplateLines -TemplatePath $TemplateEntriesPath
  $added = New-Object System.Collections.Generic.List[string]
  $action = "merge-additive"

  $lines = @()
  $nl = Get-PreferredNewLine -Path $TargetPath
  if (Test-Path -LiteralPath $TargetPath) {
    $raw = Get-Content -LiteralPath $TargetPath -Raw
    $nl = Get-PreferredNewLine -Path $TargetPath
    $lines = $raw -split '\r?\n'
    if ($lines.Count -gt 0 -and $lines[-1] -eq "") {
      $lines = $lines[0..($lines.Count - 2)]
    }
  }

  $blockStartIndex = -1
  $blockEndIndex = -1
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -eq $BlockStart) { $blockStartIndex = $i; break }
  }
  if ($blockStartIndex -ge 0) {
    for ($i = $blockStartIndex + 1; $i -lt $lines.Count; $i++) {
      if ($lines[$i] -eq $BlockEnd) { $blockEndIndex = $i; break }
    }
  }

  if ($blockStartIndex -lt 0 -or $blockEndIndex -lt 0) {
    $newLines = New-Object System.Collections.Generic.List[string]
    if ($lines.Count -gt 0) { $newLines.AddRange([string[]]$lines) }
    $newLines.Add($BlockStart)
    $newLines.AddRange([string[]]$templateLines)
    $newLines.Add($BlockEnd)
    $added.AddRange([string[]]$templateLines)

    if (-not $ReportOnly) {
      $content = ($newLines -join $nl) + $nl
      Write-AtomicUtf8NoBom -Path $TargetPath -Content $content
    }
  } else {
    $existingBlock = @()
    if ($blockEndIndex -gt ($blockStartIndex + 1)) {
      $existingBlock = $lines[($blockStartIndex + 1)..($blockEndIndex - 1)]
    }

    $existingSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($line in $existingBlock) {
      $existingSet.Add($line.TrimEnd()) | Out-Null
    }

    $blockLines = New-Object System.Collections.Generic.List[string]
    if ($existingBlock.Count -gt 0) { $blockLines.AddRange([string[]]$existingBlock) }

    foreach ($tmpl in $templateLines) {
      if (-not $existingSet.Contains($tmpl)) {
        $blockLines.Add($tmpl)
        $added.Add($tmpl) | Out-Null
      }
    }

    if ($added.Count -gt 0 -and -not $ReportOnly) {
      $newLines = New-Object System.Collections.Generic.List[string]
      if ($blockStartIndex -gt 0) { $newLines.AddRange([string[]]$lines[0..($blockStartIndex - 1)]) }
      $newLines.Add($BlockStart)
      $newLines.AddRange($blockLines)
      $newLines.Add($BlockEnd)
      if ($blockEndIndex -lt ($lines.Count - 1)) {
        $newLines.AddRange([string[]]$lines[($blockEndIndex + 1)..($lines.Count - 1)])
      }

      $content = ($newLines -join $nl) + $nl
      Write-AtomicUtf8NoBom -Path $TargetPath -Content $content
    }
  }

  return [ordered]@{
    action         = $action
    entriesAdded   = $added.Count
    addedEntries   = $added
    templateEntries = $templateLines.Count
  }
}
# End: Merge-GitIgnore

function Get-NonEmptyLineCount {
  param([Parameter(Mandatory)][string]$Path)
  $count = 0
  foreach ($line in Get-Content -LiteralPath $Path) {
    if (-not [string]::IsNullOrWhiteSpace($line)) {
      $count++
    }
  }
  return $count
}
# End: Get-NonEmptyLineCount

function Select-LegacyChangelogSource {
  param(
    [Parameter(Mandatory)][string]$HistoryPath,
    [Parameter(Mandatory)][string]$ChangelogPath
  )

  $result = [ordered]@{
    historyPath    = $HistoryPath
    changelogPath  = $ChangelogPath
    historyLines   = 0
    changelogLines = 0
    selectedPath   = $null
    reason         = $null
  }

  $hasHistory = Test-Path -LiteralPath $HistoryPath
  $hasChangelog = Test-Path -LiteralPath $ChangelogPath

  if ($hasHistory) {
    $result.historyLines = Get-NonEmptyLineCount -Path $HistoryPath
  }
  if ($hasChangelog) {
    $result.changelogLines = Get-NonEmptyLineCount -Path $ChangelogPath
  }

  if ($hasHistory -and -not $hasChangelog) {
    $result.selectedPath = $HistoryPath
    $result.reason = "only history.txt present"
    return $result
  }

  if (-not $hasHistory -and $hasChangelog) {
    $result.selectedPath = $ChangelogPath
    $result.reason = "only changelog.txt present"
    return $result
  }

  if ($hasHistory -and $hasChangelog) {
    if ($result.historyLines -ge ($result.changelogLines * 1.2)) {
      $result.selectedPath = $HistoryPath
      $result.reason = "history.txt has >=20% more non-empty lines"
      return $result
    }
    if ($result.changelogLines -ge ($result.historyLines * 1.2)) {
      $result.selectedPath = $ChangelogPath
      $result.reason = "changelog.txt has >=20% more non-empty lines"
      return $result
    }
    $result.selectedPath = $ChangelogPath
    $result.reason = "changelog.txt selected by default (line counts within 20%)"
  }

  return $result
}
# End: Select-LegacyChangelogSource

function Backup-File {
  param(
    [Parameter(Mandatory)][string]$TargetPath,
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$BackupRoot
  )

  $rel = Get-RelativePath -Root $RepoRoot -FullPath $TargetPath
  $dest = Join-Path $BackupRoot ($rel -replace '/', '\')
  Ensure-Directory -Path (Split-Path -Parent $dest)
  Copy-Item -LiteralPath $TargetPath -Destination $dest -Force
  return $dest
}
# End: Backup-File

function Add-Result {
  param(
    [Parameter(Mandatory)]$List,
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Action,
    [string]$Reason
  )

  $item = [ordered]@{
    path   = $Path
    action = $Action
  }
  if ($Reason) {
    $item.reason = $Reason
  }
  $List.Add($item) | Out-Null
}
# End: Add-Result

$results = New-Object System.Collections.Generic.List[object]
$warnings = New-Object System.Collections.Generic.List[string]
$errors = New-Object System.Collections.Generic.List[string]

$now = Get-Date
$isoTimestamp = $now.ToString("yyyy-MM-dd HH:mm:ss")
$reportStamp = $now.ToString("yyyyMMdd-HHmmss")

$repoRoot = Get-RepoRoot -TemplateSourcePath $TemplateSourcePath -TargetRepoPath $TargetRepoPath
$templateVersionPath = Join-Path $repoRoot ".template\version.txt"
$manifestRel = ".template/manifest.json"
$versionRel = ".template/version.txt"
$changelogRel = "docs/changelog/CHANGELOG.md"
$exitCode = 0

$effectiveMode = $Mode
if ($DryRun -or $Mode -eq "ReportOnly") {
  $effectiveMode = "ReportOnly"
}
$isReportOnly = ($effectiveMode -eq "ReportOnly")

if (-not $ReportDir) {
  $ReportDir = Join-Path $repoRoot ".template\reports"
}

$changelogInfo = [ordered]@{
  changelogPath           = (Join-Path $repoRoot "docs\changelog\CHANGELOG.md")
  existingChangelogMd     = $false
  forceChangelog          = [bool]$ForceChangelog
  legacyHistoryPath       = (Join-Path $repoRoot "history.txt")
  legacyChangelogPath     = (Join-Path $repoRoot "changelog.txt")
  legacyHistoryLines      = 0
  legacyChangelogLines    = 0
  selectedSource          = $null
  selectionReason         = $null
  generated               = $false
  removedLegacyFiles      = $false
}
$gitignoreInfo = [ordered]@{
  action          = "merge-additive"
  entriesAdded    = 0
  addedEntries    = @()
  templateEntries = 0
}

# Detect existing template.
if (Test-Path -LiteralPath $templateVersionPath) {
  $errors.Add("Template detected; use upgrade script.")
  $exitCode = 1
} else {
  try {
    $sourceRoot = $TemplateSourcePath
    if (-not [System.IO.Path]::IsPathRooted($sourceRoot)) {
      $sourceRoot = Join-Path $repoRoot $sourceRoot
    }
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
      throw "Template source path not found: $sourceRoot"
    }
    $sourceRoot = (Resolve-Path -LiteralPath $sourceRoot).Path

    $gitignoreTemplatePath = Join-Path $sourceRoot ($GitIgnoreManagedRel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $gitignoreTemplatePath)) {
      throw "Gitignore template entries file not found: $gitignoreTemplatePath"
    }

    # Build managed path list from the template source.
    $sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force
    $managedPaths = New-Object System.Collections.Generic.List[string]
    foreach ($item in $sourceFiles) {
      $managedPaths.Add((Get-RelativePath -Root $sourceRoot -FullPath $item.FullName))
    }

    if (-not ($managedPaths -contains ".gitignore")) { $managedPaths.Add(".gitignore") }
    if (-not ($managedPaths -contains $manifestRel)) { $managedPaths.Add($manifestRel) }
    if (-not ($managedPaths -contains $versionRel)) { $managedPaths.Add($versionRel) }

    $managedPaths = $managedPaths | Sort-Object

    $managedFiles = New-Object System.Collections.Generic.List[object]
    foreach ($path in $managedPaths) {
      if ($path -eq ".gitignore") {
        $managedFiles.Add([ordered]@{
            path  = $path
            mode  = "merge-additive"
            block = "TEMPLATE-MANAGED"
          }) | Out-Null
      } else {
        $managedFiles.Add([ordered]@{
            path = $path
            mode = "overwrite"
          }) | Out-Null
      }
    }

    # Changelog migration (legacy -> CHANGELOG.md).
    $changelogInfo.existingChangelogMd = (Test-Path -LiteralPath $changelogInfo.changelogPath)
    $skipChangelogCopy = $false
    $legacySelection = Select-LegacyChangelogSource -HistoryPath $changelogInfo.legacyHistoryPath -ChangelogPath $changelogInfo.legacyChangelogPath
    $changelogInfo.legacyHistoryLines = $legacySelection.historyLines
    $changelogInfo.legacyChangelogLines = $legacySelection.changelogLines
    $changelogInfo.selectedSource = $legacySelection.selectedPath
    $changelogInfo.selectionReason = $legacySelection.reason

    $shouldGenerate = $ForceChangelog -or (-not $changelogInfo.existingChangelogMd)
    if ($shouldGenerate -and $legacySelection.selectedPath) {
      $skipChangelogCopy = $true
      if ($isReportOnly) {
        Add-Result -List $results -Path $changelogRel -Action "wouldGenerate" -Reason "legacy source selected"
      } else {
        $generateScript = Join-Path $PSScriptRoot "generate-changelog.ps1"
        Ensure-Directory -Path (Split-Path -Parent $changelogInfo.changelogPath)
        & $generateScript -SourcePath $legacySelection.selectedPath -TargetPath $changelogInfo.changelogPath
        Add-Result -List $results -Path $changelogRel -Action "generated" -Reason "legacy source selected"
        $changelogInfo.generated = $true

        if ($RemoveLegacyChangelogFiles) {
          if (Test-Path -LiteralPath $changelogInfo.legacyHistoryPath) {
            Remove-Item -LiteralPath $changelogInfo.legacyHistoryPath -Force
          }
          if (Test-Path -LiteralPath $changelogInfo.legacyChangelogPath) {
            Remove-Item -LiteralPath $changelogInfo.legacyChangelogPath -Force
          }
          $changelogInfo.removedLegacyFiles = $true
        }
      }
    } elseif ($changelogInfo.existingChangelogMd -and -not $ForceChangelog) {
      $skipChangelogCopy = $true
      Add-Result -List $results -Path $changelogRel -Action "skipped" -Reason "exists (use -ForceChangelog)"
    }

    # Sync template-managed files.
    $backupRoot = Join-Path $repoRoot (Join-Path ".template" (Join-Path "backup" $reportStamp))

    foreach ($entry in $managedFiles) {
      $rel = $entry.path
      if ($rel -eq $manifestRel -or $rel -eq $versionRel) { continue }

      if ($rel -eq ".gitignore") {
        $gitignoreInfo = Merge-GitIgnore -TargetPath (Join-Path $repoRoot ".gitignore") -TemplateEntriesPath $gitignoreTemplatePath -BlockStart $GitIgnoreBlockStart -BlockEnd $GitIgnoreBlockEnd -ReportOnly:$isReportOnly
        $gitignoreReason = if ($isReportOnly) { "entries to add: {0}" -f $gitignoreInfo.entriesAdded } else { "entries added: {0}" -f $gitignoreInfo.entriesAdded }
        Add-Result -List $results -Path $rel -Action "merge-additive" -Reason $gitignoreReason
        continue
      }

      if ($rel -eq $changelogRel -and $skipChangelogCopy) { continue }

      $sourcePath = Join-Path $sourceRoot ($rel -replace '/', '\')
      $targetPath = Join-Path $repoRoot ($rel -replace '/', '\')

      if (-not (Test-Path -LiteralPath $sourcePath)) {
        $errors.Add("Missing source file: $sourcePath")
        Add-Result -List $results -Path $rel -Action "error" -Reason "source missing"
        continue
      }

      if (Test-Path -LiteralPath $targetPath) {
        $same = (Get-FileHashHex -Path $sourcePath) -eq (Get-FileHashHex -Path $targetPath)
        if ($same) {
          Add-Result -List $results -Path $rel -Action "unchanged"
          continue
        }


        # Bootstrap into an existing repo: never overwrite existing non-template files unless they are still template placeholders.
        if ($rel -notlike ".template/*") {
          $isTemplateStub = Test-TemplatePlaceholder -TargetPath $targetPath -SourcePath $sourcePath -RelPath $rel
          if (-not $isTemplateStub) {
            Add-Result -List $results -Path $rel -Action "skipped" -Reason "exists (preserved)"
            continue
          }
        }
        if ($isReportOnly) {
          Add-Result -List $results -Path $rel -Action "wouldOverwrite" -Reason "different content"
          continue
        }

        if ($effectiveMode -eq "Safe") {
          $backupPath = Backup-File -TargetPath $targetPath -RepoRoot $repoRoot -BackupRoot $backupRoot
          $warnings.Add(("Overwriting changed file: {0} (backup: {1})" -f $rel, $backupPath))
        }

        Ensure-Directory -Path (Split-Path -Parent $targetPath)
        Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
        Add-Result -List $results -Path $rel -Action "overwritten"
      } else {
        if ($isReportOnly) {
          Add-Result -List $results -Path $rel -Action "wouldCopy"
        } else {
          Ensure-Directory -Path (Split-Path -Parent $targetPath)
          Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
          Add-Result -List $results -Path $rel -Action "copied"
        }
      }
    }

    if ($ConfigureReadme) {
      $readmePath = Join-Path $repoRoot "README.md"
      $toolingManifestPath = Join-Path $sourceRoot ".template\manifest.json"
      if (-not (Test-Path -LiteralPath $toolingManifestPath)) {
        throw "Template manifest not found: $toolingManifestPath"
      }

      $projectName = Get-ReadmeProjectName -RepoRoot $repoRoot
      $shortDescription = Get-ReadmeShortDescription -Prompt "Enter a short description for README"
      $readmeNl = Get-PreferredNewLine -Path $readmePath
      $toolingBlock = Get-ReadmeToolingBlock -RepoRoot $repoRoot -ManifestPath $toolingManifestPath -NewLine $readmeNl
      $readmeResult = Update-ReadmeMarkers -ReadmePath $readmePath -ProjectName $projectName -ShortDescription $shortDescription -ToolingBlock $toolingBlock -ReportOnly:$isReportOnly

      if ($readmeResult.changed) {
        $action = if ($isReportOnly) { "wouldUpdate" } else { "updated" }
        Add-Result -List $results -Path "README.md" -Action $action -Reason $readmeResult.reason
      } else {
        Add-Result -List $results -Path "README.md" -Action "skipped" -Reason $readmeResult.reason
      }
    }

    # Write version flag and manifest.
    if ($isReportOnly) {
      Add-Result -List $results -Path $versionRel -Action "wouldWrite" -Reason "template version"
      Add-Result -List $results -Path $manifestRel -Action "wouldWrite" -Reason "template manifest"
    } else {
      Ensure-Directory -Path (Split-Path -Parent $templateVersionPath)
      Write-AtomicUtf8NoBom -Path $templateVersionPath -Content ("{0}`r`n" -f $CurrentTemplateVersion)
      Add-Result -List $results -Path $versionRel -Action "created" -Reason "template version"

      $manifest = [ordered]@{
        templateVersion    = $CurrentTemplateVersion
        templateSourcePath = $sourceRoot
        generatedAt        = $isoTimestamp
        managedFiles       = $managedFiles
      }
      $manifestJson = ConvertTo-JsonStable -Data $manifest
      $manifestPath = Join-Path $repoRoot ($manifestRel -replace '/', '\')
      Write-AtomicUtf8NoBom -Path $manifestPath -Content $manifestJson
      Add-Result -List $results -Path $manifestRel -Action "created" -Reason "template manifest"
    }
  } catch {
    $errors.Add($_.Exception.Message)
    if ($exitCode -eq 0) { $exitCode = 3 }
  }
}

$summary = [ordered]@{
  total          = $results.Count
  copied         = ($results | Where-Object { $_.action -eq "copied" }).Count
  overwritten    = ($results | Where-Object { $_.action -eq "overwritten" }).Count
  unchanged      = ($results | Where-Object { $_.action -eq "unchanged" }).Count
  skipped        = ($results | Where-Object { $_.action -eq "skipped" }).Count
  generated      = ($results | Where-Object { $_.action -eq "generated" }).Count
  wouldCopy      = ($results | Where-Object { $_.action -eq "wouldCopy" }).Count
  wouldOverwrite = ($results | Where-Object { $_.action -eq "wouldOverwrite" }).Count
  wouldGenerate  = ($results | Where-Object { $_.action -eq "wouldGenerate" }).Count
  errors         = $errors.Count
  warnings       = $warnings.Count
}

$report = [ordered]@{
  script              = "bootstrap-template.ps1"
  timestamp           = $isoTimestamp
  mode                = $Mode
  effectiveMode       = $effectiveMode
  dryRun              = [bool]$DryRun
  repoRoot            = $repoRoot
  templateSourcePath  = $TemplateSourcePath
  templateVersion     = $CurrentTemplateVersion
  exitCode            = $exitCode
  summary             = $summary
  changelog           = $changelogInfo
  gitignore           = $gitignoreInfo
  warnings            = $warnings
  errors              = $errors
  items               = $results
}

Ensure-Directory -Path $ReportDir
$reportBase = "bootstrap-report_${reportStamp}"
$mdPath = Join-Path $ReportDir "${reportBase}.md"
$jsonPath = Join-Path $ReportDir "${reportBase}.json"

$md = New-Object System.Collections.Generic.List[string]
$md.Add("# Template Bootstrap Report")
$md.Add("")
$md.Add(("- Timestamp: {0}" -f $isoTimestamp))
$md.Add(("- Mode: {0} (Effective: {1}, DryRun: {2})" -f $Mode, $effectiveMode, [bool]$DryRun))
$md.Add(("- Repo root: {0}" -f $repoRoot))
$md.Add(("- Template source: {0}" -f $TemplateSourcePath))
$md.Add(("- Template version: {0}" -f $CurrentTemplateVersion))
$md.Add(("- Exit code: {0}" -f $exitCode))
$md.Add("")
$md.Add("## Summary")
$md.Add(("- Total items: {0}" -f $summary.total))
$md.Add(("- Copied: {0}" -f $summary.copied))
$md.Add(("- Overwritten: {0}" -f $summary.overwritten))
$md.Add(("- Unchanged: {0}" -f $summary.unchanged))
$md.Add(("- Skipped: {0}" -f $summary.skipped))
$md.Add(("- Generated: {0}" -f $summary.generated))
$md.Add(("- Would copy: {0}" -f $summary.wouldCopy))
$md.Add(("- Would overwrite: {0}" -f $summary.wouldOverwrite))
$md.Add(("- Would generate: {0}" -f $summary.wouldGenerate))
$md.Add(("- Warnings: {0}" -f $summary.warnings))
$md.Add(("- Errors: {0}" -f $summary.errors))
$md.Add("")
$md.Add("## Actions")
foreach ($item in $results) {
  $line = "- [{0}] {1}" -f $item.action, $item.path
  if ($item.reason) { $line = "{0} - {1}" -f $line, $item.reason }
  $md.Add($line)
}

$md.Add("")
$md.Add("## Changelog Migration")
$md.Add(("- Existing CHANGELOG.md: {0}" -f $changelogInfo.existingChangelogMd))
$md.Add(("- ForceChangelog: {0}" -f $changelogInfo.forceChangelog))
$md.Add(("- history.txt lines: {0}" -f $changelogInfo.legacyHistoryLines))
$md.Add(("- changelog.txt lines: {0}" -f $changelogInfo.legacyChangelogLines))
$md.Add(("- Selected source: {0}" -f $changelogInfo.selectedSource))
$md.Add(("- Selection reason: {0}" -f $changelogInfo.selectionReason))
$md.Add(("- Generated: {0}" -f $changelogInfo.generated))
$md.Add(("- Removed legacy files: {0}" -f $changelogInfo.removedLegacyFiles))

$md.Add("")
$md.Add("## .gitignore")
$md.Add(("- Action: {0}" -f $gitignoreInfo.action))
$md.Add(("- Entries added: {0}" -f $gitignoreInfo.entriesAdded))
if ($gitignoreInfo.entriesAdded -gt 0) {
  $md.Add("- Added entries:")
  $limit = [Math]::Min(10, $gitignoreInfo.addedEntries.Count)
  for ($i = 0; $i -lt $limit; $i++) {
    $md.Add("  - " + $gitignoreInfo.addedEntries[$i])
  }
  if ($gitignoreInfo.addedEntries.Count -gt 10) {
    $md.Add("  - +" + ($gitignoreInfo.addedEntries.Count - 10) + " more")
  }
}

if ($warnings.Count -gt 0) {
  $md.Add("")
  $md.Add("## Warnings")
  foreach ($w in $warnings) { $md.Add("- " + $w) }
}

if ($errors.Count -gt 0) {
  $md.Add("")
  $md.Add("## Errors")
  foreach ($e in $errors) { $md.Add("- " + $e) }
}

$mdOut = ($md -join "`r`n") + "`r`n"
Write-AtomicUtf8NoBom -Path $mdPath -Content $mdOut
Write-AtomicUtf8NoBom -Path $jsonPath -Content (ConvertTo-JsonStable -Data $report)

Write-Host $mdOut

exit $exitCode
