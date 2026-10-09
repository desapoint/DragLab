param([string]$FixtureName = ('phase-3h-' + (Get-Date -Format 'yyyyMMdd-HHmmss')), [switch]$ShowPlayer)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
$taskFolder = Join-Path $taskRoot ('reports/player-fixtures/' + $FixtureName)
if (Test-Path -LiteralPath $taskFolder) { throw 'Use a fresh fixture name; preserve existing evidence.' }
New-Item -ItemType Directory -Path $taskFolder | Out-Null
$taskStyle = if ($ShowPlayer) { 'Normal' } else { 'Hidden' }
$taskBase = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','--draglab-verify',('"' + $taskFolder + '"'),'--draglab-economy-integration')
$taskArgs = $taskBase + @('-logFile',('"' + (Join-Path $taskFolder 'stage-player.log') + '"'))
$taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle $taskStyle -PassThru
Write-Output "3H interruption fixture: $taskFolder"
$taskDeadline = (Get-Date).AddSeconds(90)
try {
    while (!(Test-Path -LiteralPath (Join-Path $taskFolder 'recovery-ready.txt'))) {
        if ($taskProcess.HasExited) { throw 'Stage player exited before durable integration checkpoint.' }
        if ((Get-Date) -gt $taskDeadline) { throw 'Stage checkpoint timeout.' }
        Start-Sleep -Milliseconds 100
    }
    if ([IO.Path]::GetFullPath($taskProcess.MainModule.FileName) -ne [IO.Path]::GetFullPath($taskPlayer)) { throw 'Fixture process identity mismatch.' }
    Stop-Process -Id $taskProcess.Id -Force
    $taskProcess.WaitForExit()
} finally { if (!$taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id -Force } }
$taskArgs = $taskBase + @('-logFile',('"' + (Join-Path $taskFolder 'resume-player.log') + '"'),'--draglab-economy-resume')
$taskResume = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle $taskStyle -PassThru
$taskResume.WaitForExit()
if ($taskResume.ExitCode -ne 0) { throw "3H resume failed ($($taskResume.ExitCode)); inspect resume-player.log." }
Get-Content -LiteralPath (Join-Path $taskFolder 'runtime-verification.txt') -Encoding UTF8
