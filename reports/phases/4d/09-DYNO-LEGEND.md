# 4D.5 — Dyno legend correction (2026-10-10)

- Replace ambiguous peak-only labels with HP and Torque entries including current peak figures and matching solid line samples.
- Give no-wear HP and torque their own labelled dashed samples and separate blue/mint curve colors. Keep current HP red and torque amber. Reference peak/RPM values remain available in tooltips.
- Use shared palette variables for text, samples and curves; preserve two-column legend layout within the existing right-side dyno. No new controls, simulation or asset/resource changes.

## Verification

- Windows development build passed (Unity 6000.6.4f1); local Windows player updated.
- Existing native player probe passed **345 checks / 0 runtime errors**. Dyno source/condition isolation, cached display, compact/1080p layout and existing interaction/payment/F8 guards remain passing. Evidence: `09-native-verification.txt`. No new implementation-mirroring tests were added for this style correction.
- Reviewed 1024×768, 1280×720 and 1920×1080 captures: explicit labels and matching solid/dashed samples fit in two columns; blue HP reference and mint torque reference match their plotted lines. Existing 4K settings/grid checks passed; no dedicated 4K legend capture or physical display review.
- UTF-8 and whitespace checks passed. Font build serialization noise normalized; raw captures/builds/logs remain ignored. Broader mechanics/economy gates and separate comparison probe retained, not rerun. Human visual acceptance pending.

## Review

Compare the four legend samples to curves on a worn car at compact/current/1080p sizes. A car without an output difference keeps only the two current entries. Stop for visual review before later controls; human acceptance pending.
