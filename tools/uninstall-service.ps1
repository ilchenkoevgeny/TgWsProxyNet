param(
    [string]$ServiceName = "TgWsProxy"
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run PowerShell as Administrator."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if (-not $service) {
    Write-Host "Service '$ServiceName' not found."
    exit 0
}

if ($service.Status -ne 'Stopped') {
    sc.exe stop $ServiceName | Out-Host
    Start-Sleep -Seconds 2
}

sc.exe delete $ServiceName | Out-Host
Write-Host "Service '$ServiceName' deleted."
