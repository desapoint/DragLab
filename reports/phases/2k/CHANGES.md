# 2K mechanical condition and run recovery

2026-10-08. Reconciled scope: `docs/SLICE_2_PLAN_RECONCILIATION.md`. Physics remains the shared cylinder/service model v14; this increment connects owned-run persistence rather than replacing its stress solver.

## Implementation

- Every real race captures a unique run ID and the actual owned vehicle ID. Periodic checkpoints retain all installed component identities/conditions, individual engine faults, tire conditions, dedicated clutch fluids, fuel and oil/coolant histories.
- Checkpoints commit through the existing candidate/revision/validation/atomic-save transaction. Start, new critical failures, pause/focus loss, finish, garage abandonment, retry and quit use this boundary. Shutdown driving also advances wear and checkpoints. A failed write leaves the candidate unpublished and the live race available for retry.
- Normal progress saves every 120 physics ticks (one simulated second); critical changes save at the end of their Update frame. A forced termination can lose progress since the last durable checkpoint: up to one simulated second plus a capped render-step interval. Disk writes are not promised every tick. Graceful boundaries flush immediately.
- Startup closes an interrupted receipt, retaining committed damage/resources without resuming a race or adding cash/records. Ordinary save inspection does not close a live run. Receipts keep metadata; owned components retain the full condition trees, avoiding repeated historical copies.
- Reject stale/duplicate/settled, wrong-model, incomplete/foreign and healing/rewinding checkpoints. Optional `hasFuel` preserves earlier condition-only checkpoint fixtures. History comparisons tolerate JSON's sub-ULP decimal round-trip differences; health/failure repair remains rejected.
- Pause-menu Garage now uses the same cooldown/save/closure as other navigation. Cooldown is applied once if a failed write is retried.

## Verification evidence

- `RecoveryVerify`: 185 targeted checks: 148 recovery cases plus existing ownership/inventory checks. Four-car real pull/shift/lift/coast/brake timelines commit every second, reload, close, reject forged resources/metadata/healing, retry failed writes and retain failed originals after replacement. `recovery-matrix.csv` records per-component definitions, condition, work/stress/heat and a causally overloaded shaft failure.
- Native `phase-2k-forced-recovery`: 12 checks, zero runtime/GUI/DSP errors. Host kills only its exact isolated player process after a real shaft failure, skips quit callbacks, relaunches, checks retained fuel/oil/IDs/no payout, clicks replacement and completes a practice pass.
- Existing stock, component service, individual-cylinder/clutch, brake/drive/AWD isolation, wear difficulty and destructive replay suites remain the mechanical evidence; the final 2L gate reruns them with the full current model.
- The general native regression exposed outdated fixture lifecycle assumptions. Fixtures now close the previous race before hardware changes, initialize new preview ownership and wait for an exact physical stop instead of ending their wait at a small nonzero speed. No production stopping assertion was relaxed.

General native gameplay `phase-2k-gameplay-accepted` passes automatic career settlement, manual/sequential/clutch driving, pause, braking/shutdown, records/settings/reload, practice and red-light denial with zero runtime errors. Source and complete Windows player are archived in this phase folder before 2L integration. The copied foundation report is the earlier 5,242-check baseline; the final 2L full gate includes the new 148 recovery checks. Human durability calibration is not measured OEM life. Prices, rebuild timing, random faults, nitrous and full economy remain their documented scope.
