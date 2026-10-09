$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer = Join-Path $taskRoot 'Game/Builds/Windows/DragLab.exe'
if (-not (Test-Path -LiteralPath $taskPlayer)) { throw 'Build Windows first with Invoke-Unity.ps1 -Action BuildWindows.' }
# Running this launcher explicitly requests an interactive sound bench.
Start-Process -FilePath $taskPlayer -ArgumentList '--draglab-engine-bench'
