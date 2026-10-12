# Builds ReWASD.exe for Windows: one self-contained file, nothing to install (the .NET runtime is inside it).
#
#   scripts\package-windows.ps1                 # -> artifacts\windows\ReWASD.exe
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

# A running copy would keep showing the old build (and lock the file), so close it first
Get-Process -Name 'ReWASD' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*artifacts*' } | Stop-Process -Force -ErrorAction SilentlyContinue

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 9 SDK is needed to build ReWASD: https://dotnet.microsoft.com/download/dotnet/9.0'
}

$out = 'artifacts/windows'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish src/ReWASD.UI/ReWASD.UI.csproj -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Move-Item "$out/ReWASD.UI.exe" "$out/ReWASD.exe" -Force
Write-Host ""
Write-Host "Built $out/ReWASD.exe"
