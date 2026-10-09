param(
    [string]$RunName = ('slice3-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch]$DomainOnly,
    [switch]$NativeOnly,
    [switch]$SkipBuild,
    [switch]$ShowPlayer
)
$ErrorActionPreference = 'Stop'
if ($RunName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use letters, digits, hyphens and underscores for RunName.' }
if ($DomainOnly -and $NativeOnly) { throw 'Choose DomainOnly or NativeOnly, not both.' }
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskReportRoot = Join-Path $taskRoot 'reports/phases/3h'
$taskEvidence = Join-Path $taskReportRoot ('full-validation/' + $RunName)
if (Test-Path -LiteralPath $taskEvidence) { throw 'Use a fresh RunName; existing evidence is immutable.' }
New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
$taskRows = [Collections.Generic.List[object]]::new()
function Save-GateRow([string]$stage, [string]$gate, [string]$result, [string]$evidence) {
    $taskRows.Add([pscustomobject]@{Stage=$stage;Gate=$gate;Result=$result;Evidence=$evidence;UTC=[DateTime]::UtcNow.ToString('O')})
    $taskRows | Export-Csv -LiteralPath (Join-Path $taskEvidence 'manifest.csv') -Encoding utf8 -NoTypeInformation
}
# Preserve historical review evidence byte-for-byte. Generated raw fixtures stay isolated locally.
$taskHistorical = @{}
foreach ($taskPath in $(if (!$NativeOnly) { & git -C $taskRoot ls-files reports })) {
    if (!$taskPath.StartsWith('reports/phases/3h/')) {
        $taskAbsolute = Join-Path $taskRoot $taskPath
        if (Test-Path -LiteralPath $taskAbsolute -PathType Leaf) { $taskHistorical[$taskAbsolute] = [IO.File]::ReadAllBytes($taskAbsolute) }
    }
}
try {
    if (!$NativeOnly) {
        $taskCases = @(
            @{Name='thermal';Action='ThermalVerify';Files=@('reports/phases/3h/thermal-verification.txt')},
            @{Name='collection';Action='CollectionVerify';Files=@('reports/phases/3a/verification.txt','reports/phases/3a/calibration.csv')},
            @{Name='assembly';Action='AssemblyVerify';Files=@('reports/phases/3b/verification.txt')},
            @{Name='valuation';Action='ValuationVerify';Files=@('reports/phases/3c/verification.txt','reports/phases/3c/valuation-matrix.csv')},
            @{Name='paid';Action='PaidVerify';Files=@('reports/phases/3d/verification.txt')},
            @{Name='career';Action='CareerExpandedVerify';Files=@('reports/phases/3e/verification.txt','reports/phases/3e/career-build-matrix.csv')},
            @{Name='business';Action='BusinessVerify';Files=@('reports/phases/3f/verification.txt','reports/phases/3f/business-matrix.csv')},
            @{Name='balance';Action='BalanceVerify';Files=@('reports/phases/3g/verification.txt','reports/phases/3g/economy-ledger-matrix.csv','reports/phases/3g/market-matrix.csv','reports/phases/3g/fuel-mode-matrix.csv')},
            @{Name='economy-save';Action='EconomyIntegrationVerify';Files=@('reports/phases/3h/integration-verification.txt','reports/phases/3h/integration-matrix.csv')},
            @{Name='foundation';Action='Verify';Files=@('reports/unity-verification.txt','reports/career-path.txt','reports/balance-matrix.csv','reports/stock-durability/verification.txt','reports/stock-durability/parts.csv','reports/slice-2-audit/verification.txt','reports/slice-2-audit/runs.csv','reports/slice-2-audit/parts.csv')},
            @{Name='interrupted-physics';Action='RecoveryVerify';Files=@('reports/phases/2k/recovery-verification.txt','reports/phases/2k/recovery-matrix.csv')},
            @{Name='integrated-tools';Action='IntegratedVerify';Files=@('reports/phases/2l/tools-verification.txt','reports/phases/2l/controlled-runs.csv')},
            @{Name='scene';Action='SceneCheck';Files=@('reports/unity-scene-verification.txt')}
        )
        foreach ($taskCase in $taskCases) {
            Write-Output ('RUN domain: ' + $taskCase.Name)
            & (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -Action $taskCase.Action
            foreach ($taskPath in $taskCase.Files) {
                $taskSource = Join-Path $taskRoot $taskPath
                if (!(Test-Path -LiteralPath $taskSource)) { throw "Expected verification evidence missing: $taskPath" }
                $taskLabel = if($taskPath.StartsWith('reports/slice-2-audit/')) {'audit-'} elseif($taskPath.StartsWith('reports/stock-durability/')) {'stock-'} else {''}
                $taskTarget = Join-Path $taskEvidence ($taskCase.Name + '-' + $taskLabel + [IO.Path]::GetFileName($taskSource))
                Copy-Item -LiteralPath $taskSource -Destination $taskTarget
            }
            Save-GateRow 'Domain' $taskCase.Name 'PASS' ($taskCase.Name + '-*')
        }
    }
    if (!$DomainOnly) {
        if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -Action BuildPlayerOnly }
        & (Join-Path $PSScriptRoot 'Test-EconomyInterruptedRun.ps1') -FixtureName ($RunName + '-economy-interruption') -ShowPlayer:$ShowPlayer
        $taskFixture = 'reports/player-fixtures/' + $RunName + '-economy-interruption/runtime-verification.txt'
        Copy-Item -LiteralPath (Join-Path $taskRoot $taskFixture) -Destination (Join-Path $taskEvidence 'economy-interruption-runtime.txt')
        Save-GateRow 'Windows' 'economy-interruption' 'PASS' $taskFixture
        $taskCases = @(
            @{Name='collection';Flag='CollectionOnly'},
            @{Name='thermal';Flag='ThermalOnly'},
            @{Name='stock-durability';Flag='DurabilityOnly'},
            @{Name='setups';Flag='AssemblySetupsOnly'},
            @{Name='valuation';Flag='ValuationOnly'},
            @{Name='paid';Flag='PaidOnly'},
            @{Name='paid-resume';Flag='PaidOnly';Resume='PaidResume';Fixture='paid'},
            @{Name='career';Flag='ExpandedCareerOnly'},
            @{Name='business';Flag='BusinessOnly'},
            @{Name='business-resume';Flag='BusinessOnly';Resume='BusinessResume';Fixture='business'},
            @{Name='balance';Flag='BalanceOnly'},
            @{Name='gameplay';Flag=''},
            @{Name='keyboard';Flag='KeyboardOnly'},
            @{Name='reset';Flag='ResetOnly'},
            @{Name='physical-bench';Flag='DrivingBenchOnly'},
            @{Name='bench-config';Flag='BenchConfigOnly'},
            @{Name='audio-levels';Flag='AudioLevelsOnly'}
        )
        foreach ($taskCase in $taskCases) {
            $taskSuffix = if ($taskCase.Fixture) { $taskCase.Fixture } else { $taskCase.Name }
            $taskArgs = @{FixtureName=($RunName + '-' + $taskSuffix);ShowPlayer=$ShowPlayer}
            if ($taskCase.Flag) { $taskArgs[$taskCase.Flag] = $true }
            if ($taskCase.Resume) { $taskArgs[$taskCase.Resume] = $true }
            Write-Output ('RUN Windows: ' + $taskCase.Name)
            & (Join-Path $PSScriptRoot 'Test-WindowsSlice.ps1') @taskArgs
            $taskFixture = 'reports/player-fixtures/' + $taskArgs.FixtureName + '/runtime-verification.txt'
            Copy-Item -LiteralPath (Join-Path $taskRoot $taskFixture) -Destination (Join-Path $taskEvidence ($taskCase.Name + '-runtime.txt'))
            Save-GateRow 'Windows' $taskCase.Name 'PASS' $taskFixture
        }
    }
} catch {
    Save-GateRow 'Runner' 'stopped-on-failure' 'FAIL' $_.Exception.Message
    throw
} finally {
    foreach ($taskPath in $taskHistorical.Keys) { [IO.File]::WriteAllBytes($taskPath, $taskHistorical[$taskPath]) }
}
Write-Output ('Passed gates recorded in ' + $taskEvidence)
