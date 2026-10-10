# Icon and label alignment (2026-10-10)

Requested visual correction within the current native shell. No later interface is started; human visual acceptance remains pending.

- Resource rows move up one logical point as a group. Their labels retain a one-point font correction, matching button captions; the combined label position remains unchanged from the previous two-point text-only offset. Icons align with visible glyphs at native and scaled resolutions.
- Shared icon elements explicitly center themselves in their row. Flag and fuel-pump silhouettes are shifted half a viewbox unit up to center their drawn bounds, retaining sizes and horizontal spacing.
- Keep centered text, Settings icon-only button, navigation layout, filled-icon style, picking behavior, hover/pressed/selected states, no-focus/no-Tab and current footer joins.

## Verification

- Unity 6000.6.4f1 import/compile and Windows development build passed; updated `Game/Builds/Windows/DragLab.exe` and its supporting folder.
- [Native UI verification](16-native-verification.txt): **228 checks, zero runtime errors**. Settings/drafts, Apply/Cancel, dialogs, pointer states, F8 guards and responsive checks pass.
- [Rendered measurements](16-icon-alignment.txt): **27 icon/label pairs** at 1280×720, 1920×1080 and the true 3840×2160 target are within one logical point. Navigation pairs are within 0.25 points; resource pairs within one point. Inspected their header captures plus the 1024×768 compact header; Settings gear retains its centered placement. True 4K target review is not physical 4K input/device acceptance.
- Existing text centering is retained. Standalone race/bench F8 (48 checks), detailed text calibration, footer connectivity and physics/economy matrices remain the previous checkpoints' baselines; not rerun for this alignment-only change.
- Edited UTF-8, local links and staged whitespace passed. No new assets/dependencies or GUID changes; generated font whitespace normalized to baseline. Existing compiler/initial 4K upload-sizing warnings remain with zero runtime errors in the final probe.

Raw captures/log/isolated profile remain ignored under `reports/player-fixtures/native-icon-label-alignment-final/`.

## Review

Compare icons beside Garage/Build/Tuning/Race/Business/Data and cash/fuel/reputation labels at the default window and after resizing. Their visible centers should align, with the Settings gear still centered in its button. Review before another control.
