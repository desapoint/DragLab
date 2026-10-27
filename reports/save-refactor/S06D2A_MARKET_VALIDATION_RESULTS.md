# Parts-market validation identity-set component

Date: 2026-10-26 (local). Scope: replace only the transient identity-set construction in `PartsMarket.Validate` with ordered direct loops. Live Parts UI, typed cart previews and full S06 remain open.

## Evidence

- Standalone Editor42758 exits0 with217 checks. The frozen full original validator is the oracle; comparisons include exact acceptance/first error, exception type/message, eager null-source checks, malformed rows, null/empty IDs, descendant filtering, source order and collisions across owned parts/members, engines, loose units, dealer stock and vehicle listings against actual offered engines/parts/members.
- Real fixture: seed16102026,2 cars,1081 owned parts,59 vehicle listings,2362 listing parts and4 actual market engine offers. Ten direct full-validator samples after one warmup: original median7.8011 /max11.1546 ms; current median6.2214 /max9.9181 ms. Allocation bytes were not measured. These are directional Editor component timings, not native frame or whole-game acceptance.
- Source inspection confirms no JSON/profile-copy work inside either timed direct-validator call. This is source evidence, not a global serializer counter interception assertion.
- An independent static extraction confirms the entire original `Validate` body is text-identical except its identity-set expression. UTF-8 and metadata presence checks pass.
- Raw log: ignored `S06H1/market-validation-corrected-editor.log`; checks/timings and unique fixtures: `S06H1/market-validation-component/`; frozen manifest: `S06H1/market-validation-corrected-source-sha256.txt`.

Full managed63595 exits0 on the frozen corrected source. Logged totals:7 encoder,37 cache,1821 vehicle validation,81 pricing,214037 browser,13384 ready ledger,1691 base readset,1615 catalogue and37702 supply. Counts include timing notes and bounded-loop checks. Log: `S06H1/market-validation-full-editor.log`.

Windows2041 exits0 with Build Successful /DRAGLAB BUILD SUCCESS in `S06H1/market-validation-windows-build.log`. The frozen manifest still matches focused42758, full managed63595 and this build. The build compiles the whole worktree, including pending UI sources; the standalone component runner is independently scoped to the market validator.

Independent Luna-high review is **PASS for this isolated component**, recorded in [the review](../../docs/save-refactor/S06D2A_MARKET_VALIDATION_REVIEW.md). It covers source, focused parity, full managed regressions and the same-source Windows build. It does not approve live UI, full S06 or frame performance. Verification continued into2026-10-27 local time.

## Retained failure

Standalone7633 compiles and reaches109 passing checks, then a test wrongly expects a vehicle-listing validation error from this direct market leaf. The original oracle and current market validator both enumerate the malformed listing source instead. Related null-row and first-purchase expectations were corrected to the actual original method boundary, without changing production code or the frozen oracle. Original failed check output is retained as `S06H1/attempt-7633-market-validation-checks.txt`.

## Limits and remaining work

No identities, conditions, stock, resources, pricing or validation predicates are cached or skipped. Direct loops preserve original LINQ source-null preflight order, unconditional instance IDs including null, member filtering and malformed-record timing. The resulting set remains operation-local. Iterator removal is evident in source; its share of native allocations and frame stalls remains unproven.

The current UI candidate still has actual >50 ms frames, including interactive Move previews using whole-profile JSON copies. The full raw intervals stay in evidence; nested `raceGUIMs` and `nativePartsMs` are not additive. Typed nonempty/multi-position preview, owned refresh, all remaining S06 gates and S07–S14 still must be completed. No exception to the final no-runtime-JSON/no-full-profile-clone requirements is granted.
