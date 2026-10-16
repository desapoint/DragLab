# Built-in to URP migration — 2026-10-16

Status: technical checkpoint complete. User visual acceptance remains separate.

## Implementation

- Unity remains on 6000.6.4f1. URP 17.6.0 supplies the 2D Renderer and Render Graph. Graphics and every Quality level explicitly reference `DragLabURP`.
- Preserve Gamma color space, full render scale and the current unlit artwork. Use 2× MSAA, no HDR, no post-processing and no camera depth/opaque copies.
- Cars, track sprites, lamps and smoke explicitly share the URP Sprite Unlit resource material. A small URP mesh shader retains the road/line textures, vertex colors, transparent blending and sorting without depth writes.
- Save/reload checks validate the actual renderer, shader support, all six quality entries and serialized Graphics/Quality GUIDs. The three build scenes have no authored lights, lightmaps, probes or PPv2 effects requiring conversion/baking.
- Keep native/legacy UI, F8, physics, audio and the debug bench implementation intact. No later Slice 4 interface increment is included.

## Capture coverage and reproducibility

The local [comparison gallery](index.html) is generated from `before/` and `after/`. Every matched image has a 50% Pillow overlay and a difference image amplified 4×. NumPy reports changed pixels above 8 channel levels, mean/max channel error, changed bounds and newly magenta pixels. Images are not resized, aligned, masked or color-normalized. Alternate resolutions live in each suite's `resolution-tests/` subfolder; 4K is the established UI render-target check, not a claim of a physical 4K desktop.

Coverage includes the native Garage/owned details, dealership/inspection, all Settings categories, Tuning and saved setups, Build/shop/inventory/installed/details, component purchasing, cart, warnings, engine workshop/block preparation, maintenance/rebuild, sale/salvage, race selection, race HUD/shifters/pause/results, legacy main routes and F8 replacement states. The sound bench has all nine tabs and active dyno/overlay captures. The physical-bench visual survey records a real shared-simulation pull and replay, each graph group, diagnostics, component and sound panels.

One generated starting database is reused across both pipelines. Specific shop probes use a second owned-car fixture with stocked catalog rows so normal random availability cannot hide the controls being reviewed. These are isolated generated fixtures, never player assets or changes to market behavior. `RenderPipelineMigration.PrepareCaptureSeeds()` generates the inputs. The capture orchestrator creates fresh per-run fixtures and archives the prior current suite before replacing its images.

The original executable is preserved locally under `builtin-player/`. A historical-only `DRAGLAB_BUILTIN_CAPTURE` build define permits regenerating the baseline with the corrected capture probes; it is excluded from normal players. Temporary pipeline switching restores assets by persistent path because Unity can unload cached native references during builds. Restoration has been built and re-verified on disk.

## Checks and limits

- Python overlay tests: four pass, including exact/known pixels, missing images, dimension changes and failed-suite rejection.
- Screenshot-export cleanup/resolution/path-guard test passes.
- All 15 final suites pass under both pipelines: **411 before / 411 after images**, no missing pairs or dimension mismatches. Each phase reports **5,067 checks**, including overlapping setup/regression checks, with zero reported runtime errors. The full native menu suite contributes 1,534 checks, stocked Build 1,402 and workshop 480. See [coverage](coverage-matrix.csv).
- **167 pairs are pixel-identical; no pair introduces magenta pixels.** The remaining pairs retain measured differences in [the image matrix](image-matrix.csv). The largest changed area is 5.45562%; the largest mean channel difference is 4.82839 / 255. These are review measurements, not an automatic visual pass threshold.
- Gallery JavaScript syntax and all 822 generated overlay/difference paths pass validation. Asset metadata/GUID and saved shader/quality checks pass. The final player excludes the historical Built-in capture branch.
- Two old fixtures were corrected: the whole-interface probe still expected a JSON save path; the AWD stat fixture requested physical modes through tuning despite hardware restrictions. Gameplay contracts are unchanged. Engine mouse probes now wait for actual viewport visibility.
- **Pre-existing functional-test limit:** the physical-bench OS mouse suite fails on its tire-health interaction under Built-in as well. Exploratory gesture/viewport changes did not establish a fix and were removed. Its original implementation/test are retained for the deferred bench work. Rendering coverage uses the explicit scenario/replay visual survey, not a claim that this unrelated input suite passes. Raw failures remain in `attempts/`.
- The historical whole-interface input probe also encountered a Recovery/F8 focus assertion. The rendering survey directly selects each presentation; focused native suites continue to test actual F8/input behavior. No claim of a full legacy input-suite pass is made.

The largest-difference contact sheet and representative before/after/difference images were reviewed. Road/asphalt shading, car tint/edges, lights, finish checks, gauges and shared controls remain intact. The largest mean difference is a legacy Parts performance estimate completing asynchronously in one capture, changing text and the following layout. Some full-menu probes regenerate market state during their run: the used-inspection pictures select different cars, with corresponding cylinder counts, specs and component rows. A maintenance quote uses stocked units in one capture and express units in the other, changing the price and dialog height. These images cover those interfaces but cannot establish pixel equivalence of identical data. No alignment/masking was used to hide them. File paths, quote identities, live races, hover shine and bench trace values also differ with timing. The isolated initial databases match; subsequent full-suite state is not wholly deterministic.

The tested URP player is installed in `Game/Builds/Windows`; the prior installed build is preserved locally. Installation verifies SHA256 parity for every copied file, followed by a packaged startup smoke check. No unrelated user changes are included in this checkpoint.

## Review checklist

- Inspect road shading, car tint/edges, start lights and finish pattern.
- Compare native controls, fonts, dialog scrolling, dyno lines and warning colors.
- Inspect legacy and bench plots, including replay overlays.
- Review compact, 1080p and 4K target images in the gallery.

No visual acceptance is inferred from automation. Source/configuration and concise matrices will be committed; generated images, binaries, databases and raw logs remain local.
