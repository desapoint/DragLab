param(
    [Parameter(Mandatory=$true)][string]$Phase,
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,31}$')][string]$Revision
)
$ErrorActionPreference = 'Stop'
if ($Phase -notmatch '^(2[a-l]|3[a-h])$') { throw 'Expected phase 2a through 2l or 3a through 3h.' }
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDestination = Join-Path $taskRoot "reports/phases/$Phase"
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
$taskBuild = Join-Path $taskRoot 'Game/Builds/Windows'
$taskSuffix = if($Revision){'-'+$Revision}else{''}
$taskZip = Join-Path $taskDestination "Windows-checkpoint$taskSuffix.zip"
if (Test-Path -LiteralPath $taskZip) { throw 'Checkpoint already exists; choose an explicit new checkpoint instead of overwriting it.' }
Compress-Archive -LiteralPath $taskBuild -DestinationPath $taskZip -CompressionLevel Fastest
$taskSourceZip=Join-Path $taskDestination "source-checkpoint$taskSuffix.zip"
if($Phase -eq '3h') {
    # Preserve repository-relative paths and required migration/report fixtures.
    # Git's inventory excludes local caches, players, raw saves and older archives.
    if(Test-Path -LiteralPath $taskSourceZip){throw 'Source checkpoint already exists.'}
    $taskFiles=& git -c core.quotepath=false -C $taskRoot ls-files --cached --others --exclude-standard
    if($LASTEXITCODE -ne 0){throw 'Cannot inventory versioned source.'}
    $taskSourceArchive=[IO.Compression.ZipFile]::Open($taskSourceZip,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach($taskRelative in ($taskFiles | Sort-Object -Unique)) {
            $taskAbsolute=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
            if(!$taskAbsolute.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source inventory escaped workspace.'}
            if(Test-Path -LiteralPath $taskAbsolute -PathType Leaf){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskSourceArchive,$taskAbsolute,$taskRelative.Replace('\','/'),[IO.Compression.CompressionLevel]::Fastest)|Out-Null}
        }
    } finally {$taskSourceArchive.Dispose()}
} else {
    Compress-Archive -LiteralPath @((Join-Path $taskRoot 'Game/Assets/DragLab'), (Join-Path $taskRoot 'Game/Packages'), (Join-Path $taskRoot 'Game/ProjectSettings'), (Join-Path $taskRoot 'docs'), (Join-Path $taskRoot 'tools')) -DestinationPath $taskSourceZip -CompressionLevel Fastest
}
if ($Phase -eq '3h') {
    # Fresh domain/native evidence is already collected under 3h. Do not relabel
    # restored historical root reports as a current acceptance gate.
} elseif ($Phase.StartsWith('3')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-verification.txt') -Destination (Join-Path $taskDestination 'prior-2l-foundation-verification.txt')
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-scene-verification.txt') -Destination (Join-Path $taskDestination 'scene-verification.txt')
} else {
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-verification.txt') -Destination (Join-Path $taskDestination 'unity-verification.txt')
}
Write-Output "Saved $Phase executable, required data and source checkpoint in $taskDestination"
