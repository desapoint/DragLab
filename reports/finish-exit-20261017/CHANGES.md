# Post-finish dashboard and early exit — 2026-10-17

Native results retain the textured dashboard, live speed/RPM gauges, wheel and transmission display. The right-side scrollable slip/actions reserve the bottom instrument band, including the taller H-pattern shifter. F8 still switches the full native/legacy results presentation.

A player finish now determines its result independently of the other lane ending. `RaceSimulation.ResultReady` separates that result from `RacePhase.Finished`; both cars still use the existing authoritative stepping, shutdown and independent finish beams. A valid player-first finish is a win. Rival-first races still wait for the player's result; failed/incomplete runs retain their existing finalization. No opponent timing is fabricated or required for a confirmed win.

The existing staged settlement and durable writer start at result readiness. Garage, retry and their shortcuts become available once that save succeeds, even with an unfinished rival. Exit captures final player resources and closes its checkpoint; failed saves retain retry and block conflicting result actions. The ledger still awards once. A later opponent timeout cannot replace a valid player finish with a TIMEOUT label.

Staying on the slip lets the opponent continue racing. Unfinished elapsed/speed values remain blank; later reaction/split/finish figures update the existing labels without rebuilding the paper, resetting disclosures/scroll or settling the win again. Focus loss after the player's finish does not auto-pause the rival; explicit pause and suspend/exit save boundaries remain protected.

## Checks actually run

- Cross-car domain/finish presentation: **209 checks, zero failures**, all 13 authored vehicles in both finish orders, early result readiness, separate beam publication, immutable ET/trap, progressive stopping and brake-pressure bounds. `FinishPresentationVerification.RunEarlyResults` writes here without replacing historical reports.
- Packaged Windows `-RaceHudOnly -FixtureName finish-hud-20261017-a`: **216 checks, zero runtime errors**. Covers instruments/transmissions, F8, pause, later rival timing, results/export/retry, paid early exits, failed save/recovery, held background writes, single rewards, seeded refresh and final resource reload. Ran against the first implementation build before the final slip-band adjustment and additional layout assertions.
- Final packaged `-FinishExitOnly -FixtureName finish-exit-20261017-final`: **44 checks, zero runtime errors**, real native input and isolated fresh save. Confirms paid wins with a 40-second delayed opponent, Garage and retry before that opponent finishes, blank rival ET, durable single rewards/checkpoint closure, and clear instrument/shifter bounds at 1024×768, 1280×720 and 1920×1080. Includes starter-purchase prerequisites; overlapping checks are not summed.
- Final Unity 6000.6.4f1 Windows development build succeeded at `Game/Builds/Windows/DragLab.exe`; fingerprint is in build-manifest.txt. Final captures at compact/default/1080p were visually reviewed. Raw images/logs/saves remain local under the corresponding player-fixtures folders and interface-screenshots.
- Targeted/staged whitespace checks pass. Existing unrelated reports, Tests.ps1, Resources.meta and race-exit-regression artifacts are preserved. Build-generated renderer/font serialization noise was verified and backed up locally before removal.

Technical checkpoint complete; user visual acceptance remains pending. Review the retained dash and shorter scrollable paper, then use Garage or Race again while the rival is still running. No later interface or phase is advanced; debug benches are unchanged.
