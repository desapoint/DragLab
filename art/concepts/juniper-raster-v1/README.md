# Juniper raster style review - 2026-10-18

Current candidate: `04-strong-outline-preview.png`, generated with the built-in image tool. This review iteration strengthens the exterior/panel outlines and simplifies the shading following the user's request for a more 2D finish. Original output bytes and earlier iterations are preserved. Exact earlier prompts are in `prompts.json`; this edit is recorded in `04-strong-outline-prompt.txt`. File hashes, dimensions and alpha counts are in `assets.json`.

The user requested believable real-car proportions with a more 2D, cel-shaded raster finish, and explicitly rejected procedural/vector body drawings. The third candidate has no wheels, tires or brakes: both arch openings are transparent. The earlier wheel-bearing iterations record the style development and are not proposed game layers.

Awaiting explicit user review of this first model before creating any additional car or wheel sprites. Not integrated into Resources or a packaged build. The rejected procedural experiments and renderer wiring were archived locally; production rendering remains unchanged. The separately tested staging-idle fix is committed as `e15cae0`.

Approved scope to resume after style review: distinct designs across all 25 model families/52 trims, old and modern silhouettes, varied lengths and axle spacing, plus a visual for every distinct shop wheel product. Trims may share their model body. Wheels, tires, rotating rotors and stationary calipers remain separate layers. Actual per-corner fitted rims and tire sizes must carry through menus and races, with no physics, identity, condition or save-state changes implied by artwork.

Checks run for this review artifact: RGBA transparency and empty former wheel centers in both openings; source hashes recorded. Full measured layer preparation, hub/occlusion/rotation/recolor QA and runtime checks remain pending after approval. No Unity rerun is required for these files outside the game project.
