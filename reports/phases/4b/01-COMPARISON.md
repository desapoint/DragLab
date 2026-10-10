# 4B.1 — Whole-interface F8 comparison (2026-10-09)

**Requested shortcut implemented; ready for native user review. The rest of 4B is not implemented.** Baseline `b50d275`. The user declined the HTML presentation and directs future native controls one button or slider per committed checkpoint. Web layouts/source hashes remain archived baseline evidence, not an approved design or current runtime inventory.

## Behavior

- Plain **F8** switches the current main interface Old ↔ New/empty. A status badge names the current route and mode; the empty view explains how to return. All its subpanels disappear together. Existing car/world rendering and audio continue. Each main route remembers its selection for the session; relaunch defaults to Old. No preference is written to player saves.
- Game menus, collection, configurations, assemblies, valuation/recovery, race, pause/results/race settings and both benches use the same comparison helper. Panel/category selections, scroll positions, pending draft/cart objects and current simulation stay in place; the toggle does not route, reset, apply, pause, heal or publish resources.
- Existing Update/simulation/audio loops remain active. Held keyboard gas and shifts continue. Hidden race mouse/touch hitboxes cannot receive commands; returning to Old requires a fresh pointer press instead of inheriting a click made on the empty view. Hidden bench RepeatButton pedals cannot remain latched.
- Active text focus, pointer gestures and visible reset/sale confirmation block switching. F8 repeat cannot toggle continuously. The helper exits the current IMGUI event after a presentation change so its next Layout event rebuilds the proper control tree.
- Sound-bench scenario B moves from F8 to **Shift+F8** and retains its button. Plain F8 cannot reset the bench or start a scenario. The existing gas event bridge is shared between visible/hidden presentation; control ownership is unchanged.
- No new UI framework/package/scene assets, gameplay changes, profile schema changes or replacement controls. The empty view and comparison badge use the existing development UI only. Future native controls require the user's next instruction.

Ordinary race journals, definitive saves, running scenarios, fuel use and wear still happen at their existing boundaries while the UI is hidden. Presentation comparison itself does not save or freeze the simulation. To navigate an unimplemented replacement screen, press F8 to return to Old.

## Checks actually run

- Unity 6000.6.4f1 import/compile and `DragLab.Editor.PlayerBuild.BuildPlayerOnly` passed. Updated runnable Windows player: `Game/Builds/Windows/DragLab.exe` (run with its complete build folder).
- [Final native report](01-verification.txt): **48 checks, zero runtime/GUI errors**, real Windows keys and mouse input in the development player, isolated saves under `reports/player-fixtures/interface-comparison-native-4`. Thirteen menu routes plus valuation, race, sound and physical benches; per-route session memory; unchanged profile/disk save; exact draft/cart objects; reset/text-focus guards; held F8/W; hidden race shift hitbox; Shift+F8 scenario B; continued physical ticks/keyboard ownership and recording remaining OFF.
- Six native screenshots retained locally. Garage New/empty and restored physical Old captures inspected. Test launcher: `tools/Test-InterfaceComparison.ps1 -FixtureName <new-name> -ShowPlayer`. It checks its player window before injecting input; use a visible interactive test player for real-key validation.
- UTF-8, local documentation links, new script metadata/GUIDs, PowerShell parsing and staged Git whitespace checks. Domain, scenes, packages and project settings are unchanged.

The initial restricted Unity batch build stalled on local licensing. Its process was identified by the exact batch command/project/log before stopping only that process and retrying successfully. Initial native attempts exposed fixture issues: a hidden window cannot receive the probe's real OS keys; programmatic focus of a disabled/clipped field is not an actual editing test; scenario shortcuts correctly remain suppressed while text editing owns input. The final run uses a visible player, an actual numeric-field mouse click and explicit W takeover before scenario B. Earlier incomplete attempts are not passing gates.

The full historical Slice 2/3 physics/economy matrix, controller and Android touch gates were not rerun for this presentation-only switch. Keyboard-held gas is verified; simultaneous real-device touches are a later gate. F8 is fixed for this development checkpoint; configurable bindings/framework migration remain future requested work.

## Native review checklist

1. Launch the updated player or Play `FirstSlice` in Unity. Press F8 in Garage: only world/car, status badge and empty-view message remain. Press F8 again to restore the full current interface.
2. Try Settings, Parts and Tuning. Compare a draft or cart, then return; nothing should apply or disappear. Clear text focus/finish a pointer gesture before toggling.
3. In a practice race or either bench, hold W while toggling. Revs/control ownership and the ongoing session continue. Use F8 to recover all mouse controls. In the sound bench, scenario B is Shift+F8.
4. Tell us the next specific native control and its placement/behavior. Do not advance the rest of 4B automatically.

**Human validation is pending.** This is the requested comparison checkpoint only. Existing phase archives remain untouched and history stays additive.
