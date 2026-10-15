# Engine rebuild flow validation — 2026-10-15

Checkpoint requested after the liquid/shop quote correction at `b872b33`. No new interface phase or rebuild behavior is introduced.

## Domain and durable-save matrix

`EngineRebuildFlowVerification.Run` passed **299 checks across all 13 authored vehicle configurations**, including petrol/diesel and varied cylinder counts. Each case uses an isolated SQLite save, damaged/seized pistons, a serviceable worn block, retained out-of-scope tire/cooling wear, low fluids and deliberately exhausted shop stock.

Verified:

- Preview and payment prices agree, including replacement supply/delivery, filter and renewal oil. Preview/cancel does not spend or write a save.
- The paid action atomically records one charge and a timed reservation; replayed payment is rejected.
- Failed persistence publishes no payment, stock change or job. Reserved installed parts cannot be removed, and the reserved engine cannot drive.
- Collection before the exact deadline is rejected. Job identity, price and deadline survive actual SQLite reload.
- Failed collection persistence preserves the original paid job and all conditions. Ready collection commits once with no second charge; repeated collection is rejected.
- Unsalvageable piston packs are replaced with healthy typed members. The removed damaged pack retains its original identity and condition in inventory.
- A serviceable block is reconditioned while retaining its identity. Oil is renewed and the fitted upgraded filter specification retained.
- Tire/cooling wear, coolant, fuel and saved tuning remain unchanged. The completed job survives SQLite reload and ownership validation.
- Missing required engine hardware is rejected; rebuilding does not manufacture missing parts.

The simulated clock supplied to domain collection is the saved ready timestamp; tests do not wait real shop minutes or alter the player's clock/save.

## Native interface and build

The Windows player build passed. The focused native mouse test passed **37 checks, zero runtime errors**, including fresh starter purchase prerequisites and the actual rebuild flow. No rebuild gameplay fix was needed.

The added development-only `--draglab-rebuild-only-verify` probe is available using `tools/Test-NativeMenu.ps1 -FixtureName <isolated-name> -RebuildOnly`. Normal gameplay does not enter it.

The probe covers the actual car-details action, displayed price, cancel, confirmation, disabled waiting-state controls, saved deadline, premature collection rejection, ready-state collection and retained damaged parts. Its ready-state transition advances only its disposable fixture's saved job timestamps.

Native fixture: `reports/player-fixtures/rebuild-flow-20261015`. Quote and completed screenshots were visually inspected at the normal 1280×720 window size; the confirmation content and actions fit. This checkpoint does not claim a fresh resolution matrix or real multi-minute wall-clock wait. The ready boundary and saved timestamps were tested explicitly instead.

Review screenshots are copied to the normal screenshot root: `reports/interface-screenshots/rebuild-quote.png`, `rebuild-waiting.png`, `rebuild-ready.png` and `rebuild-collected.png`. They remain generated local artifacts. The updated Windows player is `Game/Builds/Windows/DragLab.exe` with its companion data folder.

## Existing verifier limitation

Follow-up: [the pricing-assertion audit](PRICING-ASSERTIONS.md) diagnosed and corrected this prerequisite; the full individual-engine wrapper subsequently passed. The paragraph below retains the limitation at the time of rebuild validation.

`IndividualEngineVerify` was attempted first and stopped before its rebuild section at `city-three: replacement retains original and neighbours`. That combined assertion expects a unit's base price despite time-seeded supply/delivery fees, matching the pricing-test limitation documented in the original audit. Each operand of that combined failure was not independently diagnosed here; its later assertions are not claimed executed. The focused rebuild matrix avoids that unrelated prerequisite and uses authoritative paid quotes.

## Reproduce and evidence

Invoke `DragLab.Editor.EngineRebuildFlowVerification.Run` through the existing `ValidationBatch` manifest runner. Local evidence: `reports/branch-audit/rebuild/verification.txt`, `rebuild.tsv.results.tsv`, `unity-rebuild-flow.log`, `unity-rebuild-build.log`, and the native fixture/screenshots identified in the final result below. Generated fixtures/logs remain untracked.

The separate incompatible-reset and worn-tire rounding findings from [the audit](AUDIT.md) are outside this validation checkpoint. Existing user report edits and untracked files are preserved.
