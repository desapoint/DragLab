# Per-tire branch release audit — 2026-10-15

Candidate: `refactor/per-tire-authority`, original PR head `81e5699`, against main `9bcc706`. This report supersedes the original failed audit without replacing its evidence. This is a correctness/merge checkpoint; it does not accept new visual designs or advance Slice 4.

## Corrected behavior

- Incompatible-save reset archives the entire database/recovery/reset-marker family and rolls back failed publication. A real packaged startup reset leaves a reloadable new game and preserves separate driving/audio preferences.
- Per-tire wear cannot increase health through float rounding. Exhausted life counts as failure in grip, inspection, valuation, maintenance and inventory selection. The tire pack remains a neutral ownership wrapper; actual members own condition and temperature.
- Exact snapshot encoding includes nested clutch doubles. Serialized driving continuation and replay retain the required bit-exact values. Physics model is now v20; obsolete live traces are rejected. Known v18/v19 resource journals can still recover monotonic wear/fuel under ownership checks, without replaying old driving inputs.
- Maintenance cart quotes distinguish empty preparation ownership after JSON reload, while protecting actual prepared engine owners. Stock and partial express delivery use the existing reserved-cart flow.
- Missing AWD stat-help entries now have applicable More/Less explanations.

Earlier branch corrections remain included: fluid quote stability, category-specific faults, rebuild transactions, part-position purchases, stock/express maintenance cart entries, predictable menu defaults, compatible/in-stock default filters, advanced filters and fitted-part shop shortcuts. See the earlier reports and branch commits for their implementation checkpoints.

## Test repairs and scope

The old ownership/assembly/economy fixtures expected automatic imports of deliberately unsupported contracts. They now separately verify read-only legacy preservation and current-contract feature behavior. No production guard was relaxed to satisfy these tests. The residual compatibility suite restores a known-current test context before a following suite saves.

Other stale fixtures requested unavailable AWD hardware through tuning, put a current spark-plug subtype in the old ignition composite, or failed a neutral tire wrapper rather than its members. Fixtures now install the real hardware and set the applicable member failure. Unique output folders and append-only progress avoid Windows file-mapping/truncation failures. The durability entry point now fails its runner when any collected scenario fails.

## Evidence

See `release-validation-matrix.csv` for named Editor entry points and their selected source run. Curated coverage is 67 entry points, including the restored ownership, assembly, paid operations, economy integration and residual checks. Four overlapping convenience composites remain manual diagnostics rather than being counted again.

The original full Slice 2 run executed 5,702 runs / 80,487 virtual seconds / 14,442 assertions. Its only 13 failures were the obsolete wrapper-failure fixture; the other 14,429 assertions passed. The corrected shared failure/AWD subset then passed 1,149 checks / 555 runs / 1,251 virtual seconds. Release evidence combines those unchanged full-matrix assertions with the corrected subset; the complete hour-long matrix was not executed a second time.

Native Windows checks cover navigation and filtered fitted-part shortcuts; stock/express maintenance; engine rebuilding; prices; cart checkout; completed-race Garage exit and durable settlement reload; race selection; setup rename/delete; disposal; used inspection; SQLite/settings/reset. The 11-suite native run passed, plus the separately executed incompatible-save startup reset (13 checks, zero runtime errors). Settings coverage includes compact and true 4K presentation. Generated screenshots remain local under `reports/interface-screenshots`, with resolution variants in `resolution-tests`.

Final reruns: `IntegratedVerify` passed (354.517 s) across all 13 cars; enforced `DurabilityAudit` passed (114 checks, 536.513 s), covering ten drag passes, sustained pull, idle, disconnected coupling cooling, brake heat/failure and ungoverned overrev. `SqliteStateVerify` passed (61.175 s). Latest per-gate results are 66 PASS plus one PASS_COMBINED (Slice 2); no unresolved required gate remains. The original failed All invocation is retained and is not relabeled as a successful single run.

The user explicitly deferred the legacy debug bench UI on 2026-10-15. Experimental bench UI/test edits from this audit were reverted; its source stays intact for a later rework. The packaged bench mouse probe did not pass and is not claimed as release evidence. Shared authoritative simulation coverage remains in `BenchVerify`, `DrivingContactVerify`, the Slice 2 matrix and focused domain suites. Bench-specific wrapper displays/editing and its legacy clipped mouse probe remain outside this release checkpoint by that direction.

## Packaged interface checks

| Suite | Checks | Runtime errors |
| --- | ---: | ---: |
| Navigation/default filters/fitted-part shortcut | 107 | 0 |
| Maintenance and exhausted-tire indicators | 80 | 0 |
| Engine rebuild | 39 | 0 |
| Prices | 113 | 0 |
| Cart/stock/express checkout | 297 | 0 |
| Race HUD/completed-run exit | 135 | 0 |
| Race selection | 51 | 0 |
| Setup management | 50 | 0 |
| Disposal | 52 | 0 |
| Used-car inspection | 61 | 0 |
| SQLite/settings | 148 | 0 |
| Incompatible-save startup reset | 13 | 0 |
| Isolated responsiveness / real durable purchase | 44 | 0 |

These suites reuse checks; counts are per run, not unique aggregate coverage. Raw fixtures are `reports/player-fixtures/release-audit-*-20261015`; native summary is `reports/branch-audit/release-native-results.csv` (local artifacts).

## Final build and responsiveness

`BuildPlayerOnly` succeeded on the final source after restoring the legacy bench files. The runnable player is `Game/Builds/Windows/DragLab.exe`; its current game assembly was produced on 2026-10-15 at 18:54 local time. Build log: `reports/branch-audit/release-build-final.log` (local).

The isolated packaged responsiveness probe passed 44 checks with zero runtime errors, without a competing Unity Editor process. The browser reported 1,852 ready offers / 0.00 s ready wait and zero full comparisons at shop entry. Idle browsing produced no rebuild, requery or new cards. Search/details/cart/tuning/settings preserve authoritative previews; preview interactions left profile and saved bytes unchanged.

Measured durable checkout: clone 44.71 ms; operation 107.92 ms; database capture/validation 23.07 ms; database transaction/durability 160.04 ms; total 362.69 ms. This is a measured remaining synchronous definitive-action cost, not a claim that all storage latency or hitches have disappeared. Across 2,390 recorded frames after frame 20, median was 6.947 ms, p95 8.253 ms, p99 26.019 ms, and worst 525.572 ms during Garage/first-purchase activity. These are one-machine observations, not universal frame guarantees. Raw frames and measurements remain at `reports/player-fixtures/release-audit-responsiveness-20261015/`.

Static checks passed: staged diff whitespace, UTF-8 edited text, 22 added Unity asset/metadata files with no missing metadata, and no duplicate asset GUIDs. Historical generated tracked reports were restored after local evidence archival; the two pre-existing user report changes and untracked files remain untouched.

## Compatibility and limits

Save contract 12 is intentional. Older logical profile contracts remain protected until explicit reset; originals are archived rather than silently rebuilt with fabricated components. Current-contract identities, resources and faults remain authoritative. Save schema and physics/replay versions are separate.

The specialized component/member types still share some existing base summary/service fields. Removing every shared field is a future architecture change, not a missing gameplay regression in this checkpoint. Tests overlap and their counts are not unique independent scenarios. No subjective audio listening, new UI acceptance or platform certification is claimed.

Pre-existing user edits to two verification reports, the untracked `Tests.ps1`, and the old race-exit diagnostic folder are preserved. Generated evidence is retained locally; historical tracked reports are not rewritten to manufacture a green baseline.
