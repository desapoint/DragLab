# 4D.5 — Dyno inspection and details scrollbar (2026-10-10)

- Group the legend into two containers, Current and No wear, aligned with the actual plot edges. Neutral title/figure text shows peak HP and torque for each; colored samples preserve current red/amber and dashed reference blue/mint. Both groups are available for attached engines even when outputs match, superseding the earlier difference-only display.
- Hover inside the plot shows a visible light vertical cursor line, circles on each curve intersection, and a clamped readout with RPM/current/no-wear HP and torque in the selected units. Interpolate between existing inspection samples at the cursor RPM; never extrapolate output below the first sample. Leave/outside/resize clears hover. The popup stays beside the line rather than covering its intersections.
- Move scroll padding from the frame to the ScrollView so the existing slim Settings scrollbar has the same ten-point right inset. Keep content/scrollbar separation and rounded frame clipping.
- Preserve read-only cached authoritative inspection, missing-engine behavior, named instances, selection, maintenance, draft payments, no-Tab policy and F8. No simulation equations, pricing or player assets change; no later Garage control is included.

## Verification

- Final Windows development build passed (Unity 6000.6.4f1). Local Windows executable/supporting files updated.
- Native player probe passed **353 checks / 0 runtime errors**, including eight additional checks for grouped peak figures, the exact shared scrollbar inset, real pointer interpolation/current/reference values, readout bounds, no below-idle extrapolation, leave cleanup and compact/1080p hover layout. Existing cached inspection, read-only assets, payment rollback, maintenance and F8 remain passing. Evidence: `10-native-verification.txt`.
- Reviewed hover captures at 1024×768, 1280×720 and 1920×1080: neutral legend groups align with the plot; the visible vertical line and four colored dots agree with the interpolated values; popup stays beside the line; scrollbar now sits near the right frame edge. Maintenance continues to scroll below.
- Existing 4K settings/grid render-target gates passed; no dedicated 4K cursor inspection capture or physical display review was performed. The full historical mechanics/economy matrices and separate comparison probe were retained, not rerun.
- UTF-8 and whitespace checks passed. Build-only font serialization noise normalized; raw builds/logs/captures remain ignored. Technical completion does not imply human visual acceptance.

## Review

Check Current/No wear maxima, neutral text and graph alignment. Move the mouse across RPM, including below idle and both edges; compare dots/line and the value readout. Leave the plot and resize. Review the scrollbar's position and continued maintenance scrolling at compact/current/1080p sizes. Human validation pending; stop here before later controls.
