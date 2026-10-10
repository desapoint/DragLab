# Settings hierarchy, alignment and dialog corners (2026-10-10)

Requested correction in the current native Settings checkpoint. No later screen is started. Human visual acceptance remains pending.

## Delivered

- Driving and Simulation presets appear in a separate compact charcoal group labelled Presets (36-point preset rows; ordinary rows remain 44 points), above Individual settings. Existing scopes are retained: presets update multiple related draft values; they do not change every preference or publish without Apply. Information controls explain each scope.
- Settings keeps its full-bar-height rectangular hit area and slanted face, with a 12-logical-point right inset. The nav remains full-width and resource spacing remains responsive.
- Shared button labels fill their content height and use middle alignment with a one-point optical font correction; modal headings/status follow that correction. Header/resource/footer text uses a two-point adjustment. Captions and numeric inputs retain native centering after removing asymmetric padding; info/X controls balance their padding. Offsets were calibrated against rendered glyph bounds instead of shifting every text role by the same amount. Multiline descriptions retain reading flow. No font assets or UI scale defaults change.
- Dialogs reuse RacingSurface's brushed panel, rounded outline and local top-left/bottom-right red corner fade. Removed the former full red left border. X/Cancel, primary confirmation, dimming, scrolling and input isolation remain.
- Shared design decisions record these refinements. Retain the [no-focus/no-Tab control standard](11-CONTROL-STANDARD.md), mouse-down styling, uninterrupted hover, selected states, explicit Apply/Cancel, reset and F8.

## Verification

- Unity 6000.6.4f1 import/compile and final Windows development build passed. Updated `Game/Builds/Windows/DragLab.exe` and supporting folder; current resizable window defaults are retained.
- [Native UI verification](12-native-verification.txt): **225 checks, zero runtime errors**. Separate preset hierarchy/labels; 12-point Settings inset at tested sizes; rendered dialog corner accents/neutral middle edge and no old left border. All retained draft, Apply/Cancel, reset/recovery, hover/mouse-down, no-focus/no-Tab, thermal/audio/unit and F8 guards pass.
- [F8/input regression](12-comparison-verification.txt): **48 checks, zero runtime errors**. Shared live race/bench state and held gas remain intact; historical empty-replacement assertion names are retained.
- [Rendered glyph sampling](12-text-alignment.txt): five representative caption/value/unit/tab/action roles are within one physical pixel of their default-window control center. Inspected final Difficulty/Driving, Sound and confirmation captures. Other text retains visual-review acceptance; this is not an exhaustive glyph/device claim.
- Preset grouping initially reduced the compact page to eight visible driving rows, failing the retained density gate. Tightened its headings, spacing and preset rows; final default-window capture and checks retain at least nine full driving rows. Ordinary rows stay 44 points and preset buttons keep their existing 32-point height. A blanket two-point text offset moved captions/numbers too high; rendered measurements guided role-specific correction before final verification.
- Final probe retains compact/1080p/true 3840×2160 target coverage and shared scaling. A 4K render target is not physical 4K input acceptance; desktop window remains monitor-limited. Existing unrelated compiler warnings and the first 4K D3D12 upload sizing message remain, with zero runtime errors in final probes.
- Edited UTF-8/local links, UXML parse, 344 unique asset GUIDs and staged whitespace passed. Unity font serialization-only whitespace normalized to baseline; no font metadata/dependency changes.

Raw captures, logs and isolated profiles remain ignored under `reports/player-fixtures/native-settings-hierarchy-final/` and `interface-comparison-hierarchy-final/`; intermediate attempts remain local. Historical physics/economy matrices are retained rather than rerun for this presentation correction.

## Review gate

1. Open Difficulty/Driving and compare the two preset rows with Individual settings; try a preset, inspect its changes, then Cancel or Revert.
2. Review the Settings inset and text centering in nav, choices, sliders/numbers and actions at your window size.
3. Open help, Apply or reset confirmation and review both corner accents; confirm X/Cancel and ordinary Apply work. Validate this correction before another screen/control is implemented.
