# Release Guide for 2ND BRAIN CLI

This guide explains how to create GitHub releases and distribute the CLI via Scoop.

## Prerequisites

- Scoop installed on your system
- GitHub repository for your project
- GitHub repository for your Scoop bucket (optional, for public distribution)

## Step 1: Build and Package Release

1. **Build the release package:**
   ```powershell
   .\scoop\build-release.ps1 -Version "1.0.0"
   ```
   
   This will:
   - Build the application in Release mode
   - Create a zip file in the `releases/` directory
   - Calculate and display the SHA256 hash

2. **Note the output:**
   - Release package location: `releases/SecondBrain-1.0.0-win-x64.zip`
   - SHA256 Hash: (displayed in console)

## Step 2: Create GitHub Release

1. **Go to your GitHub repository**
   - Navigate to: `https://github.com/yourusername/SecondBrain-CLI/releases`
   - Click "Create a new release"

2. **Fill in release details:**
   - **Tag version**: `v1.0.0` (must match your version)
   - **Release title**: `v1.0.0` or `Release 1.0.0`
   - **Description**: Add release notes, changelog, etc.

3. **Upload the zip file:**
   - Drag and drop `releases/SecondBrain-1.0.0-win-x64.zip` to the release
   - Or click "Attach binaries" and select the file

4. **Publish the release:**
   - Click "Publish release"

5. **Copy the download URL:**
   - Right-click on the uploaded zip file
   - Copy the direct download link
   - Example: `https://github.com/yourusername/SecondBrain-CLI/releases/download/v1.0.0/SecondBrain-1.0.0-win-x64.zip`

## Step 3: Update Scoop Manifest

1. **Open the manifest:**
   - Edit `scoop/2b.json`

2. **Update the following fields:**
   ```json
   {
     "version": "1.0.0",
     "homepage": "https://github.com/yourusername/SecondBrain-CLI",
     "architecture": {
       "64bit": {
         "url": "https://github.com/yourusername/SecondBrain-CLI/releases/download/v1.0.0/SecondBrain-1.0.0-win-x64.zip",
         "hash": "PASTE_THE_HASH_FROM_STEP_1_HERE"
       }
     },
     "checkver": {
       "github": "yourusername/SecondBrain-CLI"
     }
   }
   ```

3. **Save the manifest**

## Step 4: Set Up Scoop Bucket (Choose One Option)

### Option A: Local Bucket (For Personal Use)

1. **Create local bucket:**
   ```powershell
   mkdir $env:USERPROFILE\scoop-2b-bucket
   Copy-Item scoop\2b.json $env:USERPROFILE\scoop-2b-bucket\2b.json
   ```

2. **Add bucket to Scoop:**
   ```powershell
   scoop bucket add local-2b $env:USERPROFILE\scoop-2b-bucket
   ```

3. **Install:**
   ```powershell
   scoop install local-2b/2b
   ```

### Option B: GitHub Bucket (For Public Distribution)

1. **Create a new GitHub repository:**
   - Name: `scoop-2b` (or any name you prefer)
   - Make it public
   - Initialize with a README

2. **Clone and set up:**
   ```powershell
   git clone https://github.com/yourusername/scoop-2b.git
   cd scoop-2b
   Copy-Item ..\SecondBrain-CLI\scoop\2b.json .
   git add 2b.json
   git commit -m "Add 2b manifest"
   git push
   ```

3. **Add bucket to Scoop:**
   ```powershell
   scoop bucket add 2b https://github.com/yourusername/scoop-2b
   ```

4. **Install:**
   ```powershell
   scoop install 2b
   ```

### Option C: Submit to Main Scoop Bucket (For Official Distribution)

1. **Fork the main bucket:**
   - Go to: https://github.com/ScoopInstaller/Main
   - Click "Fork"

2. **Add your manifest:**
   ```powershell
   git clone https://github.com/yourusername/Main.git
   cd Main\bucket
   Copy-Item ..\..\SecondBrain-CLI\scoop\2b.json .
   git add 2b.json
   git commit -m "Add 2b (Second Brain CLI)"
   git push
   ```

3. **Submit pull request:**
   - Go to your fork on GitHub
   - Click "New Pull Request"
   - Submit for review

## Step 5: Usage

### Installation

```powershell
# If using local bucket
scoop install local-2b/2b

# If using GitHub bucket
scoop install 2b

# If in main bucket
scoop install 2b
```

### Verify Installation

```powershell
SecondBrain.exe version
# or
2b version  # if you have the alias set up
```

### Update

```powershell
scoop update 2b
```

### Uninstall

```powershell
scoop uninstall 2b
```

## Step 6: Future Releases

For each new release:

1. **Update version in `appsettings.json`:**
   ```json
   "CLI": {
     "ApplicationVersion": "1.0.1"
   }
   ```

2. **Build new release:**
   ```powershell
   .\scoop\build-release.ps1 -Version "1.0.1"
   ```

3. **Create GitHub release** (Step 2)

4. **Update manifest:**
   - Update `version` in `scoop/2b.json`
   - Update `url` with new release URL
   - Update `hash` with new hash

5. **Commit and push manifest:**
   ```powershell
   # If using GitHub bucket
   cd scoop-2b
   git add 2b.json
   git commit -m "Update 2b to v1.0.1"
   git push
   ```

6. **Users update:**
   ```powershell
   scoop update 2b
   ```

## Troubleshooting

### Manifest Validation

Test your manifest before committing:
```powershell
scoop install scoop/2b.json
```

### Hash Calculation

If you need to recalculate the hash:
```powershell
Get-FileHash releases\SecondBrain-1.0.0-win-x64.zip -Algorithm SHA256
```

### Check Manifest Syntax

Validate JSON syntax:
```powershell
Get-Content scoop\2b.json | ConvertFrom-Json
```

### Common Issues

1. **"Hash mismatch"**: Make sure the hash in the manifest matches the actual file hash
2. **"URL not found"**: Verify the GitHub release URL is correct and the file is uploaded
3. **"Version already installed"**: Uninstall first: `scoop uninstall 2b`

## Notes

- Scoop automatically manages PATH - no manual configuration needed
- The executable is available as `SecondBrain.exe` after installation
- To use `2b` as a command, set up an alias in your shell profile (see main README)
- Configuration files (`appsettings.json`) are in the Scoop install directory
- Updates are handled automatically by Scoop's `checkver` mechanism

