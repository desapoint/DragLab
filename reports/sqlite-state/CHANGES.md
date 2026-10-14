# SQLite persistence checkpoint — 2026-10-14

## Delivered

Production state now lives in `save-slot-1.sqlite3`, with identity-addressed records, component/member payloads, relational ownership/slot references, transactional deltas and durable publication. Authored definitions remain project content; driving/difficulty, audio and units remain separate files. Slots have an API, not a new UI.

Versioned migration resources record immutable checksums and validate populated upgrades before commit. Legacy flat/compressed saves import from retained staging copies without rewriting originals. Future schema/contract rejection, online recovery copies, quarantined damaged primary/sidecars, reset intent/generation protection, stale-writer tokens and SQLite run checkpoints preserve the prior ownership/resource rules. Copies used for workshop plans retain their save token. Native fixture restoration now drains handles and invalidates row caches before restoring isolated databases.

The writer receives captured managed values. Unity resources/normalization and authoritative publication stay on the main thread. Normal saves schedule a bounded coalesced recovery copy; durable primary success does not depend on a subsequent background copy succeeding. Initial/reset backups are synchronous. No simulation tick or preview writes SQL. Settings confirmation never rewrites progression.

## Executed checks

- `Build-SqliteNative.ps1`: reproduced pinned SQLite 3.54.0 Windows x64 and Android ARM64 artifacts; published archive hashes verified. Android ELF LOAD alignment is `0x4000` (16 KiB). The packaged Windows plugin hash matches the source dependency.
- `SqliteStateVerify`: **205 checks / 0 failures**, all 13 cars. Exact import/reload includes wear/faults, Unicode names, fluids, independent engines, loose members, saved setups, market seeds and nine-race cart holds. Covers ordinary backup advancement, failed/locked purchases, ownership FK rejection, populated migrations/rollback, stale writers, separate Settings, reset interruptions, corrupt WAL quarantine, future schemas, interrupted resource checkpoints and independent slots. [Matrix](verification.txt).
- Current Windows `BuildPlayerOnly`: success with the native SQLite plugin included.
- Packaged native cart: **295 checks / 0 runtime errors**, real keyboard/mouse, actual SQLite save/reload and reserved stock lifecycle. [Evidence](native-cart.txt).
- Packaged native Settings: **145 checks / 0 runtime errors**, all tabs, draft/confirmation/cancel, three-file preference rollback, reset and compact/1080p/4K rendering. [Evidence](native-settings.txt).
- Static source/metadata audit: no missing asset metadata or duplicated Unity GUIDs; required historical migration fixture remains tracked. Native binaries/migrations are included in version control. Generated databases, logs, screenshots, dependency downloads and builds remain local artifacts.

Earlier phase matrices remain historical baselines, not rerun SQLite gates. Explicit `.json` Editor verification fixtures still use the extracted legacy backend; the new matrix and packaged probes use production SQLite. `timing-verification.txt` is an earlier two-model diagnostic, not the final acceptance gate. Counts overlap and should not be summed as unique coverage.

## Measured limits

The final matrix's warm one-component saves wrote **2 state records** each, taking **64–233 ms** on this machine (including capture/validation and durable commit). These are isolated samples, not a percentile benchmark or a claim that every purchase/menu action is faster. The profile is still serialized once for capture, and existing domain transactions still clone it. Definitive actions currently wait for durable commit before publishing. SQLite establishes migration/transaction structure and avoids rewriting unchanged records; further responsiveness work requires measurement of those remaining costs.

Android native compilation/import settings passed; APK/device testing was **not** performed. Other player platforms require a matching native artifact and are guarded. Full career/bench simulation matrices were not rerun for this storage-only increment.

## User review

Launch the current Windows build; verify existing cars, names, money, wear, fluids and saved builds after import/restart. Make a purchase and reload, apply Settings and restart, then test progression reset on a disposable save: preferences should remain and assets should stay cleared. Review UI separately; this infrastructure checkpoint does not approve or advance a Slice 4 page.

See [storage contract](../../docs/SAVE_STORAGE_DIRECTION.md) for schema, import/recovery and thread boundaries.
