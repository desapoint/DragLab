# 4D.2 — Car details page and current-car selection (2026-10-10)

User approved Garage access direction and the immediate next control: clicking an owned card and Use this car. The user then specified a details page replacing Garage content, with Back, rather than a dialog. This checkpoint implements that interaction only; search/filtering, sorting, dealership, full inspection/dyno/drawer, refueling/service, Build/Tuning shortcuts and valuation remain future reviewed controls.

- Two rounded, red-corner cards per row remain. Click a card to replace the grid with that exact instance's details: model/identity, layered preview and common specs/condition. The main navigation stays visible. Shared brushed card faces use the clipped hover sweep and mouse-down shading; no focus highlight or Tab navigation.
- Opening/reading/Back/Escape are read-only. The filled-arrow Back button returns to the same grid scroll position. F8 compares the whole Garage page and retains the inspected instance when returning. The hidden practice shortcut cannot launch a race while native details are displayed.
- Use this car calls VehicleCollection.Select through existing atomic MechanicsTransactions/ProfileStore persistence. Success refreshes the actual current-car view/marker and returns to the grid. The already-current car has a disabled action and creates no redundant save.
- Displayed details refresh with definitive profile changes and capture the matching revision. The command still rejects stale revisions, missing instances, active-run restrictions and failed saves. Errors are inline; retry is available after a save failure. Failed/incomplete cars remain selectable for management under existing rules, without repairs/recreated components or relaxed race-entry requirements.
- Reuse shared palette, surfaces, icons, scrolling and action controls. Settings/dialog behavior remains unchanged. The new page is not the future full inspection drawer.

## Verification

- Unity 6000.6.4f1 Windows player build passed (`Invoke-Unity.ps1 -Action BuildPlayerOnly`). Runnable checkpoint: `Game/Builds/Windows/DragLab.exe` with its supporting files.
- Native player probe passed **283 checks / 0 runtime errors**. Evidence: `04-native-verification.txt`. Covers details/Back/Escape, scroll retention, Settings Cancel return, F8, hidden practice shortcut, exact-instance selection and persisted JSON, one revision per successful switch, current-car no-op, stale rejection, locked-save rollback/retry, and incomplete/failed cars without healing. Comparing the whole profile confirms selection changes leave other assets, condition, resources, settings and records intact.
- Standalone interface-comparison probe passed **48 checks / 0 runtime errors**. Evidence: `04-comparison-verification.txt`. Retains drafts, cart, held throttle and authoritative bench/race sessions through F8.
- Reviewed final details captures at 1280×720 and 1024×768: navigation, preview, facts, rounded red corners, Back and Use remain visible. Existing two-column grid checks also passed at 1920×1080 and offscreen 3840×2160. Physical 4K input testing and a separate 4K details capture were not performed.
- Two earlier probe failures were corrected before the final run: the read-only grid fixture used a dummy reservation without a real job, so it now creates a valid photo-session reservation; Escape could miss the page after UITK focus moved off the hidden card, so the visible Garage page now polls Escape in LateUpdate. Production reservation validation was preserved.
- Selection tests operate only in the isolated player fixture; fixture profile/backup bytes and original live profile are restored afterward. The fixture suffix `accepted` identifies the successful technical run, not user acceptance.
- UTF-8/whitespace checks passed; no asset GUID changes. Build-generated font trailing whitespace was normalized without substantive font edits. Broader mechanical/economy simulation gates retain their historical baselines and were not rerun for this interface checkpoint.

Technical checkpoint complete; human interface acceptance is pending.

## Review

Click a Garage card and check its exact identity, preview, right-side facts and condition. Back/Escape returns to the grid; F8 retains the details page. Use another car and verify CURRENT/top-bar fuel updates, then inspect it again: Use this car is disabled. Review hover/mouse-down, compact resize and scroll retention. This checkpoint does not complete 4D; stop for user validation.
