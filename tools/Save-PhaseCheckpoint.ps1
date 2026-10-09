param([Parameter(Mandatory=$true)][string]$Phase)
$ErrorActionPreference = 'Stop'
if ($Phase -notmatch '^(2[a-l]|3[a-g])$') { throw 'Expected phase 2a through 2l or 3a through 3g.' }
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDestination = Join-Path $taskRoot "reports/phases/$Phase"
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
$taskBuild = Join-Path $taskRoot 'Game/Builds/Windows'
$taskZip = Join-Path $taskDestination 'Windows-checkpoint.zip'
if (Test-Path -LiteralPath $taskZip) { throw 'Checkpoint already exists; choose an explicit new checkpoint instead of overwriting it.' }
Compress-Archive -LiteralPath $taskBuild -DestinationPath $taskZip -CompressionLevel Fastest
Compress-Archive -LiteralPath @((Join-Path $taskRoot 'Game/Assets/DragLab'), (Join-Path $taskRoot 'Game/Packages'), (Join-Path $taskRoot 'Game/ProjectSettings'), (Join-Path $taskRoot 'docs'), (Join-Path $taskRoot 'tools')) -DestinationPath (Join-Path $taskDestination 'source-checkpoint.zip') -CompressionLevel Fastest
if ($Phase.StartsWith('3')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-verification.txt') -Destination (Join-Path $taskDestination 'prior-2l-foundation-verification.txt')
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-scene-verification.txt') -Destination (Join-Path $taskDestination 'scene-verification.txt')
} else {
    Copy-Item -LiteralPath (Join-Path $taskRoot 'reports/unity-verification.txt') -Destination (Join-Path $taskDestination 'unity-verification.txt')
}
Write-Output "Saved $Phase executable, required data and source checkpoint in $taskDestination"

