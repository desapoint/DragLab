# L1D R1 — typed reservation values and legacy import

Status: **independent Luna-high GO for this bounded technical checkpoint**. See the [review](../../docs/save-refactor/S06D2B1L1DR1_IMPLEMENTATION_REVIEW.md). The complete L1D query, remaining S06 caller/native work and S07–S14 remain open.

## Implementation boundary

Three new source/meta pairs add pure typed reserved-build values/context, a named Infrastructure legacy importer, and an entirely Editor-only checker. No existing production, business, load/save, schema, simulation, UI or frozen-reference source is changed.

Values own explicit copies of reserved fuel, fluids, tune/AWD, paint, parts, conditions, clutch state and individual members. They preserve nulls, ordered contents and element-array runtime types. Internal immutable values are shared rather than repeatedly copied; mutable getters return defensive copies. Exact comparison uses allocation-free IEEE bit conversion; legacy fallback keeps Single.Equals and the double equality/finite/sign/four-ULP rules.

Match order retains hash/capture error metadata, the exact hash baseline with slot labels, opaque payload binding, exact-text baseline without separate slot labels, then decoded expected values or deferred errors. A malformed fallback cannot invalidate a matching baseline. A decoded expectation remains available after an initial mismatch. Import is explicit and dormant, never a query/cache-miss fallback. Created-job capture is only for newly created jobs; existing jobs must retain their imported expectations.

## Actual runs

Unity 6000.6.4f1, Windows batch/nographics, `DragLab.Runtime.ReservedBuildValuesChecks.RunBatch`, explicit `-draglabR1ReportFolder`. Raw evidence stays local in `reports/save-refactor/S06D2B1L1DR1/`.

| Run | Result | Evidence limit / correction |
|---|---|---|
| A | 7 PASS, then runner exception; OS exit not captured | Checker expected false from an old matcher that correctly throws on malformed JSON. Full matrix not reached. |
| B | Actual process exit 1; 3,836 PASS / 3 FAIL | Incorrect roundtrip/restoration expectations and a numeric fixture comparing four ULPs above 1.0 against a different oil level. Only checker corrected. |
| C | Actual exit 0; 3,841/0, empty runner-error | Retained diagnostic pass, **not field-completeness proof**: JSON fixture cloning could already differ before mutation. |
| D | Actual exit 0; 7,613/0, empty runner-error | Independent exact fixture clone plus clean-clone controls: 1,882 collected scalar paths, 1,882 mutations, zero unsupported. Missing optional structures explicitly reported. |
| E | Actual exit 0; **7,730/0, zero OPEN, empty runner-error** | Final focused evidence, including previously absent optional data and registered types. |

E adds 16 AWD fields from actual `atlas/Differential/0` calibration and three clutch scalar-resource fields on a private copy of an actual clutch part: 19 isolated mutations, no owner writes or identity creation. All 14 registered concrete member types have actual existing fixture sources; GlowPlugElement comes from the generated Fjord listing. One detached base EngineElement diagnostic adds no owned row. The trace's `registryCount=15` means **14 registered concrete types plus one diagnostic comparison type**, not 15 registered gameplay member types.

The nullable control observes source fuel null becoming non-null after Unity decoding. Frozen and typed decoded comparisons both return false; the independently verified exact-text branch succeeds. The restoration control uses an independently decoded bounded payload and frozen Serialized comparison, not candidate-produced expectations or repaired owner data.

## Exact final evidence

| Artifact | SHA-256 |
|---|---|
| E checks.txt, 589,770 bytes | `9FC1623B30BB7E308A910C4C43F8E8F8064E04765CB8D85567BD30B70F78B6FB` |
| E trace.txt, 7,445 bytes | `F99D09765EE45764B089C3BF87CAF2611579E299DE514D1C03AF52A12D07EF26` |
| E runner-error.txt, zero bytes | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| Domain values | `15BB2630BAE32FEF60C79B94252961C9EABF9B969337AC36814EF9A3BB73C697` |
| Infrastructure importer | `15218F0D7551C2CA9DEAB5574B8D3E8B9973E09BA657D1D0F0ABCFB0DA48B011` |
| Final Editor checker | `E9FF5679605C951CDF92F8D1B2A3FA5D52F3F7A627E293014A221D02D5A3D93E` |
| E 2,177-path source manifest | `0652B04AB7A13B48EED20F8210E301919CFF257E641AFED4E407EBFCDDFD1AB6` |

The [pre-edit provenance](S06D2B1L1DR1_PRE_EDIT.md) pins 2,171 existing paths, 15 reused declarations and nine schema files. All old paths remain unchanged; the six additions are exactly the three new source/meta pairs. Full frozen-source, required Domain266 and prior-reference28 hashes were checked after every run/build, including E. Editor/player absence was verified before thawing. Metadata GUID uniqueness, UTF-8 and whitespace checks pass.

## Windows checkpoint

`DragLab.Editor.PlayerBuild.BuildPlayerOnly` completed with actual process exit 0, success at `reservation-build-a-unity.log:8132` and return-zero marker at line 8160. Managed artifact:

- `Game/Builds/Windows/DragLab_Data/Managed/Assembly-CSharp.dll`: 4,105,728 bytes.
- SHA-256 `B68135DED78473C14BA7C9B5FA83A7D731310D092304BFC83CC97264F7AC3C33`, unchanged after E.

Build A uses source freeze C, `C492C4902FEC610E2EB18B090A12895C31C6539ACDD890649AC156EEC212529E`. D/E change only the entirely Editor-only checker. Fail-closed Roslyn parsing under player symbols yields zero errors and zero compilation-unit members, and the C-to-E manifests differ only at that path. Current runtime input therefore matches the built feature; no post-E whole-source build is claimed. `player-exclusion-proof-e.txt` retains the proof. An earlier failed GetRoot-overload attempt is marked invalid and excluded from evidence.

## Limits and next gate

The pure value/match route has no JsonUtility/parser, reflective serialization, whole-profile copy, Ensure, identity or RNG path. Domain hashes an opaque existing snapshotJson string after a baseline miss for stale binding: this is **not zero raw JSON reads**. The instrumented counter is not a global interceptor and is not used alone to establish the boundary. New serialization is isolated in the named legacy importer and Editor-only reference/fixture code.

The [contract](../../docs/save-refactor/S06D2B1L1D_RESERVATION_CONTRACT.md) explicitly retains the Unity JSON spelling-collision limitation. This introduces no cross-frame/worker freshness guarantee, live producer handoff, current business-command conversion or schema change. Future caller integration must preserve validated import/creation boundaries and lifecycle fences.

No Android/device, native responsiveness or visual acceptance is claimed. The complete L1D TotalOnly/FeeOnly/PhysicalOnly/Combined implementation and finite method gate remain next. The full refactor still must remove runtime JSON data manipulation and every full-profile save clone; this prerequisite alone does not establish those global requirements.
