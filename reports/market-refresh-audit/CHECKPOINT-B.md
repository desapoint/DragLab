# Market responsiveness Checkpoint B — safe startup classification warmup

Checkpoint B prepares only **authored new-product category eligibility and branch membership**. It intentionally leaves product equivalence, catalog fitting/layout, stock, RNG, vehicle inspections, worker isolation, save and UI action scheduling for later checkpoints; Checkpoint A's real third-race 309 ms `parts/layout and selection` hitch is **not yet fixed**.

## Lifetime and concurrency

- At the end of `PrototypeGame.Start`, `NewSupplyCatalogIndex.Request(profile.catalog)` takes a reference snapshot of the author's current part set; it does not synchronously classify it or block shop navigation.
- On non-race frames only, `PrototypeGame.UpdateMeasured` advances classification work with a **0.75 ms soft budget per frame**, and skips frames with active mouse/GUI input. One product cannot be preempted mid-classification; this budget does not guarantee a hard frame cap.
- Construction happens exclusively on the Unity main thread. No worker accesses authored definitions. A private mutable dictionary is atomically published as a read-only snapshot only when all definitions have been processed.
- A market refresh that starts before completion uses the *original* per-product `Branch.Accepts` behavior. It never accesses partially prepared membership. The shop itself is independent of preparation and keeps its current committed inventory and navigation behavior.
- On normal progression reset the existing completed index is reused (same MechanicalCatalog); a replacement catalog builds a fresh index. Count changes force a new build. Every individual lookup verifies definition identity, id, category, stage and induction before using classified membership; altered definitions fall back to original evaluation.
- Dynamic inputs — fitment, maximum quantity, ownership, stock units, discovery, wear, cart, seed and player data — are **never cached** here. No RNG is consumed during classification.

## Tests and local gate

`NewSupplyVerify` now asserts that a cold/unready/partial cache remains inaccessible, the complete index is published atomically, every authored category branch matches a cold-product classifier, seeded choices preserve exact RNG positions/definition identities, reset reuses ready classifications, and changed authored classification keys reject stale membership.

After pulling and closing the Unity Editor:

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-b-20261019-a
```

For immediate shop access, start a fresh game on the packaged player and open Build immediately after finishing onboarding/first-car purchase; inspect for missing products, crashes and responsiveness. The seeded market validation, frame-pacing report and runtime error counts must remain clean. Physical Android frame pacing remains pending Checkpoint G. This commit was not executed through Unity remotely: local validation is required before Checkpoint C.

### Expected effect and limits

Avoid repeated deep category classification across refreshes once the startup index has published, while preserving cold-cache fallback when the shop is opened early. This does **not** precompute the cold `MarketCatalogLayout.Get`, merge the 548 iterator boundaries, remove the non-market 500 ms save/clone wait, or make results controls queueable. Those are separate measured issues to be addressed later.
