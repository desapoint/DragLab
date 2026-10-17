# Stored-tire selection — 2026-10-17

Implemented the requested tire-first replacement flow in Build → Swap stored tires. This supersedes the unfinished dropdown styling attempt; no popup polling or separate control theme remains.

- Select a grouped stored-tire entry, then use the same replacement cards as Purchase for Front left/right and Rear left/right. Back returns to the grouped list without discarding the draft; multiple groups can contribute to one swap.
- Entries group by exact definition, health, consumed life and failure/fault state. Equal displayed rounded percentages alone do not merge different wear. Quantity is the count of actual physical tires; identity, storage, distance and lifetime history remain individual. The tooltip retains storage/distance/identity details.
- All/Front/Rear presets choose distinct tires from the selected group and reset the draft to that selection. Insufficient group quantity disables the preset. Individual slot choices cannot duplicate a physical tire or exceed the four available corners. Click a selected slot again to release its tire; Clear empties the draft.
- Cards show the fitted tire/condition and planned replacement. Failed selections retain a warning. Shared surfaces, selected/pressed/hover states, fonts, modal scrollbar and no Tab navigation are reused.
- Browsing and Cancel do not save or mutate assets. Confirm uses the existing revision guard and atomic InventoryUnits.SwapTires transaction, retaining displaced tires and all incoming wear.

## Checks actually run

- Unity 6000.6.4f1 Windows development build: succeeded, packaged at `Game/Builds/Windows/DragLab.exe`.
- `tools/Test-NativeMenu.ps1 -TireSwapOnly -FixtureName tire-swap-20261017-b`: **57 checks, zero runtime errors**, real Windows mouse/keyboard, isolated fresh save. Includes starter purchase prerequisites, grouping four equivalent tires with differing retained distance histories, separate worn stock, 1024×720/1280×720 slot and scrollbar bounds, stock-limited presets, individual deselect/reuse, mixed-wear draft, Tab guard, Cancel byte invariance, confirmed four-ID transfer, displaced history retention, no purchase charge and SQLite reload.
- Captures reviewed: grouped stock, Front selection at compact/default sizes, mixed-wear draft. Raw captures remain local under `reports/player-fixtures/tire-swap-20261017-b` and the interface screenshot index. Short 720p dialogs scroll to the lower cards while the title/X and confirmation actions stay fixed.
- Earlier diagnostic runs failed: obsolete white-popup experiment and the initial new-dialog initialization order. Initial refresh now runs after dialog actions are constructed; the fresh packaged rerun above passes. Those attempts are not acceptance evidence.
- Targeted diff whitespace check passed. Unrelated existing files were preserved. Verified Unity-generated serialization noise was backed up locally and removed; authored rendering/font assets are unchanged.

## Review boundary

This is only the requested stored-tire control checkpoint. User visual acceptance is pending; no later interface or slice has advanced. Review the grouped quantities, Back/slot selection and All/Front/Rear behavior in the new build.
