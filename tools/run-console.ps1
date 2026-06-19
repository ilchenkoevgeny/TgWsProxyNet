param(
    [string]$ProjectPath = "src\TgWsProxy\TgWsProxy.csproj"
)

$ErrorActionPreference = "Stop"

dotnet run --project $ProjectPath
