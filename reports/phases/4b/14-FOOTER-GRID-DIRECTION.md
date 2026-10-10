# Footer diagonals and browser direction (2026-10-10)

Requested decoration correction plus planning. Cars/parts grid, search/filtering and sorting requirements are recorded; 4D Garage is recommended next but not started. Exact subpage grouping remains a proposal. Human visual acceptance is separate from technical checks.

- Footer corners now use three separate straight diagonal lines mirrored left/right, with antialiased coverage. Remove the bent lead-in; retain the fading red strip and metal center break.
- Shared header and Settings controls retain their existing styling and behavior. Decorative geometry ignores picking.
- Document responsive grids with search/filtering and sorting for cars and parts. Recommend Garage My cars/Dealership and later Build Installed/Inventory/Shop/Cart as reviewable groupings, preserving exact instances, condition, compatible fitment, units and transaction rules.
- Proposed next checkpoint is the owned-car grid only. Search/filter, sorting, selection/details and dealership follow separate user instructions; the inspection drawer remains separately gated.

## Verification

- Unity 6000.6.4f1 import/compile and Windows development build passed; updated `Game/Builds/Windows/DragLab.exe` with its supporting folder.
- [Native UI verification](14-native-verification.txt): **228 checks, zero runtime errors**. Settings/draft/Apply/Cancel, dialogs, pointer states, F8 guards, no-focus/no-Tab and responsive checks pass.
- Inspected both footer ends in the 1280×720 capture and true 3840×2160 render target: separate straight diagonals, mirrored ends and smooth edges. Render-target validation is not physical 4K input/device acceptance.
- Standalone race/bench F8 regression (48 checks) and text calibration remain the [12 checkpoint](12-HIERARCHY-ALIGNMENT.md) baseline; physics/economy gates were not rerun for this decorative change.
- Edited UTF-8/local links and whitespace checked. No new Unity assets/dependencies or GUID changes; generated font whitespace normalized to baseline. Existing compiler warnings/first-4K upload sizing message remain, with zero runtime errors in the probe.

Raw captures/log/isolated profile remain ignored under `reports/player-fixtures/native-footer-diagonals/`.

## Review

Check both bottom corners at default window size and after resizing: separate straight slashes, mirrored ends, no bent terminal. Review the proposed Garage-first sequence before starting any Garage control.
