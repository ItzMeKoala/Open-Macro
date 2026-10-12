# Builds the ReWASD app for Windows and puts it in .\dist\ReWASD.exe. Double-click Build.cmd, or run:  .\build.ps1 [-Run]
param([switch]$Run)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

& "$PSScriptRoot\scripts\package-windows.ps1"
New-Item -ItemType Directory -Force dist | Out-Null
Copy-Item 'artifacts\windows\ReWASD.exe' 'dist\ReWASD.exe' -Force

Write-Host ""
Write-Host "Done: $PSScriptRoot\dist\ReWASD.exe  (just double-click it; no install needed)"
if ($Run) { Start-Process "$PSScriptRoot\dist\ReWASD.exe" }
