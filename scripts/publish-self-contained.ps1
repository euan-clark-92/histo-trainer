#Requires -Version 5.1
<#
.SYNOPSIS
    Builds a self-contained TissueTrainer deployment folder (no .NET install required on lab PCs).

.PARAMETER Runtime
    Windows runtime identifier: win-x64 (default), win-x86, or win-arm64.

.PARAMETER Configuration
    Build configuration: Release (default) or Debug.

.PARAMETER OutputDir
    Output folder for the published app. Defaults to dist/TissueTrainer-<runtime>.

.PARAMETER CreateZip
    Also create a zip archive next to the output folder.

.EXAMPLE
    .\scripts\publish-self-contained.ps1

.EXAMPLE
    .\scripts\publish-self-contained.ps1 -Runtime win-x64 -CreateZip
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $OutputDir = '',

    [switch] $CreateZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-PlatformFromRuntime([string] $Rid) {
    switch ($Rid) {
        'win-x64' { return 'x64' }
        'win-x86' { return 'x86' }
        'win-arm64' { return 'ARM64' }
        default { throw "Unsupported runtime: $Rid" }
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'src\TissueTrainer\TissueTrainer.csproj'
$migrationsSource = Join-Path $repoRoot 'src\TissueTrainer.Data\Migrations'
$contentSource = Join-Path $repoRoot 'content'
$configSource = Join-Path $repoRoot 'app.config.json'
$platform = Get-PlatformFromRuntime $Runtime

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist\TissueTrainer-$Runtime"
}
else {
    $OutputDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)
}

Write-Step "Repository: $repoRoot"
Write-Step "Runtime: $Runtime ($platform)"
Write-Step "Configuration: $Configuration"
Write-Step "Output: $OutputDir"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

Write-Step "Checking for .NET SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw @"
.NET SDK not found on PATH.

Install .NET 8 SDK once on a build machine:
  https://dotnet.microsoft.com/download/dotnet/8.0

Lab PCs only need the published folder from this script — not the SDK.
"@
}

$installedSdks = & dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to query installed .NET SDKs. Details: $installedSdks"
}

if ($installedSdks -notmatch '8\.') {
    Write-Warning "No .NET 8 SDK detected. WinUI publish may fail until .NET 8 SDK is installed."
    Write-Host $installedSdks
}

if (Test-Path $OutputDir) {
    Write-Step "Cleaning existing output folder"
    Remove-Item -Path $OutputDir -Recurse -Force
}

Write-Step "Restoring NuGet packages"
& dotnet restore $projectPath -p:Platform=$platform
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

Write-Step "Publishing self-contained app"
& dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=$platform `
    -p:PublishReadyToRun=true `
    -p:WindowsAppSDKSelfContained=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Step "Ensuring deployment files are present"
$migrationsTarget = Join-Path $OutputDir 'Migrations'
$contentTarget = Join-Path $OutputDir 'content'

New-Item -ItemType Directory -Force -Path $migrationsTarget | Out-Null
Copy-Item -Path (Join-Path $migrationsSource '*.sql') -Destination $migrationsTarget -Force

if (Test-Path $contentSource) {
    if (Test-Path $contentTarget) {
        Remove-Item -Path $contentTarget -Recurse -Force
    }
    Copy-Item -Path $contentSource -Destination $contentTarget -Recurse -Force
}

Copy-Item -Path $configSource -Destination (Join-Path $OutputDir 'app.config.json') -Force

$deployReadme = @"
TissueTrainer - Self-contained deployment
=========================================

Run on lab PCs:
  TissueTrainer.exe

No .NET SDK or runtime install is required on this PC.

Folders:
  content/     Question bank and histology images (trainer-updatable)
  Migrations/  Local SQLite schema scripts
  app.config.json

Associate progress database:
  %AppData%\TissueTrainer\progress.db

To update training content only:
  Replace the content/ folder and keep app.config.json pointed at it.

Built: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Runtime: $Runtime
Configuration: $Configuration
"@

Set-Content -Path (Join-Path $OutputDir 'DEPLOY-README.txt') -Value $deployReadme -Encoding UTF8

if ($CreateZip) {
    $zipPath = "$OutputDir.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Step "Creating zip archive: $zipPath"
    Compress-Archive -Path (Join-Path $OutputDir '*') -DestinationPath $zipPath -Force
}

$exePath = Join-Path $OutputDir 'TissueTrainer.exe'
$folderSizeMb = [math]::Round(((Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 1)

Write-Host ''
Write-Host 'Publish complete.' -ForegroundColor Green
Write-Host "  EXE:      $exePath"
Write-Host "  Size:     $folderSizeMb MB"
Write-Host "  Folder:   $OutputDir"
if ($CreateZip) {
    Write-Host "  Zip:      $OutputDir.zip"
}
Write-Host ''
Write-Host 'Copy the entire output folder to lab PCs or wrap it in MSI/Inno Setup.' -ForegroundColor Yellow
