# PR #4 and architecture direction review — 2026-10-20

## Decision

**Retain PR #4 as the first dependency, finish its acceptance, merge it, then do the broader refactor from updated main.** It is a useful authored-fitment optimization and does not need to be replaced by a large rewrite. It does not solve ordinary transaction, ownership lookup or full-profile save preparation costs.

No PR was merged, published, closed or modified by this review. The current checkout stays on main. The approved follow-up is the [one-checkpoint-at-a-time plan](../../docs/ARCHITECTURE_PERFORMANCE_PLAN.md); implementation starts at AP-00 after the planning delivery, not automatically.

## Reviewed revisions and method

- Repository: `desapoint/DragLab`; [draft PR #4](https://github.com/desapoint/DragLab/pull/4).
- Reviewed PR head: `1ac43ccfc97e6efba865b472e50e3adc1de7eedc`; common base: `d69490f9dad417e0f2706c97801dcebdae5c85e4`.
- Local main: `1a295a2`, seven commits beyond the common base. PR has 23 commits / eight changed files.
- `git merge-tree --write-tree` combines these revisions without conflicts; candidate tree: `2d4b9bf2bef16aee355af534c576506a47973740`. This does not change the checked-out branch or merge the PR.
- Reviewed all eight changed files, the supplied F.7 evidence, and current transaction, profile store, SQL projection, inventory, cache and settlement call paths.
- GitHub returned no CI check runs/statuses for the reviewed head. That is missing automation evidence, not a passing CI result.
- Focused Unity tests use the seven C# files from the combined candidate. Exact source bytes and the three already-modified verification reports are backed up, temporarily replaced for verification, then restored. No worktree, branch switch, player-save access, or current-player rebuild is needed for these domain checks. Raw staging/logs remain ignored under `reports/player-fixtures/pr4-review-20261020/`.

## Progress toward the approved direction

The PR adds a catalogue-scoped ordered fitment index built alongside existing grouping. It reuses that index for initial new-stock guarantees, detached ownership preference, final new-stock guarantees and used-stock guarantee eligibility. Revision/group/model matching invalidates it. Selection order and both coverage random streams are preserved. This is appropriately separate from runtime ownership, fitted-part compatibility and quantities.

The code does not add a new persistence authority, change SQLite schema, or move Unity resources/live simulation into its index worker. Empty garages intentionally use all authored models for the initial guarantee, but no owned products for owned-preference/final guarantees. The new parity tests cover that distinction, multiple models, low reputation, worn members and held reservations.

No confirmed production correctness regression was identified in this review. This is bounded by the tests below; it is not a claim that all UI/platform behavior was freshly exercised.

## Remaining costs supported by code

| Priority | Evidence | Consequence / next direction |
| --- | --- | --- |
| High | `MechanicsTransactions.Try` uses full `JsonUtility.ToJson/FromJson`, then `MechanicsOwnership.Ensure` both before and after every action | A refill or small purchase still pays for unrelated profile/market state. Introduce scoped commands and targeted working sets with equivalent validation. |
| High | `ProfileStore.TrySaveCore` starts a task and synchronously waits with `GetAwaiter().GetResult()` | SQL being on a worker does not make the caller responsive. Introduce explicit pending-command coordination while retaining durable acknowledgement. |
| High | `SqliteStateStore.CaptureManaged` serializes the full profile; `StateRows.CaptureJson` parses/projects it; `Sync` reads whole relationship tables before calculating deltas | Current SQL delta writes are useful but upstream preparation remains whole-state. Project changed records and relations directly; preserve commit fences and recovery. |
| Medium | Ownership/checkpoint resolution and `SharedInventory.Entries` repeatedly scan lists by part/car/owner/slot identity | Add transient in-memory indexes; SQL indexes cannot accelerate these list scans. Preserve duplicate/orphan validation and mutation invalidation. |
| Medium | Broad revision-bound UI/commerce contexts coexist with physical fingerprints and bounded caches | Retain physical results across unrelated changes using explicit dependency versions; avoid unsafe global cache reuse. |
| Needs finer profiling | Latest historical PR report labels an 18.490 ms maximum main step as `settlement/publish` | The observer measures an entire scheduler advance and its final label. Split completion/retirement/publication and subsequent UI work before blaming a single assignment or the fitment index. |

The fresh candidate market test measured **575.0 ms** mean for three quote-plus-transaction operations on its **4,291,714-character** profile with a no-op successful persister. This is a large synthetic fixture in the Editor, with other desktop work present; it is neither a matched baseline/candidate comparison nor a prediction for every purchase/phone. It does establish that removing disk I/O alone cannot remove all transaction CPU cost.

## Validation and limitations

- Fresh combined-candidate NewSupplyVerify: **353 checks, zero failures**, including exact synchronous/worker market/dealer parity across six seeded configurations, member wear/cart holds, fitment index equivalence/revision invalidation and used-stock historical fingerprints.
- Fresh combined-candidate PartsMarketVerify: **1,455 checks, zero failures**, including quantities, three-race refresh, retained sold assets, engine stock, setup/checkout behavior, fees and failed-persistence rollback.
- Fresh combined-candidate SqliteStateVerify: **772 checks, zero failures**, including import/migration, exact ownership/resources, stock reload, commit/rollback, corrupt-primary/backup handling, reset floors, future-version protection, checkpoints and save slots.
- The reversible test run exited successfully and verified byte-exact restoration of all seven temporary source files and all three pre-existing modified reports. No restore conflicts occurred. Current main has no runtime/source changes from this review.
- Editor compilation and the domain/persistence runs pass with current main. Player-only conditional verifier code still needs its packaged build. No fresh candidate packaged-player build, matched frame benchmark or physical Android test is claimed here.
- Historical PR results include passing market/held-writer/reload checks, but a first-launch fitted-parts collapse/reopen failure remains unroot-caused. A later rerun passed. The head adds diagnostics, not a demonstrated production fix.
- Historical performance runs differ in observed ~60/120 FPS cadence and sometimes workload. The isolated initial-guarantee CPU reduction supports the index; their total settlement times do not establish a controlled end-to-end speedup.

AP-00 must finish the matched packaged gate and resolve/classify the UI test failure before AP-01. The broader plan keeps existing atomicity, WAL/FULL durability, save recovery, component identities, real wear/resources, seeds and reservation rules.

## Scope and preserved work

The architecture direction was explicitly confirmed by the user in this chat after an explanation of the intended changes. This review and its plan do not begin those changes. Existing unrelated modified new-parts/parts-market/SQLite reports, `Tests.ps1`, attachments, race-exit diagnostics and shop-responsive reports are preserved and excluded from the planning commit.
