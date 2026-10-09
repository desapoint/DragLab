# Slice 3 full audit — 2026-10-09

Status: **3A–3H developer acceptance passed.** All 13 domain gates and 18 Windows workflows passed. The thermal implementation is committed as `29b83c5`; the final evidence/tools/checkpoint commit follows it locally.

## Scope and retained requirements

The [requirement map](../docs/SLICE_3_ACCEPTANCE.md) covers 3A–3H against the approved objective and the spontaneous changes. Eight authored models, repeat owned instances, complete/partial engine trees, saved hardware/tunes, paid missing-part Apply, condition-based non-additive valuation, quote-backed sale/salvage/service, captured career settlement, offline jobs and funded recovery remain implemented. The roster includes the pickup; the four newer mechanical models reuse placeholder layered bodies.

The same authoritative simulation runs in races and the physical bench. Inventory identities, failed originals, individual engine elements, hardware tuning permissions, applied drafts, reduced 25% rules, independent wheel/contact physics, AWD failure paths, fluids, repairs and real-clock shop reservations remain intact. The bench remains isolated from player cash/hardware/resources. No economy simplification replaces those mechanics.

## Corrections found during acceptance

- Neutral/staging retained converter dissipation from an earlier loaded tick. Disconnected coupling now clears the old loss/slip/lock values; loaded road substeps publish average converter work.
- The supercharged V8 survived steady load but its factory converter gearbox overheated after repeated launches. Factory Automatic/CVT heat rejection changed from 25 to 80 W/C; the 140 C thermal limit, capacity and failure thresholds are unchanged. The eight-model endurance and destructive cases are rerun.
- Funded career testing used the previous fixed cooldown instead of the new race-entry policy. It now shares that policy. The classic V8 route buys rank-2 tires with earned cash before its D/C rivals; opponent strength and rewards are unchanged. The ledger has 330 paid/earned operations rather than the historical 327.
- Profile normalization resolved the owner's full build once per installed part, slowing repeated eight-car saves and causing the native aggregate watchdog to expire. It now resolves once per car/stored engine per normalization, with exact identity/condition/resource preservation checked. The eight-car fixture also receives a 600-second aggregate budget; other fixture budgets and gameplay limits are unchanged.
- The integrated harness corrected four malformed range labels and made opening-event selection deterministic by ID. A misleading CSV fuel column and historical model label were corrected.

## Approved thermal addition

Heat management ON allows Fully cooled, Optimal at start, or Live elapsed cooling. Heat management OFF holds optimum throughout driving and disables start-temperature choices. Live uses a persisted per-car UTC checkpoint, cooling components and each tire according to heat rejection/capacity, with engine-off oil/coolant behavior. Every temperature policy advances real parked fluid leaks before assigning any assisted starting temperatures. No fixed 60-second cooldown, repair or refill is granted by garage return/retry.

Optional warm-up starts in neutral before the countdown. Shared pedal/clutch/contact/stress physics permits real FWD/RWD line-lock burnouts; AWD retains normal axle behavior. Ready requires stopped body/wheels and retains heat, wear and consumed fuel/fluids. Warm-up is OFF by default. The [thermal contract](../docs/HEAT_AND_RACE_TURNAROUND.md) documents clocks, legacy state and reduced-order cooling limits.

## Verification and provenance

The current shared-physics/component matrix passed **5,662 checks / 2,429 simulation runs / zero failed assertions**, covering 36,629 simulated seconds across all eight models. It exercises compatible individual replacements, named part stats, transmission/induction/wear combinations, idle/blips/staging/mixed pedals, each failed category, tire/flywheel size extremes and authored AWD modes. Injected failures and overloaded builds may break as expected; zero failed assertions does not mean every experimental build remains healthy.

Passing raw runs: `slice3-final-domain-04` and `slice3-final-windows-04`, under local `reports/phases/3h/full-validation`. Both complete manifests passed the evidence collector, including model/timestamp checks and an exact 2,429-row/eight-car audit CSV check. See the [31-gate manifest](phases/3h/full-validation-matrix.csv) and [counts](phases/3h/full-validation-summary.txt). Overlapping suites are reported separately, never summed as unique cases.

| Domain gate | Passing checks / result |
| --- | ---: |
| Thermal rules/warm-up | 156 |
| Collection/ownership | 399 |
| Assemblies/configurations including prior ownership | 179 |
| Valuation | 2,405 |
| Paid operations | 226 |
| Expanded career | 208 |
| Garage business | 159 |
| Funded economy | 1,388; 330 ledger rows |
| Combined economy/save matrix | 500 |
| Full foundation (includes overlapping sub-suites) | 16,719 |
| Dedicated interrupted-physics recovery | 296 |
| Dedicated integrated tools | 1,465 |
| Scene | PASS |

The full foundation includes the 5,662-check matrix, 74 stock/coupling reliability checks and the existing physics/audio/parts/fluids/ownership/control regressions. The separate tooling/recovery reruns overlap it. The [configuration runs](phases/3h/domain-foundation-audit-runs-matrix.csv) and [part stats](phases/3h/domain-foundation-audit-parts-matrix.csv) contain the detailed matrix. The foundation balance export has **256 combinations**, now reflected dynamically in the editor message.

The eight real Windows OEM races completed, stopped without failed parts and reloaded retained condition/resources. Minimum component health was 99.49–99.91%; quarter-mile times were 13.929–18.082 seconds. All 18 Windows workflows passed with zero reported runtime/GUI/audio errors, covering forced interruption/restart, collection, thermal settings/warm-up, stock races, setups, valuation, paid recovery, career, business/restart, funded recovery, race controls, actual OS keyboard, reset, physical bench, independent acoustic configuration and measured volume sliders.

The OEM Windows probe uses full wear, realistic fuel, evolving heat and normal shutdown. Driver commands are supplied at the shared input boundary; actual OS keyboard/mouse behavior is covered by separate workflows.

| OEM model | Quarter mile, s | Minimum health after shutdown | Brake temperature at stop, C |
| --- | ---: | ---: | ---: |
| Three-cylinder hatch | 18.082 | 99.91% | 59.0 |
| AWD rally sedan | 13.929 | 99.67% | 141.1 |
| Sport coupe | 14.903 | 99.78% | 124.0 |
| Starter hatch | 16.936 | 99.87% | 72.3 |
| Supercharged V8 | 14.521 | 99.49% | 188.6 |
| Inline-six tourer | 15.795 | 99.62% | 128.4 |
| Pickup | 15.542 | 99.62% | 121.0 |
| Classic V8 | 14.899 | 99.52% | 152.8 |

Failed exploratory runs remain diagnostic evidence, not acceptance. Domain 01 exposed gearbox heat; domain 02 exposed the colder funded-route tire need. Windows 02 expired its old whole-roster watchdog. Run 03 was deliberately stopped for the leak-rule/performance corrections and remains incomplete. The retained [thermal failure](phases/3h/pre-fix-foundation-failure.txt) and [funded-route/watchdog](phases/3h/pre-fix-balance-failure.txt) reports explain the fixes. Historical 2L/3A–3G reports are preserved; final review copies belong to 3H.

## Versions, packaging and remaining limits

Save schema remains 5 with additive thermal preferences/timestamps. Mechanics catalogue/trace formats are unchanged; exact physical capture model is `draglab-integrated-tools-v16`. Old exact recordings require new captures; owned saves, identities, damage, quantities and supported earned-save migration remain valid.

The runnable player is `Game/Builds/Windows/DragLab.exe`; distribute its entire Windows folder. Complete source/player ZIPs and `checkpoint-integrity.txt` belong to the final 3H checkpoint folder. The [checkpoint instructions](phases/3h/CHECKPOINT.md) and external integrity receipt cover both archives. The source archive preserves repository-relative paths, all required Unity metadata and the static legacy-earned migration fixture. No remote repository or storefront is published.

Automated developer acceptance does not certify human economy pacing, engine-sound realism or driving feel. Android/controller hardware, final art/UI/audio, production performance and commercial distribution remain separate gates. Cooldown is a reduced-order 20 C ambient model evaluated lazily at race entry; climate and spatial heat soak are not resolved. Carburetor fueling/audio warm-up remains an authored transient rather than cylinder-wall thermal diffusion; loose inventory is not continuously thermally simulated. Display jobs are engine-off exhibits; driving customer jobs, rotating used markets and larger career content remain future content. Random intermittent faults and nitrous operation remain deferred as previously agreed.

After the full suite, an editor-only export-count label changed from fixed 128 to roster length times 32; a fresh compiler/scene check passed. Runtime source/player stayed unchanged. The [editor recheck](phases/3h/editor-recheck-scene.txt) records this targeted follow-up.

Approval review rejected a proposed broad report restoration. Explicit canonical hashes/diffs showed the historical reports already matched HEAD, so none required restoration. Only a later reviewed scene fixture-path change was conditionally preserved in 3H and restored in the historical root report after exact non-fixture equality checks. The new exploratory diagnostic remains local and ignored. No user edits were discarded.
