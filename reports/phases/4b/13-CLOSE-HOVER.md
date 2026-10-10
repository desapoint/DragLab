# Dialog close hover (2026-10-10)

Requested X-button correction within the current native Settings checkpoint. No later screen is started; human visual acceptance remains pending.

- Shared `.close-button` gains a brighter grey face and palette-accent border on mouse hover, with a 120 ms color transition.
- Its darker mouse-down face remains. Pointer leave returns to the normal face/border even while the dialog keeps focus. No focus highlight or Tab traversal is introduced.
- X still dismisses only the dialog and preserves the Settings draft. Layout, text alignment, panel corners, Apply/Cancel and actual progression/resources are unchanged.

## Verification

- Unity 6000.6.4f1 import/compile and Windows development build passed; updated `Game/Builds/Windows/DragLab.exe` with supporting folder.
- [Native UI verification](13-native-verification.txt): **228 checks, zero runtime errors**. Actual pointer/rendered checks confirm a brighter X while focused, a distinct darker mouse-down face, dismissal retaining the draft and hover clearing on pointer leave despite retained focus. Existing Settings, recovery/reset, modal/F8 guards, no-focus/no-Tab, animation and responsive checks pass.
- Inspected `dialog-close-hover.png`. Compact/1080p/4K-target checks are retained in the rerun; target rendering is not physical 4K input/device acceptance.
- Standalone 48-check race/bench F8 regression and glyph calibration remain the [12 checkpoint](12-HIERARCHY-ALIGNMENT.md) baseline, not rerun for this USS-only production change. Physics/economy matrices are also retained historical baselines.
- Edited UTF-8/local links and staged whitespace passed. No new assets/dependencies or GUID changes; generated font whitespace was normalized to baseline. Existing compiler warnings/first-4K upload sizing message remain, with no runtime errors reported.

Raw capture/log/isolated profile remain ignored under `reports/player-fixtures/native-dialog-close-hover/`.

## Review

Open a help, Apply or reset dialog. Hover X, press/release it and reopen/move away: review the brighter hover, darker pressed face and return to normal without a persistent focus highlight. Validate this correction before another control.
