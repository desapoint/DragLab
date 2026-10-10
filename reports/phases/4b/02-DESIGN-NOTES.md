# Native UI requirements note (2026-10-09)

Documentation checkpoint after `67e09df`; **no runtime interface work or phase completion**.

Recorded the user's [native grey/red design contract](../../../docs/NATIVE_UI_DESIGN.md): red primary/confirm actions and grey secondaries; bordered small rounded buttons, shading and hover/focus effects; consistent persistent top navigation with cash/fuel/progression and far-top-right Settings; full-screen menu backgrounds hiding the strip; contextual Back/pause exceptions; modal dialogs with X/cancel and editable content.

Settings is the first replacement page and combines Sound, Difficulty/driving and the other preference categories, removing separate Driving navigation in the new presentation. All new Settings edits remain draft until explicit Save/confirmation. This explicitly supersedes the previous live/gesture-end-save proposal without changing legacy behavior now. Dialog cancellation retains drafts, dirty exit offers Save/Discard/Stay, failed Save retains applied values, and reset remains a separate command preserving applied preferences.

Reconciled the objective, Slice 4 plan, implementation plan, game plan, status and repository instructions. Minimum common style/navigation/widgets/draft/dialog infrastructure may accompany each requested Settings control; completing all of 4B is not a prerequisite or an authorization. F8, shared simulation, actual ownership, transaction guards and one-control review checkpoints remain.

Checks: changed documentation read as explicit UTF-8; local links resolved; all requested requirement categories and updated Save semantics checked; Git whitespace/staged diff inspected. No `Game/` files changed. No Unity build/native test rerun for this documentation-only change; the previous Windows build and 48 native F8 checks remain historical runtime evidence.

Next target recorded: Settings entry/minimal screen foundation and the first user-specified control. **Implementation has not started.** Stop after this locally committed requirements note.
