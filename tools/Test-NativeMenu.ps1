param([string]$FixtureName = 'native-menu-shell', [switch]$DealershipOnly, [switch]$InspectionOnly, [switch]$TuningOnly, [switch]$ShineOnly, [switch]$PartsOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskFixture = [IO.Path]::GetFullPath((Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)))
if (-not $taskFixture.StartsWith((Join-Path $taskRoot 'reports/player-fixtures/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must stay inside reports/player-fixtures.' }
New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
$taskArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + (Join-Path $taskFixture 'player.log') + '"'),'--draglab-verify',('"' + $taskFixture + '"'),'--draglab-native-menu-verify')
if ($InspectionOnly) { $taskArgs += '--draglab-inspection-only-verify' }
if ($DealershipOnly) { $taskArgs += '--draglab-dealership-only-verify' }
if ($TuningOnly) { $taskArgs += '--draglab-tuning-only-verify' }
if ($ShineOnly) { $taskArgs += '--draglab-shine-only-verify' }
if ($PartsOnly) { $taskArgs += '--draglab-parts-only-verify' }
# Real native events require the interactive player window, not a hidden helper.
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Normal -PassThru
$taskProcess.WaitForExit()
$taskReport = Join-Path $taskFixture 'verification.txt'
if (Test-Path -LiteralPath $taskReport) { Get-Content -LiteralPath $taskReport -Encoding UTF8 }
if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskReport)) { throw "Native menu check failed; inspect $taskFixture/player.log" }
