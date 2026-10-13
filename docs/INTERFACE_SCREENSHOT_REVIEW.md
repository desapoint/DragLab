# Interface screenshot review

Open `reports/interface-screenshots` in Explorer. `native-*`, `legacy-*` and `interface-comparison` folders contain the most recent run of that script/suite. PNG names identify the page, dialog, state or resolution. `RUN.txt` identifies PASS, RUNNING or FAILED / INCOMPLETE; `images.csv` records each source and capture date. A failed run exports only images captured during that attempt.

Each run of `tools/Test-NativeMenu.ps1`, `tools/Test-WindowsSlice.ps1` or `tools/Test-InterfaceComparison.ps1` clears the previous immediate PNGs in its fixture and dedicated review folder before launching, then copies fresh captures on exit. A different FixtureName still replaces the same suite's review images. Other suites, logs, saves and historical fixtures remain intact. Paths must stay in their project report containers; directory links and suite path escapes are rejected. Raw gallery contents remain ignored by Git.

`tools/Collect-InterfaceScreenshots.ps1` provides `recorded-native` and `recorded-legacy`: the newest recorded successful capture for each filename across existing fixtures. These are explicitly historical snapshots, not a fresh whole-interface test. Use their source/date manifest to distinguish earlier presentation from current-run folders.

Examples:

```powershell
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-tuning -TuningOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-build -PartsOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-workshop -WorkshopOnly
./tools/Test-NativeMenu.ps1 -FixtureName ui-review-menus
./tools/Collect-InterfaceScreenshots.ps1
```

Mouse tests require an available interactive Windows session. Run them sequentially. Gallery export does not change player assets or test simulation behavior. `tools/Test-InterfaceScreenshotExport.ps1` checks replacement, failure labels, preservation of logs/other suites and path guards without running Unity.
