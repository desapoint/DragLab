# Raster roster checkpoint — 2026-10-18

All 25 model families / 52 configurations now use distinct raster bodies in native car previews and races. Trims share their model body. Existing saves resolve stable vehicle IDs without changing saved art IDs or physical specifications. Unknown/custom vehicles and debug benches retain the existing fallback.

Paintable panels use a neutral foundation; glass, lamps, trim and unpainted fittings retain their own colours. Wheels/brakes remain separate. The approved existing wheel artwork is reused; distinct shop rims are not included in this checkpoint.

## Checks actually run

- Reproducible preparation of all 25 masters; fresh-directory replay matches all generated PNG/manifest bytes.
- Five contrasting repaints per model preserve opaque protected-fixture RGB exactly; red/blue changes reach every exposed painted pixel.
- Skill QA and visual inspection: native axle overlays, paint/fixtures/underlays, dark/white backgrounds, assembled cars and wheel rotations. Technical reviews/package manifests are bound to the current content fingerprints.
- Unity roster verification: 678 checks passed, covering all 52 configurations, distinct model identity, fallback, immutable specifications, layer order, repaint isolation and independent axle geometry/rotation.
- Windows development player build passed.
- Real mouse Dealership suite: 270 checks, zero runtime errors; starter ownership, dealership, details/comparison/filtering and compact/1080p/4K captures.
- Race HUD suite reached 232 checks, zero runtime exceptions, then failed the existing paid-refresh pacing gate: `parts/layout and selection` took 293.590 ms (limit 50 ms). Retry itself passed: fresh session, cleared slip and independent scoreboard lanes. This is a real performance issue, tracked in the next requested audit; the full Race HUD gate is **not passed**.

The current small-preview filtering is a baseline for the user's next antialiasing request. No Android/device verification is claimed for these new bodies yet. All-model visual acceptance remains with the user.

Local offline review: `reports/roster-cel-v1/index.html`; player evidence: `reports/player-fixtures/raster-roster-dealership-20261018-a` and `raster-roster-race-20261018-a`.
