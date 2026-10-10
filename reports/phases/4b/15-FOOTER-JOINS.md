# Continuous footer joins (2026-10-10)

Requested follow-up to [footer diagonals](14-FOOTER-GRID-DIRECTION.md). Human visual acceptance remains pending; no later interface is started.

- The innermost diagonal at each footer end now joins the horizontal red strip using shared miter vertices in one continuous rail mesh.
- Match line thickness/color at the join and carry the antialias fringe continuously around it. No gap, blunt overlapping strokes or brightness seam; horizontal trim still fades toward the neutral center.
- Keep the two smaller parallel slashes and mirror the rail at the opposite corner. Decorative geometry still ignores picking; header, Settings controls and behavior retain their existing implementation.

## Verification

- Unity 6000.6.4f1 import/compile and Windows development build passed; updated `Game/Builds/Windows/DragLab.exe` and its supporting folder.
- [Native UI verification](15-native-verification.txt): **228 checks, zero runtime errors**. Settings, dialogs, draft Apply/Cancel, F8 guards, pointer states and responsive checks pass.
- [Rendered connection checks](15-join-render-checks.txt): all four joints (left/right at 1280×720 and 3840×2160) form continuous red pixel components. Inspected both captures for thickness/color and antialias continuity. The render check caught mirrored triangle winding, corrected before the final build. True 4K render-target review is not physical 4K input/device acceptance.
- One final-build probe stopped at an existing pointer-hover assertion; the subsequent complete rerun passed. No unrelated input/widget behavior was changed.
- Standalone race/bench F8 regression (48 checks), text calibration and physics/economy audits remain historical baselines; not rerun for this decorative correction.
- No new assets/dependencies or GUID changes. Generated font whitespace normalized to baseline. Edited UTF-8, local links and staged whitespace checks passed.

Raw capture/log/isolated profiles remain ignored under `reports/player-fixtures/native-footer-joined-rails*`.

## Review

Check both diagonal-to-horizontal joints at default window size and after resizing. The main diagonal should meet the red line cleanly, with matching thickness and smooth edges.
