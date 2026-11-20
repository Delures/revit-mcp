param (
    [string]$RevitYear = "2023",
    [string]$Configuration = "",
    [string]$McpName = "RevitMCP",
    [string]$AddInGuid = "D4A8E24E-721D-40EF-8A35-1317A3EE3691",
    [string]$VendorDescription = "Model Context Protocol Server"
)

$ErrorActionPreference = "Stop"

if (-not $Configuration -or $Configuration -eq "Release" -or $Configuration -eq "Debug") {
    $Configuration = "R$RevitYear"
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Deploying $McpName Plugin for Revit $RevitYear" -ForegroundColor Cyan
Write-Host " Central Storage: %LocalAppData%\$McpName" -ForegroundColor Cyan
Write-Host " Configuration:   $Configuration" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Build the solution
Write-Host "Building project for $Configuration..." -ForegroundColor Yellow
dotnet build RevitMCP.sln -c $Configuration

$binDir = Join-Path $PSScriptRoot "src\RevitMCP.Plugin\bin\$Configuration"
$pluginDll = Join-Path $binDir "RevitMCP.Plugin.dll"

if (-not (Test-Path $pluginDll)) {
    Write-Error "Build output not found at: $pluginDll"
    exit 1
}

# 2. Setup Centralized LocalAppData Storage for Binaries
$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$mcpCentralFolder = Join-Path $localAppData $McpName
$targetBinFolder = Join-Path $mcpCentralFolder "bin\$RevitYear"

if (Test-Path $targetBinFolder) {
    Get-ChildItem -Path $targetBinFolder -Exclude "*.log","*.db" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $targetBinFolder -Force | Out-Null
}

# 3. Copy binaries and dependencies to %LocalAppData%\<McpName>\bin\$RevitYear
Write-Host "Copying plugin binaries to $targetBinFolder..." -ForegroundColor Yellow
Copy-Item -Path "$binDir\*" -Destination $targetBinFolder -Recurse -Force

if (Test-Path "$PSScriptRoot\src\RevitMCP.Plugin\Resources") {
    Copy-Item -Path "$PSScriptRoot\src\RevitMCP.Plugin\Resources" -Destination $targetBinFolder -Recurse -Force
}

# 4. Clean up legacy subfolders from %AppData%\Autodesk\Revit\Addins\$RevitYear
$appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$addinFolder = Join-Path $appData "Autodesk\Revit\Addins\$RevitYear"
$legacySubfolder = Join-Path $addinFolder $McpName

if (Test-Path $legacySubfolder) {
    Write-Host "Cleaning up legacy subfolder in Addins: $legacySubfolder..." -ForegroundColor DarkGray
    Remove-Item -Path $legacySubfolder -Recurse -Force
}

if (-not (Test-Path $addinFolder)) {
    New-Item -ItemType Directory -Path $addinFolder -Force | Out-Null
}

# 5. Generate clean .addin manifest pointing to %LocalAppData%\<McpName>\bin\$RevitYear\RevitMCP.Plugin.dll
$addinManifestPath = Join-Path $addinFolder "$McpName.addin"
$installedDllPath = Join-Path $targetBinFolder "RevitMCP.Plugin.dll"

$manifestContent = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>$McpName Server</Name>
    <Assembly>$installedDllPath</Assembly>
    <AddInId>$AddInGuid</AddInId>
    <FullClassName>RevitMCP.Plugin.RevitMcpApp</FullClassName>
    <VendorId>$McpName</VendorId>
    <VendorDescription>$McpName $VendorDescription</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

Set-Content -Path $addinManifestPath -Value $manifestContent -Encoding UTF8

# 6. Publish CLI to %LocalAppData%\<McpName>\bin\cli as well as workspace publish/cli
Write-Host "Publishing CLI Release..." -ForegroundColor Yellow
$cliTargetDir = Join-Path $mcpCentralFolder "bin\cli"
dotnet publish "$PSScriptRoot\src\RevitMCP.Cli\RevitMCP.Cli.csproj" -c Release -o $cliTargetDir | Out-Null
dotnet publish "$PSScriptRoot\src\RevitMCP.Cli\RevitMCP.Cli.csproj" -c Release -o "$PSScriptRoot\publish\cli" | Out-Null

Write-Host "==========================================" -ForegroundColor Green
Write-Host " Deployment Completed Successfully!" -ForegroundColor Green
Write-Host " Only .addin file in Addins: $addinManifestPath" -ForegroundColor Green
Write-Host " Central Plugin Binaries:     $targetBinFolder" -ForegroundColor Green
Write-Host " Central CLI Executable:      $cliTargetDir\RevitMCP.Cli.exe" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
