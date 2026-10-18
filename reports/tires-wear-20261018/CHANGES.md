# Tire choice and persistent wear — 2026-10-18

Requested checkpoint: more usable traction for upgraded/boosted cars, broader tire choices and meaningful consumable wear. Approved target: 40–60 clean quarter-mile passes for normal performance tires at Full wear. No later interface phase is advanced.

## Findings and changes

- The previous strongest compound offered 1.36× grip. Six new fixed compounds per model extend the catalogue to eleven choices, up to 3.35×. Grip, life, heat requirements and cost are separate tradeoffs. Existing IDs remain valid. [Current compound table](../../docs/TIRE_COMPOUNDS_LIFE.md).
- Reference rolling lives previously ranged from 5,000 to 65,000 km. That made rolling wear negligible in a drag career. Future use now consumes game-calibrated reserves: normal drag compounds have 30–40 km reference life, with higher-life street/touring and shorter-life qualifying alternatives. Sliding-work, pressure, load, size and heat still change actual life.
- Some small mechanical wear deductions disappeared when subtracting directly from float health. Existing persisted double remainders now retain every unrepresented loss, including valvetrain/service wear. No arbitrary global engine-damage increase. None/Partial/Full retain 0/25/100% rates and the bench retains its multiplier.
- The shared stat ranges now fit the new grip/life values. Existing stock, individual tire positions, axle selections, cart fees, ownership, mixed compounds, saved wear and inventory reuse remain authoritative.
- Existing saved markets/reservations are preserved; new definitions join the normal seeded refresh after three eligible races. No new stock guarantee or market shortcut. Current consumed-life fractions and faults are retained, with no retroactive wear invented.
- Content revision 7 / physics model v21. Older input recordings need a new capture. Known v18–v20 interrupted-run resource journals retain monotonic resource recovery. No database schema migration is required.

## Measured results

- The expanded after matrix completed **67/67 quarter-mile cases**, covering five reference models/all eleven compounds, stock clean passes and copies of three owned builds. The original matrix has 33 cases. Both include actual shutdown wear and shared contact/heat/drive behavior. Per-gear spin is measured above 5 m/s with more than 15% absolute driven-wheel slip; the metric does not count launch creep.
- At approximately **651 HP**, the previous club slick recorded **1.66 seconds** of third-gear-and-up spin and an **11.75 s** quarter-mile. The medium competition slick recorded **0 seconds** in those gears and **10.56 s**, with peak engine output within 0.1%. The pro radial also removed that higher-gear spin. Wheelspin is not universally disabled.
- In the copied **541 HP owned V8**, existing worn club slicks ran **13.47 s**; fresh club slicks ran **12.48 s** with second-gear spin; pro radials ran **12.21 s** without the measured powered spin. Other installed wear/resources were retained. These are isolated diagnostic purchases, not changes to the user's save.
- Controlled fully cooled V8 tire endurance reached the first failed tire on **pass 45** (club slick) and **pass 51** (medium slick). The same four tire identities and histories were retained through each pass and shutdown. Fresh non-tire fixture hardware isolates compound life; this test does not claim an unrepaired engine was refreshed during career play.

## Verification

The shared regression matrix passes: tire/contact **494**, tire failure **5**, OEM durability **114**, resource checkpoints **672**, service/fluids **1,917**, individual engine/clutch **879**, healthy OEM/parts compatibility **102**, reward contracts **164**, funded economy **2,537**, and mixed axle/fitment/ownership **541**. Counts overlap; see `regressions.tsv`.

The final focused progression matrix passes **202** checks, including all thirteen models' authoring, 25%/None wear, tiny-loss serialization, new compound rear-unit cart purchases, exact retained fronts/removed wear and known v18–v20 journal recovery. Unknown models remain rejected.

The funded route completed **125 races / 123 wins / 2 losses**, owned two cars and finished with **$13,643 / REP 68**. It paid **$1,800** for a same-spec replacement tire set and **$423** for a preventive engine rebuild, alongside upgrades, fuel and delivery costs. The worn tire set remained in inventory at about 25% life. See `career-ledger.csv`. No external cash injection, wear reset or waived operating costs were used in that route.

An OEM calibration fixture now disables wear only while comparing healthy authored/resolved output; it keeps the original one-step/time and displayed-speed bounds and exact resolved-repeat determinism. Full-wear durability remains separately enforced. New diagnostic fixture errors (checkout bypassing the real ledger transaction; a clutch-loss check initially selecting a torque-converter automatic) were corrected rather than changing game behavior to satisfy them.

Final endurance/coupling checks pass **110/0**. In the manual V8 fixture, the worn stock clutch transmits **0.89 MJ** over eight seconds versus **2.22 MJ** with the upgraded healthy clutch, while dissipating **0.63 MJ** versus **0.086 MJ** as clutch heat. The check bounds output by engine work plus initial rotational energy. The automatic test correctly uses a torque converter instead of that clutch-loss path.

The Unity 6000.6.4f1 Windows development build passed; `Game/Builds/Windows/DragLab.exe` and its accompanying folder are updated. `build-manifest.txt` records file hashes. Packaged purchase checks pass **293/0**, covering exact single-corner tires, new/used kit rules, transaction persistence, native controls and compact/1080p/4K layouts. Packaged stored-tire selection/swaps pass **57/0** and broken/threshold maintenance → cart → checkout passes **80/0**, including exact stock/express disclosures, retained removed wear and excluded components. Fresh review captures are in `reports/interface-screenshots`; alternate resolutions remain under `resolution-tests`. Raw logs, screenshots and copied save fixtures remain local. Historical phase reports are restored after archiving their current verifier output locally.

Re-run the focused matrix with:

```powershell
./tools/Invoke-Unity.ps1 ValidationBatch -BatchManifest './reports/tires-wear-20261018/validation.tsv' -LogPath './reports/unity-tire-progression.log'
```

The optional owned-build rows read only the already-copied fixture at `reports/player-fixtures/tires-wear-20261018/live-save.sqlite3`; they do not access or alter the real save. The main simulation, purchasing, precision, migration and endurance checks do not require that fixture.

## Scope and limits

The compound factors and lifespan are fictional game calibration, not manufacturer claims. Tire life tests isolate retained tires from fresh non-tire fixture hardware; the separate funded-career test charges actual maintenance and retains all owned conditions. Cold starts, burnouts, sustained spin, hard braking and unsuitable setups can shorten life. Better tires do not guarantee traction at unlimited torque, and healthy engine internals are not forced to fail during one career.

## Review checkpoint

Technical implementation, regression checks and Windows build are complete. Subjective grip/wear pacing remains for player review. Compare the new compounds in Build, try a boosted launch with an appropriate radial/slick, and review the declining per-corner condition over repeated Full-wear runs. The current markets keep their normal refresh schedule. F8, existing settings and the debug bench remain intact. Stop here; no later UI phase is advanced.

Unrelated pre-existing work remains untouched: `reports/maintenance-batch/verification.txt`, `reports/shop-inventory/cart-verification.txt`, `Tests.ps1`, `Game/Assets/Resources.meta`, `.codex-remote-attachments/` and `reports/race-exit-regression/`. Historical verifier output and verified Unity build-only serialization noise are preserved locally and restored to their original tracked contents.
