# Car engine listening — 2026-10-22/23

Owned-car and dealership details now offer **Hear engine** in the header, preserving the existing Stats/Fitted parts and Warnings/History rows. The shared dialog provides Replay, Stop and Close.

## Playback and isolation

- The existing CarSimulation and EngineAudio run on private copies of the actual fitted hardware, condition, fluids and tuning. No new sound generator, simplified physics, bench changes or persistent resources are introduced.
- A roughly 20–26 second sequence covers idle, hardware-limited neutral blips, a loaded pull with actual automatic gear changes, lift-off/pressure release and return to idle. The listening driver uses an earlier safe shift target based on the weakest fitted RPM limit; it does not alter the player's tune.
- BOV events require actual pressure and supported fitted hardware. The turbo matrix verifies a real delivered event while playing; Stop intentionally resets the current audio counter.
- Engine volume follows the existing audio settings; tire audio is muted. Closing, stopping, hiding the interface, changing context/revision, losing focus or suspending disposes/silences the preview. F8 retains its existing modal guard.
- Uninspected used listings use the same estimated public build as their visible stats and disclose that estimate. Advertised non-running engines cannot be auditioned. No hidden inspection state is revealed.

## Checks and builds

- Full live-audio sequence: **64 checks, zero runtime errors**, including petrol/diesel, I3/I4/V8, multi-gear factory builds, physical turbo release, playback cleanup, exact captured-input immutability and unchanged player/save bytes. Turbo pressure release is recorded while playing, not after Stop resets it.
- Header controls: **49 checks, zero runtime errors**, including both real buttons, audible playback and no ownership/inspection changes.
- Owned fitted/dealership layout regressions: running. Windows header build passed. Android is rebuilt after the remaining authorized warning control.
- Normal listening dialog screenshot reviewed locally. Physical Android listening and human sound/visual acceptance remain pending.

Reproduce with tools/Test-NativeMenu.ps1 -EngineListeningOnly or -ListeningControlsOnly and a fresh disposable FixtureName. Earlier interrupted probes remain local: invalid post-tick telemetry equality, a modal F8 expectation, missing stable Tempest ID, a worn-starter gear assumption and an after-Stop counter measurement were corrected rather than reported as completed suites. A temporary missing namespace compile failure was fixed before successful builds. The physical diagnostic additionally exposed unsafe neutral blips, so only the demonstration driver's inputs were made hardware-aware; audio and physics models remain unchanged.
