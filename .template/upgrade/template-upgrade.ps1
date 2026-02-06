#requires -Version 7.0
# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.8.3
<#
.SYNOPSIS
  Upgrades template-managed files based on the template manifest.

.PARAMETER Mode
  Safe (default), Strict, or ReportOnly.

.PARAMETER DryRun
  Report-only behavior without writing template-managed files.

.PARAMETER ReportDir
  Directory to write the markdown and JSON reports.

.PARAMETER FromVersion
  Optional override for the detected template version.

.PARAMETER ToVersion
  Optional override for the target template version.

.PARAMETER AdoptFromVersion
  Adopt legacy repos without .template/version.txt by writing this version and continuing.

.PARAMETER ConfigureReadme
  Update README marker blocks for adopt operations (default: $true).

.PARAMETER MigrateReadmeToV1_8_2
  Opt-in migration to add README markers and configuration for v1.8.2.

.EXAMPLE
  pwsh -File .\.template\upgrade\template-upgrade.ps1 -Mode Safe
#>

[CmdletBinding(DefaultParameterSetName = "Default")]
param(
  [ValidateSet("Safe","Strict","ReportOnly")]
  [string]$Mode = "Safe",

  [switch]$DryRun,

  [string]$ReportDir,

  [Parameter(ParameterSetName = "FromVersion", Mandatory = $true)]
  [Parameter(ParameterSetName = "InvalidCombo", Mandatory = $true)]
  [string]$FromVersion,

  [string]$ToVersion,

  [Parameter(ParameterSetName = "AdoptFromVersion", Mandatory = $true)]
  [Parameter(ParameterSetName = "InvalidCombo", Mandatory = $true)]
  [string]$AdoptFromVersion,

  [bool]$ConfigureReadme = $true,

  [switch]$MigrateReadmeToV1_8_2
)

$ErrorActionPreference = "Stop"

$CurrentTemplateVersion = "1.8.3"
$GitIgnoreBlockStart = "### BEGIN TEMPLATE-MANAGED (DO NOT EDIT)"
$GitIgnoreBlockEnd = "### END TEMPLATE-MANAGED"
$GitIgnoreManagedRel = ".template/managed/gitignore.txt"

function Get-RepoRoot {
  $oneUp = Split-Path -Parent $PSScriptRoot
  $twoUp = Split-Path -Parent $oneUp
  return (Resolve-Path -LiteralPath $twoUp).Path
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
    [Parameter(Mandatory)][string]$NewLine,
    [switch]$NonInteractive
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

    if ([string]::IsNullOrWhiteSpace($synopsis) -and $NonInteractive) {
      $synopsis = $rel
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

  .PARAMETER InsertMigrationMarker
    Insert the README migration marker for v1.8.2.

  .PARAMETER ReportOnly
    If set, report intended changes without writing.

  .EXAMPLE
    Update-ReadmeMarkers -ReadmePath $readme -ProjectName $name -ShortDescription $desc -ToolingBlock $tooling -InsertMigrationMarker -ReportOnly:$false
  #>
  param(
    [Parameter(Mandatory)][string]$ReadmePath,
    [Parameter(Mandatory)][string]$ProjectName,
    [Parameter(Mandatory)][string]$ShortDescription,
    [Parameter(Mandatory)][string]$ToolingBlock,
    [switch]$InsertMigrationMarker,
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

  if ($InsertMigrationMarker) {
    $markerPattern = '<!-- TEMPLATE:README_MIGRATED v1\.8\.2 -->'
    if (-not ([regex]::IsMatch($text, $markerPattern))) {
      $lines = $text -split '\r?\n'
      $inserted = $false
      for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*#\s+') {
          $newLines = New-Object System.Collections.Generic.List[string]
          if ($i -gt 0) { $newLines.AddRange([string[]]$lines[0..$i]) }
          else { $newLines.Add($lines[$i]) }
          $newLines.Add('<!-- TEMPLATE:README_MIGRATED v1.8.2 -->')
          if ($i -lt ($lines.Count - 1)) {
            $newLines.AddRange([string[]]$lines[($i + 1)..($lines.Count - 1)])
          }
          $text = $newLines -join $nl
          $inserted = $true
          break
        }
      }

      if (-not $inserted) {
        $text = "<!-- TEMPLATE:README_MIGRATED v1.8.2 -->${nl}${text}"
      }
    }
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
    action          = $action
    entriesAdded    = $added.Count
    addedEntries    = $added
    templateEntries = $templateLines.Count
  }
}
# End: Merge-GitIgnore

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

function Try-ParseSemVer {
  param([Parameter(Mandatory)][string]$Version)
  $m = [regex]::Match($Version, '^\s*(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?\s*$')
  if (-not $m.Success) { return $null }
  $p4 = 0
  if ($m.Groups[4].Success) { $p4 = [int]$m.Groups[4].Value }
  return @([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value, $p4)
}
# End: Try-ParseSemVer

function Compare-SemVer {
  param(
    [Parameter(Mandatory)][int[]]$Left,
    [Parameter(Mandatory)][int[]]$Right
  )
  $len = [Math]::Max($Left.Count, $Right.Count)
  for ($i = 0; $i -lt $len; $i++) {
    $l = if ($i -lt $Left.Count) { $Left[$i] } else { 0 }
    $r = if ($i -lt $Right.Count) { $Right[$i] } else { 0 }
    if ($l -gt $r) { return 1 }
    if ($l -lt $r) { return -1 }
  }
  return 0
}
# End: Compare-SemVer

function Normalize-ManagedEntries {
  param([Parameter(Mandatory)]$Manifest)
  $rawEntries = @()

  if ($null -ne $Manifest.managedFiles) {
    $rawEntries = @($Manifest.managedFiles)
  } elseif ($null -ne $Manifest.managedPaths) {
    $rawEntries = @($Manifest.managedPaths)
  } else {
    throw "Template manifest has no managed files list."
  }

  $map = @{}
  foreach ($entry in $rawEntries) {
    $path = $null
    $mode = $null
    $block = $null

    if ($entry -is [string]) {
      $path = $entry
      $mode = "overwrite"
    } else {
      $path = $entry.path
      $mode = $entry.mode
      $block = $entry.block
      if (-not $mode) { $mode = "overwrite" }
    }

    if ([string]::IsNullOrWhiteSpace($path)) { continue }

    $key = $path.ToLowerInvariant()
    if (-not $map.ContainsKey($key)) {
      $item = [ordered]@{
        path = $path
        mode = $mode
      }
      if ($block) { $item.block = $block }
      $map[$key] = $item
    }
  }

  return @($map.Values)
}
# End: Normalize-ManagedEntries

$results = New-Object System.Collections.Generic.List[object]
$warnings = New-Object System.Collections.Generic.List[string]
$errors = New-Object System.Collections.Generic.List[string]

$now = Get-Date
$isoTimestamp = $now.ToString("yyyy-MM-dd HH:mm:ss")
$reportStamp = $now.ToString("yyyyMMdd-HHmmss")

$repoRoot = Get-RepoRoot
$templateVersionPath = Join-Path $repoRoot ".template\version.txt"
$manifestPath = Join-Path $repoRoot ".template\manifest.json"
$manifestRel = ".template/manifest.json"
$versionRel = ".template/version.txt"
$exitCode = 0
$fromVersionUsed = $null
$toVersionUsed = $null
$templateSourceUsed = $null
$adoptedLegacyRepo = $false
$adoptFromVersionUsed = $null
$gitignoreInfo = [ordered]@{
  action          = "merge-additive"
  entriesAdded    = 0
  addedEntries    = @()
  templateEntries = 0
}
$skipReadme = $false

$effectiveMode = $Mode
if ($DryRun -or $Mode -eq "ReportOnly") {
  $effectiveMode = "ReportOnly"
}
$isReportOnly = ($effectiveMode -eq "ReportOnly")

if (-not $ReportDir) {
  $ReportDir = Join-Path $repoRoot ".template\reports"
}

if ($FromVersion -and $AdoptFromVersion) {
  $errors.Add("Parameters -FromVersion and -AdoptFromVersion are mutually exclusive.")
  $exitCode = 3
} elseif ($FromVersion -and -not (Test-Path -LiteralPath $templateVersionPath)) {
  $errors.Add("Parameter -FromVersion requires an existing .template/version.txt.")
  $exitCode = 3
}

if ($exitCode -eq 0) {
  if (-not (Test-Path -LiteralPath $templateVersionPath)) {
    if (-not $AdoptFromVersion) {
      $errors.Add("No template detected. Use bootstrap script.")
      $exitCode = 1
    } else {
      $adoptParsed = Try-ParseSemVer -Version $AdoptFromVersion
      if ($null -eq $adoptParsed) {
        $errors.Add("Unsupported version format. AdoptFromVersion: $AdoptFromVersion")
        $exitCode = 2
      } else {
        $adoptedLegacyRepo = $true
        $adoptFromVersionUsed = $AdoptFromVersion
        $warnings.Add(("Adopted legacy repo: created version.txt = {0}" -f $AdoptFromVersion))
        if (-not $isReportOnly) {
          Ensure-Directory -Path (Split-Path -Parent $templateVersionPath)
          Write-AtomicUtf8NoBom -Path $templateVersionPath -Content ("{0}`r`n" -f $AdoptFromVersion)
        }
      }
    }
  } elseif ($AdoptFromVersion) {
    $warnings.Add("AdoptFromVersion ignored because version.txt already exists.")
  }
}

if ($exitCode -eq 0) {
  try {
    # Resolve versions.
    $fromVersion = $FromVersion
    if (-not $fromVersion) {
      if ($adoptedLegacyRepo -and $adoptFromVersionUsed) {
        $fromVersion = $adoptFromVersionUsed
      } else {
        $fromVersion = (Get-Content -LiteralPath $templateVersionPath -Raw).Trim()
      }
    }
    $fromVersionUsed = $fromVersion

    $toVersion = $ToVersion
    if (-not $toVersion) { $toVersion = $CurrentTemplateVersion }
    $toVersionUsed = $toVersion

    $fromParsed = Try-ParseSemVer -Version $fromVersion
    $toParsed = Try-ParseSemVer -Version $toVersion
    if ($null -eq $fromParsed -or $null -eq $toParsed) {
      $errors.Add("Unsupported version format. FromVersion: $fromVersion ToVersion: $toVersion")
      $exitCode = 2
      throw "Unsupported version"
    }

    if ((Compare-SemVer -Left $fromParsed -Right $toParsed) -gt 0) {
      $errors.Add("Unsupported version: from_version is newer than to_version.")
      $exitCode = 2
      throw "Unsupported version"
    }

    # Load template manifest.
    if (-not (Test-Path -LiteralPath $manifestPath)) {
      $errors.Add("Template manifest missing: $manifestPath")
      $exitCode = 2
      throw "Unsupported version"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    try {
      $managedFiles = Normalize-ManagedEntries -Manifest $manifest
    } catch {
      $errors.Add("Template manifest has no managed files.")
      $exitCode = 2
      throw "Unsupported version"
    }
    if ($managedFiles.Count -eq 0) {
      $errors.Add("Template manifest has no managed files.")
      $exitCode = 2
      throw "Unsupported version"
    }

    $hasGitIgnore = $false
    foreach ($item in $managedFiles) {
      if ($item.path -ieq ".gitignore") {
        $hasGitIgnore = $true
        $item.mode = "merge-additive"
        $item.block = "TEMPLATE-MANAGED"
      }
    }
    if (-not $hasGitIgnore) {
      $managedFiles += [ordered]@{
        path  = ".gitignore"
        mode  = "merge-additive"
        block = "TEMPLATE-MANAGED"
      }
    }

    if (-not ($managedFiles | Where-Object { $_.path -ieq $manifestRel })) {
      $managedFiles += [ordered]@{ path = $manifestRel; mode = "overwrite" }
    }
    if (-not ($managedFiles | Where-Object { $_.path -ieq $versionRel })) {
      $managedFiles += [ordered]@{ path = $versionRel; mode = "overwrite" }
    }
    $managedFiles = $managedFiles | Sort-Object { $_.path }

    # Resolve template source path.
    $sourceRoot = $manifest.templateSourcePath
    if (-not $sourceRoot) { $sourceRoot = "D:\Source\ai\template" }
    if (-not [System.IO.Path]::IsPathRooted($sourceRoot)) {
      $sourceRoot = Join-Path $repoRoot $sourceRoot
    }
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
      throw "Template source path not found: $sourceRoot"
    }
    $sourceRoot = (Resolve-Path -LiteralPath $sourceRoot).Path
    $templateSourceUsed = $sourceRoot

    $gitignoreTemplatePath = Join-Path $sourceRoot ($GitIgnoreManagedRel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $gitignoreTemplatePath)) {
      throw "Gitignore template entries file not found: $gitignoreTemplatePath"
    }

    $backupRoot = Join-Path $repoRoot (Join-Path ".template" (Join-Path "backup" $reportStamp))

    # Sync template-managed files.
    foreach ($entry in $managedFiles) {
      $rel = $entry.path
      if ($rel -eq $manifestRel -or $rel -eq $versionRel) { continue }

      # Explicitly skip template-managed entries marked as "skip".
      if ($entry.mode -eq "skip") {
        if ($rel -ieq "README.md") { $skipReadme = $true }
        Add-Result -List $results -Path $rel -Action "skipped" -Reason "mode=skip"
        continue
      }

      if ($rel -eq ".gitignore") {
        $gitignoreInfo = Merge-GitIgnore -TargetPath (Join-Path $repoRoot ".gitignore") -TemplateEntriesPath $gitignoreTemplatePath -BlockStart $GitIgnoreBlockStart -BlockEnd $GitIgnoreBlockEnd -ReportOnly:$isReportOnly
        $gitignoreReason = if ($isReportOnly) { "entries to add: {0}" -f $gitignoreInfo.entriesAdded } else { "entries added: {0}" -f $gitignoreInfo.entriesAdded }
        Add-Result -List $results -Path $rel -Action "merge-additive" -Reason $gitignoreReason
        continue
      }

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


        # Adopt legacy repos: never overwrite existing user files unless they are still template placeholders.
        if ($adoptedLegacyRepo) {
          $isTemplateStub = Test-TemplatePlaceholder -TargetPath $targetPath -SourcePath $sourcePath -RelPath $rel
          if (-not $isTemplateStub) {
            Add-Result -List $results -Path $rel -Action "skipped" -Reason "adopt preserve existing"
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

    $shouldConfigureReadme = $false
    $insertMigrationMarker = $false
    $readmePath = Join-Path $repoRoot "README.md"

    if ($adoptedLegacyRepo) {
      if ($ConfigureReadme) {
        $shouldConfigureReadme = $true
      } else {
        Add-Result -List $results -Path "README.md" -Action "skipped" -Reason "ConfigureReadme disabled"
      }
    } elseif ($MigrateReadmeToV1_8_2) {
      if (-not (Test-Path -LiteralPath $readmePath)) {
        Add-Result -List $results -Path "README.md" -Action "skipped" -Reason "README missing"
      } else {
        $readmeRaw = Get-Content -LiteralPath $readmePath -Raw
        if ($readmeRaw -match '<!-- TEMPLATE:README_MIGRATED v1\.8\.2 -->') {
          Add-Result -List $results -Path "README.md" -Action "skipped" -Reason "migration marker present"
        } else {
          $shouldConfigureReadme = $true
          $insertMigrationMarker = $true
        }
      }
    }

    if ($shouldConfigureReadme -and -not $skipReadme) {
      $projectName = Get-ReadmeProjectName -RepoRoot $repoRoot
      # Avoid interactive prompts during report-only runs.
      if ($isReportOnly) {
        $shortDescription = "Short description of the project, its goal, and its main stakeholders."
      } else {
        $shortDescription = Get-ReadmeShortDescription -Prompt "Enter a short description for README"
      }
      $readmeNl = Get-PreferredNewLine -Path $readmePath
      if ($isReportOnly) {
        $toolingBlock = Get-ReadmeToolingBlock -RepoRoot $repoRoot -ManifestPath $manifestPath -NewLine $readmeNl -NonInteractive
      } else {
        $toolingBlock = Get-ReadmeToolingBlock -RepoRoot $repoRoot -ManifestPath $manifestPath -NewLine $readmeNl
      }
      $readmeResult = Update-ReadmeMarkers -ReadmePath $readmePath -ProjectName $projectName -ShortDescription $shortDescription -ToolingBlock $toolingBlock -InsertMigrationMarker:$insertMigrationMarker -ReportOnly:$isReportOnly

      if ($readmeResult.changed) {
        $action = if ($isReportOnly) { "wouldUpdate" } else { "updated" }
        Add-Result -List $results -Path "README.md" -Action $action -Reason $readmeResult.reason
      } else {
        Add-Result -List $results -Path "README.md" -Action "skipped" -Reason $readmeResult.reason
      }
    }

    # Update template version flag and manifest.
    $existingVersion = $null
    if (Test-Path -LiteralPath $templateVersionPath) {
      $existingVersion = (Get-Content -LiteralPath $templateVersionPath -Raw).Trim()
    } elseif ($adoptedLegacyRepo -and $adoptFromVersionUsed) {
      $existingVersion = $adoptFromVersionUsed
    } else {
      throw "Template version file missing: $templateVersionPath"
    }
    if ($existingVersion -eq $toVersion) {
      Add-Result -List $results -Path $versionRel -Action "unchanged"
    } elseif ($isReportOnly) {
      Add-Result -List $results -Path $versionRel -Action "wouldUpdate" -Reason "template version"
    } else {
      Write-AtomicUtf8NoBom -Path $templateVersionPath -Content ("{0}`r`n" -f $toVersion)
      Add-Result -List $results -Path $versionRel -Action "updated" -Reason "template version"
    }

    $newManifest = [ordered]@{
      templateVersion    = $toVersion
      templateSourcePath = $sourceRoot
      generatedAt        = $isoTimestamp
      managedFiles       = $managedFiles
    }
    $newManifestJson = ConvertTo-JsonStable -Data $newManifest
    $existingManifestJson = Get-Content -LiteralPath $manifestPath -Raw

    if ($existingManifestJson -eq $newManifestJson) {
      Add-Result -List $results -Path $manifestRel -Action "unchanged"
    } elseif ($isReportOnly) {
      Add-Result -List $results -Path $manifestRel -Action "wouldUpdate" -Reason "template manifest"
    } else {
      Write-AtomicUtf8NoBom -Path $manifestPath -Content $newManifestJson
      Add-Result -List $results -Path $manifestRel -Action "updated" -Reason "template manifest"
    }
  } catch {
    if ($exitCode -eq 0) { $exitCode = 3 }
    if ($_.Exception.Message -and -not ($errors -contains $_.Exception.Message)) {
      $errors.Add($_.Exception.Message)
    }
  }
}

$summary = [ordered]@{
  total          = $results.Count
  copied         = ($results | Where-Object { $_.action -eq "copied" }).Count
  overwritten    = ($results | Where-Object { $_.action -eq "overwritten" }).Count
  unchanged      = ($results | Where-Object { $_.action -eq "unchanged" }).Count
  skipped        = ($results | Where-Object { $_.action -eq "skipped" }).Count
  wouldCopy      = ($results | Where-Object { $_.action -eq "wouldCopy" }).Count
  wouldOverwrite = ($results | Where-Object { $_.action -eq "wouldOverwrite" }).Count
  wouldUpdate    = ($results | Where-Object { $_.action -eq "wouldUpdate" }).Count
  updated        = ($results | Where-Object { $_.action -eq "updated" }).Count
  errors         = $errors.Count
  warnings       = $warnings.Count
}

$report = [ordered]@{
  script             = "template-upgrade.ps1"
  timestamp          = $isoTimestamp
  mode               = $Mode
  effectiveMode      = $effectiveMode
  dryRun             = [bool]$DryRun
  repoRoot           = $repoRoot
  fromVersion        = $fromVersionUsed
  toVersion          = $toVersionUsed
  templateSourcePath = $templateSourceUsed
  adoptedLegacyRepo  = $adoptedLegacyRepo
  adoptFromVersion   = $adoptFromVersionUsed
  exitCode           = $exitCode
  summary            = $summary
  gitignore          = $gitignoreInfo
  warnings           = $warnings
  errors             = $errors
  items              = $results
}

Ensure-Directory -Path $ReportDir
$reportBase = "upgrade-report_${reportStamp}"
$mdPath = Join-Path $ReportDir "${reportBase}.md"
$jsonPath = Join-Path $ReportDir "${reportBase}.json"

$md = New-Object System.Collections.Generic.List[string]
$md.Add("# Template Upgrade Report")
$md.Add("")
$md.Add(("- Timestamp: {0}" -f $isoTimestamp))
$md.Add(("- Mode: {0} (Effective: {1}, DryRun: {2})" -f $Mode, $effectiveMode, [bool]$DryRun))
$md.Add(("- Repo root: {0}" -f $repoRoot))
$md.Add(("- From version: {0}" -f $fromVersionUsed))
$md.Add(("- To version: {0}" -f $toVersionUsed))
$md.Add(("- Exit code: {0}" -f $exitCode))
$md.Add(("- Adopted legacy repo: {0}" -f $adoptedLegacyRepo))
$md.Add(("- AdoptFromVersion: {0}" -f $adoptFromVersionUsed))
$md.Add("")
$md.Add("## Summary")
$md.Add(("- Total items: {0}" -f $summary.total))
$md.Add(("- Copied: {0}" -f $summary.copied))
$md.Add(("- Overwritten: {0}" -f $summary.overwritten))
$md.Add(("- Updated: {0}" -f $summary.updated))
$md.Add(("- Unchanged: {0}" -f $summary.unchanged))
$md.Add(("- Would copy: {0}" -f $summary.wouldCopy))
$md.Add(("- Would overwrite: {0}" -f $summary.wouldOverwrite))
$md.Add(("- Would update: {0}" -f $summary.wouldUpdate))
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
