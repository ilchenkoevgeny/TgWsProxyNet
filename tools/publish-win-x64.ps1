param(
    [string]$ProjectPath = "src\TgWsProxy\TgWsProxy.csproj"
)

$ErrorActionPreference = "Stop"

dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true
