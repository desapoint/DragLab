# Native input policy and shared control standard (2026-10-10)

Requested correction within the current Settings checkpoint. No subsequent interface or phase is started. Technical verification is recorded below; human visual acceptance remains pending.

## Delivered

- Removed focus-triggered shine, focus border/glow and switch focus underline. Selected destinations, category tabs, choices and switch values keep their existing state styling.
- Disabled Tab and Shift+Tab traversal in the native shell, numeric fields and generated dialogs. Mouse-focused numeric fields retain typing, selection/caret and Enter commit. The shell excludes descendants from sequential focus and blocks next/previous navigation events as well as Tab key events, without blocking mouse focus or driving shortcuts.
- **Mouse-down styling is retained**, following the user's clarification. Pointer down/up decoration runs in the trickle-down phase before Unity Button's click handler consumes the event. Pressed shading remains clipped to the real control outline. Mouse-up restores the normal face; clicking never restarts or interrupts the single hover sweep. Info/Close actions retain a darker mouse-down face without an added focus outline.
- Consolidated current palette, fonts/icons, surfaces/hover, rows, joined choices, sliders/numbers, switches, actions, panels/scrolling, modals, scaling and draft rules into the [shared native control standard](../../../docs/NATIVE_UI_DESIGN.md#shared-native-control-standard-2026-10-10). Updated project instructions and Slice 4's earlier focus/Tab expectations to match. Subsequent approved screens must reuse these implementations and theme instead of inventing separate controls.
- Existing explicit Apply/confirmation, Cancel return, reset behavior, F8 and simulation/resource/input ownership are preserved. Legacy UI/bench presentation remains its existing implementation until separately approved for migration.

## Verification

- Unity 6000.6.4f1 import/compile and final Windows development build passed. Updated `Game/Builds/Windows/DragLab.exe` and supporting folder; launch defaults remain unchanged.
- [Native UI checks](11-native-verification.txt): **221 checks, zero runtime errors**. Actual Tab/Shift+Tab input leaves the destination, mouse-focused numeric editing and generated dialog focus intact. Focusing a button starts no shine/repaint; rendered mouse-down darkening and mouse-up restoration pass. Retained responsive/4K-target, hover/click animation, Apply/Cancel, reset, recovery, unit/audio and thermal dependency checks pass.
- [F8/input regression](11-comparison-verification.txt): **48 checks, zero runtime errors**. Held race/bench throttle, ownership, text/gesture/dialog protection and isolated state remain intact. Historical empty-replacement assertion names are retained.
- Viewed the populated Sound capture for consistent sliders/numbers, selected-tab theme and panel/nav layout. The probe retains compact/1080p/true 3840×2160 target coverage. True 4K rendering is not physical 4K input acceptance; the desktop window remains monitor-limited.
- Intermediate probes failed the new pressed-feedback and numeric Tab checks. Corrected pointer propagation and blocked next/previous navigation events; final evidence above supersedes those attempts. Build uses current `FocusController.IgnoreEvent`, avoiding the obsolete `PreventDefault` API. Existing unrelated compiler warnings and initial 4K D3D12 upload-buffer sizing message remain; final probes report no runtime errors.
- Edited UTF-8/link checks, asset metadata uniqueness and staged whitespace checks passed. Unity font serialization-only trailing whitespace was normalized back to baseline. No asset GUID changes or new dependencies.

Raw captures/logs/isolated profiles remain ignored under `reports/player-fixtures/native-settings-control-standard-reviewed/` and `interface-comparison-control-standard/`; intermediate attempts remain local. Physics/economy matrices are retained historical Slice 2/3 baselines, not rerun for this UI change.

## Review gate

1. Open native Settings and click/drag choices, switches, sliders and number fields. Review mouse-down feedback and release; no extra focus ring or focus shine should remain.
2. Press Tab and Shift+Tab in the shell, an edited number and a dialog: focus/value/page must not move. Type a numeric value with the mouse-selected field; review ordinary Apply/Cancel and Escape/X dismissal.
3. Validate this correction before another screen/control is implemented. The retained UI and shared palette are the reuse baseline.
