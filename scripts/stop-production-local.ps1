$ErrorActionPreference = "Stop"

$runtimePath = "c:\Users\54221\FinanzasIA\.runtime"
$pidFiles = @(
    (Join-Path $runtimePath "api.pid"),
    (Join-Path $runtimePath "backoffice.pid")
)

foreach ($pidFile in $pidFiles)
{
    if (-not (Test-Path $pidFile))
    {
        continue
    }

    $processIdText = Get-Content $pidFile
    try
    {
        $pidValue = [int]$processIdText
        $process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
        if ($null -ne $process)
        {
            Stop-Process -Id $process.Id
            Write-Host "Stopped process $($process.Id)"
        }
    }
    catch
    {
    }

    Remove-Item $pidFile -Force
}
