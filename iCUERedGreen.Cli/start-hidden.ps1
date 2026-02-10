# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.0.1

<#
.SYNOPSIS
  Starts iCUERedGreen.Cli.exe hidden and waits for it to exit.
#>

[CmdletBinding()]
param(
  [string]$ExecutablePath,
  [string[]]$Arguments = @()
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
  $ExecutablePath = Join-Path -Path $PSScriptRoot -ChildPath "iCUERedGreen.Cli.exe"
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
  throw "Executable not found: $ExecutablePath"
}

$workingDirectory = Split-Path -Path $ExecutablePath

# Start hidden so Task Scheduler does not show a console window.
$startArgs = @{
  FilePath = $ExecutablePath
  WorkingDirectory = $workingDirectory
  WindowStyle = "Hidden"
  PassThru = $true
}

if ($Arguments.Count -gt 0) {
  $startArgs.ArgumentList = $Arguments
}

$process = Start-Process @startArgs
if ($process) {
  # Keep the task active while the app runs.
  Wait-Process -Id $process.Id
  exit $process.ExitCode
}
