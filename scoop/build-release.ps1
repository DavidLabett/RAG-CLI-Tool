# Script to build and package for Scoop release
param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "releases"
)

$ErrorActionPreference = "Stop"

Write-Host "Building 2ND BRAIN CLI for Scoop release..." -ForegroundColor Cyan

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Build and publish
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create zip file
$publishDir = "bin\Release\net8.0\win-x64\publish"
$zipName = "SecondBrain-$Version-win-x64.zip"
$zipPath = Join-Path $OutputDir $zipName

Write-Host "Creating zip archive..." -ForegroundColor Yellow
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

# Calculate hash
Write-Host "Calculating hash..." -ForegroundColor Yellow
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "✓ Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Release package: $zipPath" -ForegroundColor Cyan
Write-Host "SHA256 Hash: $hash" -ForegroundColor Cyan
Write-Host ""
Write-Host "Update your Scoop manifest with:" -ForegroundColor Yellow
Write-Host "  - URL: (your release URL)" -ForegroundColor Gray
Write-Host "  - Hash: $hash" -ForegroundColor Gray
Write-Host "  - Version: $Version" -ForegroundColor Gray

