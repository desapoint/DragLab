param([string]$FixtureName = (Get-Date -Format 'yyyyMMdd-HHmmss'), [switch]$TireFeedbackOnly, [switch]$PacingOnly, [switch]$FrameProbe, [switch]$CaptureOnly, [switch]$ThermalOnly, [switch]$ExpandedCareerOnly, [switch]$BalanceOnly, [switch]$BusinessOnly, [switch]$BusinessResume, [switch]$CollectionOnly, [switch]$ValuationOnly, [switch]$PaidOnly, [switch]$PaidResume, [switch]$AssemblySetupsOnly, [switch]$KeyboardOnly, [switch]$InductionOnly, [switch]$PowertrainOnly, [switch]$EngineBenchOnly, [switch]$MusicOnly, [switch]$AudioSettingsOnly, [switch]$AudioLevelsOnly, [switch]$ShiftHoldOnly, [switch]$BenchConfigOnly, [switch]$PartsOnly, [switch]$DrivingBenchOnly, [switch]$DrivingFailureOnly, [switch]$ResetOnly, [switch]$InspectionOnly, [switch]$DurabilityOnly, [switch]$AxleFailuresOnly, [switch]$PartEffectsOnly, [switch]$TuningGesturesOnly, [switch]$MaintenanceOnly, [switch]$StatsSummaryOnly, [switch]$ShowPlayer)
$ErrorActionPreference = 'Stop'
if ($BusinessResume -and -not $BusinessOnly) { throw 'BusinessResume requires BusinessOnly and the original FixtureName.' }
if ($PaidResume -and -not $PaidOnly) { throw 'PaidResume requires PaidOnly and the same FixtureName used for the paid first process.' }
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build the Windows player first with Invoke-Unity.ps1 -Action BuildWindows.' }
$taskFixtures = Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)
New-Item -ItemType Directory -Path $taskFixtures -Force | Out-Null
$taskFixtures = (Resolve-Path -LiteralPath $taskFixtures).Path
$taskLog = Join-Path $taskFixtures 'player.log'
Write-Output "Player verification fixtures: $taskFixtures"
$taskArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + $taskLog + '"'),'--draglab-verify',('"' + $taskFixtures + '"'))
if ($PacingOnly) { $taskArgs += '--draglab-pacing-only' }
if ($EngineBenchOnly -or $MusicOnly -or $AudioSettingsOnly -or $AudioLevelsOnly -or $ShiftHoldOnly -or $BenchConfigOnly) {
    $taskArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + $taskLog + '"'),'--draglab-engine-bench','--draglab-bench-verify',('"' + $taskFixtures + '"'))
    if ($KeyboardOnly) { $taskArgs += '--draglab-bench-keyboard' }
    if ($MusicOnly) { $taskArgs += '--draglab-bench-music' }
    if ($AudioSettingsOnly) { $taskArgs += '--draglab-audio-settings' }
    if ($AudioLevelsOnly) { $taskArgs += '--draglab-audio-levels' }
    if ($ShiftHoldOnly) { $taskArgs += '--draglab-bench-shift-hold' }
    if ($BenchConfigOnly) { $taskArgs += '--draglab-bench-config' }
}
if ($ThermalOnly) { $taskArgs += '--draglab-thermal-only' }
if ($CaptureOnly) { $taskArgs += '--draglab-capture-only' }
if ($DrivingBenchOnly -or $DrivingFailureOnly) { $taskArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + $taskLog + '"'),'--draglab-driving-bench','--draglab-driving-verify',('"' + $taskFixtures + '"')) }
if ($DrivingFailureOnly) { $taskArgs += '--draglab-driving-failure' }
if ($FrameProbe) { $taskArgs += '--draglab-frame-probe' }
if ($TireFeedbackOnly) { $taskArgs += '--draglab-tire-feedback-only' }
if ($AssemblySetupsOnly) { $taskArgs += '--draglab-assembly-setups-only' }
if ($BalanceOnly) { $taskArgs += '--draglab-balance-only' }
if ($BusinessOnly) { $taskArgs += '--draglab-business-only' }
if ($BusinessResume) { $taskArgs += '--draglab-business-resume' }
if ($ExpandedCareerOnly) { $taskArgs += '--draglab-expanded-career-only' }
if ($PaidOnly) { $taskArgs += '--draglab-paid-only' }
if ($PaidResume) { $taskArgs += '--draglab-paid-resume' }
if ($ValuationOnly) { $taskArgs += '--draglab-valuation-only' }
if ($CollectionOnly) { $taskArgs += '--draglab-collection-only' }
if ($PartsOnly) { $taskArgs += '--draglab-parts-only' }
if ($ResetOnly) { $taskArgs += '--draglab-reset-only' }
if ($InspectionOnly) { $taskArgs += '--draglab-inspection-only' }
if ($DurabilityOnly) { $taskArgs += '--draglab-durability-only' }
if ($StatsSummaryOnly) { $taskArgs += '--draglab-stats-summary-only' }
if ($MaintenanceOnly) { $taskArgs += '--draglab-maintenance-only' }
if ($TuningGesturesOnly) { $taskArgs += '--draglab-tuning-gestures-only' }
if ($PartEffectsOnly) { $taskArgs += '--draglab-part-effects-only' }
if ($AxleFailuresOnly) { $taskArgs += '--draglab-axle-failures-only' }
if ($KeyboardOnly) { $taskArgs += '--draglab-keyboard-only' }
if ($InductionOnly) { $taskArgs += '--draglab-induction-only' }
if ($PowertrainOnly) { $taskArgs += '--draglab-powertrain-only' }
# ShowPlayer opens the interactive game for a visible rendering review.
$taskStyle = if ($ShowPlayer) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle $taskStyle -PassThru -Wait
if ($taskProcess.ExitCode -ne 0) { throw "Player verification failed ($($taskProcess.ExitCode)). Read $taskLog" }
$taskReport = Join-Path $taskFixtures 'runtime-verification.txt'
if (-not (Test-Path -LiteralPath $taskReport)) { throw "Player exited without a verification report. Read $taskLog" }
Get-Content -LiteralPath $taskReport
