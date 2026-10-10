# Icons, secondary details and responsive layout (2026-10-09)

Documentation checkpoint following `5ee16f9`; no runtime interface implementation or phase completion.

Updated the [native UI contract](../../../docs/NATIVE_UI_DESIGN.md) with the user's requirements: recognizable icons wherever practical; small info buttons opening dialogs for secondary descriptions/specifications; HP, torque, weight and durability as primary part stats with actual effects; consistent shared scaling and responsive anchors across resolutions, aspect ratios and fullscreen/windowed modes. Navigation spans the top; Settings is right-anchored with a full-bar-height hit area and a proportionate icon.

Retain essential warnings, accessible icon names/labels, authoritative comparisons, explicit Settings Save, F8 and one-control review checkpoints. Resizing must retain drafts and interaction/simulation state. Exact reference sizes and scaling exceptions will be reviewed in native implementation. Durability and current condition remain distinct; unavailable/inapplicable stats must not be fabricated.

Reconciled objective, Slice 4 plan, status and repository instructions. Checks: UTF-8/garbled-text scan, local Markdown links, requirement coverage and Git whitespace/staged diff checks. No `Game/` changes and no Unity build/simulation/native resize rerun for this documentation-only request. Previous build and F8 checks remain historical evidence, not proof of the new layout requirements.

Next implementation target remains Settings, one user-directed control/minimal infrastructure checkpoint at a time. No new control started here.
