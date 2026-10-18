# Career reward pacing — 2026-10-17

Target: the player requested roughly **4–6 clean wins** to afford a useful early $600 upgrade. Opening repeat income is $150 before running costs; $600 takes four gross wins, or five for a $660 express purchase. Fuel and maintenance remain payable.

## Reward changes

| Event | Before repeat net | After repeat net |
| --- | ---: | ---: |
| First pass | $500 | $150 |
| Club run | $700 | $225 |
| Coupe rival | $1,000 | $325 |
| Local final | $1,600 | $450 |
| F qualification / rival | $650 / $650 | $475 / $525 |
| E qualification / rival | $825 / $825 | $575 / $650 |
| D qualification / rival | $1,000 / $1,000 | $725 / $825 |
| C qualification / rival | $1,175 / $1,175 | $925 / $1,050 |
| B qualification / rival | $1,350 / $1,350 | $1,175 / $1,350 |
| A qualification / rival | $1,525 / $1,525 | $1,525 / $1,750 |
| S qualification / rival | $1,700 / $1,700 | $1,975 / $2,250 |

Repeat net deducts entry, before physical running costs. Optional challenges now pay $325 / $425 / $375 net instead of $875 each. Main-path first-clear bonuses are $150–$475 rather than $300–$900, bounded by each event's repeat prize. Free-event consolation is $10; paid consolation refunds at most half the entry, removing positive-net loss farming there. DNF/disqualification rules are unchanged.

The first four wins now pay $1,150 rather than $3,800. First clears of the whole 18-event main path, including bonuses and entry deductions, pay $21,300 rather than $28,650. Later progression is worth more than repeatedly farming the old Local final.

## Preservation and scope

Only future event financial terms change. Balances, parts, resources, wins, REP awards, unlocks, entries, rival hardware, physics and driver behavior are retained. Existing entered-run contracts keep their captured old payout; no save migration or reset is required. Content-creation tools use the same explicit calibration; ordinary loading does not rewrite authored content. The Apply Slower Career Rewards editor action is deliberate, not an automatic asset migration.

`before-matrix.csv` contains the baseline payouts and two actual rival runs per event (42 shared-simulation runs). `after-matrix.csv` contains the new complete financial curve. **Rival pace is not strictly ordered by progression**: F is slower than Local final, and some later qualifiers are slower than preceding rivals. This is recorded as a separate opponent/content calibration issue; rising reward order does not claim monotonically harder physical opponents. No driving guarantees were added.

## Checks

- Reward contracts: **164 passed**. All 21 event defaults, strictly rising main repeat net, bounded first bonuses/consolation, early-upgrade target, captured payout after definition change/reload, duplicate protection and repeat-only cash/REP semantics.
- Expanded career: **308 passed**. Current evidence copied here as `expanded-*`; original 3E archives retained unchanged.
- Asset invariance: **21 passed**, confirms no rival/physics/unlock/entry alterations. Unity serialized omitted zero-default tune fields; they are semantically unchanged.
- Starter diagnostic (`starter-matrix.csv`): six paid hardware configurations with an explicitly diagnostic budget, distinct from funded acceptance. Free optional mass removal and launch tuning can beat the next rival; it is not a guaranteed starter win or player recommendation.
- Full economy scenario: **2,452 passed**, including a fresh paid starter through all 18 main events without injected cash, physical race/shutdown persistence, recovery, repeated losses, failure/replacement, downgrade/reuse, paid rebuild/sale, empty-garage recovery, 13-model disposal margins, per-definition no-arbitrage, fuel-mode matrix, reserved-job roundtrips, stale-resource rejection and disk reload. Current financial/service ledger and concise results are retained here; raw unique run is `reports/phases/3g/validation-f64bba61f6784c13b98c481511c724f2`.
- The former short, no-service funding recipe no longer describes slower income. Verification now keeps original cabin parts after stripping optional mass, buys a gearbox/clutch before power, prices actual stock/delivery quotes, progresses to the better-paying cleared Coupe rival, and budgets rank-2 tires on the second car. It buys fuel as needed and pays/waits for a preventive rebuild below 88% relevant engine condition when affordable. The passing route bought a starter for $4,012 and its second car for $11,250, paid one $420 preventive rebuild, retained actual parts/condition/resources, and finished the main path at REP 68. Its recorded clock includes service waits; it is not measured human play time or a mandatory shopping recipe. Abandoned diagnostic runs are retained locally and are not counted as passing evidence. No production wear/physics rule was relaxed.
- Final Unity 6000.6.4f1 Windows development build succeeded at `Game/Builds/Windows/DragLab.exe`; hashes are in `build-manifest.txt`.
- Packaged native Race selection: **53 / 0 runtime errors** on the final build. Authoritative fees/prizes, captured entry/reload, blocked/stale/durable entry, F8/Settings and compact/1080p/4K bounds pass. The first-build pass was repeated after the verification-specific page/timeout correction; the recorded evidence is the final run.
- Final packaged recovery/race/purchase/job: **10 scenario assertions / 0 runtime errors**, plus common control/finish checks. Uses real 60-second recovery, three $150 wins, actual stock/express-inclusive purchase, retained original, disk reload and real job wait. The older probe expected fixed $500 rewards and legacy controls despite the new interface default. It now uses captured/authored payouts, actual quotes, explicitly selects legacy Parts/Business only in this isolated probe, and allows 300 seconds for its longer real-time sequence. The initial missing-control run is retained locally and excluded from pass counts.
- Final packaged first/repeat payout check: **6 scenario assertions / 0 runtime errors**, plus common control/finish checks. Both first and repeat $150 wins retain first-only REP and exact balances/outcomes/resources through actual SQLite reload.

Independent ledger continuity check passes across 406 fresh-route actions: $4,700 opening cash plus recorded deltas equals the $13,496 ending balance, including the later $20 garage job. The route includes 118 races (116 wins / 2 losses); it is an illustrative conservative build route, not a required or optimized completion count.

Unrelated pre-existing files were preserved: `reports/maintenance-batch/verification.txt`, `reports/shop-inventory/cart-verification.txt`, `Tests.ps1`, `Game/Assets/Resources.meta` and `reports/race-exit-regression/`. Historical 3E reports and two tracked 3G diagnostics were backed up/copied as appropriate and restored after rerunning their verifiers. Renderer/font serialization noise was verified against the known build-only deltas, backed up locally and removed.

No later interface, debug-bench rework or phase is advanced. Human pacing and visual acceptance remain user review. Review: first-pass $150 and later net prizes in Race selection; a normal $600 purchase after roughly 4–6 clean opening wins; repeat wins without repeat REP/bonuses; earned cash and entered old contracts retained. Rival ordering remains a separately scoped content recommendation.

Technical checkpoint complete. Review pacing in play; user acceptance is separate from automated checks.
