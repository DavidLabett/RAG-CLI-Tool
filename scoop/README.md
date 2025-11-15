# Scoop Manifest for 2ND BRAIN CLI

This directory contains the Scoop manifest for installing `2b` via Scoop.

## Setup Options

### Option 1: Local Bucket (For Development/Testing)

1. **Create a local bucket:**
   ```powershell
   # Create bucket directory
   mkdir $env:USERPROFILE\scoop-bucket
   cd $env:USERPROFILE\scoop-bucket
   
   # Initialize git repo (optional but recommended)
   git init
   ```

2. **Copy the manifest:**
   ```powershell
   # Copy manifest to bucket
   Copy-Item "path\to\SecondBrain-CLI\scoop\2b.json" "$env:USERPROFILE\scoop-bucket\2b.json"
   ```

3. **Add local bucket to Scoop:**
   ```powershell
   scoop bucket add local-bucket $env:USERPROFILE\scoop-bucket
   ```

4. **Install:**
   ```powershell
   scoop install local-bucket/2b
   ```

### Option 2: Custom GitHub Bucket (Recommended for Distribution)

1. **Create a GitHub repository** for your Scoop bucket (e.g., `yourusername/scoop-2b`)

2. **Update the manifest** with your GitHub release URLs:
   - Update `homepage` with your repo URL
   - Update `url` with your release download URL
   - Update `checkver.github` with your repo
   - Calculate and update the `hash` for your release

3. **Add the bucket:**
   ```powershell
   scoop bucket add 2b https://github.com/yourusername/scoop-2b
   ```

4. **Install:**
   ```powershell
   scoop install 2b
   ```

### Option 3: Add to Main Bucket (For Public Distribution)

If you want to add to the main Scoop bucket:
1. Fork https://github.com/ScoopInstaller/Main
2. Add your manifest to the `bucket` directory
3. Submit a pull request

## Creating a Release Package

Before using the manifest, you need to create a zip file of your published application:

```powershell
# Build and publish
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Create zip file
$publishDir = "bin\Release\net8.0\win-x64\publish"
$zipPath = "SecondBrain-1.0.0-win-x64.zip"
Compress-Archive -Path $publishDir\* -DestinationPath $zipPath -Force

# Calculate hash for manifest
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
Write-Host "Hash: $hash"
```

Then update the manifest with:
- The actual download URL (GitHub release, etc.)
- The calculated hash
- Correct version number

## Manifest Fields Explained

- **version**: Application version (must match release)
- **description**: Short description shown in `scoop search`
- **homepage**: Project homepage URL
- **license**: License type
- **architecture.64bit.url**: Download URL for the zip file
- **architecture.64bit.hash**: SHA256 hash of the zip file (for verification)
- **bin**: Executable name (Scoop will create a shim)
- **checkver**: Auto-update configuration (checks GitHub releases)
- **autoupdate**: Auto-update URL template

## Notes

- Scoop automatically adds the executable to PATH
- The executable will be available as `SecondBrain.exe` (or whatever you set in `bin`)
- To create an alias for `2b`, you can add it to your PowerShell profile or use Scoop's alias feature

