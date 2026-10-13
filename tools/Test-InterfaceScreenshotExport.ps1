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
[IO.File]::WriteAllBytes((Join-Path $fixture 'new-4k.png'), [byte[]]@(7,8))
Complete-InterfaceScreenshotRun $run $false | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $run.Gallery 'new.png')) -or -not ((Get-Content -LiteralPath $run.StatusFile -Encoding UTF8) -match 'FAILED')) { throw 'Failed-run screenshots/status were not refreshed.' }
if ((Test-Path -LiteralPath (Join-Path $run.Gallery 'new-4k.png')) -or -not (Test-Path -LiteralPath (Join-Path $run.Gallery 'resolution-tests/new-4k.png'))) { throw 'Alternate-resolution image was not separated.' }
$normalHeader=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAABQAAAALQ')
$alternateHeader=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAACAAAAASA')
[IO.File]::WriteAllBytes((Join-Path $fixture 'header-normal-4k.png'),$normalHeader)
[IO.File]::WriteAllBytes((Join-Path $fixture 'header-other.png'),$alternateHeader)
Complete-InterfaceScreenshotRun $run $true | Out-Null
if(-not (Test-Path -LiteralPath (Join-Path $run.Gallery 'header-normal-4k.png')) -or -not (Test-Path -LiteralPath (Join-Path $run.Gallery 'resolution-tests/header-other.png'))){throw 'Actual PNG dimensions did not override filename hints.'}
$again=Start-InterfaceScreenshotRun $root $fixture 'native-test'
if ((Test-Path -LiteralPath (Join-Path $run.Gallery 'new.png')) -or (Test-Path -LiteralPath (Join-Path $run.Gallery 'resolution-tests/new-4k.png'))) { throw 'Normal/alternate previous images were not both cleared.' }
[IO.File]::WriteAllBytes((Join-Path $fixture 'shared.png'),[byte[]]@(1))
Complete-InterfaceScreenshotRun $again $true | Out-Null
[IO.File]::WriteAllBytes((Join-Path $other.Fixture 'shared.png'),[byte[]]@(2))
Complete-InterfaceScreenshotRun $other $true | Out-Null
Start-InterfaceScreenshotRun $root $fixture 'native-test' | Out-Null
if([IO.File]::ReadAllBytes((Join-Path $run.Gallery 'shared.png'))[0] -ne 2){throw 'Older suite removed a newer shared-name capture.'}
foreach ($path in @((Join-Path $root 'outside'), (Join-Path $root 'reports/player-fixtures/../outside'))) {
    $rejected = $false
    try { Start-InterfaceScreenshotRun $root $path 'native-test' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Path escape accepted.' }
}
$rejected=$false
try { Start-InterfaceScreenshotRun $root $fixture '../bad' | Out-Null } catch { $rejected=$true }
if (-not $rejected) { throw 'Suite path escape accepted.' }
'Screenshot export: repeat-run cleanup, fresh copies, failed-run status, retained logs/other suites and path guards PASS.'
