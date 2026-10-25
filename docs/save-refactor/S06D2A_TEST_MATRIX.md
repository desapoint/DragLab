# S06d2a ready Shop product group test matrix

Baseline: `0d3c7ae` (the S06d2a checkpoint). The managed verifier is
`NativeReadyProductGroupChecks.Run(Action<bool, string>, string)`. The native
runner is owned by the parent checkpoint driver; this matrix does not claim a
build or test result.

| Area | Oracle |
| --- | --- |
| Cold and incremental warmup | A cold catalog returns no ready groups. At every yielded `WarmSteps` step before `parts/shop ready`, the ready API remains unavailable. The marker follows publication of the complete group snapshot; ready groups remain available while unrelated market fitting finishes. No partial group list is exposed. |
| Fallback parity | Compare each ready group and its IDs in order with the former `GroupBy(NewProductKey, StringComparer.Ordinal)` result. Assert every catalog definition occurs exactly once. |
| Representative and aliases | Assert exact browser card order and representative IDs against the old fitted-definition, same-vehicle-prefix, then ordinal-ID selection. The fixture explicitly creates fitted, vehicle-prefix, and ordinal aliases; search through an alias must still find the grouped card. |
| Live state | Change stock, price quote, cash, ownership, and a matching active cart reservation after warmup. Cards must read current values, including reserved held units, without changing authored groups. |
| Invalidation | Directly edit a public authored price without rebuilding indexes, then replace a definition object at the same list count. The ready lookup must reject stale data; the cold browser fallback must match the exact old grouping behavior. An authoring revision change also requires rewarming. |
| Stable-key field coverage | On disposable definition copies, mutate public scalar, string, and enum fields, including AWD hardware and calibration fields, and assert the stable product key changes. The fixture must contain AWD-authored hardware. |
| Descriptive performance | Record ready validation separately from old grouping; record ready and invalidated-fallback browser construction; report each warmup's yield count, total duration, and largest `MoveNext`. Timings are diagnostic only and have no pass threshold. |

The verifier uses generated in-memory catalog/profile state and the caller's
isolated fixture directory label. It does not write a player save or restore
SQLite bytes over accepted work. The parent owns execution and will append the
actual Unity/native results after the source freeze.
