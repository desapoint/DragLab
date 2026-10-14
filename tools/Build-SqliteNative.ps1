param([string]$EditorPath='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskCache=Join-Path $taskRoot 'reports/sqlite-state/dependencies'
New-Item -ItemType Directory -Path $taskCache -Force | Out-Null
function Get-PinnedSqlite([string]$Name,[string]$Url,[string]$Hash) {
    $target=Join-Path $taskCache $Name
    if (-not (Test-Path -LiteralPath $target)) { Invoke-WebRequest -Uri $Url -OutFile $target -UseBasicParsing }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Hash) { throw "SQLite artifact checksum mismatch: $Name" }
    return $target
}
$taskWindows=Get-PinnedSqlite 'windows.zip' 'https://www.sqlite.org/2026/sqlite-dll-win-x64-3540000.zip' 'a548a9b5d8f695e669f71bcc4669fdc8b3e1eb79995761c81a9128262126f71c'
$taskSource=Get-PinnedSqlite 'source.zip' 'https://www.sqlite.org/2026/sqlite-amalgamation-3540000.zip' '68e913b0fe8ec6b4e5f391c594a28872884e80d6cd042a1e50c957be46b5aa4d'
Expand-Archive -LiteralPath $taskWindows -DestinationPath (Join-Path $taskCache 'windows') -Force
Expand-Archive -LiteralPath $taskSource -DestinationPath (Join-Path $taskCache 'source') -Force
$taskPlugin=Join-Path $taskRoot 'Game/Assets/DragLab/Plugins/SQLite'
$taskWinDir=Join-Path $taskPlugin 'Windows/x86_64'
$taskAndroidDir=Join-Path $taskPlugin 'Android/arm64-v8a'
New-Item -ItemType Directory -Path $taskWinDir,$taskAndroidDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskCache 'windows/sqlite3.dll') -Destination (Join-Path $taskWinDir 'draglab_sqlite3.dll') -Force
$taskClang=Join-Path $EditorPath 'Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe'
if (-not (Test-Path -LiteralPath $taskClang)) { throw 'Install the Unity Android NDK module to rebuild ARM64 SQLite.' }
& $taskClang --target=aarch64-linux-android26 -shared -fPIC -O2 -DSQLITE_THREADSAFE=1 -DSQLITE_DEFAULT_FOREIGN_KEYS=1 '-Wl,-z,max-page-size=16384' (Join-Path $taskCache 'source/sqlite-amalgamation-3540000/sqlite3.c') -o (Join-Path $taskAndroidDir 'libdraglab_sqlite3.so') -lm
if ($LASTEXITCODE -ne 0) { throw 'Android SQLite compilation failed' }
Write-Output 'SQLite 3.54.0 binaries rebuilt. Existing Unity metadata/GUIDs were preserved.'
