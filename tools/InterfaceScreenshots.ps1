# Shared current-run image export; historical reports and other suites stay intact.
function Assert-InterfaceScreenshotPath {
    param([string]$Root, [string]$Path, [string]$Container)
    $resolved = [IO.Path]::GetFullPath($Path)
    $allowed = [IO.Path]::GetFullPath((Join-Path $Root $Container)).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Screenshot path must stay inside its project report container.' }
    $ancestor = $resolved
    while ($ancestor.Length -ge $allowed.TrimEnd('\').Length) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Screenshot paths cannot use directory links.' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
        if (-not $ancestor) { break }
    }
    return $resolved
}
function Clear-InterfaceScreenshotImages {
    param([string]$Path)
    # Only immediate PNGs are replaced. Never recursively remove a fixture or gallery.
    if (Test-Path -LiteralPath $Path) {
        foreach ($file in Get-ChildItem -LiteralPath $Path -File -Filter '*.png') { Remove-Item -LiteralPath $file.FullName }
    }
}
function Start-InterfaceScreenshotRun {
    param([string]$Root, [string]$Fixture, [string]$Suite)
    if ($Suite -notmatch '^[A-Za-z0-9_-]+$') { throw 'Screenshot suite must use letters, digits, hyphens and underscores.' }
    $fixturePath = Assert-InterfaceScreenshotPath $Root $Fixture 'reports/player-fixtures'
    $gallery = Assert-InterfaceScreenshotPath $Root (Join-Path $Root ('reports/interface-screenshots/' + $Suite)) 'reports/interface-screenshots'
    New-Item -ItemType Directory -Path $fixturePath, $gallery -Force | Out-Null
    Clear-InterfaceScreenshotImages $fixturePath
    Clear-InterfaceScreenshotImages $gallery
    $run = [pscustomobject]@{ Fixture=$fixturePath; Gallery=$gallery; Suite=$Suite; Started=[DateTime]::UtcNow }
    'Running; previous images cleared. New screenshots will be exported when this run exits.' | Set-Content -LiteralPath (Join-Path $gallery 'RUN.txt') -Encoding UTF8
    'Latest interface-test screenshots. Each suite folder is replaced by its next run; failed runs are labelled. Historical logs and saves stay in reports/player-fixtures. Open PNG files directly for full resolution.' | Set-Content -LiteralPath (Join-Path (Split-Path $gallery -Parent) 'README.txt') -Encoding UTF8
    return $run
}
function Complete-InterfaceScreenshotRun {
    param($Run, [bool]$Passed, [string]$Report='')
    $files = @(Get-ChildItem -LiteralPath $Run.Fixture -File -Filter '*.png' | Sort-Object Name)
    foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $Run.Gallery $file.Name) -Force }
    $status = if ($Passed) { 'PASS' } else { 'FAILED / INCOMPLETE; these images are from this attempt only.' }
    $text = @("Suite: $($Run.Suite)", "Status: $status", "Started UTC: $($Run.Started.ToString('o'))", "Fixture: $($Run.Fixture)", "Screenshots: $($files.Count)")
    if ($Report -and (Test-Path -LiteralPath $Report) -and (Get-Item -LiteralPath $Report).LastWriteTimeUtc -ge $Run.Started) {
        $text += Get-Content -LiteralPath $Report -Encoding UTF8 | Where-Object { $_ -match '^(Checks:|FAIL:)' } | Select-Object -First 2
    }
    $text | Set-Content -LiteralPath (Join-Path $Run.Gallery 'RUN.txt') -Encoding UTF8
    $files | ForEach-Object { [pscustomobject]@{ Image=$_.Name; Source=$_.FullName; CapturedUtc=$_.LastWriteTimeUtc.ToString('o') } } | Export-Csv -LiteralPath (Join-Path $Run.Gallery 'images.csv') -NoTypeInformation -Encoding UTF8
    Write-Output "Interface screenshots: $($Run.Gallery)"
}
