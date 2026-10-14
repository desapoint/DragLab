# Save storage — implemented 2026-10-14

Player progression now uses SQLite. Authored cars, mechanical definitions, compatibility and career defaults remain editable project assets/code. Settings stay in separate driving, audio and unit JSON files; progression reset preserves them.

## Runtime contract

The first launch creates `save-slot-1.sqlite3` in Unity's persistent-data directory. `ProfileStore.UseSlot` supports separate slot IDs; there is no new slot-selection interface in this checkpoint. The simulation and authoritative player profile stay in memory. Physics ticks and preview controls do not query the database.

The store projects ownership, members, markets, reservations, saved builds and progression into identity-addressed records, with evolving detail in JSON payloads. Owners, components and installed slots also have relational indexes with deferred foreign keys. This is not a single compressed profile blob or a database copy of authored content. Purchases and transfers update their changed state records, money, stock and ownership in one transaction; publication follows a successful durable commit.

Schema version (`PRAGMA user_version`), logical profile contract, created/current game build, content revision and physics identity are independent. Ordered migration scripts live in `Game/Assets/DragLab/Resources/Persistence/Migrations`. Applied script checksums are immutable: append a new migration rather than editing a released migration. Existing populated upgrades validate domain state before committing. Unknown future schemas/contracts block writes and reset.

## Import, recovery and reset

Legacy flat and compressed `profile-v1.json` saves import once. Original primary/backup/checkpoint bytes are retained with `.pre-sqlite` copies; logical migration runs against staging copies. Database schema upgrades retain a `.schema-N.bak` original. IDs, wear, faults, balances, fluids, stock seeds and reservations are preserved rather than reconstructed from defaults.

WAL with FULL synchronization makes the primary commit durable. Recovery copies use SQLite's online backup API, with a bounded coalescing worker after normal commits. A forced shutdown can leave the recovery copy behind the latest durable primary; normal shutdown drains it. Backup errors surface without falsely reporting a committed purchase as failed. Corrupt primary files and obsolete WAL/SHM/journal files are quarantined during recovery.

Progression reset writes a durable intent, transactionally replaces state, then records a reset-generation floor. Recovery must never restore assets from before a committed reset. An abandoned pre-commit reset can recover the healthy original state. Settings use their separate recoverable three-file commit and do not rewrite progression.

Race resource checkpoints use a database table and the existing monotonic identity/version validation. A full state commit retires its pending checkpoint inside the same transaction. Interrupted runs retain spent resources/wear without awarding a result or resuming racing.

## Thread and performance boundaries

Unity resources, profile capture/normalization and state publication stay on the main thread. Only captured managed values enter database work; no Unity scene or asset access is moved to the writer. A serialized writer protects SQL operations and commit tokens reject stale writers. Current definitive actions wait for durability before publishing; this does not promise a completely hitch-free interface.

Only changed records are written, and unchanged projections/digests are reused. The current domain transaction still clones the profile and database capture still serializes it once. Eliminating those remaining full-profile costs would be a separate measured optimization, not a consequence automatically supplied by SQLite.

## Platforms and developer verification

Windows x64 Editor/player use the pinned native dependency. Android ARM64 is reproducibly compiled with the installed Unity NDK and 16 KiB load alignment, but APK/device validation remains outstanding. Other player targets are guarded until their native artifact is supplied. Native binaries, plugin metadata, migration scripts and the rebuild tool are versioned; downloaded source archives, test saves, logs and build outputs remain local.

Run `tools/Invoke-Unity.ps1 SqliteStateVerify`, then `BuildPlayerOnly`. Packaged Settings and cart tests exercise production SQLite. Historical editor fixtures that explicitly select a `.json` verification path retain the old store for compatibility and are not evidence of SQLite coverage. See [delivery checks and review](../reports/sqlite-state/CHANGES.md).
