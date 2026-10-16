param(
    [Parameter(Mandatory)][ValidateSet('before','after')][string]$Phase,
    [string[]]$Suites = @('Comparison','Shell','Parts','Purchase','Cart','Warnings','Workshop','Block','Maintenance','Rebuild','Disposal','RaceSelection','SetupManagement','RaceHud','EngineBench'),
    [string]$FixtureSuffix = '',
    [string]$ComparisonPlayerPath = '',
    [string]$PlayerPath = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InterfaceScreenshots.ps1')
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskCaptureRoot = Join-Path $taskRoot 'reports/render-pipeline-20261016'
$taskRunStamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
if (!$FixtureSuffix) { $FixtureSuffix = $taskRunStamp }
$taskDestination = Join-Path $taskCaptureRoot $Phase
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
foreach ($taskSuite in $Suites) {
    if ($taskSuite -notin @('Comparison','Shell','Parts','Purchase','Cart','Warnings','Workshop','Block','Maintenance','Rebuild','Disposal','RaceSelection','SetupManagement','RaceHud','EngineBench','DrivingBench')) { throw "Unknown interface suite: $taskSuite" }
    $taskFixtureName = 'render-' + $Phase + '-' + $taskSuite.ToLowerInvariant()
    if ($FixtureSuffix) { $taskFixtureName += '-' + $FixtureSuffix }
    $taskFixture = Join-Path $taskRoot ('reports/player-fixtures/' + $taskFixtureName)
    $taskOutput = Join-Path $taskDestination $taskSuite
    if ((Test-Path -LiteralPath $taskOutput) -and @(Get-ChildItem -LiteralPath $taskOutput -Force).Count) {
        $taskArchive = [IO.Path]::GetFullPath((Join-Path $taskCaptureRoot ('attempts/' + $Phase + '-' + $taskSuite + '-' + $taskRunStamp)))
        $taskResolvedOutput = [IO.Path]::GetFullPath($taskOutput)
        $taskPrefix = [IO.Path]::GetFullPath($taskCaptureRoot) + [IO.Path]::DirectorySeparatorChar
        if (!$taskResolvedOutput.StartsWith($taskPrefix,[StringComparison]::OrdinalIgnoreCase) -or !$taskArchive.StartsWith($taskPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Capture archive escaped its workspace.' }
        if ((Get-Item -LiteralPath $taskOutput).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Capture folder must not be a junction or symlink.' }
        New-Item -ItemType Directory -Path (Split-Path $taskArchive) -Force | Out-Null
        Move-Item -LiteralPath $taskResolvedOutput -Destination $taskArchive
    }
    New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
    # Before/after use the same fresh initialization, never the real player save.
    # Retain failed attempts rather than deleting fixtures or earlier evidence.
    if ((Test-Path (Join-Path $taskFixture 'profile-v1.json')) -or (Test-Path (Join-Path $taskFixture 'save-slot-1.sqlite3'))) { throw "Use a fresh fixture: $taskFixture" }
    New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null
    $taskSeed = if ($taskSuite -in @('Parts','Purchase','Workshop','Block')) { 'stocked.sqlite3' } else { 'save-slot-1.sqlite3' }
    Copy-Item -LiteralPath (Join-Path $taskRoot ('reports/render-pipeline-20261016/seed/' + $taskSeed)) -Destination (Join-Path $taskFixture 'save-slot-1.sqlite3')
    Write-Output "Starting $Phase / $taskSuite"
    $taskLog = Join-Path $taskOutput 'capture.log'
    try {
        if ($taskSuite -eq 'Comparison') {
            if (!$ComparisonPlayerPath -and $PlayerPath) { $ComparisonPlayerPath = $PlayerPath }
            & (Join-Path $PSScriptRoot 'Test-InterfaceComparison.ps1') -FixtureName $taskFixtureName -ShowPlayer -CaptureAll -PlayerPath $ComparisonPlayerPath *> $taskLog
        } elseif ($taskSuite -in @('EngineBench','DrivingBench')) {
            $taskOptions = @{FixtureName=$taskFixtureName;ShowPlayer=$true}
            if ($PlayerPath) { $taskOptions.PlayerPath = $PlayerPath }
            $taskOptions[$taskSuite+'Only']=$true
            & (Join-Path $PSScriptRoot 'Test-WindowsSlice.ps1') @taskOptions *> $taskLog
        } else {
            $taskOptions = @{FixtureName=$taskFixtureName}
            if ($PlayerPath) { $taskOptions.PlayerPath = $PlayerPath }
            if ($taskSuite -ne 'Shell') { $taskOptions[$taskSuite+'Only']=$true }
            & (Join-Path $PSScriptRoot 'Test-NativeMenu.ps1') @taskOptions *> $taskLog
        }
        foreach ($taskReportName in @('verification.txt','runtime-verification.txt')) {
            $taskReportPath = Join-Path $taskFixture $taskReportName
            if (Test-Path -LiteralPath $taskReportPath) {
                $taskReportText = Get-Content -LiteralPath $taskReportPath -Encoding UTF8 -Raw
                if ($taskReportText -match '(?im)^FAIL:|runtime errors: [1-9]') { throw "Capture report contains failed checks/runtime errors: $taskReportPath" }
            }
        }
        'PASS' | Set-Content (Join-Path $taskOutput 'status.txt') -Encoding UTF8
    } catch {
        $_ | Out-File $taskLog -Append -Encoding UTF8
        'FAIL' | Set-Content (Join-Path $taskOutput 'status.txt') -Encoding UTF8
    } finally {
        # Copy to a permanent phase folder: normal test runs replace their gallery images.
        if (Test-Path $taskFixture) {
            foreach ($taskFile in Get-ChildItem -LiteralPath $taskFixture -File) {
                if ($taskFile.Extension -eq '.png') {
                    $taskImageFolder = if (Test-InterfaceScreenshotResolution $taskFile) { Join-Path $taskOutput 'resolution-tests' } else { $taskOutput }
                    New-Item -ItemType Directory -Path $taskImageFolder -Force | Out-Null
                    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskImageFolder -Force
                } elseif ($taskFile.Name -in @('verification.txt','runtime-verification.txt','player.log')) {
                    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskOutput -Force
                }
            }
        }
    }
    $taskCount = @(Get-ChildItem -LiteralPath $taskOutput -Filter '*.png' -Recurse).Count
    Write-Output "$Phase / $taskSuite : $(Get-Content (Join-Path $taskOutput 'status.txt')) / $taskCount images"
}
$taskFailed = @(Get-ChildItem -LiteralPath $taskDestination -Directory | Where-Object { (Get-Content (Join-Path $_.FullName 'status.txt') -ErrorAction SilentlyContinue) -eq 'FAIL' })
if ($taskFailed.Count) { throw ('Capture suites failed: ' + ($taskFailed.Name -join ', ')) }
