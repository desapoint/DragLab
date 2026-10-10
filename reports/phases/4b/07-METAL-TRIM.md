# Native shell revision — metal and red trim (2026-10-10)

Requested visual correction after `ec297e8`; **ready for visual review, acceptance pending**. This remains within the current 4B.2/4C.0 shell checkpoint. No preference controls, Save/dialogs, inspection drawer or later phase introduced.

## Delivered

- Settings uses a red parallelogram face with bright upper/left bevels, dark lower shading, small glints and highlighted gear edges. Its 80-logical-pixel-wide control stays right-anchored and full bar height; the hit area includes the clear corners around the slanted face. Icon remains centered with its original proportions.
- Header has partial red bottom accents at both ends and short terminal chevrons. The middle remains neutral metal. Footer has a segmented red upper strip, metal center break and raised angular corner/chevron details.
- Background is static brushed metal: soft dark lighting, fine irregular diagonal brushing, subtle deterministic grain and sparse scratches. Buttons receive restrained grain. No photograph, moving background, extra labels or repeated descriptions. This supersedes the previous flat-color background direction in the [design contract](../../../docs/NATIVE_UI_DESIGN.md).
- Detail uses native geometry and per-vertex color/alpha; no enlarged screenshot, new texture imports or raster noise atlas. Geometry remains cached after creation/resize. The local deterministic generator does not consume Unity's gameplay random state. Background/trim ignore picking. Captured background pixels remain identical between successive button-shine frames.
- Retains circular bottom navigation selection glow, condensed fonts, brief hover/focus shine, real resources, old/new route ownership and window defaults. Shine uses real elapsed time; child-origin pointer/focus events cannot restart a parent's sweep.

## Checks actually run

- Unity 6000.6.4f1 import/compile and Windows development build passed. Updated player: `Game/Builds/Windows/DragLab.exe`, with supporting build folder required.
- [Native probe](07-native-verification.txt): **104 checks, zero runtime errors**. Rendered pixels verify both slanted plate edges, partial red header ends with neutral center, footer segmentation and retained circular navigation glow. Real mouse checks include the transparent Settings corner hit area. Hover/Tab shine completes, inactive button/background geometry remains cached, F8/categories/profile/disk save stay intact.
- [F8 regression](07-comparison-verification.txt): **48 checks, zero runtime errors**, including exact tune/cart state and held throttle in race and both benches. Historical “empty replacement” assertion labels refer to route/mode checks; native probe checks actual rendering.
- Final screenshots inspected at 1280×720 and 1024×768 and true 3840×2160 render target; 1080p/fullscreen/layout checks passed. Requested 4K desktop window remains monitor-limited to 3360×2160; offscreen 4K is rendering coverage, not physical 4K device input acceptance.
- UTF-8, local documentation links, UXML control names, metadata/GUID preservation and staged whitespace checked. Project window defaults unchanged. Raw captures/logs/isolated profiles remain ignored under `reports/player-fixtures/native-menu-metal-final/`.

First probe passed the new rendered shape/trim assertions but exposed shine-completion failure. Switched its decorative clock to real elapsed time and filtered redundant child-origin entry/focus events before the final passing run. That initial run is not counted as acceptance evidence.

This checks static caching, not a complete UI performance budget. Earlier physics/economy acceptance is retained, not rerun; Android/controller/mobile and further workflow migration remain pending.

## Review

1. Launch the updated player and press **F8**. Inspect the Settings bevel/shape, partial header accents, footer corners and brushed-metal background.
2. Open Settings, hover/use Tab and resize the window. Confirm the surface detail stays subtle and does not obscure labels or resources.
3. Validate this visual revision before the next control. No subsequent implementation starts automatically.
