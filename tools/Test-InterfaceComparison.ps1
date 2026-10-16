param([string]$FixtureName = 'interface-comparison-native', [switch]$ShowPlayer, [switch]$CaptureAll, [string]$PlayerPath = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'InterfaceScreenshots.ps1')
$taskFixture = [IO.Path]::GetFullPath((Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)))
if (-not $taskFixture.StartsWith((Join-Path $taskRoot 'reports/player-fixtures/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must stay within reports/player-fixtures.' }
$taskScreenshots = Start-InterfaceScreenshotRun $taskRoot $taskFixture 'interface-comparison'
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
if ($PlayerPath) { $taskPlayer = [IO.Path]::GetFullPath($PlayerPath) }
$taskArgs = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + (Join-Path $taskFixture 'player.log') + '"'), '--draglab-verify', ('"' + $taskFixture + '"'), '--draglab-ui-verify')
if ($CaptureAll) { $taskArgs += '--draglab-capture-all-interfaces' }
# Native OS-key checks require an interactive player window; no helper window is shown.
$taskStyle = if ($ShowPlayer) { 'Normal' } else { 'Hidden' }
$taskPassed = $false
$taskReport = Join-Path $taskFixture 'verification.txt'
try {
    $taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle $taskStyle -PassThru
    if (-not $taskProcess.WaitForExit(120000)) { throw "UI probe still running; inspect $taskFixture/player.log (PID $($taskProcess.Id))." }
    if (Test-Path -LiteralPath $taskReport) { Get-Content -LiteralPath $taskReport -Encoding UTF8 }
    $taskPassed = $taskProcess.ExitCode -eq 0 -and (Test-Path -LiteralPath $taskReport) -and (Get-Item -LiteralPath $taskReport).LastWriteTimeUtc -ge $taskScreenshots.Started
    if (-not $taskPassed) { throw "UI probe failed; inspect $taskFixture/player.log" }
} finally { Complete-InterfaceScreenshotRun $taskScreenshots $taskPassed $taskReport }
