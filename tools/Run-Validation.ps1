<#
.SYNOPSIS
Runs curated DragLab Unity checks with failure details and CSV results.
Only Smoke, Functional and Extended are included in All.
Quarantined targets are older schema/composite suites needing separate review.
Diagnostics are measurements, not pass/fail feature gates.
#>
param(
    [ValidateSet('Smoke','Functional','Extended','All','Quarantined','Diagnostics')]
    [string]$Group = 'Smoke',
    [string]$StartAt = '',
    [switch]$ContinueOnFailure,
    [switch]$ListOnly
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$invoke = Join-Path $PSScriptRoot 'Invoke-Unity.ps1'
$reportRoot = Join-Path $root 'reports'

$suite = @{
    Smoke = @(
        'TireVerify',
        'TireFailureVerify',
        'AxleWheelVerify',
        'VehicleMarketVerify',
        'CareerVarietyVerify',
        'CheckpointVerify',
        'ResetVerify',
        'SceneCheck',
        'SqliteStateVerify'
    )
    Functional = @(
        'CareerVerify',
        'CareerExpandedVerify',
        'PartsMarketVerify',
        'PartsMarketRegression',
        'MaintenanceReplaceVerify',
        'CartVerify',
        'CartReservationVerify',
        'InventoryCommerceVerify',
        'UsedInspectionVerify',
        'ValuationVerify',
        'ServiceVerify',
        'IndividualEngineVerify',
        'FuelingConversionVerify',
        'ThermalVerify',
        'AxleVerify',
        'AwdHardwareVerify',
        'AwdGeometryVerify',
        'AwdContactVerify',
        'DrivingContactVerify',
        'ContactVerify',
        'LaunchVerify',
        'MassVerify',
        'ManualGearboxVerify',
        'PartHardwareVerify',
        'PartsEffectsVerify',
        'CollectionVerify',
        'BusinessVerify',
        'BalanceVerify',
        'BenchVerify',
        'TireFeedbackVerify',
        'TireSoundVerify',
        'RacePresentationVerify',
        'MenuPreviewVerify',
        'SetupPreviewVerify',
        'ShopCacheVerify',
        'StatHelpVerify',
        'PartComparisonVerify',
        'PartBrandVerify',
        'VehicleVarietyVerify',
        'VarietyMechanicalVerify',
        'RecoveryVerify',
        'FuelVerify'
    )
    Extended = @(
        'ResidualCheckpointVerify',
        'ResidualRecoveryVerify',
        'DrivingCorrectionsVerify',
        'DurabilityAudit',
        'SliceTwoAudit',
        'UnitsVisualVerify',
        'IntegratedVerify'
    )
    Quarantined = @(
        'OwnershipVerify',
        'AssemblyVerify',
        'PaidVerify',
        'EconomyIntegrationVerify',
        'ResidualPerformanceVerify',
        'Verify',
        'VerifyRemaining',
        'VerifyRemainingSimulation'
    )
    Diagnostics = @(
        'TireFeedbackDiagnose',
        'SqliteTimingVerify',
        'MenuOptimizationVerify',
        'MenuPerformanceBefore',
        'MenuPerformanceAfter'
    )
}
$tests = switch ($Group) {
    'All' { @($suite.Smoke) + @($suite.Functional) + @($suite.Extended) }
    default { @($suite[$Group]) }
}
if ($StartAt) {
    $position = [Array]::IndexOf([string[]]$tests, $StartAt)
    if ($position -lt 0) { throw "Action '$StartAt' is not part of the $Group group." }
    $tests = @($tests | Select-Object -Skip $position)
}
if ($ListOnly) {
    Write-Host "$Group: $($tests.Count) actions"
    $tests | ForEach-Object { Write-Output $_ }
    return
}

$editors = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($editors.Count -gt 0) {
    throw 'Close all Unity Editor/batch processes before running validation. No processes were terminated.'
}
if ($Group -eq 'Quarantined') {
    Write-Warning 'These checks still include obsolete save-migration or repeated composite coverage. Results are not a current-release gate.'
}
if ($Group -eq 'Diagnostics') {
    Write-Warning 'These are measurement/diagnosis actions; a completed run is not equivalent to a verification PASS.'
}

New-Item -Path $reportRoot -ItemType Directory -Force | Out-Null
$start = Get-Date -Format 'yyyyMMdd-HHmmss'
$csv = Join-Path $reportRoot ('validation-' + $Group.ToLowerInvariant() + '-' + $start + '.csv')
$results = New-Object System.Collections.Generic.List[object]
Push-Location $root
try {
    for ($index = 0; $index -lt $tests.Count; $index++) {
        $action = $tests[$index]
        $timer = [System.Diagnostics.Stopwatch]::StartNew()
        $errorText = ''
        $result = 'PASS'
        Write-Host ("[{0}/{1}] {2}" -f ($index+1), $tests.Count, $action) -ForegroundColor Cyan
        try {
            & $invoke -Action $action
        }
        catch {
            $result = 'FAIL'
            $errorText = $_.Exception.Message
            Write-Host $errorText -ForegroundColor Red
            $logPath = Join-Path $reportRoot ('unity-' + $action.ToLowerInvariant() + '.log')
            if (Test-Path -LiteralPath $logPath) {
                $matches = @(Select-String -LiteralPath $logPath -Pattern @(
                    '^[A-Za-z][A-Za-z0-9.]*Exception:',
                    'FAIL:',
                    'error CS[0-9]+',
                    'executeMethod method .* threw exception'
                ) -ErrorAction SilentlyContinue)
                foreach ($line in ($matches | Select-Object -Last 8)) {
                    Write-Host ($line.Line.Trim()) -ForegroundColor Yellow
                }
            }
        }
        finally {
            $timer.Stop()
        }
        if ($Group -eq 'Diagnostics' -and $result -eq 'PASS') { $result = 'COMPLETED' }
        $results.Add([pscustomobject]@{
            Action = $action
            Status = $result
            Seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 2)
            Commit = (git rev-parse --short HEAD)
            LogFile = 'reports/unity-' + $action.ToLowerInvariant() + '.log'
            Error = $errorText
        })
        $results | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
        if ($result -eq 'FAIL' -and -not $ContinueOnFailure) {
            Write-Host 'Stopped at first failure. Use -StartAt to resume after a fix.' -ForegroundColor Yellow
            break
        }
    }
}
finally {
    Pop-Location
}
$results | Format-Table Action,Status,Seconds -AutoSize
Write-Host ("Summary: {0}" -f $csv)
if (@($results | Where-Object { $_.Status -eq 'FAIL' }).Count -gt 0) {
    throw 'Validation had failures; see the CSV and individual Unity logs.'
}
