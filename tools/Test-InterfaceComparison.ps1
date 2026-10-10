param([string]$FixtureName = 'interface-comparison-native', [switch]$ShowPlayer)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskFixture = [IO.Path]::GetFullPath((Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)))
if (-not $taskFixture.StartsWith((Join-Path $taskRoot 'reports/player-fixtures/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must stay within reports/player-fixtures.' }
New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
$taskArgs = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + (Join-Path $taskFixture 'player.log') + '"'), '--draglab-verify', ('"' + $taskFixture + '"'), '--draglab-ui-verify')
# Native OS-key checks require an interactive player window; no helper window is shown.
$taskStyle = if ($ShowPlayer) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle $taskStyle -PassThru
if (-not $taskProcess.WaitForExit(120000)) { throw "UI probe still running; inspect $taskFixture/player.log (PID $($taskProcess.Id))." }
$taskReport = Join-Path $taskFixture 'verification.txt'
if (Test-Path -LiteralPath $taskReport) { Get-Content -LiteralPath $taskReport -Encoding UTF8 }
if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskReport)) { throw "UI probe failed; inspect $taskFixture/player.log" }
