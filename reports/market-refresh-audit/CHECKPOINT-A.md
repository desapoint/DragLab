# Market refresh responsiveness — Checkpoint A (measurement only)

**Scope:** opt-in measurement; no changes to market selection, stock, RNG, shop UI, settlement scheduling or save semantics. No new runtime cache or worker.

The existing `FinishPacingOnly` test intentionally blocks the SQLite writer for half a second; its duration is **not** a normal settlement baseline. This checkpoint adds `MarketBaselineOnly`, a packaged Windows development-build test using a disposable player fixture, with three unblocked runs:

- Practice race (no paid market refresh)
- Ordinary paid race (no stock refresh)
- Third paid race (new/used parts, engines and vehicles refresh)

It records finish-to-commit wall time, results appearance latency, pending frame count, max/p95 frame spacing, worst single settlement `MoveNext`, camera displacement, and time to show/open a browsable shop. Wall-span telemetry separately measures clone, after-race stock generation including iterator gaps, checkpoint writer wait, main-thread save dispatch and background save task completion. Existing `PartsMarket.Measurement` samples are reported with counts and maximums. **Do not interpret all those samples as CPU totals**: older multi-yield stage spans include Unity frame gaps, whereas `delivery/*` and `wear/preparation CPU total` are CPU-only.

The command below runs the actual packaged player and writes `reports/player-fixtures/<name>/verification.txt` and `frame-pacing.csv`, not the Editor-only market synthetic benchmark:

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action BuildWindows
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-20261019-a
Select-String -Path .\reports\player-fixtures\market-baseline-20261019-a\verification.txt -Pattern 'MEASURE: market baseline','FAIL:','Checks:'
```

Close the Unity Editor before a batch-mode build. Choose a fresh fixture name for every repeat. Compare the three runs, then run the existing held-write pacing test as an independent durability / smoothness gate:

```powershell
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-held-write-20261019-a
```

**Acceptance before Checkpoint B:** zero runtime/verification failures; record third-race finish-to-commit elapsed and pending frames, worst frame/settlement step, shop visibility and comparison ready times, and whether the real results/slip stays smooth in the player. Re-run once for a warm-catalog comparison if possible. Leave the current controls and timing behaviour unchanged until these measurements are reviewed.

**Known limits:** frame spacing measured in the coroutine also reflects scheduler pacing and probe overhead, not solely market CPU. The test necessarily starts with automatic controls and an isolated starter. Shop timing begins **after** settlement to establish a baseline; simultaneous shop navigation during pending settlement belongs to Checkpoint E. Physical Android review belongs to Checkpoint G. No Unity checks were run in the connected GitHub environment.
