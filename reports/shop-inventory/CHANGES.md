# Shop, inventory and condition comparisons — 2026-10-09

This finishes the requested increment following `adbab44` (contact-modulated tire sound, audible threaded opponent) and `e8ffe2b` (background menu probes and definitive settings saves). No later slice/content phase is advanced.

## Using the interfaces

Garage has **Shop / parts cart** and **Inventory / retained spares** buttons. Parts has distinct **Installed**, **Inventory** and **Shop** pages; the selected slot/category filters the view. Inventory can show all categories and optionally only compatible parts. Incompatible retained spares remain visible/sellable with installation disabled. Stored engine trees remain accessible through Assemblies. Installed service/replacement shortcuts remain available, and the shop retains its existing immediate single-item buy shortcuts.

Add offers across categories to the cart, change quantities, and check **Install one after buying** for the intended slot replacements. Extra copies and unchecked offers become inventory spares. Checking another offer for the same slot clears the preceding installation selection; both offers may still be purchased. The preview overlays the cumulative checked hardware on the saved tune/current condition. Nothing is purchased, assigned an owned identity, applied to the race car or saved by cart editing. **Buy cart / install checked** publishes the complete purchase, installations, preserved displaced identities, cash and one operating receipt with one recoverable save. Insufficient funds, incompatible/locked offers, conflicting installs, stale/expired/replayed quotes and failed saves cannot partially publish a purchase.

The cart is temporary for this session/selected owned car; changing cars or resetting progression clears it. Quantities are 1–99 per offer, up to 100 distinct offers. Quotes use the operating policy deadline and capture ownership/resources plus catalog definitions; a changed context requires fresh checkout. This is not a persisted wishlist.

## Worn versus healthy

When relevant fitted condition is degraded, stats include a blue **no-wear reference**: the same installed hardware/tune, component temperatures, fuel and fluid quantities, with copied part/individual-element/tire condition restored. It clears copied faults, deposits, leaks and maladjustment and restores wet-clutch oil condition without adding oil. Missing slots remain missing; actual owned condition/resources never change. With a proposed part/cart, grey is current, green is cumulative preview and blue is the current installed build without wear. Without a proposal, the summary/detail comparison shows current versus no wear. The reference can be hidden.

Engine curves can overlap when damage affects grip, coupling or braking rather than engine output; the quarter-mile, grip, clutch and braking stats expose those effects. Curves remain settled engine output; they do not claim to be wheel horsepower or transient turbo response. GUI-based chart strokes respect the stats area, a positive detail viewport prevents clipped layout, and verbose installed component ratings are a foldout to avoid redrawing hundreds of offscreen labels. The existing detailed explanations remain available. Audio preferences, hardware tuning permissions, keyboard/slider ownership and Apply/Revert are retained.

## Responsiveness and saves

The pre-change inspection workflow repeatedly spent about 1.24–1.28 seconds inside OnGUI when changing previews. The same new full workflow has GUI p95 12.4 ms / maximum 31.2 ms after startup exclusion. Final cart-workflow tuning gestures had frame median/p95/max 10.4/21.1/23.8 ms and no frames above 33.3 ms in that window. A held driving-settings slider did not alter the profile file for 1.2 seconds; releasing saved its final value. Service price previews and hardware resolution are cached; one bounded background task uses copied cars and ignores stale results.

Full saves at checkout, Apply, race entry/exit and other definitive operations remain synchronous and can pause briefly: the final shop window reached 137.5 ms at a transaction, and the full inspection workflow reached 386.8 ms around race transition/full-save work. Background physics still allocates and can contribute to global GC pauses. This change removes synchronous full-pass preview stalls; it does not claim all menus are allocation-free, exports are asynchronous or every frame is hitch-free. Opponent paired batch timing improved 22–26%; this is not an FPS promise. See pacing-summary.csv, [opponent parity](../race-audio-worker/CHANGES.md) and [menu probe implementation](../menu-preview/CHANGES.md).

The old partial inspection fixture stopped at a visual assertion that incorrectly assumed an exclusive artwork radius per model. Shared artwork uses a nominal art radius, so the current assertion compares the applied multiplier to its captured stock visual scale. The before timing remains a limited baseline; the corrected new workflow completed all 19 named checks. Some horizontal bands visible behind the translucent left menu are road decoration, not chart curves.

## Validation actually run

| Gate | Named checks | Result |
|---|---:|---|
| RacePresentationVerify / eight-car exact worker parity, outcomes, grip/load spectrum and attenuation | 163 | PASS |
| MenuPreviewVerify / real background math, immediate/full dyno parity, owned-state isolation, audio migration/persistence | 6 | PASS |
| CartVerify / eight-car cumulative preview/checkout, save rollback, resources/identities, faults, stale/expiry/replay/quantity bounds, legacy contact arrays | 67 | PASS |
| TireSoundVerify / DSP headroom/reset/continuity plus shared contact/effects/export checks | 71 | PASS |
| Analyze-TireAudio / screech, upper-hiss and narrow-band limits | 12 | PASS |
| Windows Shop / cart, inventory, healthy graph, settings save gestures, opponent DSP/mute/pause | 16 | PASS / 0 errors |
| Windows Inspection / previews, Apply/Revert, visible wheel radius, practice entry, units | 19 | PASS / 0 errors |
| Windows Summary / pinned changes, scrolling, strength/grip/life and units | 10 | PASS / 0 errors |
| Windows physical tire effects / FWD, RWD, AWD | 22 | PASS / 0 errors |
| Windows acoustic tire preview / shared master, mute, pause/reset | 6 | PASS / 0 errors |

Total: **392 named checks**, plus helper visibility/wait assertions. The final Windows player built successfully. Actual Windows mouse fixtures controlled only their isolated test player; another user's player copy was left untouched. Raw logs, WAVs, screenshots, frame CSVs and generated profile/audio settings remain local and ignored. The concise reports and matrix are versioned. Historical full Slice 2/3 foundation/matrix gates were retained, not rerun. No gameplay/save schema or tire/contact force calibration was changed. Optional presentation diagnostic arrays are null-safe for older wheel checkpoints.
