param([string]$FixtureName = 'native-menu-shell', [switch]$DealershipOnly, [switch]$InspectionOnly, [switch]$TuningOnly, [switch]$ShineOnly, [switch]$PartsOnly, [switch]$PurchaseOnly, [switch]$CartOnly, [switch]$WarningsOnly, [switch]$WorkshopOnly, [switch]$BlockOnly, [switch]$MaintenanceOnly, [switch]$RebuildOnly, [switch]$PricingOnly, [switch]$FrameProbe, [switch]$ResponsivenessOnly, [switch]$AwdHardwareOnly, [switch]$PartBrandsOnly, [switch]$SqliteStateOnly, [switch]$DisposalOnly, [switch]$RaceSelectionOnly, [switch]$SetupManagementOnly, [switch]$RaceHudOnly, [switch]$ResultsOnly, [switch]$FinishPacingOnly, [switch]$OwnedFittedOnly, [switch]$TireSwapOnly, [switch]$RacePauseOnly, [switch]$NavigationOnly, [switch]$ResetRequiredOnly, [string]$PlayerPath = '', [switch]$DeterministicCapture)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'InterfaceScreenshots.ps1')
$taskFixture = [IO.Path]::GetFullPath((Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)))
if (-not $taskFixture.StartsWith((Join-Path $taskRoot 'reports/player-fixtures/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must stay inside reports/player-fixtures.' }
$taskSuite = @($PSBoundParameters.Keys | Where-Object { $_ -like '*Only' -and $PSBoundParameters[$_] } | Sort-Object)
$taskScreenshotSuite = if ($taskSuite.Count) { 'native-' + ($taskSuite -join '-') } else { 'native-menus' }
$taskScreenshots = Start-InterfaceScreenshotRun $taskRoot $taskFixture $taskScreenshotSuite
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
if ($PlayerPath) { $taskPlayer = [IO.Path]::GetFullPath($PlayerPath) }
$taskArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + (Join-Path $taskFixture 'player.log') + '"'),'--draglab-verify',('"' + $taskFixture + '"'),'--draglab-native-menu-verify')
if ($ResetRequiredOnly) {
    $taskArgs += '--draglab-reset-required-only-verify'
    if (Test-Path -LiteralPath (Join-Path $taskFixture 'save-slot-1.sqlite3')) { throw 'Reset-required verification needs a fresh disposable FixtureName.' }
    $resetFiles = @{''='isolated incompatible database'; '.bak'='isolated incompatible recovery'; '.reset-generation'='1'; '.reset-intent'='2|1'}
    foreach ($suffix in $resetFiles.Keys) {
        [IO.File]::WriteAllText((Join-Path $taskFixture ('save-slot-1.sqlite3' + $suffix)), $resetFiles[$suffix], [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $taskFixture ('reset-original' + $suffix + '.bin')), $resetFiles[$suffix], [Text.UTF8Encoding]::new($false))
    }
    [IO.File]::WriteAllText((Join-Path $taskFixture 'driving-settings.json'), '{"version":1,"driving":{"wearMode":1,"shifting":1},"difficulty":2}', [Text.UTF8Encoding]::new($false))
}
if ($NavigationOnly) { $taskArgs += '--draglab-navigation-only-verify' }
if ($RacePauseOnly) { $taskArgs += '--draglab-race-pause-only-verify' }
if ($FinishPacingOnly) { $taskArgs += '--draglab-finish-pacing-only-verify' }
if ($OwnedFittedOnly) { $taskArgs += '--draglab-owned-fitted-only-verify' }
if ($TireSwapOnly) { $taskArgs += '--draglab-tire-swap-only-verify' }
if ($ResultsOnly) { $taskArgs += '--draglab-race-results-only-verify' }
if ($RaceHudOnly) { $taskArgs += '--draglab-race-hud-only-verify' }
if ($SetupManagementOnly) { $taskArgs += '--draglab-setup-management-only-verify' }
if ($RaceSelectionOnly) { $taskArgs += '--draglab-race-selection-only-verify' }
if ($DisposalOnly) { $taskArgs += '--draglab-disposal-only-verify' }
if ($SqliteStateOnly) { $taskArgs += '--draglab-sqlite-state-only-verify' }
if ($PartBrandsOnly) { $taskArgs += '--draglab-part-brands-only-verify' }
if ($AwdHardwareOnly) { $taskArgs += '--draglab-awd-hardware-only-verify' }
if ($ResponsivenessOnly) { $taskArgs += '--draglab-responsiveness-only-verify' }
if ($FrameProbe) { $taskArgs += '--draglab-frame-probe' }
if ($PricingOnly) { $taskArgs += '--draglab-pricing-only-verify' }
if ($RebuildOnly) { $taskArgs += '--draglab-rebuild-only-verify' }
if ($MaintenanceOnly) { $taskArgs += '--draglab-maintenance-only-verify' }
if ($InspectionOnly) { $taskArgs += '--draglab-inspection-only-verify' }
if ($DealershipOnly) { $taskArgs += '--draglab-dealership-only-verify' }
if ($TuningOnly) { $taskArgs += '--draglab-tuning-only-verify' }
if ($ShineOnly) { $taskArgs += '--draglab-shine-only-verify' }
if ($PartsOnly -or $PurchaseOnly -or $CartOnly -or $WarningsOnly -or $WorkshopOnly -or $BlockOnly) { $taskArgs += '--draglab-parts-only-verify' }
if ($BlockOnly) { $taskArgs += '--draglab-block-only-verify' }
if ($WorkshopOnly) { $taskArgs += '--draglab-workshop-only-verify' }
if ($WarningsOnly) { $taskArgs += '--draglab-warnings-only-verify' }
if ($CartOnly) { $taskArgs += '--draglab-cart-only-verify' }
if ($PurchaseOnly) { $taskArgs += '--draglab-purchase-only-verify' }
# Real native events require the interactive player window, not a hidden helper.
$taskReport = Join-Path $taskFixture 'verification.txt'
if ($DeterministicCapture) { $taskArgs += '--draglab-deterministic-capture' }
$taskPassed = $false
try {
    $taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Normal -PassThru
    $taskProcess.WaitForExit()
    if (Test-Path -LiteralPath $taskReport) { Get-Content -LiteralPath $taskReport -Encoding UTF8 }
    $taskPassed = $taskProcess.ExitCode -eq 0 -and (Test-Path -LiteralPath $taskReport) -and (Get-Item -LiteralPath $taskReport).LastWriteTimeUtc -ge $taskScreenshots.Started
    if (-not $taskPassed) { throw "Native menu check failed; inspect $taskFixture/player.log" }
} finally { Complete-InterfaceScreenshotRun $taskScreenshots $taskPassed $taskReport }
