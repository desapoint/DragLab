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
    $kind=if($content -match '^Native navigation'){ 'native' }else{'legacy'}
    foreach($file in Get-ChildItem -LiteralPath $fixture.FullName -File -Filter '*.png') {
        if($file.LastWriteTimeUtc -gt (Get-Item -LiteralPath $report).LastWriteTimeUtc){continue}
        $key=$kind+'/'+$file.Name
        if(-not $taskImages.ContainsKey($key) -or $file.LastWriteTimeUtc -gt $taskImages[$key].LastWriteTimeUtc){$taskImages[$key]=$file}
    }
}
foreach($kind in @('native','legacy')) {
    $gallery=Assert-InterfaceScreenshotPath $taskRoot (Join-Path $taskRoot ('reports/interface-screenshots/recorded-'+$kind)) 'reports/interface-screenshots'
    New-Item -ItemType Directory -Path $gallery -Force | Out-Null
    Clear-InterfaceScreenshotImages $gallery
    $files=@($taskImages.Keys | Where-Object { $_.StartsWith($kind+'/') } | Sort-Object | ForEach-Object { $taskImages[$_] })
    foreach($file in $files){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $gallery $file.Name) -Force}
    $files | ForEach-Object { [pscustomobject]@{Image=$_.Name;Source=$_.FullName;CapturedUtc=$_.LastWriteTimeUtc.ToString('o')} } | Export-Csv -LiteralPath (Join-Path $gallery 'images.csv') -NoTypeInformation -Encoding UTF8
    @('RECORDED snapshots from successful historical test runs; not a fresh full-interface rerun.', 'Each filename uses its newest recorded successful image. See images.csv for source and capture date.', 'Current test suites have separate folders; their next run clears and regenerates their own images.', "Screenshots: $($files.Count)") | Set-Content -LiteralPath (Join-Path $gallery 'RUN.txt') -Encoding UTF8
    Write-Output "Recorded screenshots: $gallery ($($files.Count))"
}
