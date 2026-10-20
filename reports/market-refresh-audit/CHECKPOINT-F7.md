# F.7 — Worker-owned initial fitting guarantee (draft)

## Baseline and intent

F.6 worker settlement validation was merged into `main` as
`d69490f9dad417e0f2706c97801dcebdae5c85e4`.
The comparable Windows player paid-third baseline improved from F.5
**45.702 ms maximum frame / 38.829 ms maximum main step** to F.6
**26.913 ms maximum frame / 20.019 ms maximum main step**.
The remaining largest step was labeled `parts/new guarantee`
(20.019 ms). F.6 result-to-commit was 937.573 ms and worker
validation CPU was 29.428 ms.

F.7 moves only the **initial fitting guarantee** from that step into
the **existing `BuildInitialPlan` selection task**. It does not change
the worker-supply guarantee that already runs in `NewPartSupply`,
nor the used-parts guarantee, engine offers, market refresh cadence,
serialized schema, or ownership/settlement validation.

## Preserved semantics

- Each group is generated in its original order with the same
  `newCount`, selection, delivery-percent and initial-quantity rolls.
- The initial selection task receives product group IDs/maxima, base
  reputation requirements, the fully built immutable per-model
  `MarketCatalogLayout.FitmentIndex`, and detached owned model IDs.
  It does not read the live `MechanicalCatalog`, profile, Unity
  objects, `Resources`, or `JsonUtility`.
- A canonical product group differs in definition ID only.
  `MarketCatalogLayout` now preserves matching **group positions per
  vehicle model** directly during its original `Fits` pass, alongside
  the original global **maximum** quantity across catalog models.
  Repeated market epochs reuse the resulting fitment index instead
  of rescanning all product definitions and vehicle specifications.
- The guarantee's `coverageRandom` and `coverageQuantity` streams
  advance in the same order and are returned with the completed
  initial plan. The later used guarantee starts from these returned
  RNG states, preserving seeded used-part identities and quantities.
- The worker publishes no partial rows. Only the completed plan is
  attached to the isolated candidate, as before.
- The synchronous/editor `PartsMarket.Refresh` retains the
  original guarantee algorithm for exact-output parity tests.

## Verification added

`NewSupplyVerification` compares **complete serialized market
inventory** after synchronous and worker refresh for several seeds
and guarantee levels: 1/0, 41/5, 981/20, -7/20, as well as the
existing worn-member and held-cart-reservation fixture. It checks
the finished worker restored its original catalog. The player
`MarketBaselineChecks` now checks that the third-race initial
guarantee emitted the `initial fitting guarantee worker CPU` metric.

The **first F.7 implementation** passed the user-run Unity commands
(`NewSupplyVerify`, `PartsMarketVerify`, `SqliteStateVerify`,
`BuildPlayerOnly`), plus **84 market baseline** and **91 held-writer**
packaged-player checks with zero runtime errors. Paid-third peak frame
was **15.451 ms**, peak settlement main step **11.772 ms**
(`settlement/publish`), and result-to-commit **951.523 ms**.
The initial guarantee worker CPU was **15.382 ms**.

The **new fitment-index extension** has **not** yet been compiled or
benchmarked in Unity. Keep PR #4 a draft until the same deterministic
output, SQLite durability and player checks pass again with the index,
and compare frame/wall time with those first F.7 measurements.

## Existing catalog runtime index reuse

`MarketCatalogLayout.Layout` now owns a `FitmentIndex` keyed by
vehicle model, containing ascending canonical product group indices.
It is built during the **same fitment pass** that already calculates
group `deliverable` and `maximum`, in both the paced editor path
and detached cold-layout worker. The index is published only alongside
a complete layout and reused across market stock generations.

One model lookup returns its indexed groups; multiple owned models
produce a union in the **original group order**, without duplicates.
The initial guarantee's empty-garage fallback uses the union of all
authored model fitments. The detached supply worker's `Product.owned`
classification and **final fitting guarantee** instead treat zero
owned cars as zero owned products. All paths preserve the separate
reputation gate and seeded RNG order.

The index only describes **authored catalog-level fitment**. It is
not an installed-part condition index and does not replace build
resolution or tune-specific fitment checks in unrelated UI flows.
It is memory-only, not added to SQLite.

`Layout.Matches` now checks the authored catalog revision and
model IDs as well as product identities and equivalence keys,
invalidating obsolete model-fitment snapshots. A cold worker layout
builds and publishes the index **off the main thread**; a paced
synchronous cold build fills the same groups during its existing
fitment loop.

`NewSupplyChecks.LayoutRegression` verifies per-model indexes against
full `catalog.Fits` scans, exact union/empty-garage ordering,
direct-vs-worker index parity, cache reuse, and catalog revision
invalidation. Existing complete-inventory seeded parity fixtures
also cover the indexed initial guarantee, later used and new-part
guarantees, and the owned-product selection tree.

## Windows local verification

Start on the new branch after ensuring your working tree is clean.
Your local `main` can be updated with `git pull --ff-only origin main`
separately; do not merge F.7 before the benchmarks pass.

```powershell
git fetch origin
git switch perf/f7-worker-market-guarantee
git pull --ff-only origin perf/f7-worker-market-guarantee
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName settlement-f7-index-market-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-f7-index-held-a
```

Inspect `reports/new-parts-supply/verification.txt`,
`reports/player-fixtures/settlement-f7-index-market-a/verification.txt`,
and `reports/player-fixtures/settlement-f7-index-held-a/verification.txt`.
Focus on `market baseline paid third / max frame`,
`max settlement step`, `parts/new guarantee`,
`initial fitting guarantee worker CPU`, and exact-once save checks.

## F.7 continuation — indexed used-parts guarantee

Prior *indexed* F.7 Windows result: complete Unity checks succeeded
(84/84 market and 91/91 held-writer), with the initial fitting guarantee
worker cost reduced from **15.382 to 1.615 ms**. The new run's max
paid-third frame **27.275 ms** and result-to-commit **1,299.735 ms**
were slower than first F.7, while practice/ordinary frames also slowed;
the cadence was ~16.7 ms/frame versus ~7 ms/frame in first F.7,
and the market generated 41 rather than 29 delivery opportunities.
These runs are not matched enough to attribute wall/frame changes to
indexing.

The remaining main-thread `parts/used guarantee` still performed
full catalog enumeration, repeated `catalog.Fits`, `GroupBy`, sort
and then repeated fitment scans for candidate vehicle selection.

This update:
- Uses `MarketCatalogLayout.FitmentIndex.ForModels(owned)` for the
  current player-owned model union and filters that already ordered
  group index by original wear/reputation rules. No new RNG calls.
- Uses a sorted per-model group-membership binary search
  (`FitsModel(model,groupIndex)`) when choosing a compatible source
  platform; preserves the **original target array order** and the
  same RNG position for both candidate and source selection.
- Reuses the already filtered product keys to identify previously
  generated used offers; without iterating the entire catalog.
- Keeps the synchronous/editor full `catalog.Fits` + grouping
  implementation as an exact reference for deterministic parity.
- Records `used guarantee eligible products` separately from
  `used parts/guarantee`, making it possible to distinguish
  group lookup from offer creation and yields.

The `NewSupplyVerification` matrix now tests six seed/guarantee
scenarios, including a two-model owned garage and a two-model
low-reputation garage; it compares complete serialized new/used/engine
market and dealer rows against synchronous refresh. The layout
verification also compares binary-search group membership against
full `catalog.Fits` results for each model and group.

### Controlled player frame pacing

`MarketBaselineChecks` (only in Windows development verification)
now saves and restores `QualitySettings.vSyncCount` and
`Application.targetFrameRate` around the entire suite, sets
VSync=0 / target FPS=120, and reports **observed mean and median
frame time** per case. This is a requested cap, not a guarantee:
display/OS/GPU constraints may still prevent reaching 120 FPS.
Compare like-for-like runs with similar observed pacing and delivery
opportunity counts before concluding wall-time improvements.

**Current continuation changes are source-reviewed, not yet rerun
through Unity or a packaged player.** Keep PR #4 draft.

```powershell
git switch perf/f7-worker-market-guarantee
git pull --ff-only origin perf/f7-worker-market-guarantee
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName settlement-f7-used-index-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-f7-used-held-a
```

## F.7 indexed used guarantee: local Windows verification (20 Oct 2026)

User ran `NewSupplyVerify`, `PartsMarketVerify`,
`SqliteStateVerify` and `BuildPlayerOnly`: all four Unity
commands finished. `FinishPacingOnly` passed **91/91** with
zero runtime errors. The **first** `MarketBaselineOnly` run failed
at the pre-market fitted-parts UI assertion
`Reopening Engine retains all captured part rows` (34 checks);
the **second** run, using the same fixture, passed **72/72**
with zero runtime errors, including the indexed used-guarantee
instrumentation assertion. The first failure predates market
settlement in the test flow and reproduced neither in the
held-writer run nor on rerun. This is an intermittent UI-test
failure, *not yet root-caused*. `NativeMenuVerification.Inspection`
now reports whether the Engine group actually expanded, how
many rows remained, and the clickable rectangle if this fails
again; it does not retry or relax the assertion. The diagnostics
change itself still requires a new packaged-player build.

### Performance comparison (different workloads, observational)

| Paid third-race metric | Prior model-index F.7 | New used-index F.7 |
| --- | ---: | ---: |
| Maximum frame | 27.275 ms | 22.884 ms |
| P95 frame | 23.969 ms | 12.845 ms |
| Max measured settlement step | 12.186 ms (`parts/used guarantee`) | 18.490 ms (`settlement/publish`) |
| Result-to-commit | 1,299.735 ms | 1,088.222 ms |
| Market-generation wall incl. coroutine yields | 715.172 ms | 487.799 ms |
| Initial fitting guarantee worker CPU | 1.615 ms | 1.614 ms |
| Complete new-supply worker CPU | 95.995 ms | 94.412 ms |
| Used-guarantee candidate preparation | Not separately measured | 2.313 ms |
| Used parts/guarantee stage | 15.217 ms | 19.556 ms |
| Delivery opportunities | 41 | 41 |
| Observed median frame | ~16.7 ms | 8.343 ms |

**Caution:** The previous run was effectively paced at ~60 FPS
and the new run targeted 120 FPS (VSync 0) with median 8.343 ms.
This changes elapsed time for coroutine yields; market data
and save record count may also differ across fixtures. Thus
the lower total wall time is **not a controlled speedup claim**.
`used parts/guarantee` is timed from before the entire
initial-used-offer selection and `parts/used supply` yields; its
19.556 ms figure cannot be interpreted as only the indexed
guarantee CPU cost. `used guarantee eligible products` reports
a specific 2.313 ms for the indexed candidate/present scan.
The maximum frame step moved to `settlement/publish`, which
deserves independent profiling; it may not be directly caused
by the index.

Do not merge solely on these single-run comparisons. The
functional market and save checks are passing on rerun, but
intermittent fitted-parts UI navigation and main-thread
publication spikes should be monitored. Keep PR draft until
a controlled A/B performance comparison or an explicit
acceptance of these performance tradeoffs.
