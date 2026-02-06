# Author: Sven Widowski
# Copyright: Sven Widowski, 2026
# Version: 1.0.0

<#
.SYNOPSIS
  Updates the published iCUERedGreen deployment and restarts the scheduled task.
#>

[CmdletBinding()]
param(
  [string]$SourcePath = "D:\Source\Repos\Privat\iCueRedGreen\artifacts\publish\win-x64",
  [string]$TargetPath = "D:\Tools\fritzbox\DECT200\iCUERedGreen",
  [string]$TaskName = "iCUERedGreen",
  [bool]$PreserveConfig = $true,
  [bool]$PreserveLogs = $true,
  [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourcePath)) {
  throw "Source path not found: $SourcePath"
}

if (-not (Test-Path -LiteralPath $TargetPath)) {
  New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

$runningFilePath = Join-Path -Path $TargetPath -ChildPath "running.txt"
if (Test-Path -LiteralPath $runningFilePath) {
  Remove-Item -LiteralPath $runningFilePath -Force
  Write-Host "Requested graceful stop (removed running.txt)."
}

$exePath = Join-Path -Path $TargetPath -ChildPath "iCUERedGreen.exe"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$stillRunning = $true

while ((Get-Date) -lt $deadline) {
  $processes = Get-Process -Name "iCUERedGreen" -ErrorAction SilentlyContinue
  if ($processes) {
    $matching = $processes | Where-Object {
      $_.Path -eq $exePath -or [string]::IsNullOrWhiteSpace($_.Path)
    }
  } else {
    $matching = @()
  }

  if (-not $matching) {
    $stillRunning = $false
    break
  }

  Start-Sleep -Seconds 1
}

if ($stillRunning) {
  throw "Timed out waiting for iCUERedGreen.exe to exit after $TimeoutSeconds seconds."
}

$roboArgs = @(
  $SourcePath,
  $TargetPath,
  "/MIR",
  "/R:2",
  "/W:2",
  "/NP",
  "/NFL",
  "/NDL"
)

if ($PreserveConfig) {
  $roboArgs += "/XF"
  $roboArgs += "appsettings.json"
  $roboArgs += "nlog.config"
}

if ($PreserveLogs) {
  $roboArgs += "/XD"
  $roboArgs += "logs"
}

Write-Host "Updating published files..."
& robocopy @roboArgs | Out-Host

if ($LASTEXITCODE -ge 8) {
  throw "Robocopy failed with exit code $LASTEXITCODE."
}

if (-not [string]::IsNullOrWhiteSpace($TaskName)) {
  Write-Host "Starting scheduled task: $TaskName"
  schtasks /Run /TN "$TaskName" | Out-Host
}

Write-Host "Update complete."
