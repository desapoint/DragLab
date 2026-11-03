# S06D2B1L1C1 — typed ordered non-sale cart Apply

Date: 2026-11-03. Baseline: `b6f1e2c` on local `main`.

Status: **bounded C1 technical checkpoint approved by independent Luna-high review** after full functional, prerequisite regression and Windows build gates. Full S06 and S07–S14 remain open.

## Implementation

- Add an operation-local typed copy-on-write working set for ordered raw Apply: new sets, individual units, used purchases, installs and inventory/engine moves. Existing public wrappers share the same authoritative rule bodies; the new projection is not connected to UI/cart-preview callers yet.
- Preserve one lazy enumeration, original row/quantity and guard order, explicit catalogue overrides, cash/pricing/stock/hold behavior, physical component identities, wear, faults, fluids and first-write ordering. Raw Apply still skips sales.
- Return immutable owned DTOs with concrete component fields, existing/virtual identity provenance, exact errors and deletion/insertion/source ordinals. Failures expose no successful prefix and leave source values/references intact.
- Use scoped nonpublishing catalogue/supply reads and affected-record copies. The candidate closure performs no runtime JSON encoding/decoding, full-profile mirror or catalogue copy, and creates no gameplay IDs. Scope disposal releases borrowed objects, overlays, caches and token maps.
- Reject malformed existing tire packs without repair. Fresh product initialization and subsequent individual replacement retain the frozen raw behavior, including untouched blank member IDs.

## Verification

| Gate | Actual result |
| --- | --- |
| Compile-only A | Failed with 14 compiler errors; corrected before B. No runtime verification. |
| Compile-only B | Exit 0; compile marker only. |
| Functional A | 1,058 pass / 50 fail; checker defects and a malformed-fixture getter failure. Retained as failed evidence. |
| Functional B | Licensing startup failure; no cart assertions executed. Only the verified test Editor/licensing child were stopped before retry. |
| Functional C | 1,157 pass / 1 fixture failure: covariant tire-array assignment prevented wrong-type case execution. |
| Focused malformed A | Exit 0; all seven cases, 27 pass / 0 fail. |
| Full functional D | Exit 0; 1,175 pass / 0 fail; 61 case records, seven malformed records, plus null/lazy/preview/override controls. |
| Combined reference/L1B/L1A/L0/K/J/I/H/Parts regression A | Exit 0; 219,338 helper assertions / 0 fail. |
| Windows A | Exit 0; `Build Finished, Result: Success` and the DragLab build-success marker. |
| Independent Luna-high review | Bounded C1 technical checkpoint approved for local commit; broader refactor and native/device gates remain open. |

The checker fixes preserve exact comparisons: signed cash is After−Before; whole-part installs affect the car owner unless an actual engine transition touches an engine; used units retain their exact existing IDs; generated descendant aliases remain field-aware; raw tire emissions are ordered separately from the older preview helper. Two new tire vectors cover distinct corners and repeated replacement of the same corner. Malformed fixtures use independently corrupted valid clones and inspect raw fields without invoking strict derived getters. A/C failures are not waived or relabelled as passing runs.

## Source and artifact provenance

Final source manifest: 2,163 distinct paths, SHA256 `27BD75E0FDA4266B76D9FA8CD958CFA803A71FE2F9FB8DA6F9BF5DD7897E9996`. All hashes and the complete path set remained unchanged after focused, full-functional, regression and Windows-build execution. All twelve frozen Legacy sources remained byte-identical; their required provenance is versioned in [the oracle manifest](S06D2B1L1C1/pre-production-oracle-sha256.txt), SHA256 `CF510D31C232031955393BE218294EC3BEA7E2B7FDE545431D5EA0CD8284DB0E`. Actual Editor/player absence was checked through elevated process inspection after each terminal execution and before the next launch. The remaining AppData Unity CLI helper after the build is not the Editor or player.

| Evidence | SHA256 |
| --- | --- |
| Focused malformed checks | `FD31E60DD2F7AEF1176A9DCE94E2719E4D17A8D6B455F058441CD83DF69C0657` |
| Full functional checks | `715F7C910A68CD0CDBCAF18A5B5E503D25803B2F2AEF7A6FBE19488522E7FC40` |
| Full functional trace | `2AEA9F47C38080B8B5AA9A9A1F9668B497B074392636D41C4614CA677E7EA6E2` |
| Combined regression checks | `98755AF2531BE8A90EB6E485B6B12E55BB46935BC2DF5825740B31383FBB9CA6` |
| L1B trace, byte-identical to prior baseline | `4B80401E4622B2F0B4E6660E09A9C267427E73F9D58F3A4078512FA53C737A90` |
| L1A trace, byte-identical to prior baseline | `8A842342BE6B70FD7F00F020A900663634A348E2489C6E586C54BCD00C414902` |
| L0 trace, byte-identical to prior baseline | `E3233662F204C401EF0D953021584ED32C1AAE819984BA39924B0B8C0F04E9D3` |
| Windows launcher | `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD` |
| Windows `Assembly-CSharp.dll` (4,088,320 bytes) | `8D0A7E554A6D2214624115C2DA40D3B7A4D52B1AA7B01C57355440CBC2DE60D0` |

Raw reports/logs and source manifests remain local under `reports/save-refactor/S06D2B1` and `reports/save-refactor/S06D2B1L1C1`; concise evidence and the required frozen-source manifest are versioned. [Coordination and failed-attempt details](../../docs/save-refactor/S06D2B1L1C1_IMPLEMENTATION_NOTES.md), [independent review](../../docs/save-refactor/S06D2B1L1C1_IMPLEMENTATION_REVIEW.md).

## Interface review and remaining work

No page, control, theme, navigation or F8/mobile interface mode is introduced by this checkpoint. Existing buy/install rules are covered by the independent live-reference regression. No new visual or device acceptance is claimed.

Full cart Total/DeliveryFee/PreviewProfile, sales, checkout, dependency/freshness fences and caller conversion remain pending. Native frame, scaled memory/performance and Android/device gates remain separate. The full refactor cannot be accepted while runtime JSON manipulation or full-profile save clones remain in other callers; settings drafts, equality and cache keys are included in that requirement.
