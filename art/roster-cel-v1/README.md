# Cel-shaded roster sources

25 fictional model bodies, covering 52 authored configurations. The approved Juniper establishes the strong-outline, flat 2D style. Raw generated masters and exact prompts are retained in `sources.json` and `generation-plan.json`.

`tools/prepare_roster_sprites.py --measure` records the native transparent wheel-opening measurements. Running the tool prepares neutral paint foundations, protected fixtures, wheel-well backs and separate mechanical layers using `preparation-plan.json`. It reads authored wheelbases without changing vehicle physics. Existing wheel/rotor/caliper artwork is retained; unique shop-wheel artwork is still a separate task.

Only painted panels are tinted. Fixtures render above the neutral foundation. Five contrasting repaints preserve opaque fixture RGB exactly. Source alpha noise is normalized only in derived outputs; original master bytes remain intact. Mechanical layers have independent front/rear anchors, rotating wheels/rotors and fixed calipers.

The verification, replay, standard QA, visual review and package manifests record technical checks, not user acceptance of every generated model. Raw contact sheets, rotation previews and packaged ZIPs remain local ignored artifacts. Generate the offline review page with `tools/build_roster_review.py`.
