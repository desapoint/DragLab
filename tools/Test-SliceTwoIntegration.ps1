param([string]$RunName=('slice2-'+(Get-Date -Format 'yyyyMMdd-HHmmss')),[switch]$SkipBuild,[switch]$ShowPlayer)
$ErrorActionPreference='Stop'
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use letters, digits, hyphens and underscores for RunName.'}
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskCases=@(
    @{Name='gameplay';Flag=''},
    @{Name='driving';Flag='DrivingBenchOnly'},
    @{Name='driving-failure';Flag='DrivingFailureOnly'},
    @{Name='parts';Flag='PartsOnly'},
    @{Name='reset';Flag='ResetOnly'},
    @{Name='keyboard';Flag='KeyboardOnly'},
    @{Name='durability';Flag='DurabilityOnly'},
    @{Name='audio-levels';Flag='AudioLevelsOnly'},
    @{Name='inspection';Flag='InspectionOnly'},
    @{Name='stats';Flag='StatsSummaryOnly'}
)
foreach($taskName in @('recovery')+$taskCases.Name){if(Test-Path -LiteralPath (Join-Path $taskRoot ('reports/player-fixtures/'+$RunName+'-'+$taskName))){throw 'Choose a fresh RunName; previous evidence will not be overwritten.'}}
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'Invoke-Unity.ps1') -Action BuildWindows}
& (Join-Path $PSScriptRoot 'Test-InterruptedRun.ps1') -FixtureName ($RunName+'-recovery') -ShowPlayer:$ShowPlayer
foreach($taskCase in $taskCases){
    $taskArguments=@{FixtureName=($RunName+'-'+$taskCase.Name);ShowPlayer=$ShowPlayer}
    if($taskCase.Flag){$taskArguments[$taskCase.Flag]=$true}
    & (Join-Path $PSScriptRoot 'Test-WindowsSlice.ps1') @taskArguments
}
Write-Output ('Accepted native fixtures: reports/player-fixtures/'+$RunName+'-*')
