# Settings palette and layout correction (2026-10-10)

Requested correction within the current native Settings checkpoint. Technically ready for review; **human visual acceptance remains pending**. No later interface or phase is started. [Settings behavior](08-SETTINGS.md) and [compact controls](09-CONTROL-POLISH.md) remain the retained baselines.

## Delivered

- General's Reset game is right-aligned within its data row. It uses a charcoal face and accent outline, keeping Apply visually primary. The existing reset dialog, actual reset operation and preference retention are unchanged.
- Panel corner highlights now fade within 80 logical points. Border edges are subdivided before evaluating the fade: merely lowering the old distance value still interpolated corner color across an entire long edge. The user found the first 48-point correction too small; enlarged to 80 points while retaining a local fade. Both red rounded corners remain visible; long edges are neutral.
- Main Settings scrollbar has a 10-point right inset and symmetric 12-point top/bottom insets inside the frame. The thumb remains eight points wide; compact mode retains the same right/vertical alignment. Existing dialog scrollbar styling is preserved.
- `Game/Assets/DragLab/Resources/UI/NativePalette.uss` owns `--racing-accent` (currently `#f52235`) plus core background/text/metal/panel face tokens. Change this asset to adjust the native palette; there is no new player Settings control. Native USS uses those variables and procedural header/footer/nav, red faces/borders, switches and slider fills derive shading from the same accent. Grey contrast details remain intentionally neutral. Legacy IMGUI remains its existing presentation.
- The procedural palette reader walks ancestors: Unity's `customStyle` lookup does not expose the root token directly on every child. Root palette resolution invalidates cached decoration only when the root's custom style resolves; child hover cannot trigger a whole-menu repaint. Single-sweep hover, toggle animation and draft-only preference behavior are preserved.

## Verification

- Unity 6000.6.4f1 import/compile and Windows development build passed. Updated `Game/Builds/Windows/DragLab.exe` with supporting folder.
- [Native/UI verification](10-native-verification.txt): **213 checks, zero runtime errors**. Actual reset row alignment; rendered local 80-point fade/neutral long edges; frame-aligned slim scrollbar; root palette token/ancestor reads and matching header wordmark. Prior real mouse/keyboard, Apply/Cancel, reset/preference retention, durable recovery, thermal dependency, F8-draft, animation/cache and responsive checks also pass.
- [F8/input regression](10-comparison-verification.txt): **48 checks, zero runtime errors** on the final build. Preserves held race/bench throttle, route/input ownership, drafts/cart and focus/gesture/dialog guards. Historical empty-replacement assertion names are retained.
- Inspected populated Difficulty/Driving and General captures for shortened corners, scrollbar alignment, subdued reset action and consistent red. Default resizable 1280×720 window retained; compact/1080p/true 4K target coverage follows the existing probe. A 4K render target is not physical 4K input acceptance; desktop window remains monitor-limited to 3360×2160.
- Initial build hit an editor-startup Project Auditor native crash before the game build. Retry reached compilation; a new verification-only Color32 conversion error was corrected. The first rendered probe caught ancestor palette lookup; corrected before final acceptance. Intermediate failures are diagnostic evidence, not passing gates. Startup crash log remains local under `reports/unity-native-palette-startup-crash.log`.
- Edited UTF-8/local links, UXML parse, palette variable references and new script/stylesheet metadata passed; 344 unique GUIDs. Font serialization-only trailing whitespace normalized back to baseline. Staged whitespace checked before commit.

Raw captures/logs/isolated profiles remain ignored under `reports/player-fixtures/native-settings-palette-layout-reviewed/` and `interface-comparison-palette-layout/`. Physics/economy matrices are retained historical Slice 2/3 baselines, not rerun for this presentation correction.

## Review gate

1. Launch the updated player, press F8 for New, open Settings → General and review Reset game's placement/style. Open and cancel its confirmation.
2. Review short red corners, matching header/content accents and scrollbar spacing while scrolling Difficulty/Driving, including at your current window size.
3. Confirm ordinary Apply/Cancel still behaves as before. Validate this correction before another interface is implemented.
