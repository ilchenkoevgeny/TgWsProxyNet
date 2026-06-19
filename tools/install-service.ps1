param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$ServiceName = "TgWsProxy",

    [string]$DisplayName = "Telegram WS Proxy",

    [string]$Description = "Local Telegram MTProto WebSocket proxy service."
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run PowerShell as Administrator."
}

if (-not (Test-Path $ExePath)) {
    throw "Exe file not found: $ExePath"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existing) {
    Write-Host "Service '$ServiceName' already exists. Stop/delete it first if you want to recreate it."
    exit 0
}

sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "$DisplayName" | Out-Host
sc.exe description $ServiceName "$Description" | Out-Host
sc.exe start $ServiceName | Out-Host

Write-Host "Service '$ServiceName' installed and started."
