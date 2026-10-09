param([string]$FixtureName='phase-2k-interruption',[switch]$ShowPlayer)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer=Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
$taskFolder=Join-Path $taskRoot ('reports/player-fixtures/'+$FixtureName)
if(Test-Path -LiteralPath $taskFolder){throw 'Use a fresh fixture name; existing evidence will not be overwritten.'}
New-Item -ItemType Directory -Path $taskFolder | Out-Null
$taskStyle=if($ShowPlayer){'Normal'}else{'Hidden'}
$taskBase=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','--draglab-verify',('"'+$taskFolder+'"'))
$taskStageArgs=$taskBase+@('-logFile',('"'+(Join-Path $taskFolder 'stage-player.log')+'"'),'--draglab-recovery-stage')
$taskProcess=Start-Process -FilePath $taskPlayer -ArgumentList $taskStageArgs -WindowStyle $taskStyle -PassThru
Write-Output "Interruption fixture: $taskFolder"
$taskDeadline=(Get-Date).AddSeconds(60)
try{
    while(!(Test-Path -LiteralPath (Join-Path $taskFolder 'recovery-ready.txt'))){
        if($taskProcess.HasExited){throw 'Stage player exited before durable failure checkpoint.'}
        if((Get-Date)-gt $taskDeadline){throw 'Stage checkpoint timeout.'}
        Start-Sleep -Milliseconds 100
    }
    if([IO.Path]::GetFullPath($taskProcess.MainModule.FileName)-ne [IO.Path]::GetFullPath($taskPlayer)){throw 'Fixture process identity mismatch.'}
    Stop-Process -Id $taskProcess.Id -Force
    $taskProcess.WaitForExit()
}finally{if(!$taskProcess.HasExited){Stop-Process -Id $taskProcess.Id -Force}}
$taskResumeArgs=$taskBase+@('-logFile',('"'+(Join-Path $taskFolder 'resume-player.log')+'"'),'--draglab-recovery-resume')
$taskResume=Start-Process -FilePath $taskPlayer -ArgumentList $taskResumeArgs -WindowStyle $taskStyle -PassThru
$taskResume.WaitForExit()
if($taskResume.ExitCode-ne 0){throw "Recovery player failed ($($taskResume.ExitCode)); read resume-player.log."}
Get-Content -LiteralPath (Join-Path $taskFolder 'runtime-verification.txt')
