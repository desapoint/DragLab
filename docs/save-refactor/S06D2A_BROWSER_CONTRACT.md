# S06d2a typed browser-facts prerequisite

Baseline `05d15ba`. This dormant support step precedes the bounded browser/card builder. It changes no live menu or persistence caller. The whole S06 responsiveness gate remains open, including nonempty-cart latency; atomic checkout remains S07. The first support component is a base-input read set, not a certificate for a planned cart preview or a no-JSON completion claim.

## API and ownership

- New `NativePartsOwnerContext` captures slot/canonical path/epoch on main using the existing read-only `ProfileStore.CaptureSelectedContext`. It never asks for command permission or mutates binding.
- New `NativePartsPriceFact` contains a source/asset key and nullable price. Callers supply freshly computed, owner-thread price facts for the page; no callback is retained or passed to workers, and no valuation formula is duplicated. New-part quote inputs are covered by authored definitions, stock and holds. Future integration must budget price-fact production too.
- `NativePartsBaseReadSet.BeginCapture(profile, page, planned, typedLines, units, ownerContext, prices)` returns an owner-thread capture session. `Advance(itemCap, softMilliseconds)` captures private facts in bounded batches. `TryFinish(currentInputs, out readSet, out reason)` performs the exact final same-frame comparison; `readSet.MatchesCurrent(currentInputs, out reason)` revalidates the same facts. Core/test authors must freeze the concrete input-wrapper signature together before dependent edits.
- Capture and comparison are main-only. The read set may hold owner/reference identities but must never be sent to a worker. Future workers receive separate detached value inputs only. `InspectionBuild.Capture` and `CarInspection.PrepareCar` stay on main; only isolated `CalculatePrepared(..., steadyOutput:null)` is eligible for the existing math worker boundary.

## Exact scoped facts

Always retain profile/catalog identity, revision/storageCommit, actual selected-car reference/IDs, request page/planned state, owner context, ordered cart fields and exact unit fields. Query/filter/sort/scroll/detail/drafts are live presentation state and do not belong in the base physical input key.

Capture selected-car tune/AWD, fuel/fluids, engine identity/lock fields, ordered installed bindings and deep condition/member facts for its actual fitted parts. Reuse existing typed Copy methods, with manual field comparers, exact runtime resource/condition bits, ordinal strings, null/ordered-collection handling, concrete member types and tire-specific state. No reflection/boxing loop, SnapshotEquality JSON helper, whole-profile copy, normalization or state write. Editor/development field-coverage guards must fail when an added serialized field is not accounted for by a comparator.

Shop requires only ownership definition/member-ID facts for unrelated owned assets, plus actual dealer rows and their displayed condition/pricing facts; do not copy wear/history for unrelated fitted cars merely to build an owned-definition set. Inventory/Installed additionally capture the displayed asset facts those pages read. Include actual market seed/generation/ordered rows/aliases/units/delivery values, reservation clock/owner/generation/ordered supply, cash/reputation where consumed, and the supplied page price facts. Direct same-revision edits, object replacement with reused IDs, row/order/alias changes and owner changes must reject stale data.

A nonempty cart is recorded exactly but requires a separate typed read-only cart-preview read set before a composite builder may publish planned results. This support step cannot validate or optimize the existing full-profile PreviewProfile operation by itself. The subsequent PreviewReadOnly dependency must be integrated and measured inside S06 before the gate passes; no nonempty-cart exclusion or S07 completion is implied.

## Authored snapshot fence

Extend only MarketCatalogLayout with a main-owned opaque ready-content read token. It borrows immutable group/alias metadata and the exact existing authored key-snapshot identity. Retain original definition IDs/reference order and immutable semantic snapshots used to construct that key snapshot; do not copy the whole catalog on every request. The final check compares current definitions to those captured values with typed comparisons (including nested AWD/null semantics), plus revision and identity, without calling Key/JsonUtility or re-encoding a changed definition. No player stock belongs in this authored token. Catalog mismatch returns stale; future integration requests bounded normal warmup, never synchronous completion.

Catalog Spec values are privately owned/copied by MechanicalCatalog; supported authored spec edits must continue advancing its existing AuthoringRevision. The token does not replace content revisions or command receipts. Do not use only object identity/revision to skip public mutable definition checks.

## Cost and acceptance

The 2–3 ms preparation budget is soft. Capture count/max/CPU, final exact comparison, authored validation and price production are separate measured phases. Because public fields can change without generations, a multi-frame comparison alone cannot prove atomic current state: keep the final exact check in one owner-thread turn and measure its largest tail. It is not promised to fit 3 ms. Include 1/5/20-car scopes and small/large catalogs; over-budget tails remain blockers and require actual fixes, never averages or weaker freshness.

Check dormant read-set parity, every captured field family/null/member type, unrelated-car exclusion, lifecycle/stale edits between capture batches, exact side-effect freedom, owner/path/epoch changes, snapshot lifetime/release and no JSON/copy escape. Review locally and commit this support step before adding the live builder. Later integration must measure complete query/sort/card readiness and all navigation frames; no performance acceptance follows from these helper tests.

## Exclusive files for implementation after design review

| Owner | Files |
| --- | --- |
| s06d_views_medium / Luna medium | New Runtime/NativePartsReadSet.cs + meta; Domain/MarketCatalogLayout.cs ready-content token only; S06D2A_FACTS_CORE.md |
| s06d_lifecycle_medium / Luna medium | New Runtime/NativePartsReadSetChecks.cs + meta; S06D2A_FACTS_TEST_MATRIX.md |
| Fresh current UI-design Luna-high reviewer | S06D2A_FACTS_REVIEW.md after source/tests are frozen |
| Coordinator | Editor runner; ledger/status/contract/results; all Unity/process/Git |

No overlapping writes, no NativeParts/live browser/cache changes yet, no persistence/physics/audio changes. Freeze all Game files before runs. Broader runtime JSON retirement and all S07–S14 milestones remain required.
