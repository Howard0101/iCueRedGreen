<#
.SYNOPSIS
Compatibility wrapper for docs link validation script.

.DESCRIPTION
Deprecated compatibility entry point that forwards execution to
`scripts/verify-ai-docs.ps1`.

.PARAMETER Mode
Forwarded to `verify-ai-docs.ps1`.

.PARAMETER RepoRoot
Forwarded to `verify-ai-docs.ps1`.
#>

[CmdletBinding()]
param(
  [ValidateSet("check","render")]
  [string]$Mode = "check",

  [string]$RepoRoot
)

$ErrorActionPreference = "Stop"
$targetScript = Join-Path -Path $PSScriptRoot -ChildPath "verify-ai-docs.ps1"

if (-not (Test-Path -LiteralPath $targetScript)) {
  throw "Target script not found: ${targetScript}"
}

Write-Warning "Deprecated script path: use .\scripts\verify-ai-docs.ps1."

$invokeArgs = @(
  "-NoProfile",
  "-File", $targetScript,
  "-Mode", $Mode
)

if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
  $invokeArgs += @("-RepoRoot", $RepoRoot)
}

& pwsh @invokeArgs
exit $LASTEXITCODE
