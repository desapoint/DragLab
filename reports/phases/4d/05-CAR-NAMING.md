# 4D.3 — Owned-car names (2026-10-10)

Requested checkpoint: name duplicate owned models/builds through a pencil next to the details heading, with input/confirmation in a dialog. Implements only that control; other Garage actions/browsers remain future reviewed checkpoints.

- Add an optional customName to each OwnedCar. Actual instance/model/engine/part IDs stay unchanged. Shared display-name fallback keeps older saves usable without inventing a name or migrating/healing assets. Shared ownership labels also show the name and short instance ID.
- Grid/details headings use the custom name. Keep two rounded red-corner cards per row and truthful applied stats/condition; long names truncate visually and remain available in card tooltips. Names render as plain text.
- Filled pencil opens the existing corner-highlight modal with a shared styled single-line input, red Save, grey Cancel and hover/pressed X. Blank restores the model name, as indicated beneath the input; trim surrounding whitespace and limit to 48 characters. No redundant title/subtitle content.
- Edits remain local drafts until Save. Cancel/X/Escape discard without saving or leaving details; Escape does not cascade to Back. Underlying page/navigation is disabled while modal; F8 is blocked by the existing modal/text guard. No focus highlight/Tab traversal is added; mouse focus and keyboard text editing remain available.
- Rename captures exact ID/revision and uses existing atomic MechanicsTransactions/ProfileStore persistence. Stale/missing targets, invalid names, active-run restrictions and save failure retain old state. Save errors remain inline with editable draft/retry. Unchanged names do not write another save. Names may repeat across different cars.
- Shared modal infrastructure now accepts custom content and retains its owning route instead of automatically closing outside Settings. Existing Settings draft/confirmation behavior is preserved; shared Escape handling is polled once to prevent dialog-dismissal also triggering details Back.

## Verification

- Unity 6000.6.4f1 Windows player build passed (`Invoke-Unity.ps1 -Action BuildPlayerOnly`); runnable `Game/Builds/Windows/DragLab.exe` updated with its supporting files. An initial probe compilation error was corrected by casting generated key codes to the existing ushort keyboard API before the successful build.
- Native player probe passed **305 checks / 0 runtime errors**, including 22 new naming checks. [Evidence](05-native-verification.txt). Real mouse/keyboard entry, explicit publication, durable JSON deserialization, old-data fallback, per-instance/duplicate independence, full-profile invariance except name/revision, no-op, invalid/stale/missing command rejection, cancellation/F8/Tab, locked-save rollback/retry, clearing and maximum-length/compact layout were exercised. The existing Settings/navigation/car-selection suite also passed.
- Standalone F8 probe passed **48 checks / 0 runtime errors**. [Evidence](05-comparison-verification.txt). Draft/cart/race/bench ownership and held throttle survive comparison; recording stays off.
- Reviewed 1280×720 rename dialog/grid captures and 1024×768 maximum-length details; pencil, input, X and confirmation remain accessible and use the shared theme. Existing grid offscreen 4K tests also passed. No physical 4K typing/rename test or separate 4K rename-dialog capture was performed.
- Tests use isolated player fixtures and restore original profile/backup bytes. No player assets were used for purchases or experimental renaming. Broader mechanics/economy gates retain their historical baselines and were not rerun for this cosmetic identity change.
- UTF-8 and whitespace checks passed; no new Unity assets/GUID changes. Unity-generated font trailing whitespace was normalized without substantive font edits.

Technical checkpoint complete; human interface acceptance is pending.

## Review

Open a car, click its pencil, type a name and Save. Check the details heading and grid name; a duplicate model should keep its own name. Cancel/X/Escape discard edits and keep details open. Clear the field and Save to restore the model name. Review pencil alignment, shared dialog/input styling and long-name layout. Stop here for user validation; 4D remains incomplete.
