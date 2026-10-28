param([string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskSources = @(
    (Join-Path $taskRoot 'Game/Assets/DragLab/Scripts/Domain/RuntimeEntropy.cs'),
    (Join-Path $taskRoot 'Game/Assets/DragLab/Scripts/Domain/RuntimeEntropy.Diagnostics.cs')
)
foreach ($taskSource in $taskSources) {
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Missing source: $taskSource" }
}
# Compile these pure managed files without Editor/Development defines. Do not
# instantiate the allocator or call NewGuid, and do not open a game save.
$taskTypes = @(Add-Type -LiteralPath $taskSources -PassThru -CompilerOptions '/optimize+')
$taskType = @($taskTypes | Where-Object FullName -eq 'DragLab.Domain.RuntimeEntropy')
if ($taskType.Count -ne 1) { throw 'Expected one compiled release RuntimeEntropy type.' }
$taskFlags = [Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$taskMethods = @($taskType[0].GetMethods($taskFlags))
$taskFields = @($taskType[0].GetFields($taskFlags))
$taskNested = @($taskType[0].GetNestedTypes([Reflection.BindingFlags]'Public,NonPublic'))
$taskForbidden = @($taskMethods | Where-Object { $_.Name -match 'IdentityObservation|ObserveGeneratedIdentity' })
$taskForbidden += @($taskFields | Where-Object { $_.Name -match 'IdentityObservation' })
$taskForbidden += @($taskNested | Where-Object { $_.Name -match 'IdentityObservation|IdentityEmission' })
if ($taskForbidden.Count) { throw ('Diagnostic members leaked into release: ' + (($taskForbidden | ForEach-Object Name) -join ', ')) }
$taskFactory = $taskType[0].GetMethod('NewGuid', [Reflection.BindingFlags]'Public,Static')
if ($null -eq $taskFactory) { throw 'Release allocator API is missing.' }
$taskReport = @(
    'PASS: standalone optimized C# compilation without Editor/Development defines',
    'PASS: no observation lease, emission type, field, method or partial hook in compiled release metadata',
    'PASS: original public NewGuid API remains present',
    'No allocator method invoked; no sequence, seed, Unity state or save accessed.',
    'Scope: standalone Roslyn/.NET compile proof; not a Unity player, Android or performance acceptance test.'
)
foreach ($taskSource in $taskSources) {
    $taskReport += ('SHA256 ' + (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash + '  ' + $taskSource)
}
if ($ReportPath) {
    $taskReportFile = [IO.Path]::GetFullPath($ReportPath)
    $taskReportParent = [IO.Path]::GetDirectoryName($taskReportFile)
    [IO.Directory]::CreateDirectory($taskReportParent) | Out-Null
    [IO.File]::WriteAllText($taskReportFile, (($taskReport -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
}
$taskReport
