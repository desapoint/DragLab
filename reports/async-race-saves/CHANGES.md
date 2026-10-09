# Asynchronous race checkpoints — 2026-10-09

Approved after the tire-feedback pacing audit. The driving loop now prepares only a copied snapshot of the active car, rather than cloning, normalizing and writing the complete garage ledger every simulated second.

## Behavior and durability

- The existing 120-tick cadence remains, with an immediate queued snapshot when a new component/engine/driveline failure is detected. Fuel, fluids, component and individual-element condition, heat and the UTC thermal clock are retained. No simulation, clutch, tire, wear or fixed-step calculation changes.
- One background worker serializes the copied managed data, calculates a SHA-256 integrity checksum, flushes it and atomically publishes `profile-v1.json.run`, retaining its previous checkpoint as `.run.bak`. The journal contains only the active car's condition/resource data and run/base revision identity. There is at most one pending snapshot; a newer complete snapshot replaces it, preserving accumulated damage. Snapshot mutation, tick rewind, refills and healing between queued snapshots are rejected.
- The full profile remains at the latest full transaction boundary during ordinary driving. Full saves still occur at race entry, result settlement, pause, garage/retry/quit and explicit settings or garage transactions. They drain pending work, merge applicable journal state, publish the full profile durably and only then retire the journal. An older worker cannot overwrite a result or restore pre-reset ownership. Startup worker/serializer initialization is paid at entry, and a pending preference-save timer is cleared after that successful entry save.
- Loading validates the checksum, schema/model, base revision, run/vehicle/part identities and complete resource/condition history before applying the journal. An interrupted load retains the latest durable condition and closes the run without payout or race-position resumption. A corrupt primary journal falls back to its valid backup. A stale journal cannot reopen a settled run or attach to a reset/new vehicle identity.
- Checkpoint validation now completes before modifying any part, so a late-invalid component cannot leave earlier components changed. Overlay receipts retain a valid ledger revision; journal ticks do not create separate full-ledger transaction revisions. Full-save merge and ordinary settings-save paths are covered explicitly.
- A disk-write failure is exposed to the race interface while the prior durable save and live simulation remain available. Subsequent checkpoints can retry; a full boundary can persist the latest authoritative state. Reset drains and retires outstanding work while preserving driving/audio preferences. Cleanup failures cannot roll back a full commit already published to disk.

## Measured result

Same rendered fixture workflow: healthy held-throttle driving for 12 real seconds with one owned car, then eight. The first ten rows of each phase exclude entry boundaries. The matrix retains the previous tire-audit baseline and the final new capture.

| Owned cars | Previous checkpoint median / worst | New main-thread snapshot median / worst |
| --- | --- | --- |
| 1 | 44.50 / 54.65 ms | 0.316 / 1.050 ms |
| 8 | 190.66 / 201.41 ms | 0.319 / 0.343 ms |

No frame exceeded 33.3 ms in either final driving window; worst frames were 7.36 / 7.26 ms. Render/presentation cadence differed between captures, so these are scoped checkpoint-cost results, not a claim that this change doubles FPS. The writer's durable I/O is outside the main-thread scope. Entry/result/pause/exit saves may still block at their explicit boundaries. Allocation/GC pressure from the existing physics loop remains; it was not optimized by this increment.

An initial exploratory new-build capture found a 40.99 ms first-task initialization cost. Prewarming at the entry boundary removed that driving hitch in the final capture. Both captures are retained locally; only the final capture is acceptance evidence.

## Verification

- `Invoke-Unity.ps1 -Action CheckpointVerify`: **417 checks passed** across eight car definitions and separate I/O/reset fixtures. Coverage includes immutable snapshot copies, ordered/coalesced ticks, complete resource/wear/heat restoration, queued-history protection, checksum fallback, interrupted closure, stale-journal rejection, exit while a write is pending, atomic validation, write failure/retry, ordinary settings-save merge/revision invariants and reset.
- Final Windows `BuildPlayerOnly`: passed.
- Final rendered race pacing: one/eight-car held-throttle workflows passed, zero runtime errors (`async-saves-pacing-final`).
- Forced process termination **without a pause/full condition save**: the full profile remains at entry while the journal preserves real transmitted-torque shaft failure and fuel consumption. Restart closes it without reward, a real repair purchase retains the failed original, and the replacement completes a subsequent practice pass. Zero runtime/GUI/DSP errors (`async-saves-journal-crash-final`).
- Combined paid-career/reservation/pause/forced-restart/repair/job-claim/retry workflow passed with one valid retry reward and ownership validation, zero runtime/GUI/audio errors (`async-saves-economy-final`).
- Previous giant Slice 2/3 mechanical/economy matrices remain historical baselines; they were not rerun for this focused persistence change. Existing phase ZIPs remain unchanged.

Raw fixture saves, journals, timing CSVs, screenshots and logs remain local and ignored. Concise results and the before/after matrix are versioned here. The current runnable player is `Game/Builds/Windows/DragLab.exe`.

## Practical limits and remaining performance work

An abrupt crash retains the latest **durable** snapshot; a snapshot still in flight cannot be guaranteed. Normal quit/pause/exit drains the worker and saves current state. Under slow I/O the bounded pending queue may coalesce intermediate snapshots, increasing the interval represented by the last durable checkpoint. Checksummed journals require their corresponding entered base profile/run; a journal cannot reconstruct an independently corrupted/missing entire base ledger or safely attach to an unrelated backup. Existing full-profile backup recovery remains available.

Bench CSV/JSON export, graph-history reduction, hot-loop allocations and the 100 ms catch-up clamp are unchanged and remain separate follow-up work from the earlier performance report. The physical bench still does not write player resources. Android/device and extended slow-disk acceptance have not been claimed.

Reproduce with fresh fixture names:

```powershell
./tools/Invoke-Unity.ps1 -Action CheckpointVerify
./tools/Invoke-Unity.ps1 -Action BuildPlayerOnly
./tools/Test-WindowsSlice.ps1 -PacingOnly -FrameProbe -ShowPlayer -FixtureName <fresh-pacing>
./tools/Test-InterruptedRun.ps1 -ShowPlayer -FixtureName <fresh-crash>
./tools/Test-EconomyInterruptedRun.ps1 -ShowPlayer -FixtureName <fresh-career>
```
