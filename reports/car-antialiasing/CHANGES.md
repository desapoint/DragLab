# Car thumbnail antialiasing — 2026-10-18

Raster car layers now import with uncompressed mip chains and trilinear filtering. At thumbnail scale this averages the source detail instead of skipping thin roof outlines and wheel spokes. Full-size masters and the layering/repaint contract remain unchanged. Existing non-roster sprites, fonts and UI controls retain their settings.

All 200 layered textures remain above the native panel's 64-pixel dynamic-atlas size limit, so their filtered mip chains are actually sampled rather than replaced by an atlas without mips. Mip chains add approximately one third to these textures' storage/memory; this change does not add per-frame CPU resizing or thumbnail regeneration.

Checks run: 400 Unity filtering/atlas checks, 678 roster checks, Windows player build, and the full real-mouse Dealership suite (270 checks, zero runtime errors). Inspected the same Atlas/Aster cards before/after at 1280×720, plus compact, 1080p and true 4K captures. Small outlines/spokes are smoother; body colours and protected fixtures remain intact.

Local comparison: `reports/car-antialiasing/before-after.png`. Baseline: `reports/player-fixtures/raster-roster-dealership-20261018-a`; after: `reports/player-fixtures/car-filtering-20261018-a`. No physical Android-device result is claimed here.

Implementation reference: [Unity trilinear filtering](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/filtermode/trilinear) and [dynamic atlas texture control](https://docs.unity.com/en-us/engine/6000.7/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-performance-consideration-runtime/uie-control-textures-of-the-dynamic-atlas).
