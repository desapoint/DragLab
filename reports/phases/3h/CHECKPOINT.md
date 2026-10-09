# 3H source and Windows checkpoint

The [full Slice 3 audit](../../SLICE_3_AUDIT.md) records acceptance status, exact reruns, fixes and remaining human/device work. Archives are created after the complete domain/Windows gate manifests pass. The code checkpoint for the thermal extension is `29b83c5`; the final acceptance/documentation commit is recorded in local Git.

Run the current player at `Game/Builds/Windows/DragLab.exe`. For another machine, extract the entire `Windows-checkpoint-final.zip`, then launch `Windows/DragLab.exe` with every adjacent file and data folder retained. Copying the EXE alone is insufficient.

`source-checkpoint-final.zip` preserves repository-relative paths. Extract it to a new folder and add its `Game` directory as a project in Unity Hub, using Unity 6000.6.4f1. It contains source, Unity metadata/settings/packages, development tools, design/review evidence and the required legacy migration fixture. Unity caches, built players, raw test saves/logs/media, earlier archives and the Git database are excluded. The source archive does not replace local Git history or the player's own save.

Use `tools/Save-PhaseCheckpoint.ps1 -Phase 3h -Revision final` once; existing archives are intentionally immutable. `tools/Verify-SliceThreeCheckpoint.py --revision final` checks exact inventory, CRC and every payload against the current source/player, including the compiled gameplay assembly. The resulting `checkpoint-integrity.txt` records SHA-256 hashes and is external to the archives to avoid self-referential hashing.

The earlier unnamed 3H archives are retained as the first packaging attempt and are superseded by the named final revision. Two stale handover sentences were corrected before the final source package. No previous archive was overwritten.
