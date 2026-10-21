#Requires -Version 7.0
<#
.SYNOPSIS
Build and run the multiplayer security tests, including real SignalR connections.
.EXAMPLE
pwsh -File ./test-multiplayer.ps1
.EXAMPLE
pwsh -File ./test-multiplayer.ps1 -Repeat 10
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 20)]
    [int]$Repeat = 1,

    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$runDirectory = $null
$totalPassed = 0
$totalTests = 0
$sdkDownload = 'https://dotnet.microsoft.com/download/dotnet/9.0'

Push-Location -LiteralPath $PSScriptRoot
try {
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $dotnet) {
        throw "The .NET SDK was not found. Install the .NET 9 SDK from $sdkDownload, reopen PowerShell, and try again."
    }

    $sdks = @(& $dotnet.Source --list-sdks)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect installed .NET SDKs. Check that dotnet runs correctly.' }
    $supportedSdks = @($sdks | Where-Object { $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 9 })
    if ($supportedSdks.Count -eq 0) {
        throw "These tests need the .NET 9 SDK or newer; the runtime alone cannot build tests. Install the .NET 9 SDK from $sdkDownload and try again."
    }

    $sdkVersion = & $dotnet.Source --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt 9) {
        throw 'This repository is selecting an older or unavailable SDK. Select an installed .NET SDK version 9 or newer and try again.'
    }
    $runtimes = @(& $dotnet.Source --list-runtimes)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect installed .NET runtimes.' }
    foreach ($runtime in @('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App')) {
        if (-not ($runtimes | Where-Object { $_ -match "^$([regex]::Escape($runtime)) 9\." })) {
            throw "The .NET 9 runtime ($runtime) is missing. Installing the .NET 9 SDK from $sdkDownload includes the runtimes required by this net9.0 project."
        }
    }

    $project = Join-Path $PSScriptRoot 'tests/TicTacToe.WebMultiplayer.Tests/TicTacToe.WebMultiplayer.Tests.csproj'
    if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $ResultsDirectory = Join-Path $PSScriptRoot 'artifacts/test-results'
    }
    $resultsRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $PSScriptRoot)
    $runName = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path $resultsRoot $runName
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

    Write-Host "Using .NET SDK $sdkVersion. Restoring packages and building multiplayer tests..."
    & $dotnet.Source build $project --configuration Release --disable-build-servers --nologo --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Restore or build failed. See the output above; tests have not run.' }

    for ($iteration = 1; $iteration -le $Repeat; $iteration++) {
        Write-Host "Running multiplayer security tests ($iteration of $Repeat)..."
        & $dotnet.Source test $project --configuration Release --no-build --no-restore --nologo --verbosity minimal -m:1 `
            --logger "trx;LogFileName=run-$iteration.trx" --results-directory $runDirectory
        if ($LASTEXITCODE -ne 0) { throw "Test run $iteration of $Repeat failed. See the failed tests above." }
        $reportPath = Join-Path $runDirectory "run-$iteration.trx"
        if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
            throw "Test run $iteration did not produce its results report. Check the test runner output above."
        }
        [xml]$report = Get-Content -LiteralPath $reportPath -Raw
        if ([int]$report.TestRun.ResultSummary.Counters.executed -le 0) {
            throw "Test run $iteration did not execute any tests. Check test discovery before relying on these results."
        }
        $counters = $report.TestRun.ResultSummary.Counters
        $totalPassed += [int]$counters.passed
        $totalTests += [int]$counters.total
        Write-Host "Run $iteration passed: $($counters.passed)/$($counters.total) tests."
    }

    Write-Host "PASS: $totalPassed/$totalTests multiplayer tests passed across all $Repeat run(s)." -ForegroundColor Green
    Write-Host "Results: $runDirectory"
}
catch {
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($null -ne $runDirectory) { Write-Host "Results: $runDirectory" }
    exit 1
}
finally {
    Pop-Location
}
exit 0
