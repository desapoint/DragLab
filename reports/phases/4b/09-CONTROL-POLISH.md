# Settings control refinement (2026-10-10)

Requested visual correction within the current native Settings checkpoint. **Technically ready for review; human visual acceptance pending.** No later interface or Slice 4 phase is started. The [full Settings behavior checkpoint](08-SETTINGS.md) remains the baseline.

## Changes

- Shared convex contours now clip the face, brushed texture, hover fill and shine. Rounded/slanted outlines have a one-physical-pixel alpha fringe using actual panel scale, including the true 4K target. Removed rectangular CSS state fills and the old point-clamping renderer that created tab fragments and square Settings hover. Shine still completes once across clicks/state changes.
- Content uses restrained static diagonal metal brushing and a full rounded bevel. Upper-left/lower-right red highlights now follow the curved border and fade into grey. Inspection caught reversed winding that culled the original bevel mesh; corrected and covered by rendered-pixel checks. Header layout/navigation are preserved; the Settings plate uses the corrected outline.
- Rows are 44 logical points, choices 32, tabs 36 and page/dialog actions 34. Ten complete driving rows fit in the default 1280×720 window. Revert/Cancel now share Apply's restrained six-point slant. Joined choices have flat internal seams and rounded outer first/last ends only.
- Numeric inputs use the same condensed font and compact inset styling. Slider thumbs share the track's vertical center. Native toggles retain focus/input semantics with a decorative glossy capsule; thumb position and track color ease over 180 ms, reverse from the current position and stop repainting after settling.
- Eight-point themed scrollbar/track/thumb; native arrows hidden in Settings and dialog details. The final correction constrains the slider/drag container as well as its parent: an eight-point scroller alone still allowed the default wide thumb to overflow.
- All actual settings, thermal dependencies, 25% reductions, explicit Apply/confirmation, Cancel return, draft isolation, reset behavior and F8 remain unchanged. No additional UI titles/subtitles/descriptions or simulation/economy features.

## Checks

- Unity 6000.6.4f1 import/compile and Windows development build passed. Updated `Game/Builds/Windows/DragLab.exe` with supporting folder; 1280×720 resizable windowed defaults retained.
- [Native/UI verification](09-native-verification.txt): **200 checks, zero runtime errors**. Real mouse/keyboard controls; compact density, joined corners/seams, rendered red accents and unchanged clear corners under tab/gear hover; one-pixel edges at native/4K scales, centered slider, consistent numeric font, slim scrollbar/hidden arrows, toggle easing/reversal/settled cache and dialog styling. Existing draft/Apply/Cancel/persistence/reset/dependency checks also pass.
- [F8/input regression](09-comparison-verification.txt): **48 checks, zero runtime errors** on the final build. Preserves whole-interface route ownership, tune/cart drafts, text/gesture/dialog guards, real held race/bench throttle and shared physical simulation. Historical assertion names that say empty replacement remain unchanged.
- Render review includes populated categories, lower driving options, shape-matched tab/Settings hover, switch transitions, confirmation, 1024×768 compact layout and true 3840×2160 target at shared 2× scale. Desktop 4K window request remains monitor-limited to 3360×2160; target rendering does not claim physical 4K input acceptance.
- Edited UTF-8/local links, UXML parse, new script metadata and 342 unique asset GUIDs passed. Font assets' Unity-generated trailing-space-only changes normalized back to the tracked baseline. Staged whitespace checked before commit.

Raw screenshots, logs and isolated profiles remain ignored under `reports/player-fixtures/native-settings-polish-reviewed/` and `interface-comparison-control-polish-reviewed/`. Earlier intermediate runs are diagnostic evidence only. Physics/economy matrices remain historical Slice 2/3 baselines; this presentation correction does not rerun them or claim player visual acceptance.

## User review gate

1. Launch the updated player, use F8 for New and open Settings. Review smaller shaded tabs/buttons, rounded joined choices, red panel corners, texture and narrow scrollbar at your window size.
2. Hover and click tabs/Settings: the shine should follow the slant, complete once and leave clear corners unchanged. Toggle switches repeatedly; movement should ease and reverse smoothly. Check centered slider knobs and consistent numeric text.
3. Change several tabs' options, Cancel and reopen; applied values must remain unchanged. Then Apply, review/cancel its dialog and confirm once. Validate this correction before another interface is implemented.
