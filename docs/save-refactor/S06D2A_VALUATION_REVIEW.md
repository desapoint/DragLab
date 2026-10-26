# S06d2a pure valuation component review

Date: 2026-10-26
Review type: independent Luna-high source and evidence review
Scope: scalar loose-price view, quote-only read-only dealer metadata recovery, synchronous encoder attribution probes, focused price checks and their standalone Editor runner. This review does not cover activation of the native Parts UI.

## Verdict

**Approved as a separate pure valuation component checkpoint.** The reviewed production changes preserve the existing valuation surface and full quote validation while removing the reachable whole-profile JSON round-trip from `MechanicsOwnership.ValidateForQuote`. The no-JSON result applies to the reviewed `LoosePriceView` path and the narrow stale-discriminator recovery. It does not claim that every runtime serializer in the game has been removed or intercepted.

The pending native Parts UI source is not a dependency of this component. `NativeDisplayPriceVerification.Run` calls only the public encoder and display-price check entry points. Those checks refer to `RuntimeDataEncodingProbe` and the existing `MarketCatalogLayout` warm producer; they do not construct or call `NativeParts`, `NativeMenuShell`, `NativePartBrowser`, or UI callbacks.

## Source findings

- `Valuations.LoosePriceView` validates the policy and current profile/catalog once per fresh view. Each read keeps the profile-revision guard and resolves the current buyer, catalogue, part or unit from live lists. It uses the shared `PartValue`/`LooseElementValue` arithmetic with no receipt list, preserves the same sequential factors and final checked midpoint-away-from-zero rounding, and keeps validation/current resolution separate from payment authority. No price cache, profile clone, persistent index or snapshot was added.
- `MechanicsOwnership.ValidateForQuote` keeps strict `Validate` as the first pass. Only a dealer-labelled failure enters a second read-only full validation. That pass allows the stale `isUnit` discriminator only when the row has exactly one populated identity; the shared validator still rejects missing/conflicting payloads and runs all active part/unit, condition, price, identity and descendant checks. The profile and dealer rows are not normalized or copied. The original runtime `JsonUtility.ToJson`/`FromJson<PlayerProfile>` recovery path is gone.
- The encoder probe is thread-local and synchronous. Nested scopes count at the outer scope; disposal on exceptions restores depth; worker-thread calls do not enter a main-thread scope. Its reports correctly treat it as attribution of known instrumented call sites, not a global interception of all serializers.
- Focused tests cover stale part and unit flags through fresh price-view construction and an actual price read, strict error ordering, no mutation, exact price parity, empty inactive wrappers, missing/conflicting payloads, duplicate dealer identities/descendants, price parity for worn/failed parts and individual tire corners, same-revision edits, replacement records/catalogue, revision rejection and invalid IDs. The standalone runner is independent of pending UI source.

## Evidence reviewed

- Managed helper `12204`: terminal exit 0. Reported 7 encoder-scope, 81 display-price, 54 listing-validation and the remaining Parts/cache/read-set suites passed. The source hash manifest is `validation-final-source-sha256.txt`.
- Standalone component run `92570`: terminal exit 0; encoder checks 7/7 and display-price checks 81/81 passed. I inspected the produced reports; descriptive 0 B allocation counters are not treated as proof of zero allocations.
- Existing valuation regression `20516`: terminal exit 0; 951 checks passed across `starter-hatch`, `sport-coupe`, and `utility-pickup`. This is a representative three-model run, not full-roster coverage.
- Windows player build `40468`: terminal exit 0 with `Build Successful` and `DRAGLAB BUILD SUCCESS`. The build compiled the current worktree, including pending UI sources; the independent source dependency audit above confirms the pure component and its standalone test do not depend on those UI callers. The corresponding build row in `S06D2A_VALUATION_RESULTS.md` has been updated to record terminal exit 0.

The current measured cost remains material: the standalone diagnostic reported 29.889 ms for full quote validation on a 1,081-part / 2-car / 9,294-definition starter fixture, and 29–45 ms for the tested fresh price-view workloads. These are descriptive measurements and do not establish a frame-time pass. The previous native live candidate still has two populated-Inventory frames over 50 ms and failed its held-builder scenario; that live gate remains open.

## Approval boundary

This approval covers a coherent component checkpoint containing only the approved component, tests, metadata, focused documentation/results, and required ignore/report adjustments. It does not approve live Parts activation, native responsiveness, visual acceptance, Android, full S06, or later S07–S14 work. The parent must continue the corrected native run and obtain the separate live-checkpoint review before committing or declaring the live integration complete.
