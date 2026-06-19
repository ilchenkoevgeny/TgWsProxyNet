param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "src\TgWsProxy\TgWsProxy.csproj"
$publishDir = Join-Path $root "artifacts\publish\$Runtime"
$installerScript = Join-Path $root "installer\TgWsProxy.iss"

Write-Host "Publishing TgWsProxy..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue

if ($null -eq $iscc) {
    $possiblePaths = @(
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $iscc = Get-Item $path
            break
        }
    }
}

if ($null -eq $iscc) {
    $isccSearchResult = Get-ChildItem `
        -Path "C:\Program Files", "C:\Program Files (x86)" `
        -Recurse `
        -Filter "ISCC.exe" `
        -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($null -ne $isccSearchResult) {
        $iscc = $isccSearchResult
    }
}

if ($null -eq $iscc) {
    throw "ISCC.exe was not found. Install Inno Setup or add ISCC.exe to PATH."
}

Write-Host "Using Inno Setup compiler:" -ForegroundColor Cyan
Write-Host $iscc.FullName -ForegroundColor Gray

Write-Host "Building installer..." -ForegroundColor Cyan

& $iscc.FullName "/DMyAppVersion=$Version" $installerScript

$installerDir = Join-Path $root "artifacts\installer"

Write-Host "Installer output:" -ForegroundColor Green

Get-ChildItem $installerDir -Filter "*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 5 |
    Format-Table FullName, Length, LastWriteTime