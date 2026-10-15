<#
.SYNOPSIS
Runs curated DragLab Unity checks with failure details and CSV results.
Only Smoke, Functional and Extended are included in All.
Quarantined targets repeat feature suites; run individual release gates instead.
Diagnostics are measurements, not pass/fail feature gates.
#>
param(
    [ValidateSet('Smoke','Functional','Extended','All','Quarantined','Diagnostics')]
    [string]$Group = 'Smoke',
    [string]$StartAt = '',
    [switch]$ContinueOnFailure,
    [switch]$ListOnly,
    [ValidateRange(1, 100)]
    [int]$BatchSize = 9
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
        'BranchAuditVerify',
        'ComponentFaultVerify',
        'EngineRebuildFlowVerify',
        'SupplyPricingVerify',
        'FluidPurchaseVerify',
        'CareerVerify',
        'CareerExpandedVerify',
        'PartsMarketVerify',
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
        'OwnershipVerify',
        'AssemblyVerify',
        'PaidVerify',
        'EconomyIntegrationVerify',
        'ResidualPerformanceVerify',
        'ResidualCheckpointVerify',
        'ResidualRecoveryVerify',
        'DrivingCorrectionsVerify',
        'DurabilityAudit',
        'SliceTwoAudit',
        'UnitsVisualVerify',
        'IntegratedVerify'
    )
    Quarantined = @(
        'PartsMarketRegression',
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
    Write-Host ("{0}: {1} actions" -f $Group, $tests.Count)
    $tests | ForEach-Object { Write-Output $_ }
    return
}

# Resolve methods from the existing single-action launcher; never fork its action map.
$methodByAction = @{}
$launcherSource = Get-Content -LiteralPath $invoke -Raw -Encoding UTF8
$pattern = "(?m)^\s*'(?<action>[A-Za-z0-9]+)'\s*\{\s*'(?<method>[A-Za-z0-9_.]+)'\s*\}"
foreach ($match in [regex]::Matches($launcherSource, $pattern)) {
    $methodByAction[$match.Groups['action'].Value] = $match.Groups['method'].Value
}
foreach ($test in $tests) {
    if (-not $methodByAction.ContainsKey($test)) { throw "No Unity entry point mapped for action $test." }
}

$editors = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($editors.Count -gt 0) {
    throw 'Close all Unity Editor/batch processes before running validation. No processes were terminated.'
}
if ($Group -eq 'Quarantined') {
    Write-Warning 'These checks include repeated composite coverage. Results are not a current-release gate.'
}
if ($Group -eq 'Diagnostics') {
    Write-Warning 'Diagnostic actions measure behavior; completion is not a verification PASS.'
}
New-Item -Path $reportRoot -ItemType Directory -Force | Out-Null
$batchDir = Join-Path $reportRoot 'validation-batches'
New-Item -Path $batchDir -ItemType Directory -Force | Out-Null
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $PID
$csv = Join-Path $reportRoot ('validation-' + $Group.ToLowerInvariant() + '-' + $stamp + '.csv')
$results = New-Object System.Collections.Generic.List[object]
$commit = (git -C $root rev-parse --short HEAD)
$hasFailure = $false
$halt = $false
Push-Location $root
try {
    for ($offset = 0; $offset -lt $tests.Count -and -not $halt; $offset += $BatchSize) {
        $batch = @($tests | Select-Object -Skip $offset -First $BatchSize)
        $chunkNumber = [int]([math]::Floor($offset / $BatchSize) + 1)
        $chunkName = ('{0}-{1}-batch{2:D2}' -f $Group.ToLowerInvariant(), $stamp, $chunkNumber)
        $manifest = Join-Path $batchDir ($chunkName + '.tsv')
        $statusFile = $manifest + '.results.tsv'
        $batchLog = Join-Path $reportRoot ('unity-validation-' + $chunkName + '.log')
        $rows = New-Object 'System.Collections.Generic.List[string]'
        $rows.Add(('#continue=' + [int][bool]$ContinueOnFailure))
        foreach ($action in $batch) { $rows.Add(($action + [char]9 + $methodByAction[$action])) }
        [IO.File]::WriteAllLines($manifest, $rows.ToArray(), ([System.Text.UTF8Encoding]::new($false)))
        if (Test-Path -LiteralPath $statusFile) { Remove-Item -LiteralPath $statusFile -Force }

        Write-Host ("Unity batch {0}: actions {1}-{2} of {3} ({4})" -f $chunkNumber, ($offset+1), ($offset+$batch.Count), $tests.Count, ($batch -join ', ')) -ForegroundColor Cyan
        $launchError = ''
        try {
            & $invoke -Action ValidationBatch -BatchManifest $manifest -LogPath $batchLog
        } catch {
            $launchError = $_.Exception.Message
            Write-Host $launchError -ForegroundColor Red
        }

        $processed = 0
        $batchFailed = $false
        if (Test-Path -LiteralPath $statusFile) {
            foreach ($line in @(Get-Content -LiteralPath $statusFile -Encoding UTF8)) {
                if ($processed -ge $batch.Count) { throw "Batch produced excess results: $statusFile" }
                $fields = $line.Split([char]9)
                if ($fields.Count -ne 4 -or $fields[1] -ne $batch[$processed] -or $fields[0] -notin @('PASS', 'FAIL')) {
                    throw "Invalid/misordered test result: $statusFile; line $($processed+1)."
                }
                $duration = [double]::Parse($fields[2], [Globalization.CultureInfo]::InvariantCulture)
                $problem = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($fields[3]))
                $status = $fields[0]
                if ($Group -eq 'Diagnostics' -and $status -eq 'PASS') { $status = 'COMPLETED' }
                if ($status -eq 'FAIL') { $batchFailed = $true; $hasFailure = $true }
                $results.Add([pscustomobject]@{
                    Action = $fields[1]
                    Status = $status
                    Seconds = [Math]::Round($duration, 2)
                    Commit = $commit
                    LogFile = [IO.Path]::Combine('reports', [IO.Path]::GetFileName($batchLog))
                    Error = $problem
                })
                Write-Host ("  {0}: {1} ({2:N1}s)" -f $status, $fields[1], $duration) -ForegroundColor $(if ($status -eq 'FAIL') { 'Red' } else { 'Green' })
                if ($problem) { Write-Host ($problem -split '[\r\n]+' | Select-Object -First 2) -ForegroundColor Yellow }
                $processed++
            }
        }

        # Unexpected Unity exit: preserve completed tests and mark only the first
        # unfinished one as failed. Do not claim the remaining actions executed.
        if ($processed -lt $batch.Count -and -not ($batchFailed -and -not $ContinueOnFailure)) {
            $missing = $batch[$processed]
            $problem = if ($launchError) { $launchError } else { 'Unity stopped without recording this action result.' }
            $results.Add([pscustomobject]@{
                Action = $missing
                Status = 'FAIL'
                Seconds = 0
                Commit = $commit
                LogFile = [IO.Path]::Combine('reports', [IO.Path]::GetFileName($batchLog))
                Error = $problem
            })
            $batchFailed = $true
            $hasFailure = $true
            $halt = $true
            Write-Host ("  FAIL: {0}: {1}" -f $missing, $problem) -ForegroundColor Red
        }
        if ($launchError -and -not $batchFailed) {
            # An abnormal Unity exit cannot be declared a passing suite.
            $hasFailure = $true
            $halt = $true
            Write-Host 'Unity returned an error after recording results. Review the batch log.' -ForegroundColor Red
        }
        $results | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
        if ($batchFailed -and -not $ContinueOnFailure) { $halt = $true }
    }
} finally {
    Pop-Location
}
$results | Format-Table Action, Status, Seconds -AutoSize
Write-Host ("Summary: {0}" -f $csv)
if ($hasFailure) { throw 'Validation had failures; see the CSV and corresponding Unity batch logs.' }
