# Assemble the newest recorded successful capture of each interface image.
# Live test scripts separately replace their own current-run suite folder.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'InterfaceScreenshots.ps1')
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskImages=@{}
foreach($fixture in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'reports/player-fixtures') -Directory) {
    $report=@('verification.txt','runtime-verification.txt') | ForEach-Object { Join-Path $fixture.FullName $_ } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if(-not $report){continue}
    $content=Get-Content -LiteralPath $report -Encoding UTF8 -Raw
    if($content -match '(?m)^FAIL:' -or $content -notmatch '(?m)^Checks:' -or $content -match 'runtime errors: [1-9]'){continue}
    $kind='all'
    foreach($file in Get-ChildItem -LiteralPath $fixture.FullName -File -Filter '*.png') {
        if($file.LastWriteTimeUtc -gt (Get-Item -LiteralPath $report).LastWriteTimeUtc){continue}
        $key=$kind+'/'+$file.Name
        if(-not $taskImages.ContainsKey($key) -or $file.LastWriteTimeUtc -gt $taskImages[$key].LastWriteTimeUtc){$taskImages[$key]=$file}
    }
}
$gallery=Get-InterfaceScreenshotGallery $taskRoot
$current=@{}
foreach($entry in Read-InterfaceScreenshotIndex $gallery){if($entry.Suite -ne 'recorded'){$current[$entry.Image]=$entry}}
Clear-OwnedInterfaceScreenshots $gallery 'recorded'
$run=[pscustomobject]@{Fixture='Successful historical fixtures';Gallery=$gallery;Suite='recorded';Started=[DateTime]::UtcNow;StatusFile=(Join-Path $gallery 'run-info/recorded.txt')}
$files=@($taskImages.Values | Where-Object { -not $current.ContainsKey($_.Name) -or $_.LastWriteTimeUtc -gt [DateTime]::Parse($current[$_.Name].CapturedUtc) } | Sort-Object Name)
Complete-InterfaceScreenshotRun $run $true -Images $files -Recorded
