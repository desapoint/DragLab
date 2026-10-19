# Checkpoint F.3 — Durable-save stage attribution

The third paid-race result-to-commit time fell to ~1.61 s at F.2, of
which 889.925 ms was labeled `save task and completion`. That wall span
includes task dispatch, background serialization, record projection, SQLite
delta work, commit, thread scheduling, and resume on a subsequent frame.

## New scoped diagnostics

Only the packaged market baseline opts into the new validated-save trace.
The writer owns a private trace object until its task completes; it never
calls the Unity UI/test reporter. `CompleteValidatedSave` replays the
trace on the main thread after durable publication. A failing diagnostic
callback cannot roll back or retry an already committed save.

The trace separately reports:
- Worker dispatch wait, whole worker elapsed.
- Full-profile serialization CPU/wall and JSON character/UTF-8 byte size.
- Ownership/relational projection and owner/component/slot counts.
- SQL writer gate wait, JSON-to-state-record projection, projected counts.
- SQLite open/schema checks, delta index, previous state-record count.
- Updated/inserted/deleted/unchanged state counts and state-row SQL.
- Ownership/slot delta SQL.
- Integrity digest, metadata, injected test precommit wait, actual
  `COMMIT` duration, and backup scheduling.

`PRAGMA synchronous=FULL`, WAL, all transaction boundaries, integrity
hashes, backup cadence, optimistic commit tokens, SQLite schema and record
contents are unchanged. No user-facing indicator, new UI, or market RNG
changes are included. The trace is observational and does **not** identify
which stage dominates until local player data is collected.

## Local gates

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-savedetail-f3-20261019-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-f3-20261019-a
```

Read the third-paid `SQLite save` stages in the packaged market baseline.
Compare their sum to `entire save worker`, and then compare the latter
to `wall save task and completion` to isolate scheduling/pacing overhead.
If the held-write fixture runs, its injected wait is intentionally excluded
from the measured `durable commit` phase.

Acceptance: successful Unity compile, exactly-once rewards,
durable/reload/held-writer parity, zero runtime exceptions, and rows/identity
unchanged. Decide whether to optimize JSON serialization, row projection,
SQL delta, integrity computation or waiting *based on the new measurements*.
The GitHub connector cannot run local Unity player tests.
