param(
    [ValidateSet('ResetVerify','VehicleMarketVerify','VehicleVarietyVerify','VehicleVarietyPrepare','VarietyMechanicalVerify','Prepare','InventoryCommerceVerify','CartVerify','MenuPreviewVerify','RacePresentationVerify','TireSoundBaseline','TireSoundVerify','CheckpointVerify','TireFeedbackDiagnose','TireFeedbackVerify','ThermalVerify','CareerExpandedVerify','BusinessVerify','BalanceVerify','EconomyIntegrationVerify','PaidVerify','ValuationVerify','AssemblyVerify','CollectionVerify','Verify','RecoveryVerify','IntegratedVerify','BenchVerify','AxleVerify','PartsEffectsVerify','DurabilityAudit','SliceTwoAudit','MassVerify','ContactVerify','DrivingContactVerify','LaunchVerify','AwdContactVerify','FuelVerify','FuelingConversionVerify','ServiceVerify','IndividualEngineVerify','TireVerify','TireFailureVerify','CareerVerify','SceneCheck','UnitsVisualVerify','ManualGearboxVerify','PartHardwareVerify','BuildWindows','BuildPlayerOnly','Open')]
    [string]$Action = 'Prepare',
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
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
    'InventoryCommerceVerify' { 'DragLab.Editor.InventoryCommerceVerification.Run' }
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
    'ResetVerify' { 'DragLab.Editor.GameResetVerification.RunStandalone' }
    'VehicleMarketVerify' { 'DragLab.Editor.VehicleMarketVerification.RunStandalone' }
    'Prepare' { 'DragLab.Editor.ProjectSetup.Prepare' }
    'AssemblyVerify' { 'DragLab.Editor.AssemblySetupVerification.RunStandalone' }
    'PaidVerify' { 'DragLab.Editor.PaidOperationsVerification.RunStandalone' }
    'ValuationVerify' { 'DragLab.Editor.ValuationVerification.RunStandalone' }
    'CollectionVerify' { 'DragLab.Editor.CollectionVerification.RunStandalone' }
    'Verify' { 'DragLab.Editor.Verification.Run' }
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
    'BuildWindows' { 'DragLab.Editor.PlayerBuild.BuildWindows' }
    'BuildPlayerOnly' { 'DragLab.Editor.PlayerBuild.BuildPlayerOnly' }
}
$taskLog = Join-Path $taskReports ('unity-' + $Action.ToLowerInvariant() + '.log')
$taskArgs = @('-batchmode','-nographics','-quit','-projectPath', ('"' + $taskProject + '"'),'-executeMethod',$taskMethod,'-logFile',('"' + $taskLog + '"'))
# Unity's GUI executable detaches when invoked directly; waiting is essential.
# Wait for the editor itself; successful builds can leave compiler/license children alive.
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { throw "Unity failed ($($taskProcess.ExitCode)). Read $taskLog" }
Write-Output "Unity $Action finished. Log: $taskLog"
