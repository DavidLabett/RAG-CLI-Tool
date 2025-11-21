# Setup script for Scoop bucket repository
# This script helps initialize the GitHub bucket repository

param(
    [string]$BucketPath = "..\scoop-2b"
)

# Get the script directory (where this script is located)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "Setting up Scoop bucket repository..." -ForegroundColor Cyan

# Check if bucket directory exists
if (Test-Path $BucketPath) {
    Write-Host "Bucket directory already exists: $BucketPath" -ForegroundColor Yellow
    $response = Read-Host "Do you want to continue? (y/n)"
    if ($response -ne "y") {
        exit
    }
} else {
    Write-Host "Cloning repository..." -ForegroundColor Green
    $BucketParent = Split-Path -Parent $BucketPath
    if (-not (Test-Path $BucketParent)) {
        New-Item -ItemType Directory -Path $BucketParent -Force | Out-Null
    }
    git clone https://github.com/DavidLabett/scoop-2b.git $BucketPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to clone repository. Make sure it exists on GitHub." -ForegroundColor Red
        exit 1
    }
}

# Change to bucket directory
Push-Location $BucketPath

# Copy manifest file
Write-Host "Copying manifest file..." -ForegroundColor Green
$ManifestSource = Join-Path $ScriptDir "2b.json"
Copy-Item $ManifestSource -Destination "2b.json" -Force

# Check if README exists
if (-not (Test-Path "README.md")) {
    Write-Host "Creating README.md..." -ForegroundColor Green
    $ReadmeSource = Join-Path $ScriptDir "BUCKET-README.md"
    Copy-Item $ReadmeSource -Destination "README.md" -Force
}

# Stage files
Write-Host "Staging files..." -ForegroundColor Green
git add 2b.json
if (Test-Path "README.md") {
    git add README.md
}

# Check if there are changes
$status = git status --porcelain
if ($status) {
    Write-Host "Committing changes..." -ForegroundColor Green
    git commit -m "Add 2b manifest for v1.0.0"
    
    Write-Host "`nReady to push! Run the following command:" -ForegroundColor Cyan
    Write-Host "  git push origin main" -ForegroundColor Yellow
    Write-Host "`nOr if using master branch:" -ForegroundColor Cyan
    Write-Host "  git push origin master" -ForegroundColor Yellow
} else {
    Write-Host "No changes to commit." -ForegroundColor Yellow
}

Pop-Location

Write-Host "`nBucket setup complete!" -ForegroundColor Green
Write-Host "`nAfter pushing, users can install with:" -ForegroundColor Cyan
Write-Host "  scoop bucket add 2b https://github.com/DavidLabett/scoop-2b" -ForegroundColor Yellow
Write-Host "  scoop install 2b" -ForegroundColor Yellow

