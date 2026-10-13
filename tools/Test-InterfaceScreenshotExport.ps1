$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InterfaceScreenshots.ps1')
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $project ('reports/screenshot-tool-fixture/' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $root 'reports/player-fixtures/repeated-run'
$run = Start-InterfaceScreenshotRun $root $fixture 'native-test'
[IO.File]::WriteAllBytes((Join-Path $fixture 'old.png'), [byte[]]@(1,2,3))
'Keep this log' | Set-Content -LiteralPath (Join-Path $fixture 'player.log') -Encoding UTF8
Complete-InterfaceScreenshotRun $run $true | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $run.Gallery 'old.png'))) { throw 'Initial images were not exported.' }
$other = Start-InterfaceScreenshotRun $root (Join-Path $root 'reports/player-fixtures/other') 'other-suite'
[IO.File]::WriteAllBytes((Join-Path $other.Gallery 'preserved.png'), [byte[]]@(4))
$run = Start-InterfaceScreenshotRun $root $fixture 'native-test'
if ((Test-Path -LiteralPath (Join-Path $fixture 'old.png')) -or (Test-Path -LiteralPath (Join-Path $run.Gallery 'old.png'))) { throw 'Old run screenshots were retained.' }
if (-not (Test-Path -LiteralPath (Join-Path $fixture 'player.log')) -or -not (Test-Path -LiteralPath (Join-Path $other.Gallery 'preserved.png'))) { throw 'Nonimage history or another suite was removed.' }
[IO.File]::WriteAllBytes((Join-Path $fixture 'new.png'), [byte[]]@(5,6))
Complete-InterfaceScreenshotRun $run $false | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $run.Gallery 'new.png')) -or -not ((Get-Content -LiteralPath (Join-Path $run.Gallery 'RUN.txt') -Encoding UTF8) -match 'FAILED')) { throw 'Failed-run screenshots/status were not refreshed.' }
foreach ($path in @((Join-Path $root 'outside'), (Join-Path $root 'reports/player-fixtures/../outside'))) {
    $rejected = $false
    try { Start-InterfaceScreenshotRun $root $path 'native-test' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Path escape accepted.' }
}
$rejected=$false
try { Start-InterfaceScreenshotRun $root $fixture '../bad' | Out-Null } catch { $rejected=$true }
if (-not $rejected) { throw 'Suite path escape accepted.' }
'Screenshot export: repeat-run cleanup, fresh copies, failed-run status, retained logs/other suites and path guards PASS.'
