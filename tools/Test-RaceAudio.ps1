param([switch]$SkipPrepare,[switch]$SkipBuild,[switch]$VerifyOnly,[string]$Case="")
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject=Join-Path $taskRoot 'Game'
$taskCli=Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
$taskReport=Join-Path $taskRoot 'reports/race-audio-audition'
if(-not $SkipPrepare){
    & $taskCli run $taskProject --timeout 1800 --log-file (Join-Path $taskRoot 'reports/race-audio-live-prepare.log') --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.RaceAudioAuditionVerification.PrepareInputs
    if($LASTEXITCODE -ne 0){throw 'Race input generation failed. See race-audio-live-prepare.log.'}
}
if(-not $SkipBuild){
    & $taskCli run $taskProject --timeout 1800 --log-file (Join-Path $taskRoot 'reports/race-audio-live-build.log') --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PlayerBuild.BuildPlayerOnly
    if($LASTEXITCODE -ne 0){throw 'Windows build failed. See race-audio-live-build.log.'}
}
$taskPlayer=Join-Path $taskProject 'Builds/Windows/DragLab.exe'
$taskFixture=Join-Path $taskRoot ('reports/player-fixtures/live-audio-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
[IO.Directory]::CreateDirectory($taskFixture)|Out-Null
$taskInputs=$taskReport
if($Case){
    $taskSelected=@(Get-ChildItem -LiteralPath $taskReport -Filter '*.race.json' -File | Where-Object {$_.Name.StartsWith($Case,[StringComparison]::OrdinalIgnoreCase)})
    if(-not $taskSelected.Count){throw "No audio configuration starts with $Case"}
    $taskInputs=Join-Path $taskFixture 'inputs'
    [IO.Directory]::CreateDirectory($taskInputs)|Out-Null
    foreach($taskInput in $taskSelected){Copy-Item -LiteralPath $taskInput.FullName -Destination $taskInputs}
}
$taskArgs=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+(Join-Path $taskFixture 'player.log')+'"'),'--draglab-verify',('"'+$taskFixture+'"'),'--draglab-audio-race-test',('"'+$taskInputs+'"'))
if($VerifyOnly){$taskArgs+='--draglab-audio-test-check'}
# A visible game and real AudioSources, never WAV/browser playback.
$taskProcess=Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Normal -PassThru
Write-Output "Live game PID: $($taskProcess.Id)"
Write-Output "Isolated test files: $taskFixture"
if($VerifyOnly){
    if(-not $taskProcess.WaitForExit(240000)){$taskProcess.Kill();throw 'Live audio verification timed out.'}
    $taskResult=Join-Path $taskFixture 'live-verification.txt'
    if($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskResult)){throw "Live audio verification failed: $taskFixture"}
    Get-Content -LiteralPath $taskResult -Encoding UTF8
    if(-not (Select-String -LiteralPath $taskResult -Pattern 'PASS all')){throw 'Live suite did not complete.'}
    if(-not $Case){Copy-Item -LiteralPath $taskResult -Destination (Join-Path $taskReport 'live-verification.txt')}
}else{
    Write-Output 'Playing all configurations at normal speed. Engine-only / Tires-only selected automatically. Mixed, Stop/Resume and Next car are available; Esc > Sound has independent category sliders.'
}
