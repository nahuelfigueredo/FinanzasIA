$ErrorActionPreference = "Stop"

$root = "c:\Users\54221\FinanzasIA"
$apiProject = Join-Path $root "FinanzasIA.Api\FinanzasIA.Api.csproj"
$backofficeProject = Join-Path $root "FinanzasIA.Backoffice\FinanzasIA.Backoffice.csproj"
$runtimePath = Join-Path $root ".runtime"

if (-not (Test-Path $runtimePath))
{
    New-Item -Path $runtimePath -ItemType Directory | Out-Null
}

$apiLog = Join-Path $runtimePath "api.log"
$apiErrLog = Join-Path $runtimePath "api.err.log"
$backofficeLog = Join-Path $runtimePath "backoffice.log"
$backofficeErrLog = Join-Path $runtimePath "backoffice.err.log"

$apiCmd = "set ASPNETCORE_ENVIRONMENT=Production&&set ASPNETCORE_URLS=https://localhost:7194;http://localhost:5027&&dotnet run --no-build --project ""$apiProject"""
$backofficeCmd = "set ASPNETCORE_ENVIRONMENT=Production&&set ASPNETCORE_URLS=https://localhost:7241;http://localhost:5244&&dotnet run --no-build --project ""$backofficeProject"""

$apiProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $apiCmd -WorkingDirectory $root -PassThru -RedirectStandardOutput $apiLog -RedirectStandardError $apiErrLog
$backofficeProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $backofficeCmd -WorkingDirectory $root -PassThru -RedirectStandardOutput $backofficeLog -RedirectStandardError $backofficeErrLog

Set-Content -Path (Join-Path $runtimePath "api.pid") -Value $apiProcess.Id
Set-Content -Path (Join-Path $runtimePath "backoffice.pid") -Value $backofficeProcess.Id

Write-Host "API PID: $($apiProcess.Id)"
Write-Host "Backoffice PID: $($backofficeProcess.Id)"
Write-Host "API URL: https://localhost:7194"
Write-Host "Backoffice URL: https://localhost:7241"
Write-Host "API LOG: $apiLog"
Write-Host "BACKOFFICE LOG: $backofficeLog"
Write-Host "API ERR LOG: $apiErrLog"
Write-Host "BACKOFFICE ERR LOG: $backofficeErrLog"
