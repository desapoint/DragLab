# S06d2b1l0 — canonical boundary diagnostic

Baseline main `99ad759`. This is the finite Editor-only diagnostic authorized by [L0 decisions](../../docs/save-refactor/S06D2B1L0_DECISIONS.md) and [static review](../../docs/save-refactor/S06D2B1L0_REVIEW.md). Production domain helpers remain unchanged. L1 implementation, full Step1/S06 and S07–S14 remain pending; runtime JSON manipulation and profile mirrors remain forbidden final-state dependencies.

## Execution

The 1,576-path source-a manifest SHA256 is `040537940C6C876FA9B1C283C5D1A0361AB5BA0CD2F4D0B921A22E433305AED3`. After an interrupted automatic-approval availability check, root revalidates the unchanged manifest on 2026-11-01 local time, confirms no actual Editor/player, updates Unity CLI from beta.14 to current `1.0.0-beta.15` and confirms that binary version. Editor is `6000.6.4f1`.

`canonical-a-20261101.log` exits1 before probes because the new literal reference lacks `using DragLab.Domain;` for PlayerProfile (CS0246). No diagnostic outcomes are credited. All 1,576 source hashes remain unchanged after terminal exit. Escalated process inspection finds only the AppData CLI helper, with no actual Editor/player, before root repairs that namespace import. Literal frozen method bodies and production code are unchanged by the repair.

At that point corrected execution and independent evidence review remained pending; subsequent attempts and final disposition follow below. Raw logs/manifests stay local ignored artifacts. No build, native/device/performance or new runtime behavior is claimed. Unrelated existing changes remain unstaged.

Source-b manifest SHA256 `72959E0BB55E7882EA9E595F5AAF8D37EA94984211DB46DD5743026FB01C18C8` covers the same 1,576 paths. `canonical-b-20261101.log` exits1 before probes: checker line457 passes void CartReservations.Capture into a Func<object> diagnostic wrapper (CS0029/CS1662). All 1,576 hashes remain unchanged; actual Editor/player absence is verified before the Luna-medium author changes only that lambda to call the same operation and return null. Checker SHA256 becomes `F718EF5D79FDC1C880907D0D3C65D21521362076239ADCB6C8FD97100C7F0BF7`. No expected behavior, comparison or production method is changed; no outcomes from B are credited.

Source-c manifest SHA256 `592815320861FAC5E13F5CAFC903BD23EB99CEB8E9F0569A59B72C37A2007594` completes before `canonical-c-20261101.log`. That run exits0 with **130 PASS/0 FAIL**, folder `S06D2B1/canonical-boundaries-20261102-035130462`. Checks SHA256 `8DF8610FD36F35601077A0F3B62A27334E2D681FC5E8B48E092C7ABEC6449BE1`; trace `A2FDA9B1531B0C6B483F9B71D2D662250DDB0D7D74ED15068CE15F1614492BAE`. All 1,576 hashes remain unchanged and actual Editor/player absence is verified before further edits. The reviewer independently confirms the artifacts and findings.

C observes five nullable cart string null/empty mismatches in the ordinal hypothesis, equal positive/negative-zero product keys despite different cache freshness, cold index writes and distinct Stock/Ledger/held-supply behavior. These are facts about the old paths, not acceptance of a typed replacement. Its labels accidentally include complete product-key bodies, and null-enumerable hypothesis reporting treats two false flags as agreement despite the literal exception. Those diagnostic reporting issues are corrected before D. C remains evidence only for its existing vectors.

The bounded D update adds explicit null/default/zero-row cross-pairs, null/empty position entries, description null/empty and AWD null/default product pairs; five nullable cart strings have explicit null/empty pairs in both directions. Key logging uses only length/hash/error; erroring signatures are NOT-COMPARABLE to the boolean hypothesis. It does not change production methods or previous expected outcomes. Corrected checker SHA256 `F54A9535E9C43D1DF43A29368C7F5E28DEDE1DACE2BF625939A7A145E9A07C33`.

Source-d manifest is `reports/save-refactor/S06D2B1L0/source-d-sha256.txt`, SHA256 `D49E7963E857C11A4834BCA5193E7C26C775441260109AD11B92D0E58F8C4B62`, covering1,576 paths. Capture completes before `canonical-d-20261101.log`, which exits0 with **150 PASS/0 FAIL**. Folder `S06D2B1/canonical-boundaries-20261102-040313364` has checks SHA256 `325730818087B124E012F9D8E22DA57E6CB53C5F3DEDCB54EB898C067AD25F90` and observation trace `E3233662F204C401EF0D953021584ED32C1AAE819984BA39924B0B8C0F04E9D3`. All1,576 hashes remain unchanged after terminal exit. Escalated process inspection confirms no actual Editor/player. No OPEN or runner-exception record occurs; key bodies are absent from final check labels.

## Observed boundaries

| Boundary | Actual Unity outcome |
| --- | --- |
| Product ID | Changing only id retains the key; freshness remains a separate comparison. Scalar, name, enum and nested calibration changes in the finite probes change the key. |
| Product effective defaults | Positive/negative zero have equal keys. Description null/empty and nested AWD null/default have equal keys. This does not establish every possible numeric or authored-field equivalence. |
| Stock versus Ledger | Cold Stock/Available publish an index and use direct-ID/first-equivalent fallback. A warm direct mapping wins for Stock, while the Ledger retains its own first-product row. |
| Duplicated stock definition | Stock throws ArgumentException after leaving a partial index. The isolated Ledger's assignment route returns the later row's13 units. |
| Held supply | The fixture reads4 held units through its direct/cold route, and0 through a warm alias mapping to a different row's definition membership. |
| Cart default rules | Null and empty nullable strings compare equal in both directions. Null and default-constructed rows compare equal; null and a zero-quantity row differ. Null/empty position lists and null/empty string position entries compare equal. |
| Cart ordering/errors | Reordered positions/rows differ. A null enumerable throws ArgumentNullException(source); its boolean hypothesis is explicitly NOT-COMPARABLE. |

The ordinal hypothesis deliberately remains mismatched where it lacks those rules. A passing harness proves literal legacy-reference parity and the finite observations, not a production typed comparator or stock overlay. Comparison-level effective defaults must never mutate or heal source records. Existing freshness equality must remain separate from product-equivalence equality.

## Review and scope

Final independent Luna-high review **approves this bounded diagnostic checkpoint**, [review](../../docs/save-refactor/S06D2B1L0_REVIEW.md), SHA256 `3B55BF9B8A42ADE8CD44B0F5F5F3AEC8FC37482BE4289EB6B5EBE8EE0779B1A4`. The reviewer independently verifies the final checks/trace and all1,576 source-d hashes, with zero mismatches. There are13 deliberately unresolved ordinal-hypothesis mismatches; they are retained findings, not waived comparator failures. L1 implementation is still blocked pending an exact typed equivalence/route contract and frozen mutating closure.

No production domain method has changed (`git diff --exit-code -- Game/Assets/DragLab/Scripts/Domain` is empty). The new probes/reference are UNITY_EDITOR-only, and the sole existing-code edit adds an Editor batch entrypoint. No player build, UI/native/device, performance, save or simulation acceptance follows from this diagnostic; the latest verified Windows build remains K. No bench, UI/theme, physics/audio or authored content is modified. Full Step1/S06, remaining callers and S07–S14 stay open. Both new asset GUIDs are unique; source whitespace checks pass. Explicit staging and cached checks precede the local commit, leaving unrelated changes untouched.
