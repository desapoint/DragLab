# S06d2a VehicleListings exact positive proof review

## Scope and verdict

Independent source, focused test, full managed-suite, and Windows build review approves this isolated validator component. The approval is limited to `VehicleListings.Validate` and its positive exact-value certificate. It does not approve pending Parts UI changes or the live S06d2a gate.

## Source review

The original ordered validator remains as `ValidateCore`; misses run it unchanged, and only a successful `null` result can store a certificate. A hit is scoped to one `PlayerProfile` weak-table bucket and requires the same catalog object, a current ready authored token, and exact comparison of all currently relevant validation inputs. Mismatch retires the candidate before full validation. Invalid states, null/malformed data, and cold authored content do not produce certificates. The code uses no JSON serialization, no profile-wide or owned-car snapshot, no revision-only shortcut, no price cache, and no worker-thread handoff.

The reviewed dependency closure covers market headers, first-purchase state, ordered listing and history data, every offer scalar/resource/tune field, offer-part identity and complete component/clutch/member condition, ordered owned-part IDs, and the catalog's private copied specs plus exact authored definition content. Float/double comparisons preserve exact bit patterns. Unsupported covariant member arrays skip certificate capture and retain full validation. The weak table only chooses a profile-local bucket; it does not establish content equality.

The standalone Editor runner calls only the focused validator checker and its domain/catalog prerequisites. It does not depend on pending Parts UI classes. Its test result and timing files are written to ignored `reports/save-refactor/S06H1/vehicle-proof-component` output.

## Verification evidence

Focused Editor run `85888` exited 0 with 1,821 assertions passing. Its full validation fixture uses seed `16102026`, two cars, 1,081 owned part rows, 59 listings, 2,362 listing parts, and all 9,294 authored definitions. The probe requires all supported concrete engine-member types, clutch state, AWD data, tire data, and both null/populated nested shapes. It checks exact field invalidation/restoration, same-revision A-to-B rejection and fresh revalidation, first-error/exception parity, cold-token fallback, distinct-profile/catalog isolation, scoped zero-encoder behavior, and equivalence for valid `TireCondition[]` versus ordinary `EngineElement[]` member storage.

The ten-sample timing records proof-hit median 4.2927 ms / maximum 6.8542 ms, direct full-core median 21.3620 ms / maximum 23.9794 ms, initial miss/store 34.4852 ms, and restored-state core/store 35.4919 ms. These are diagnostic samples, not a frame threshold or a guarantee for every runtime. They show the exact proof costs less than the measured full validation path on this fixture while retaining current-input comparisons each call. The original core's separate full-stage median was 19.3028 ms.

Full managed Editor run `54385` exited 0 on the same frozen source manifest as focused run `85888`: 7 encoder, 37 cache/query, 1,821 vehicle validation, 81 display-price, 214,035 browser, 13,381 ready, 1,685 base, 1,596 catalogue, and 37,699 supply checks passed. Windows build `50844` then exited 0 on that source manifest; the log records “Build Successful,” “DRAGLAB BUILD SUCCESS,” and clean batch-mode exit code 0.

## Limits

This component review does not clear the live Parts interaction or frame gate. Native run `20408` remains a failed live candidate: it passed the reported real-card/detail and stale-confirmation paths but failed three full-frame samples over 50 ms, including two populated Inventory frames. Full S06, nonempty/multi-position preview, owned refresh, Android, human visual acceptance, and S07–S14 remain open.
