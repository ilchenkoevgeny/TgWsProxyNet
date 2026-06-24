param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$ServiceName = "TgWsProxy",
    [string]$InstallDir = "C:\Program Files\TgWsProxy",
    [switch]$OverwriteConfig
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "src\TgWsProxy\TgWsProxy.csproj"
$publishDir = Join-Path $root "artifacts\publish\$Runtime"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw "Run this script from PowerShell as Administrator."
}

Write-Host "Publishing TgWsProxy..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($null -ne $service) {
    if ($service.Status -ne "Stopped") {
        Write-Host "Stopping service $ServiceName..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force

        $service.WaitForStatus("Stopped", "00:00:30")
    }
}
else {
    Write-Host "Service $ServiceName was not found. Files will be copied only." -ForegroundColor Yellow
}

Write-Host "Copying files to $InstallDir..." -ForegroundColor Cyan

New-Item -ItemType Directory -Force $InstallDir | Out-Null

$sourceFiles = Get-ChildItem -Path $publishDir -File

foreach ($file in $sourceFiles) {
    if ($file.Name -ieq "appsettings.json" -and -not $OverwriteConfig) {
        $targetConfig = Join-Path $InstallDir "appsettings.json"

        if (Test-Path $targetConfig) {
            Write-Host "Skipping appsettings.json because target config already exists. Use -OverwriteConfig to replace it." -ForegroundColor Yellow
            continue
        }
    }

    Copy-Item -Path $file.FullName -Destination $InstallDir -Force
}

if ($null -ne $service) {
    Write-Host "Starting service $ServiceName..." -ForegroundColor Cyan
    Start-Service -Name $ServiceName

    $service = Get-Service -Name $ServiceName
    $service.WaitForStatus("Running", "00:00:30")

    Write-Host "Service $ServiceName is running." -ForegroundColor Green
}

Write-Host "Deploy completed." -ForegroundColor Green