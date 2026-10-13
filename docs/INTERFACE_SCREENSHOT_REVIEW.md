# Interface screenshot review

Open `reports/interface-screenshots` in Explorer. Normal initial-resolution PNGs are directly at this folder root. `resolution-tests` contains compact/1080p/4K and other alternate-resolution captures. Routing reads actual PNG dimensions: 1280×720 is the normal initial window; filename hints are only a fallback for an incomplete image. Names identify the page, dialog and test state. `run-info` contains each suite's PASS, RUNNING or FAILED / INCOMPLETE result and a shared image source/date manifest. A failed run publishes only the images captured during that attempt.

Each run of `tools/Test-NativeMenu.ps1`, `tools/Test-WindowsSlice.ps1` or `tools/Test-InterfaceComparison.ps1` clears the previous immediate fixture PNGs and that suite's owned review images, including alternate-resolution files, before launching. It publishes fresh captures on exit. A different FixtureName still replaces the same suite's images. Other suites, logs, saves and historical fixtures remain intact. Where suites share an image name, its most recent publisher owns it; an older suite cannot clear the newer capture. Paths must stay in their project report containers; directory links and suite path escapes are rejected. Raw gallery contents remain ignored by Git.

`tools/Collect-InterfaceScreenshots.ps1` fills gaps from the newest recorded successful capture for each filename across existing fixtures. These are labelled recorded in the source manifest, not a fresh whole-interface test; current-run images are not downgraded to recorded copies. Initial collection supplied 152 normal-resolution and 52 alternate-resolution captures. The previous grouped gallery is preserved under `run-info/previous-layout`, outside the active review view.

Examples:

```powershell
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-tuning -TuningOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-build -PartsOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-workshop -WorkshopOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-menus
./tools/Collect-InterfaceScreenshots.ps1
```

Mouse tests require an available interactive Windows session. Run them sequentially. Gallery export does not change player assets or test simulation behavior. `tools/Test-InterfaceScreenshotExport.ps1` checks replacement, failure labels, preservation of logs/other suites and path guards without running Unity.
