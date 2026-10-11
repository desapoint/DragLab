# 4D.9 refinement — Affordability filter (2026-10-11)

The user requested the missing Affordable filter. Garage → Dealership → Filters now offers **Affordability: All / Affordable / Unaffordable**, using the actual current dealer price and cash balance. An equal balance counts as affordable. REP locks and purchase guards remain independent; combine Affordable with Availability → Locked to find within-budget models awaiting unlock. Unknown prices match neither budget choice.

Reuse the joined-choice theme, filter count, independent dealership query, and draft Apply/Cancel/Reset. Toolbar reset clears affordability immediately. The purchase-budget row is absent from owned-car filters. No prices, economy, save behavior, simulation or other Garage controls change.

## Checks

- Unity 6000.6.4f1 Windows development build passed; player/supporting files updated.
- Focused native dealership probe passed **66 checks / 0 runtime errors** ([evidence](17-dealership-focused-verification.txt)). Seventeen added checks cover actual choice gestures, inclusive cash boundary and balance refresh, over-budget complement, REP independence, Apply/Cancel/Reset, query retention, unknown prices, and unchanged live assets/saved bytes/cached simulation probes.
- Full native UI regression probe passed **590 checks / 0 runtime errors** ([evidence](17-native-verification.txt)), retaining Settings, owned-car query, inspection, maintenance and dealership purchase gates. Final build log has no compiler or USS errors. UTF-8/LF and diff whitespace checks pass; build-only font serialization whitespace was normalized without content changes.
- Reviewed the actual filter screenshot at 1280×720: same joined choices, scroll container and native actions. Existing dealership compact/1080p/4K render-target gates pass in the focused probe; physical 4K display and human acceptance remain pending.
- Historical physics/economy audit results are retained; no new simulation behavior requires a rerun. Raw captures/build/logs/fixture saves remain ignored.

## Review

Open Garage → Dealership → Filters, scroll to Affordability, choose Affordable and Apply. Combine it with REP locks or other criteria; compare Cancel and Reset. This completes only the requested filter refinement, pending user review.
