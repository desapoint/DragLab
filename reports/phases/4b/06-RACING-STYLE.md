# Native shell revision — racing style (2026-10-10)

User-requested correction to 4B.2/4C.0 after `94fb0c9`. **Ready for visual review, not accepted.** Only the existing navigation/buttons/empty Settings shell change; individual preference controls, explicit Save/dialogs and the inspection drawer remain pending.

## Delivered

- Reference-led dark racing bar with fine carbon diagonals, filled icons and condensed italic navigation. Actual cash/fuel/REP and the right/full-height Settings gear retain their existing behavior.
- Active navigation keeps a circular red glow rising from the bottom center, with a brighter inner core, upward/sideways fade and a fine tapered lower accent. Unselected destinations share the dark bar rather than appearing as unrelated flat tiles.
- Buttons use subtle smooth tonal shading, small rounded edges and crisp light/dark bevels. The user's rejected large blocky bands/permanent slash facets were removed. Red primary Settings remains multitone.
- Brief diagonal shine on pointer entry or keyboard focus; completes in 0.65 seconds. No perpetual timer or static-button repaint loop. Geometry is cached afterward; hidden presentation does not repaint animation. Decorative children ignore picking, leaving existing actions authoritative.
- Bundled Barlow Condensed Bold Italic for navigation/brand and Medium for tabs/resources. Original sources, hashes and SIL OFL license are retained in [font provenance](../../../Game/Assets/DragLab/Resources/UI/Fonts/PROVENANCE.md). Stable TextCore assets are generated through the Editor and preserve GUIDs on subsequent builds.
- Opaque static page background `#181c21`. Removed redundant page/category headings, descriptions, placeholder cards, unnecessary car identity text and duplicate hover footer labels. Only the short F8 hint remains. This concise-copy rule is now in the [native design contract](../../../docs/NATIVE_UI_DESIGN.md), objective and plan/status records.

Final shading uses vertex-interpolated Unity UI Toolkit geometry with analytic radial alpha, not a resized UI bitmap. Screenshot review caught the initial Painter2D gradient path rendering as a solid color in the player; replaced that path before final verification. The selected-tab pixel assertion now verifies the actual upward/sideways fade. Earlier attempts are not acceptance evidence.

## Checks actually run

- Unity 6000.6.4f1 import/compile and Windows development build passed. Player: `Game/Builds/Windows/DragLab.exe`; launch with its full supporting build folder.
- [Final native probe](06-native-verification.txt): **99 checks, zero runtime errors**. Real pointer/Tab/F8 input; bundled fonts actually resolve; selection glow is verified from rendered pixels; shine advances, finishes and then retains cached geometry. Navigation, category retention, isolated profile/save preservation, empty garage, resize and fullscreen/windowed restoration pass.
- [Final comparison regression](06-comparison-verification.txt): **48 checks, zero runtime errors**. Exact drafts/cart and held throttle in race/both benches remain intact. Historical “empty replacement” labels in this probe describe route/mode assertions; the native probe verifies the rendered controls.
- Inspected final 1280×720, 1024×768 and true 3840×2160 UI render-target captures. 1080p/fullscreen checks pass; requested 4K desktop window is still monitor-limited to 3360×2160. Render-target coverage is not a real 4K hardware/input test.
- Captured shine frames change **1,478 button pixels**; sampled empty page pixels all equal `(24, 28, 33)`. Raw evidence remains ignored under `reports/player-fixtures/native-menu-racing-final/`.
- UTF-8, local document links, source font SHA-256 hashes, new asset metadata/GUIDs and staged whitespace checked. Windowed project defaults unchanged.

Earlier physics/economy acceptance is retained, not rerun. No Android, controller/mobile acceptance or full-screen performance budget is claimed. The approved Save/draft behavior remains future work; old controls stay reachable via F8.

## Review

1. Launch the updated player; **F8** opens the new presentation. Select Garage/Build/etc. and inspect the persistent circular bottom glow.
2. Hover a destination or use Tab: inspect the brief shine and softer shading. Open Settings and compare its category buttons at the current window size.
3. Validate this style before requesting the next control. No subsequent control or phase starts automatically.
