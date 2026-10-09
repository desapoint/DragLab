# 3H — complete Slice 3 integration and validation (2026-10-09)

**3A–3H developer acceptance passed:** 13 domain gates and 18 Windows workflows. All requirements are mapped in [the acceptance contract](../../../docs/SLICE_3_ACCEPTANCE.md) and [the full audit](../../SLICE_3_AUDIT.md). No later slice was started.

The combined eight-model economy/save gate covers retained/sold engine trees, duplicate model instances, partial resources, missing/worn/failed elements, concurrent garage/shop reservations, quotes, failed writes, migration/backup and exactly-once interrupted/finished outcomes. It passed 500 checks. The Windows forced-termination/restart workflow passed 15 semantic checks across two processes with zero runtime errors.

## Current full verification

Fresh manifests are `slice3-final-domain-04` and `slice3-final-windows-04`. The [31-gate matrix](full-validation-matrix.csv) and [per-suite counts](full-validation-summary.txt) are current evidence. Foundation: **16,719 checks**, including **5,662 checks / 2,429 simulation runs / zero failures** and 74 stock/coupling reliability checks. Dedicated tooling: 1,465; recovery: 296; thermal: 156; funded economy: 1,388 / 330 ledger rows. These overlap and are not added into a unique-case total. Every Windows workflow reported zero runtime errors; actual OS keyboard/mouse, volume PCM, free isolated benches, saved setup purchase/reuse, paid jobs/recovery and reset preferences passed.

All eight full-wear OEM Windows passes and shutdown stops remained healthy and reloaded retained resources/condition. The actual configuration/part matrices and funded ledgers are retained alongside the review text. Old phase evidence remains historical; exploratory failed/aborted runs are explicitly excluded from acceptance.

## Approved additions and fixes

Separate heat management from starting temperatures: OFF holds optimum throughout driving and disables start options; ON allows ambient, optimal at start or live real elapsed cooling. Persist per-car UTC clocks; advance parked fluid leakage under every thermal policy. Optional pre-lights neutral warm-up uses shared contact/coupling/fuel/stress, real line-lock burnouts and stopped-wheel readiness without healing or rewards. See [the thermal contract](../../../docs/HEAT_AND_RACE_TURNAROUND.md).

Correct stale disconnected converter loss and average substep heat. Calibrate factory Automatic/CVT cooler rejection to 80 W/C while retaining existing limits. Fund the colder career route's tire purchase through actual earned money. Cache owner resolution once per build during normalization and check exact identities/condition/resources. Give only the eight-car native aggregate fixture a 600-second budget. Correct harness range labels, stable opening-event selection, a fuel-column label and the editor's 256-combination export message. The last editor-only label correction passed a fresh compiler/scene check; the runtime/player source was unchanged after the full suites.

## Versions, checkpoint and limits

Save schema stays 5 with additive thermal preferences/clocks. Physics capture model is v16; previous exact captures require recapture, while supported earned/owned saves, hardware IDs, faults and quantities remain valid. Code checkpoints: `6ba37eb` (initial integrated harness) and `29b83c5` (thermal addition/audit corrections), followed by this final acceptance/tools/evidence commit.

Run `Game/Builds/Windows/DragLab.exe` with the complete Windows folder. Source and player archives preserve required paths/data and are verified against every workspace payload, CRC and compiled gameplay DLL; see [checkpoint directions](CHECKPOINT.md) and `checkpoint-integrity.txt`. Archives and raw fixtures/media stay local; concise reports/matrices and all required source/metadata are versioned. No Git remote or storefront publication occurs.

Human economy pacing/driving/listening, Android/controller hardware, production art/UI/audio/performance and commercial hardening remain separate. Display jobs are engine-off; driving jobs/used-dealer rotation and expanded content remain future work. Cooling is a reduced-order per-car model evaluated lazily at race entry, rather than climate/spatial heat soak. No unresolved behavior decision required user clarification for this checkpoint.
