# Builds the latest source and starts the Windows app, closing any older copy first so you never look at a stale build.
#
#   git pull; .\scripts\run-windows.ps1
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

Get-Process -Name 'ReWASD.UI' -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 9 SDK is needed to build ReWASD: https://dotnet.microsoft.com/download/dotnet/9.0'
}

dotnet run --project src/ReWASD.UI -c Release
