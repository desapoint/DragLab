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
- The task receives copies of group representative definitions
  and copied target `VehicleSpec`s. It does not touch the live
  `MechanicalCatalog`, Unity objects, `Resources`, or `JsonUtility`.
- A canonical product group differs in definition ID only;
  `FitsDetached` is the same fitment predicate as `catalog.Fits`.
  The layout already calculates the matching original **maximum**
  quantity across all catalog models. Therefore the guarantee
  can use the group's established `maximum` rather than repeat
  the same expensive scan on the main thread.
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

**Not yet compiled or benchmarked in Unity.** Treat this checkpoint
as a draft until all tests pass and a new packaged Windows paid-third
benchmark confirms the main-thread stage moved below the F.6
20.019 ms baseline without changing saved results.

## Windows local verification

Start on the new branch after ensuring your working tree is clean.
Your local `main` can be updated with `git pull --ff-only origin main`
separately; do not merge F.7 before the benchmarks pass.

```powershell
git fetch origin
git switch --track origin/perf/f7-worker-market-guarantee
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName settlement-f7-market-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-f7-held-a
```

Inspect `reports/new-parts-supply/verification.txt`,
`reports/player-fixtures/settlement-f7-market-a/verification.txt`,
and `reports/player-fixtures/settlement-f7-held-a/verification.txt`.
Focus on `market baseline paid third / max frame`,
`max settlement step`, `parts/new guarantee`,
`initial fitting guarantee worker CPU`, and exact-once save checks.
