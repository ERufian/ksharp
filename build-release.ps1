#!/usr/bin/env pwsh
# Cross-platform release build script for K3CSharp
# Produces self-contained single-file binaries for Windows, Linux, and macOS

param(
    [switch]$SkipTests,
    [switch]$Zip,
    [string]$OutputBase = "publish"
)

$ErrorActionPreference = "Stop"
$SolutionPath = "K3CSharp.sln"

$Projects = @(
    @{ Name = "K3CSharp";     Path = "K3CSharp\K3CSharp.csproj";     Binary = "K3CSharp";     Platforms = @(
        @{ Profile = "Win-x64";     Rid = "win-x64";     Ext = ".exe"; OutputName = "ksharp-win-x64";     Dir = "win-x64" },
        @{ Profile = "Linux-x64";   Rid = "linux-x64";   Ext = "";     OutputName = "ksharp-linux-x64";   Dir = "linux-x64" },
        @{ Profile = "macOS-x64";   Rid = "osx-x64";     Ext = "";     OutputName = "ksharp-macos-x64";   Dir = "osx-x64" },
        @{ Profile = "macOS-arm64"; Rid = "osx-arm64";   Ext = "";     OutputName = "ksharp-macos-arm64"; Dir = "osx-arm64" }
    )},
    @{ Name = "K3CSharp.MCP"; Path = "K3CSharp.MCP\K3CSharp.MCP.csproj"; Binary = "K3CSharp.MCP"; Platforms = @(
        @{ Profile = "Win-x64";     Rid = "win-x64";     Ext = ".exe"; OutputName = "k3csharp-mcp-win-x64";     Dir = "k3csharp-mcp-win-x64" },
        @{ Profile = "Linux-x64";   Rid = "linux-x64";   Ext = "";     OutputName = "k3csharp-mcp-linux-x64";   Dir = "k3csharp-mcp-linux-x64" },
        @{ Profile = "macOS-x64";   Rid = "osx-x64";     Ext = "";     OutputName = "k3csharp-mcp-macos-x64";   Dir = "k3csharp-mcp-macos-x64" },
        @{ Profile = "macOS-arm64"; Rid = "osx-arm64";   Ext = "";     OutputName = "k3csharp-mcp-macos-arm64"; Dir = "k3csharp-mcp-macos-arm64" }
    )},
    @{ Name = "KMCPServer";   Path = "KMCPServer\KMCPServer.csproj";   Binary = "k";             Platforms = @(
        @{ Profile = "Win-x64";     Rid = "win-x64";     Ext = ".exe"; OutputName = "kmcp-server-win-x64";     Dir = "kmcp-server-win-x64" },
        @{ Profile = "Linux-x64";   Rid = "linux-x64";   Ext = "";     OutputName = "kmcp-server-linux-x64";   Dir = "kmcp-server-linux-x64" },
        @{ Profile = "macOS-x64";   Rid = "osx-x64";     Ext = "";     OutputName = "kmcp-server-macos-x64";   Dir = "kmcp-server-macos-x64" },
        @{ Profile = "macOS-arm64"; Rid = "osx-arm64";   Ext = "";     OutputName = "kmcp-server-macos-arm64"; Dir = "kmcp-server-macos-arm64" }
    )},
    @{ Name = "ApplyTweaks";  Path = "ApplyTweaks\ApplyTweaks.csproj"; Binary = "ApplyTweaks";  Platforms = @(
        @{ Profile = "Win-x64";     Rid = "win-x64";     Ext = ".exe"; OutputName = "applytweaks-win-x64";     Dir = "applytweaks-win-x64" },
        @{ Profile = "Linux-x64";   Rid = "linux-x64";   Ext = "";     OutputName = "applytweaks-linux-x64";   Dir = "applytweaks-linux-x64" },
        @{ Profile = "macOS-x64";   Rid = "osx-x64";     Ext = "";     OutputName = "applytweaks-macos-x64";   Dir = "applytweaks-macos-x64" },
        @{ Profile = "macOS-arm64"; Rid = "osx-arm64";   Ext = "";     OutputName = "applytweaks-macos-arm64"; Dir = "applytweaks-macos-arm64" }
    )}
)

function Write-Header($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Success($text) { Write-Host "  OK: $text" -ForegroundColor Green }
function Write-Error($text)    { Write-Host "FAIL: $text" -ForegroundColor Red }

Write-Header "K3CSharp Cross-Platform Release Build"

# Verify .NET SDK is available
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error ".NET SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK: $dotnetVersion"

# Clean
Write-Header "Step 1: Clean"
dotnet clean $SolutionPath -c Release -v quiet | Out-Null
Write-Success "Clean complete"

# Restore
Write-Header "Step 2: Restore"
dotnet restore $SolutionPath --verbosity quiet
Write-Success "Restore complete"

# Build Release (to catch compile errors before publishing)
Write-Header "Step 3: Build Release"
dotnet build $SolutionPath -c Release --no-restore -v quiet
Write-Success "Build complete"

# Run tests (unless skipped)
if (-not $SkipTests) {
    Write-Header "Step 4: Run Tests"
    Push-Location K3CSharp.Tests
    try {
        dotnet run --verbosity quiet
        Write-Success "Tests passed"
    }
    catch {
        Write-Error "Tests failed. Use -SkipTests to bypass."
        Pop-Location
        exit 1
    }
    Pop-Location
}
else {
    Write-Host "Skipping tests (-SkipTests specified)" -ForegroundColor Yellow
}

# Publish each project for each platform
Write-Header "Step 5: Publish Cross-Platform Binaries"
$published = @()

foreach ($proj in $Projects) {
    Write-Host "`n--- $($proj.Name) ---" -ForegroundColor Yellow

    foreach ($plat in $proj.Platforms) {
        $profile = $plat.Profile
        $rid = $plat.Rid
        $ext = $plat.Ext
        $outputName = $plat.OutputName
        $dir = $plat.Dir

        $publishDir = Join-Path $OutputBase $dir

        Write-Host "  Publishing $profile ($rid)..." -NoNewline

        dotnet publish $proj.Path -c Release -p:PublishProfile=$profile -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Publish failed for $($proj.Name) $profile"
            continue
        }

        # Locate the binary
        $binaryName = "$($proj.Binary)$ext"
        $binaryPath = Join-Path $publishDir $binaryName

        if (-not (Test-Path $binaryPath)) {
            Write-Error "Binary not found at $binaryPath"
            continue
        }

        $binarySize = (Get-Item $binaryPath).Length / 1MB
        Write-Success "$($proj.Name) $profile -> $publishDir ($($binarySize.ToString('F1')) MB)"
        $published += [PSCustomObject]@{
            Project = $proj.Name
            Platform = $profile
            Path = $publishDir
            Binary = $binaryPath
            SizeMB = $binarySize
            OutputName = $outputName
        }
    }
}

# Rename binaries
foreach ($p in $published) {
    $newName = $p.OutputName + [System.IO.Path]::GetExtension($p.Binary)
    $newPath = Join-Path $p.Path $newName
    Copy-Item $p.Binary $newPath -Force
    $p | Add-Member -NotePropertyName "RenamedBinary" -NotePropertyValue $newPath -Force
}

# Optional: Create ZIP archives
if ($Zip) {
    Write-Header "Step 6: Create ZIP Archives"
    foreach ($p in $published) {
        $zipName = "$OutputBase\$($p.OutputName).zip"
        Compress-Archive -Path "$($p.Path)\*" -DestinationPath $zipName -Force
        $zipSize = (Get-Item $zipName).Length / 1MB
        Write-Success "$($p.Project) $($p.Platform) archive -> $zipName ($($zipSize.ToString('F1')) MB)"
    }
}

# Summary
Write-Header "Build Summary"
Write-Host ""
Write-Host "  Project          Platform        Binary Path                              Size"
Write-Host "  --------------------------------------------------------------------------------"
foreach ($p in $published) {
    $displayPath = $p.RenamedBinary.Replace((Get-Location).Path + "\", "")
    $col1 = "$($p.Project)".PadRight(16)
    $col2 = "$($p.Platform)".PadRight(15)
    Write-Host "  $col1 $col2 $($displayPath.PadRight(40)) $($p.SizeMB.ToString('F1')) MB"
}

Write-Host "`nAll binaries published to .\$OutputBase\" -ForegroundColor Green
