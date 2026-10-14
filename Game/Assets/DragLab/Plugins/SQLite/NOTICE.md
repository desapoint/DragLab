# SQLite native dependency

SQLite 3.54.0 is public-domain software: https://www.sqlite.org/copyright.html . Upstream downloads: https://www.sqlite.org/download.html . No separate database server or user installation is required.

Windows x64 uses the official `2026/sqlite-dll-win-x64-3540000.zip`, with its DLL renamed to `draglab_sqlite3.dll`. Published SHA3-256: `f5fb500c73a3afd89976386bb8e4fdc8b5b65c052e187fb1ada1f5de034cd45b`.

Android ARM64 is built from official `2026/sqlite-amalgamation-3540000.zip`. Published SHA3-256: `7b670a62fdfbd672b75fef004cb703c8a3e87d3a5cc7d675b4a08337004a2d93`. Both downloaded archives were verified against these hashes before use. The reproducible rebuild script pins their SHA-256 hashes as well.

Rebuild with `tools/Build-SqliteNative.ps1`, using the installed Unity Android NDK. Android targets API 26, position-independent code, thread safety, default foreign keys and a 16 KiB maximum page alignment. Plugin import is restricted to Windows x64 Editor/player and Android ARM64. Other platform targets require their native SQLite artifact before building; the build guard reports this explicitly. Android compilation is distinct from APK/device verification.

Retain these native binaries and their stable Unity metadata in source control. Download/extraction outputs under reports are local artifacts. SQL migrations are authored project files in Resources/Persistence/Migrations, not default player databases.
