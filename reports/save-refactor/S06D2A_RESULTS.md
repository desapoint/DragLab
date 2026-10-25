# S06d2a — native Shop product-group reuse

Baseline `0d3c7ae`, shared main. This is the first bounded S06d2 optimization checkpoint. Full S06, owned-car completion refresh, the no-runtime-JSON/no-full-profile-save-clone conversion and S07–S14 remain open until their own gates pass.

The Shop may reuse completed source-ordered authored product groups after strict semantic validation, while stock, holds, ownership, prices, affordability and representative selection remain live. Cold/incomplete/stale groups use the original exact grouping fallback. Incremental preparation retains soft work/yield budgets; it must not move the former shop freeze to warm publication. Typed nested AWD equality removes unchanged-data JSON comparison; existing canonical-key generation and broader unconverted JSON remain explicit retirement work.

## Actual evidence

Correctness for the source component is independently approved for a local working commit. Actual managed, Windows and deterministic native results are recorded below. The full responsiveness gate still fails; this is not completion of S06d2a/S06 or the no-JSON/no-full-profile-clone refactor. Native notes include nested elapsed times; do not sum them as disjoint work.

## Review checklist

- Same stock/products, representative names, aliases, compatibility, prices and express counts.
- Cart reservations and ownership queries use current save state.
- Direct authored edits, same-count replacement and revision change reject stale groups.
- Warm preparation is incremental; all indivisible/tail work is visible in measured evidence.
- Exact stats and warnings remain; no reduced simulation fidelity or stale success.
- Existing save/publication/recovery fences pass unchanged.
- Five typed native captures retain exact visual parity; human/phone acceptance is separate.

## Initial variant — retained failed performance evidence

Editor67866 passed2337 catalogue assertions plus83 pilot wrapper entries (78 assertions +5 timing notes),83 owner,66 migration and72 storage checks. Windows29666 and native80595 passed; native70/0. The first warm dependency was nevertheless wrong for this workload: `readyGroups=False`, lookup0.038 ms, current inspection6.832 ms, inclusive Shop population99.439 ms. Actual navigation frame1599 was154.106 ms, with133.208 ms outer UI and129.062 ms Parts. Coolant completion frame1941 was53.121 ms, with40.321 ms owned work. This variant does not pass responsiveness and is not a completed checkpoint.

Its successful managed transcripts/build are retained under `S06D2/attempt67866/`; actual native artifacts remain `reports/player-fixtures/s06d2a-native-seed16102026-a/`. Earlier failed verifier attempts73984 (syntax),36363 (non-representable float mutation),87030 (stock-filter interference) and92243 (compatibility-filter interference) are retained separately. The fixture errors were corrected without changing production query defaults or weakening the intended isolated oracles.

Correction under verification: Shop group readiness is independent of unrelated market fitting, with exact key-snapshot publication fencing and bounded preparation before fitting. The unchanged player navigation still decides whether this is sufficient; no performance pass is claimed yet.

## Independent-readiness managed variant

Editor52908 exits0 with1570 ready-group assertions,83 pilot wrapper entries (78 assertions +5 timing notes),83 owner,66 migration and72 storage checks; all five raw files are fresh for this run. Ready lookup/validation was215.494 ms over32 reads versus1626.672 ms for32 old groupings; one ready browser was68.397 ms, including65.498 ms Shop population and6.777 ms strict lookup. These are descriptive warmed Editor observations, not a player responsiveness pass.

The changed readiness oracle checks the explicit complete-group marker, then verifies that the same complete groups remain readable while market fitting continues. The three largest actual iterator MoveNext samples were14.960,14.713 and13.441 ms. The reported total warm wall times3218.528,2860.589 and3118.235 ms also include the verifier's strict ready-API call and assertion on every yield; they are not pure preparation CPU or in-game elapsed warm duration. Runtime source has no such per-yield verification. The iterator budgets are soft; no hard2 ms bound is claimed.

## Independent-readiness player variant — performance still open

Windows37711 exits0. Deterministic native93511 passes70/0 with actual ready-path use: lookup7.293 ms, current inspection7.023 ms, inclusive Shop population65.873 ms, ledger setup5.716 ms, inclusive enumeration54.957 ms and classification5.184 ms. Navigation frame1632 is117.619 ms / outer UI99.577 ms / nativeParts95.420 ms. This reduces the measured cost but still fails responsiveness, so S06d2a/full S06 remain uncompleted and no next phase is advanced.

Artifacts: `reports/player-fixtures/s06d2a-native-seed16102026-b/`. The unchanged five typed PNGs were byte-identical for the first variant; final variant parity remains to be rechecked. Finer ready-loop attribution is now in the frozen source to distinguish representative/alias selection, supply counts, quote+stock+holds, entry creation and used offers. These timer sums add per-product timestamp reads and are nested inside the inclusive population measurement; they must not be summed with it. No normal per-frame log is added.

## Exact supply projection — functional working component, latency still fails

Focused Editor97278 exits0:37697 independent supply assertions and1595 catalogue wrapper entries. Windows6426 exits0; unchanged deterministic native43138 passes70/0. Its actual path is ready=True and stock binding=True. Lookup7.028 ms, current inspection6.804 ms, inclusive population55.839 ms, setup9.191 ms, enumeration32.376 ms and classification7.226 ms. Inner phases: representative4.447, count3.124, quote/hold3.346, entry13.199, aliases1.864, used0.522 and binding4.875 ms. The newest definition of enumeration excludes lookup; earlier variants included it. Population always includes the complete Shop body. Per-product timestamps add diagnostic overhead.

Navigation frame1627 remains124.561 ms / outer UI93.724 ms / Parts89.475 ms. The S06d1 reference measured Parts146.617 ms / frame169.081 ms, but these are individual observed passes, not a distribution or a guarantee. The latest frame is slower than the immediately previous117.619 ms observation despite less Parts CPU; do not claim consistent full-frame improvement. **The >50 ms responsiveness gate still fails.** Owned refresh/coolant and exceptional recovery costs also remain. No S07, full S06 or whole-refactor completion is authorized from these results.

The coherent typed group/supply source component is proposed for an independently reviewed local working commit, with correctness/behaviour evidence and the failed latency gate explicit. This protects the runnable version before the separately scoped bounded browser/card publication work; it does not replace, waive, or narrow the performance or final no-JSON/no-full-profile-save-clone requirements. The entire ordered plan remains active.

Native artifacts: `reports/player-fixtures/s06d2a-native-seed16102026-c/`; managed raw97278 is preserved in `S06D2/attempt97278/`. The backend/save regression source was unchanged by this native-only supply projection; retained full52908 pilot/owner/migration/storage evidence is historical, not a claim of a fresh97278 rerun. A final focused run adds four null/missing-interface oracle checks without altering production code or the built player's behaviour.

## Final component verification / local working-commit review

Focused Editor5742 exits0:1585 ready-group wrapper entries (1583 assertions +2 timing notes) and37690 supply assertions. Four added missing/null piston-interface checks use detached definitions and preserve the original selected/no-car fallback. Counts vary with the number of soft-budget warm yields; the final run ID and raw transcripts, not an earlier count, identify this source. Independent Luna-high `S06D2A_REVIEW.md` approves this component's correctness/local working commit while explicitly keeping responsiveness FAIL/OPEN.

The latest native coolant completion is frame1967:49.799 ms overall /39.156 ms owned refresh; it remains a meaningful unresolved cost, not a proven general bound. All five typed PNGs in final production fixture `...-c` are SHA256 byte-identical to S06d1's pinned fixture. There are958 asset metadata GUIDs with no duplicates. These are automated/source checks, not human visual/phone acceptance. Android, full F8, forced process termination and the final whole-system soak were not rerun here.
