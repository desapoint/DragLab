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
function Get-InterfaceScreenshotGallery {
    param([string]$Root)
    $info=Assert-InterfaceScreenshotPath $Root (Join-Path $Root 'reports/interface-screenshots/run-info') 'reports/interface-screenshots'
    $gallery=Split-Path $info -Parent
    $resolutions=Assert-InterfaceScreenshotPath $Root (Join-Path $gallery 'resolution-tests') 'reports/interface-screenshots'
    New-Item -ItemType Directory -Path $info,$resolutions -Force | Out-Null
    return $gallery
}
function Read-InterfaceScreenshotIndex {
    param([string]$Gallery)
    $path=Join-Path $Gallery 'run-info/images.json'
    if(Test-Path -LiteralPath $path){return @(Get-Content -LiteralPath $path -Encoding UTF8 -Raw | ConvertFrom-Json)}
    return @()
}
function Save-InterfaceScreenshotIndex {
    param([string]$Gallery, [object[]]$Entries)
    ConvertTo-Json -InputObject @($Entries) -Depth 4 | Set-Content -LiteralPath (Join-Path $Gallery 'run-info/images.json') -Encoding UTF8
}
function Get-InterfaceScreenshotImagePath {
    param([string]$Gallery, [string]$Name, [bool]$Alternate)
    if([IO.Path]::GetFileName($Name) -ne $Name -or [IO.Path]::GetExtension($Name) -ne '.png'){throw 'Invalid screenshot filename.'}
    $resolution=if($PSBoundParameters.ContainsKey('Alternate')){$Alternate}else{$Name -match '(?i)(^|[-_])(compact|1080p|4k|2k|1024|1920|3840)([-_.]|$)'}
    $folder=if($resolution){Join-Path $Gallery 'resolution-tests'}else{$Gallery}
    return Join-Path $folder $Name
}
function Test-InterfaceScreenshotResolution {
    param([IO.FileInfo]$File)
    $header=New-Object byte[] 24
    $stream=[IO.File]::OpenRead($File.FullName)
    try {$read=$stream.Read($header,0,24)}finally{$stream.Dispose()}
    if($read -eq 24 -and [Convert]::ToBase64String($header,0,8) -eq 'iVBORw0KGgo='){
        $width=[int64]$header[16]*16777216+$header[17]*65536+$header[18]*256+$header[19]
        $height=[int64]$header[20]*16777216+$header[21]*65536+$header[22]*256+$header[23]
        return $width -ne 1280 -or $height -ne 720
    }
    return $File.Name -match '(?i)(^|[-_])(compact|1080p|4k|2k|1024|1920|3840)([-_.]|$)'
}
function Clear-OwnedInterfaceScreenshots {
    param([string]$Gallery, [string]$Suite)
    $entries=@(Read-InterfaceScreenshotIndex $Gallery)
    foreach($entry in $entries | Where-Object { $_.Suite -eq $Suite }){
        $path=if($entry.PSObject.Properties['AlternateResolution']){Get-InterfaceScreenshotImagePath $Gallery $entry.Image -Alternate $entry.AlternateResolution}else{Get-InterfaceScreenshotImagePath $Gallery $entry.Image}
        if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}
    }
    Save-InterfaceScreenshotIndex $Gallery @($entries | Where-Object { $_.Suite -ne $Suite })
}
function Start-InterfaceScreenshotRun {
    param([string]$Root, [string]$Fixture, [string]$Suite)
    if ($Suite -notmatch '^[A-Za-z0-9_-]+$') { throw 'Screenshot suite must use letters, digits, hyphens and underscores.' }
    $fixturePath = Assert-InterfaceScreenshotPath $Root $Fixture 'reports/player-fixtures'
    $gallery=Get-InterfaceScreenshotGallery $Root
    New-Item -ItemType Directory -Path $fixturePath -Force | Out-Null
    Clear-InterfaceScreenshotImages $fixturePath
    Clear-OwnedInterfaceScreenshots $gallery $Suite
    $run=[pscustomobject]@{Fixture=$fixturePath;Gallery=$gallery;Suite=$Suite;Started=[DateTime]::UtcNow;StatusFile=(Join-Path $gallery ('run-info/'+$Suite+'.txt'))}
    'RUNNING; previous images owned by this suite cleared.' | Set-Content -LiteralPath $run.StatusFile -Encoding UTF8
    'Normal initial-resolution interface tests are at this folder root. Alternate-resolution images are in resolution-tests. run-info contains suite results and image source/date records. Each run replaces its own images and leaves other suites intact. Recorded captures are labelled; they are not a fresh full test.' | Set-Content -LiteralPath (Join-Path $gallery 'README.txt') -Encoding UTF8
    return $run
}
function Complete-InterfaceScreenshotRun {
    param($Run, [bool]$Passed, [string]$Report='', [IO.FileInfo[]]$Images, [switch]$Recorded)
    $files=if($PSBoundParameters.ContainsKey('Images')){@($Images)}else{@(Get-ChildItem -LiteralPath $Run.Fixture -File -Filter '*.png' | Sort-Object Name)}
    $index=@(Read-InterfaceScreenshotIndex $Run.Gallery)
    foreach($file in $files){
        $alternate=Test-InterfaceScreenshotResolution $file
        $path=Get-InterfaceScreenshotImagePath $Run.Gallery $file.Name -Alternate $alternate
        Copy-Item -LiteralPath $file.FullName -Destination $path -Force
        $index=@($index | Where-Object { $_.Image -ne $file.Name })
        $index+=[pscustomobject]@{Image=$file.Name;Suite=$Run.Suite;Source=$file.FullName;CapturedUtc=$file.LastWriteTimeUtc.ToString('o');AlternateResolution=$alternate}
    }
    Save-InterfaceScreenshotIndex $Run.Gallery $index
    $status=if($Recorded){'RECORDED successful historical captures; not a fresh full-interface rerun.'}elseif($Passed){'PASS'}else{'FAILED / INCOMPLETE; these images are from this attempt only.'}
    $text=@("Suite: $($Run.Suite)","Status: $status","Started UTC: $($Run.Started.ToString('o'))","Fixture: $($Run.Fixture)","Screenshots: $($files.Count)")
    if($Report -and (Test-Path -LiteralPath $Report) -and (Get-Item -LiteralPath $Report).LastWriteTimeUtc -ge $Run.Started){$text+=Get-Content -LiteralPath $Report -Encoding UTF8 | Where-Object { $_ -match '^(Checks:|FAIL:)' } | Select-Object -First 2}
    $text | Set-Content -LiteralPath $Run.StatusFile -Encoding UTF8
    $index | Sort-Object Image | Export-Csv -LiteralPath (Join-Path $Run.Gallery 'run-info/images.csv') -NoTypeInformation -Encoding UTF8
    Write-Output "Interface screenshots: $($Run.Gallery)"
}
