# Shifter refinement — 2026-10-22

Status: technical checkpoint complete; user visual/phone acceptance pending.

- Remove up/down buttons entirely on desktop and mobile, including decorative sequential plus/minus signs. Keyboard controls and mobile flick gestures remain.
- Reduce the selector from 220 to 180 logical pixels and bring the desktop pedals 40 pixels closer. H-pattern horizontal gate travel is narrower; upper/lower row heights, boot pivot and authoritative shift timing are preserved.
- Shade manual knobs with a radial light from the upper left, keeping the current palette, red edge and responsive rendering.
- Put the mobile flick hint next to the gear number inside the readout; both follow the animated knob. Direction guidance follows the saved input preference.
- Update existing input/layout regressions to require absent buttons while preserving clutch gates, held throttle, pointer ownership and transmission-specific behavior.

Checks: HUD **270/0**, flick/keyboard **102/0**, pedals/mouse **80/0**, mobile **171/0** — **623 checks**, zero runtime errors. Windows and Android ARM64 IL2CPP builds pass; APK ZIP integrity, signature and 16 KB page alignment pass. [Verification](verification.txt), [build hashes](build-manifest.txt). This is the first requested control in docs/UX_REFINEMENT_QUEUE.md. Quick false-start leave, car listening and warning repair links remain individually gated next controls.

The first combined HUD run passed 206 assertions, then its 207th assertion failed because the earlier pause fixture had intentionally saved engine volume at zero and never restored it. The production setting correctly muted the engine. The pause fixture now restores its captured original gains through the existing preference transaction before later audible-output assertions. The failure is retained locally; no production audio code or sound tuning changed. The complete corrected HUD rerun passes 270 checks with zero runtime errors, including audible engine DSP after the finish and durable race/result actions.

Flick/held-input regression passes 102/0. The first updated pedal run passed its touch/shift assertions but clicked stale world bounds immediately after the fixture switched back from mobile to desktop layout. Yield one layout frame before its later real mouse click. This is fixture sequencing; no input-runtime workaround was added. The corrected pedal run passes 80/0 and mobile safe-area/layout run passes 171/0; failed diagnostic output is retained locally.

## Interface review

- Desktop: check the narrower manual/automatic selector, absence of plus/minus buttons, sunlight shading and keyboard shifts.
- Mobile: check the hint beside the gear number, real shift animation, both configured flick directions and gas held through shifting.
- Review compact, default and 4K captures through reports/interface-screenshots. Alternate resolutions remain under resolution-tests. The inspected mobile example is reports/player-fixtures/shifter-flick-20261022-b/mobile-flick-shifter.png.

Stop at this requested control for user validation under AGENTS.md. Quick false-start leave, owned/dealership engine audition and warning repair links remain recorded in docs/UX_REFINEMENT_QUEUE.md. They are not implemented in this checkpoint.

## Preserved pre-existing local changes

These unrelated files remain untouched and uncommitted: reports/new-parts-supply/verification.txt, reports/parts-market/verification.txt, reports/race-audio-audition/coverage-matrix.csv, reports/race-audio-audition/verification.txt, reports/sqlite-state/verification.txt, Tests.ps1, .codex-remote-attachments/, reports/race-exit-regression/ and reports/shop-responsive-20261019/.
