# 3G — Economy balance and recovery (2026-10-09)

Recorded funded routes now cover the main career, completed losses, fuel exhaustion, actual mechanical DNF and paid replacement, upgrade/downgrade/reuse, timed rebuild and assembly sale, idle reinvestment, chassis salvage and an empty garage. Recovery advice identifies missing engines, reservations, failed packs, low fuel and the cheapest unlocked replacement. A small-fill control buys up to one litre through the normal paid quote.

## Calibration and findings

- Recovery work pays **$250 after one real minute**, below $10,000 cash. Existing jobs keep captured terms. Fuel remains $2/L; resource mass and difficulty consumption remain physical.
- Rebuild labour is authorable: **$150 + $25/cylinder**, plus actual components/service/oil. Standard / extended-life filters cost **$25 / $80**, replacing generic $200 / $700 prices without changing their physical specs. A known damaged owned engine increased from $2,432 to $3,144 after a $485 paid timed rebuild: a $227 net gain requiring actual work and retained ownership.
- A high-output starter with a worn stock clutch eventually slipped without a failed flag. The funded route buys a capable clutch before adding output, retains the OEM part and farms a reliable earlier event. Later rewards fund strength/coupling before further power. Wear is preserved rather than erased to make the route pass.
- All eight new dealer packages exceed both immediate whole-car offers and the best engine-plus-chassis offers. Margins against the best immediate sale range from $573 to $4,468. Every catalog offer also exceeds its maximum same-definition loose used offer. Assembly pricing remains independent from loose component totals; rebuilding owned damage can create value.
- The motorsport induction description now correctly says higher **boost**, matching its boostScale modifier.

The full career exposed an actual quote bug: Unity JSON serialization can move unchanged double history/resource values by one ULP. Whole-profile decimal-string hashes rejected valid post-race transactions. Paid/sale quotes now preserve a captured profile and compare it structurally before independently revalidating catalog/policy metadata, ownership, prices, expiry and revision. At most four representable steps are tolerated for finite double values; currency, IDs, revisions, flags, float condition and health remain exact. Meaningful fuel/cash/condition changes still reject atomically.

Reserved garage jobs use the same structural snapshot rule and survive previously driven condition/history through repeated transactions and reload. Legacy hash-only jobs gain this snapshot only when their existing hash exactly matches; uncertain contents are not reconstructed or healed. Existing physical simulation behavior was not replaced.

## Verification run on this checkpoint

- **1,388 targeted checks**, **327 ledger operations**, **eight-model market matrix** and **24 fuel-mode races** (None / Reduced 25% / Realistic across all eight cars).
- Fresh funded main route earns all **18 first wins** through F–S using actual race/shutdown resource persistence and paid purchases, without injected income or REP. Eight actual completed losses preserve consolation, fuel/wear and no new REP.
- Zero cash/empty tank recovers through work, paid fuel and a winning race. An explicitly accelerated end-of-life fixture produces a real mechanical DNF, pays to replace actual failed components, retains the old packs and wins afterward. Downgrade/reuse restores the actual original identity for zero cash.
- Eight-hour capped income buys a real $600 component. Last-chassis salvage retains the full engine; an explicitly zero-cash worst-case fixture funds the $4,968 replacement with 20 captured recovery claims. Domain fixtures advance their own clock to deadlines; this is not production clock manipulation or a human pacing measurement.
- Previously raced job snapshots pass 20 JSON transaction roundtrips and disk reload. Changed same-revision cash, fuel, health and failures reject quotes. The funded full-loop profile passes actual disk persistence.
- Rerun regressions: valuation **2,405**, paid operations **226**, saved setups/assembly **179** (including 37 ownership checks), garage business **159**, expanded career **208**, individual engine/clutches **518** plus parts compatibility **59**. These overlap and are not summed as unique tests. Evidence is copied into this phase; historical reports remain preserved.
- Scene integration and Windows development build passed. The Windows player passed **eight actual-control semantic checks**: real 60-second recovery deadline/claim, paid one-litre fill, race win, post-race part purchase with retained OEM, job/reload and real 30-second deadline/claim. **Zero runtime/GUI/audio errors.** Screenshots and isolated saves remain local in `reports/player-fixtures/phase-3g-balance`.

## Controls, evidence and remaining scope

Use Recovery work / operating receipts in Garage or Collection; garage businesses remain in Business. Use `DragLab > Verify Full Economy Scenarios` or `tools/Invoke-Unity.ps1 -Action BalanceVerify` for domain ledgers. The Windows fixture is `tools/Test-WindowsSlice.ps1 -BalanceOnly -ShowPlayer -FixtureName <fresh-name>` after a development build.

Read [economy details](../../../docs/ECONOMY_BALANCE.md), [ledger](economy-ledger-matrix.csv), [market margins](market-matrix.csv), [fuel modes](fuel-mode-matrix.csv) and [native results](windows-runtime-verification.txt). Simulated race/cooldown durations, fixture clock advances, known starting damage and the wear multiplier are explicitly test inputs. Exploratory failed routes remain local under `exploratory`; they are not the final passing matrix.

Source and full Windows checkpoint ZIPs remain local and immutable; CRC/SHA and compiled-DLL comparison are in `checkpoint-integrity.txt`. Prior 2L foundation evidence is retained as prior evidence, not a newly rerun gate. New asset/meta whitespace is normalized without changing values or GUIDs.

3E, 3F and 3G are complete. **3H remains next**: comprehensive interruption/migration/save/quote/reservation/pause-resume acceptance and final Slice-3 packaging. Final economy pacing needs human review; Android/device and listening acceptance are not claimed. Used-dealer rotation, speculative markets and driving customer jobs remain later content. This checkpoint is a runnable prototype, not a claim of commercial-release readiness.
