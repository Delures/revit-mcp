[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [string]$McpName = "RevitMCP",

    [Parameter(Position = 1)]
    [string]$McpTitle = "Revit MCP",

    [Parameter()]
    [string]$AppVersion = "1.0.0",

    [Parameter()]
    [string]$AppPublisher = "Delures",

    [Parameter()]
    [string]$AddInGuid = "D4A8E24E-721D-40EF-8A35-1317A3EE3691",

    [Parameter()]
    [string[]]$RevitYears = @("2019", "2020", "2021", "2022", "2023", "2024", "2025", "2026", "2027"),

    [Parameter()]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$distDir = Join-Path $scriptRoot "dist"
$outputDir = Join-Path $scriptRoot "dist-installer"
$issPath = Join-Path $scriptRoot "installer\RevitMCP.iss"

# Sanitize array parameters if passed as comma-separated strings from CLI
$RevitYears = @($RevitYears) | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim('"', "'", " ") } | Where-Object { $_ }

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Installer for $McpTitle ($McpName)" -ForegroundColor Cyan
Write-Host " Version: $AppVersion | Publisher: $AppPublisher" -ForegroundColor Cyan
Write-Host " Target Revit Versions: $($RevitYears -join ', ')" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Locate Inno Setup Compiler (ISCC)
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

[string]$iscc = $isccCandidates | Select-Object -First 1

if (-not $iscc -or -not (Test-Path $iscc)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found. Install it via: winget install JRSoftware.InnoSetup"
    exit 1
}
Write-Host "Using Inno Setup compiler: $iscc" -ForegroundColor DarkGray

if (-not $SkipBuild) {
    # 2. Clean staging dist folder
    if (Test-Path $distDir) {
        Remove-Item -Path $distDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path (Join-Path $distDir "bin\cli") -Force | Out-Null

    # 3. Publish CLI
    Write-Host "`n[1/3] Publishing .NET 8 CLI..." -ForegroundColor Yellow
    $cliProj = Join-Path $scriptRoot "src\RevitMCP.Cli\RevitMCP.Cli.csproj"
    $cliOut = Join-Path $distDir "bin\cli"
    dotnet publish $cliProj -c Release -o $cliOut --nologo -v q -p:Version=$AppVersion
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish CLI"
        exit 1
    }

    # 4. Build Plugins for each Revit version
    Write-Host "`n[2/3] Building Revit Plugins..." -ForegroundColor Yellow
    $pluginProj = Join-Path $scriptRoot "src\RevitMCP.Plugin\RevitMCP.Plugin.csproj"

    foreach ($year in $RevitYears) {
        Write-Host "  -> Building configuration R$year..." -ForegroundColor Gray
        $config = "R$year"
        dotnet build $pluginProj -c $config --nologo -v q -p:Version=$AppVersion
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Skipping Revit $year (build failed or SDK unavailable)"
            continue
        }

        $builtBin = Join-Path $scriptRoot "src\RevitMCP.Plugin\bin\$config"
        $targetYearDir = Join-Path $distDir "bin\$year"
        New-Item -ItemType Directory -Path $targetYearDir -Force | Out-Null
        Copy-Item -Path "$builtBin\*" -Destination $targetYearDir -Recurse -Force

        if (Test-Path "$scriptRoot\src\RevitMCP.Plugin\Resources") {
            Copy-Item -Path "$scriptRoot\src\RevitMCP.Plugin\Resources" -Destination $targetYearDir -Recurse -Force
        }
    }
}

# 5. Compile Inno Setup Script
Write-Host "`n[3/3] Compiling Inno Setup package..." -ForegroundColor Yellow
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$isccArgs = @(
    "/Q",
    "/DMcpName=$McpName",
    "/DMcpTitle=$McpTitle",
    "/DAppVersion=$AppVersion",
    "/DAppPublisher=$AppPublisher",
    "/DAddInGuid=$AddInGuid",
    "/DDistDir=$distDir",
    "/O$outputDir",
    "/F$McpName-Setup-$AppVersion",
    $issPath
)

& $iscc $isccArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "ISCC compilation failed with code $LASTEXITCODE"
    exit 1
}

$outputExe = Join-Path $outputDir "$McpName-Setup-$AppVersion.exe"
if (Test-Path $outputExe) {
    $sizeMb = [math]::Round(((Get-Item $outputExe).Length / 1MB), 2)
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host " Installer Built Successfully!" -ForegroundColor Green
    Write-Host " File: $outputExe" -ForegroundColor Green
    Write-Host " Size: $sizeMb MB" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
} else {
    Write-Error "Output executable was not created"
    exit 1
}
