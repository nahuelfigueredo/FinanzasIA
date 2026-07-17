$ErrorActionPreference = "Stop"

$root = "c:\Users\54221\FinanzasIA"
$publishRoot = Join-Path $root "publish"
$apiOutput = Join-Path $publishRoot "api"
$backofficeOutput = Join-Path $publishRoot "backoffice"

if (-not (Test-Path $publishRoot))
{
    New-Item -Path $publishRoot -ItemType Directory | Out-Null
}

dotnet publish (Join-Path $root "FinanzasIA.Api\FinanzasIA.Api.csproj") -c Release -o $apiOutput
dotnet publish (Join-Path $root "FinanzasIA.Backoffice\FinanzasIA.Backoffice.csproj") -c Release -o $backofficeOutput

Write-Host "Published API to: $apiOutput"
Write-Host "Published Backoffice to: $backofficeOutput"
