param(
    [ValidateSet('NewSupplyVerify','StartingVarietyPrepare','StartingVarietyVerify','EngineAudioVerify','SliceTwoChangedVerify','BranchAuditVerify','ComponentFaultVerify','EngineRebuildFlowVerify','SupplyPricingVerify','SqliteTimingVerify','SqliteStateVerify','PartBrandVerify','AwdHardwareVerify','ResidualCheckpointVerify','ResidualRecoveryVerify','ResidualPerformanceVerify','CartReservationVerify','DrivingCorrectionsVerify','MenuOptimizationVerify','MenuPerformanceBefore','MenuPerformanceAfter','MaintenanceReplaceVerify','OwnershipVerify','PartsMarketRegression','PartsMarketVerify','AwdGeometryVerify','PartComparisonVerify','AxleWheelVerify','StatHelpVerify','SetupPreviewVerify','ShopCacheVerify','UsedInspectionVerify','VerifyRemainingSimulation','VerifyRemaining','CareerVarietyVerify','ResetVerify','VehicleMarketVerify','VehicleVarietyVerify','VehicleVarietyPrepare','VarietyMechanicalVerify','Prepare','InventoryCommerceVerify','CartVerify','MenuPreviewVerify','RacePresentationVerify','TireSoundBaseline','TireSoundVerify','CheckpointVerify','TireFeedbackDiagnose','TireFeedbackVerify','ThermalVerify','CareerExpandedVerify','BusinessVerify','BalanceVerify','EconomyIntegrationVerify','PaidVerify','ValuationVerify','AssemblyVerify','CollectionVerify','Verify','RecoveryVerify','IntegratedVerify','BenchVerify','AxleVerify','PartsEffectsVerify','DurabilityAudit','SliceTwoAudit','MassVerify','ContactVerify','DrivingContactVerify','LaunchVerify','AwdContactVerify','FuelVerify','FuelingConversionVerify','ServiceVerify','IndividualEngineVerify','TireVerify','TireFailureVerify','CareerVerify','SceneCheck','UnitsVisualVerify','ManualGearboxVerify','PartHardwareVerify','BuildWindows','BuildAndroid','BuildPlayerOnly','Open','ValidationBatch','FluidPurchaseVerify')]
    [string]$Action = 'Prepare',
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe',
    [string]$BatchManifest = '',
    [string]$LogPath = ''
)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $taskRoot 'Game'
$taskReports = Join-Path $taskRoot 'reports'
if (-not (Test-Path -LiteralPath $EditorPath)) { throw 'Unity editor was not found. Pass -EditorPath with the installed Unity.exe path.' }
New-Item -ItemType Directory -Path $taskReports -Force | Out-Null
if ($Action -eq 'Open') {
    # User explicitly selecting Open requests a visible interactive editor.
    Start-Process -FilePath $EditorPath -ArgumentList @('-projectPath', ('"' + $taskProject + '"'))
    return
}
$taskMethod = switch ($Action) {
    'NewSupplyVerify' { 'DragLab.Editor.NewSupplyVerification.Run' }
    'StartingVarietyPrepare' { 'DragLab.Editor.StartingVarietySetup.Prepare' }
    'StartingVarietyVerify' { 'DragLab.Editor.StartingVarietyVerification.Run' }
    'BranchAuditVerify' { 'DragLab.Editor.BranchAuditVerification.Run' }
    'EngineAudioVerify' { 'DragLab.Editor.EngineSoundVerification.RunStandalone' }
    'ComponentFaultVerify' { 'DragLab.Editor.ComponentFaultVerification.Run' }
    'EngineRebuildFlowVerify' { 'DragLab.Editor.EngineRebuildFlowVerification.Run' }
    'SupplyPricingVerify' { 'DragLab.Editor.SupplyPricingVerification.Run' }
    'SliceTwoChangedVerify' { 'DragLab.Editor.SliceTwoAudit.ChangedCases' }
    'ValidationBatch' { 'DragLab.Editor.ValidationBatch.Run' }
    'SqliteTimingVerify' { 'DragLab.Editor.SqliteStateEditorVerification.Timings' }
    'SqliteStateVerify' { 'DragLab.Editor.SqliteStateEditorVerification.Run' }
    'PartBrandVerify' { 'DragLab.Editor.PartBrandVerification.Run' }
    'AwdHardwareVerify' { 'DragLab.Editor.AwdHardwareVerification.Run' }
    'ResidualCheckpointVerify' { 'DragLab.Editor.ResidualPerformanceVerification.Checkpoints' }
    'ResidualRecoveryVerify' { 'DragLab.Editor.ResidualPerformanceVerification.Recovery' }
    'ResidualPerformanceVerify' { 'DragLab.Editor.ResidualPerformanceVerification.Run' }
    'CartReservationVerify' { 'DragLab.Editor.CartReservationVerification.Run' }
    'DrivingCorrectionsVerify' { 'DragLab.Editor.DrivingCorrectionsVerification.Run' }
    'MenuOptimizationVerify' { 'DragLab.Editor.MenuOptimizationVerification.Run' }
    'MenuPerformanceBefore' { 'DragLab.Runtime.MenuResponsivenessChecks.Baseline' }
    'MenuPerformanceAfter' { 'DragLab.Runtime.MenuResponsivenessChecks.Optimized' }
    'MaintenanceReplaceVerify' { 'DragLab.Editor.MaintenanceReplacementVerification.Run' }
    'OwnershipVerify' { 'DragLab.Editor.OwnershipVerification.RunStandalone' }
    'PartsMarketRegression' { 'DragLab.Editor.PartsMarketVerification.Regressions' }
    'PartsMarketVerify' { 'DragLab.Editor.PartsMarketVerification.Run' }
    'InventoryCommerceVerify' { 'DragLab.Editor.InventoryCommerceVerification.Run' }
    'PartComparisonVerify' { 'DragLab.Editor.PartComparisonVerification.Run' }
    'AwdGeometryVerify' { 'DragLab.Editor.AwdGeometryVerification.Run' }
    'AxleWheelVerify' { 'DragLab.Editor.AxleWheelVerification.Run' }
    'StatHelpVerify' { 'DragLab.Editor.NativeStatHelpVerification.Run' }
    'SetupPreviewVerify' { 'DragLab.Editor.NativeSetupPreviewVerification.Run' }
    'ShopCacheVerify' { 'DragLab.Editor.NativeShopCacheVerification.Run' }
    'CartVerify' { 'DragLab.Editor.CartVerification.Run' }
    'MenuPreviewVerify' { 'DragLab.Editor.MenuPreviewVerification.Run' }
    'RacePresentationVerify' { 'DragLab.Editor.RacePresentationVerification.Run' }
    'TireSoundBaseline' { 'DragLab.Editor.TireSoundVerification.CaptureBaseline' }
    'TireSoundVerify' { 'DragLab.Editor.TireSoundVerification.Run' }
    'CheckpointVerify' { 'DragLab.Editor.RaceCheckpointVerification.RunStandalone' }
    'TireFeedbackVerify' { 'DragLab.Editor.TireFeedbackVerification.VerifyEffects' }
    'TireFeedbackDiagnose' { 'DragLab.Editor.TireFeedbackVerification.Diagnose' }
    'VarietyMechanicalVerify' { 'DragLab.Editor.VarietyMechanicalVerification.RunStandalone' }
    'VehicleVarietyPrepare' { 'DragLab.Editor.VehicleVarietySetup.Prepare' }
    'VehicleVarietyVerify' { 'DragLab.Editor.VehicleVarietyVerification.RunStandalone' }
    'UsedInspectionVerify' { 'DragLab.Editor.UsedVehicleInspectionVerification.RunStandalone' }
    'CareerVarietyVerify' { 'DragLab.Editor.CareerVarietyVerification.RunStandalone' }
    'ResetVerify' { 'DragLab.Editor.GameResetVerification.RunStandalone' }
    'VehicleMarketVerify' { 'DragLab.Editor.VehicleMarketVerification.RunStandalone' }
    'Prepare' { 'DragLab.Editor.ProjectSetup.Prepare' }
    'AssemblyVerify' { 'DragLab.Editor.AssemblySetupVerification.RunStandalone' }
    'PaidVerify' { 'DragLab.Editor.PaidOperationsVerification.RunStandalone' }
    'ValuationVerify' { 'DragLab.Editor.ValuationVerification.RunStandalone' }
    'CollectionVerify' { 'DragLab.Editor.CollectionVerification.RunStandalone' }
    'Verify' { 'DragLab.Editor.Verification.Run' }
    'VerifyRemaining' { 'DragLab.Editor.Verification.RunRemaining' }
    'VerifyRemainingSimulation' { 'DragLab.Editor.Verification.RunRemainingSimulation' }
    'ThermalVerify' { 'DragLab.Editor.RaceThermalVerification.RunStandalone' }
    'IntegratedVerify' { 'DragLab.Editor.IntegratedToolsVerification.RunStandalone' }
    'RecoveryVerify' { 'DragLab.Editor.MechanicalRecoveryVerification.RunStandalone' }
    'BenchVerify' { 'DragLab.Editor.SharedMechanicsVerification.RunStandalone' }
    'PartsEffectsVerify' { 'DragLab.Editor.PartEffectsVerification.RunStandalone' }
    'AxleVerify' { 'DragLab.Editor.AxleFailureVerification.RunStandalone' }
    'DurabilityAudit' { 'DragLab.Editor.StockDurabilityVerification.Audit' }
    'SliceTwoAudit' { 'DragLab.Editor.SliceTwoAudit.Run' }
    'EconomyIntegrationVerify' { 'DragLab.Editor.EconomyIntegrationVerification.RunStandalone' }
    'BalanceVerify' { 'DragLab.Editor.EconomyBalanceVerification.RunStandalone' }
    'BusinessVerify' { 'DragLab.Editor.GarageBusinessVerification.RunStandalone' }
    'CareerExpandedVerify' { 'DragLab.Editor.CareerExpansionVerification.RunStandalone' }
    'CareerVerify' { 'DragLab.Editor.Verification.VerifyCareerStandalone' }
    'TireFailureVerify' { 'DragLab.Editor.TireFailureVerification.RunStandalone' }
    'TireVerify' { 'DragLab.Editor.TireVerification.RunStandalone' }
    'FluidPurchaseVerify' { 'DragLab.Editor.FluidPurchaseVerification.Run' }
    'FuelVerify' { 'DragLab.Editor.FuelVerification.RunStandalone' }
    'ServiceVerify' { 'DragLab.Editor.ServiceFluidsVerification.RunStandalone' }
    'IndividualEngineVerify' { 'DragLab.Editor.IndividualEngineVerification.RunStandalone' }
    'FuelingConversionVerify' { 'DragLab.Editor.FuelingConversionVerification.RunStandalone' }
    'AwdContactVerify' { 'DragLab.Editor.AwdContactVerification.RunStandalone' }
    'LaunchVerify' { 'DragLab.Editor.DrivingContactVerification.LaunchProbe' }
    'DrivingContactVerify' { 'DragLab.Editor.DrivingContactVerification.RunStandalone' }
    'ContactVerify' { 'DragLab.Editor.WheelContactVerification.RunStandalone' }
    'MassVerify' { 'DragLab.Editor.MassLoadVerification.RunStandalone' }
    'ManualGearboxVerify' { 'DragLab.Editor.ManualGearboxVerification.RunStandalone' }
    'PartHardwareVerify' { 'DragLab.Editor.PartHardwareVerification.RunStandalone' }
    'UnitsVisualVerify' { 'DragLab.Editor.UnitsVisualVerification.RunStandalone' }
    'SceneCheck' { 'DragLab.Editor.SceneVerification.Run' }
    'BuildAndroid' { 'DragLab.Editor.PlayerBuild.BuildAndroid' }
    'BuildWindows' { 'DragLab.Editor.PlayerBuild.BuildWindows' }
    'BuildPlayerOnly' { 'DragLab.Editor.PlayerBuild.BuildPlayerOnly' }
}
$taskLog = if ($LogPath) { [System.IO.Path]::GetFullPath($LogPath) } else { Join-Path $taskReports ('unity-' + $Action.ToLowerInvariant() + '.log') }
if ($Action -eq 'ValidationBatch' -and (-not $BatchManifest -or -not (Test-Path -LiteralPath $BatchManifest))) {
    throw 'ValidationBatch requires an existing -BatchManifest containing selected checks.'
}
$taskArgs = @('-batchmode','-nographics','-quit','-projectPath', ('"' + $taskProject + '"'),'-executeMethod',$taskMethod,'-logFile',('"' + $taskLog + '"'))
if ($Action -eq 'BuildAndroid') { $taskArgs += @('-buildTarget','Android') }
if ($Action -in @('BuildWindows','BuildPlayerOnly')) { $taskArgs += @('-buildTarget','Win64') }
# Only the child Unity process inherits this manifest path.
$previousManifest = [Environment]::GetEnvironmentVariable('DRAGLAB_VALIDATION_MANIFEST', 'Process')
try {
    if ($Action -eq 'ValidationBatch') {
        [Environment]::SetEnvironmentVariable('DRAGLAB_VALIDATION_MANIFEST', [System.IO.Path]::GetFullPath($BatchManifest), 'Process')
    } else {
        [Environment]::SetEnvironmentVariable('DRAGLAB_VALIDATION_MANIFEST', $null, 'Process')
    }
    # Wait for the actual Unity Editor, not only its detached console wrapper.
    $taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
} finally {
    [Environment]::SetEnvironmentVariable('DRAGLAB_VALIDATION_MANIFEST', $previousManifest, 'Process')
}
if ($taskProcess.ExitCode -ne 0) { throw "Unity failed ($($taskProcess.ExitCode)). Read $taskLog" }
Write-Output "Unity $Action finished. Log: $taskLog"
