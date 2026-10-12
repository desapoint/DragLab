# Shared angled hover shine (2026-10-12)

The user requested an angled shine on every button that already has one, with the same visible angle on slanted buttons.

## Delivered

- One shared sweep at **25° from vertical** for navigation, ordinary/primary buttons, both tab shapes, primary/secondary/destructive actions, joined-choice ends/middles and clickable car cards.
- Slope derives from UI-space height, without incorporating contour slant or width-normalized skew. Uniform responsive scaling preserves its visible angle on short, tall, wide and narrow surfaces.
- The sweep clips to each existing rounded/slanted contour; navigation uses its rectangular bounds. Expanded traversal margins let the band enter and leave completely.
- Existing hover-only .65-second timing, full animation after clicks/selected-state changes/pointer leave, pressed feedback, no focus/Tab shine, inactive-surface caching and non-picking decoration remain intact. Button shapes/layout, palette, settings, progression and simulation workflows are unchanged.

## Validation

- Unity 6000.6.4f1 Windows development build passed; final log has no compiler or USS errors. Executable/supporting files updated.
- Focused rendered-shine probe: **87 checks / 0 runtime errors**, PASS. Real mouse hover at 1280×720 tests all twelve shine surface families plus a wide 32-point-high action. A second Settings sample tests 1080p. True 3840×2160 render-target samples test Settings and a short action at scale 2; these use pooled pointer-enter events because the physical window is not a 4K monitor. [Focused evidence](17-shine-verification.txt).
- Pixel sampling subtracts the settled hovered face from the animated frame, then measures the highlight ridge at two heights. Measured angles span **24.97°–26.18°**, within pixel-sampling tolerance of the shared 25° geometry. Slanted Settings' clear corner remains untouched. Sweeps finish, profiles/save bytes remain unchanged, and representative captures were visually inspected.
- Full native regression rerun: **1,133 checks / 0 runtime errors**, PASS. Includes hover shine progress/completion, click/selected-state/leave continuity, focus/Tab rules, untouched clear corners, shared controls and all current Tuning/Settings/Garage/dealership interactions. [Full evidence](17-full-native-verification.txt). Prior checkpoint evidence is retained unchanged.
- UTF-8, local-link and staged whitespace checks pass before commit. Unity font serialization is restored only after proving whitespace-only equivalence. Raw logs, screenshots and fixture/build folders remain local/ignored; new verification script metadata is included.
- Physical 4K monitor and subjective shine appearance remain user review. This is a requested shared-style correction; later 4E controls/configuration-library work remains gated.

## Review

Hover Garage/Build, Settings, tabs, joined settings choices and Apply/Cancel. The band should lean at the same angle despite the button shapes, stay inside their outlines and finish once without restarting on click.

Technical completion is separate from visual acceptance. Stop for review after this correction.
