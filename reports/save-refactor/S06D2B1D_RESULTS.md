# S06d2b1d — limits, first errors and normalization attribution

Baseline `e012158` on main. Previous goal turn made progress through committed native/identity evidence. This increment adds Editor-only evidence before the shared pure scoped planner can be reviewed; it does not implement that planner or retire runtime JSON/clones. All S00–S14, preservation, durability, simulation and no-runtime-JSON/no-whole-profile-save-copy requirements remain active.

## Execution and provenance

Unity 6000.6.4f1; CLI1.0.0-beta.14 verified current. New tests, matching metadata and wrapper/collector wiring were reviewed by Luna-high after two Luna-medium contributions. Explicit staged checks run before source freeze. Each manifest contains1,058 files (DragLab C#/metadata, Packages, ProjectSettings and existing diagnostic tools). Terminal runs are hash-verified before any thaw. Raw files remain local/ignored under S06D2B1 supplemental timestamp folders and S06D2B1D logs/manifests.

| Run | Result | Evidence/disposition |
| --- | --- | --- |
| compile a | CS1628; no runtime assertions credited | Fixture captured out `carId` in a lambda; corrected to a local after terminal/source verification. |
| limits b /164418104 | **137 PASS /4 FAIL** | Empty/101 errors correct. Actual100 distinct authored IDs from8,494 eligible definitions: Total3255 /fee275;100 allocator events and316 mapped new physical/member IDs per output. Quantity99: Total2673 /fee198;99 events/IDs per output. Both bijections and full typed output comparisons pass. Four combined input-invariance failures remain (production/reference preview for both cases). No fixture shrink or shortage. |
| source-diffs b /164911662 | 52 PASS /10 FAIL | Historical pre-marker walker result only. Nonprimitive type/presence markers were incomplete; its path counts are superseded. |
| source-diffs c /165641650 | **57 PASS /10 FAIL** | Runtime type/presence, array types, alias topology, signed zero/ULP/order self-probes pass. Ten exact input-diff boundaries remain strict failures; see below. |
| ensure c /165951543 | **72 PASS /0 FAIL** | Valid no-op and17 independent diagnostic cases observed. These passes certify observations/isolation, not typed no-heal implementation or approval of repairs. |
| malformed c /170420577 | **223 PASS /6 FAIL** |20 first-error cases match reference/manual expectations. Two Move cases each fail production/source invariance and bridge invariant, despite matching exceptions. Historical run before the last two expectations were pinned; superseded by malformed d. |
| malformed d /171530443 | **223 PASS /6 FAIL** | Final rerun pins `ArgumentNullException("source").Message` and the exact Unity Mono null-reference text. All20 first-error cases match. The same six Move input/bridge failures remain strict failures. |

The listed numeric suffixes identify timestamp folders from2026-10-28. Full raw folder names and commands are retained in logs. Do not sum overlapping provenance/helper assertions or pre/post-fix runs as unique coverage.

Trace SHA256 values: limits b `52a40cab9f182d2c8639d5860dd4b73914bbdf81e8858a44cd57c25100362ce1`; historical source-diffs b `cd590b10f03e7c7bcbd4c50c934ce6e9017ee058e8b36397eb4e7c04902e4d3b`; corrected source-diffs c `afc15348b1bdf1c801a6090876996dea490a57c0fafbf60b0374313e89d07fe0`; ensure c `cf41c9ac23cd3012124572696330a05677573f6de144c3b70556178e19737a01`; malformed c `a77e26bdacd353e861cba850d7b70b401d76d11e9c2ead9708d4ea6294a71269`; final malformed d `ce0d6b263e022607a140d44655593abb30332cdded5d8be999ead79449ec88cf`.

## Exact serialization attribution

The corrected walker records574 changed typed paths and106 changed reference paths at each of ten boundaries: ToJson-only; round trip; production/reference set PreviewProfile; production/reference Move Total; production/reference Move PreviewProfile; and production/reference sequential Move Total→PreviewProfile. All field-diff sidecars have identical contents, confined to `inventory.dealer`: eight null inactive part payloads materialize default PartInstance/condition/array data (224 paths); fifty null inactive unit payloads materialize LooseEngineElement defaults (350 paths). Reference changes are8 parts,8 conditions,8 arrays,32 empty-string references and50 unit records. ToJson-only reproduces this mutation directly. Do not attribute all remaining call-chain effects exclusively to Ensure.

FromJson-only does not mutate its supplied input; neither do production/reference no-move Total. Rows, row references, original fixtures and Unity RNG remain unchanged in the attribution suite. Output differences are recorded separately and include normal copy/new-part effects; they are not source corruption. All ten source mutations remain failing assertions. Detailed typed path/value and reference sidecars are retained without dumping complete profiles/catalogs to console.

The field walker includes runtime types and bitwise numeric values; the reference walker is reviewed against the current serialized profile closure, whose reference-bearing records are classes. It is not a general proof for future reference fields nested inside custom structs. Catalog values/root reference are observed; this does not certify every nonserialized cache or arbitrary authored alias topology.

## Ensure observations and pending typed policy

On a validator-accepted normalized fixture, a second Ensure changes zero values/references, emits zero identities (closed/nonoverflowed observer) and preserves Unity RNG. Four optional nulls—AWD tune, pass, custom name and garage-job ID—validate and remain untouched.

Malformed/legacy controls differ: old schema version is updated; null parts becomes an empty list before a fitment exception; stale dealer discriminator is repaired. Invalid null fluids are replaced (16 paths/1 reference); initialized null fuel is rejected with zero changes. A missing service part under its explicit legacy marker is inserted (33 paths/13 references/one part ID). Missing piston members are **validator-accepted**, then populated (43 paths/13 references); do not relabel this accepted shape as invalid or approve blanket rejection. Missing piston identity is rejected without repair. Empty tire members are repaired (74 paths/17 references/four tire IDs); one missing tire ID is repaired (one value/reference/ID). Null wheel members make the pre-validator throw NullReferenceException, then Ensure fills them (43 paths/13 references/no GUID). Missing market/reservation metadata is rejected without changes. Every isolated case preserves the original fixture and Unity RNG.

These are exact legacy observations, including partial effects before exceptions. Future typed previews must preserve accepted optional/aggregate representations and derive private facts as appropriate; genuinely missing/invalid action dependencies require an explicit reviewed error/no-heal policy. None of these tests implements or approves that new behavior. Migration/load repair remains a separate lifecycle boundary, not permission to repair during preview.

## Limits and remaining gates

Stopwatch frequency is10,000,000. The known8,192-byte allocation probe reports zero, so the thread allocation counter is unavailable; old zero-byte values are not zero-allocation evidence. No native-frame, per-operation allocation, Windows/Android rebuild or device acceptance is credited here. Production code is unchanged by the Editor-only increment; prior Windows j/native e/f evidence remains historical.

The complete case matrix, remaining owner/topology/error/reservation/native/freshness variants and actual latency/allocation gates remain open, as do the shared typed planner/resolver and all live nonempty-preview callers. S07–S14 remain required. Current strict source and allocator failures must not be hidden by warming, normalizing, deleting tests or a legacy fallback.

Final malformed d terminated with the expected failing assertion result, not a hang; its 1,058-file source manifest is unchanged and no Unity/player process remains. Limits b, source-diffs c and Ensure c are independent targeted runs. The final d source changes only two malformed expectation pins after c; no change to the limits, corrected walker or Ensure paths is credited as a rerun. The matrix evidence ledger indexes remaining finite gaps without treating these diagnostic observations as runtime conversion approval.

Independent Luna-high final review approves this diagnostic-only checkpoint subject to the scoped staged checks. It confirms the earlier collect-all executed the two reservation-lifetime assertions, while detailed boundary state and other-cart/changed-cart evidence remain open; the evidence ledger now distinguishes these. No Step1, typed behavior, full S06 or performance approval follows.
