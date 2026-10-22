param(
    [string]$Cases='03-i4-turbo-hiss,08-v8-carb,09-v8-supercharger,17-v8-burnout,26-launch-hard-ignition',
    [string]$Output='reports/unity-audio-reference-20261021',
    [string]$InputFolder='reports/race-audio-audition',
    [switch]$Rebase,
    [switch]$SkipCapture,
    [switch]$SkipAnalysis
)
$ErrorActionPreference='Stop'
$audioRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$audioOut=if([IO.Path]::IsPathRooted($Output)){$Output}else{Join-Path $audioRoot $Output}
$audioInputFolder=if([IO.Path]::IsPathRooted($InputFolder)){$InputFolder}else{Join-Path $audioRoot $InputFolder}
if(-not $SkipCapture){
    $audioCommit=(& git -C $audioRoot rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify the source revision.'}
    $audioCli=Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
    $audioCaptureArgs=@('-nographics','-buildTarget','Win64','-executeMethod','DragLab.Editor.UnityAudioReferenceExport.Run','--draglab-reference-output',$audioOut,'--draglab-reference-input',$audioInputFolder,'--draglab-reference-commit',$audioCommit,'--draglab-audio-case',$Cases)
    if($Rebase){$audioCaptureArgs+='--draglab-reference-rebase'}
    & $audioCli run (Join-Path $audioRoot 'Game') --timeout 1800 --log-file (Join-Path $audioRoot 'reports/unity-audio-reference-export.log') --no-tail -- @audioCaptureArgs
    if($LASTEXITCODE -ne 0){throw 'Unity reference capture or numeric replay verification failed.'}
}
& node (Join-Path $PSScriptRoot 'audio-workbench/verify_unity_reference.cjs') $audioOut $Cases
if($LASTEXITCODE -ne 0){throw 'Browser reference replay verification failed.'}
& node (Join-Path $PSScriptRoot 'audio-workbench/verify_reference_ui.cjs') $audioOut
if($LASTEXITCODE -ne 0){throw 'Reference UI/worklet contract checks failed.'}
if(-not $SkipAnalysis){
    & python (Join-Path $PSScriptRoot 'audio-workbench/analyze_unity_reference.py') $audioOut
    if($LASTEXITCODE -ne 0){throw 'Signal analysis failed; see docs/UNITY_AUDIO_REFERENCE.md for dependencies or use -SkipAnalysis.'}
}
& node (Join-Path $PSScriptRoot 'audio-workbench/package_unity_reference.cjs') $audioOut
if($LASTEXITCODE -ne 0){throw 'Reference review package failed.'}
Write-Output "Unity/browser reference review: $(Join-Path $audioOut 'index.html')"
