# Market new-supply profiling (2026-10-19)

Two independent instrumentation checkpoints, without changing random streams, inventory contents, or per-frame scheduling:

1. Wear preparation: `PartsMarket.GenerateSteps` sums CPU time spent within every `SupplyWear.Steps` iterator `MoveNext` and records the slowest individual advance. It excludes all time suspended between yields; `NewPartSupply.Prepare` is outside this measurement.
2. New deliveries: `NewPartSupply.Deliver` reports eligible-pool filtering, ownership/discovery filtering, wear-candidate matching, wear-probability computation, product selection, and quantity/stock updates. Only the subscribed `PartsMarket.Measurement` path is timed. The verifier aggregates elapsed milliseconds, number of calls, and maximum single-call duration by stage.

### Local Windows PowerShell

Close the Unity Editor before launching a batch-mode test, then from the repository root:

```powershell
git switch main
git pull --ff-only
./tools/Invoke-Unity.ps1 -Action NewSupplyVerify
Select-String -Path reports/new-parts-supply/verification.txt -Pattern 'Measured wear/','Measured delivery/','Checks:'
./tools/Invoke-Unity.ps1 -Action PartsMarketVerify
```

The NewSupplyVerify action also compares staged versus synchronous seeded market contents and checks preserved used stock fingerprints. For wider regression use `./tools/Invoke-Unity.ps1 -Action PartsMarketRegression` (can be lengthy). Generated verification.txt is an existing report; inspect any local modifications before committing or discarding generated output.

### Interpretation

The previous `Measured new supply opportunities` span can include iterator yields and is not equivalent to CPU-only readings during live gameplay. The new wear/ and delivery/ figures are scoped CPU measurements. The sum of delivery stage totals does not include wear-preparation or Unity vehicle-market generation; benchmark differences may reflect cache warmth and fixture profile. No Unity or player build was executed by this remote instrumentation change. Re-run locally to obtain actual new numbers.
