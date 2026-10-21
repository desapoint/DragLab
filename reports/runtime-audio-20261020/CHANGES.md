# Runtime causal audio integration — 2026-10-20

## Requested checkpoint

The user authorizes integrating the reviewed audio work, asks that the new intake
be mostly unobtrusive, and requests the full visible in-game listening suite.
This checkpoint connects the mixture-driven afterfire model, reviewed shared C#
pressure source and subtle intake rush to production audio. The architecture/UI
plan does not advance. Prior previews stay available as historical evidence.

## Live data path

- `FuelDynamics` exposes physical delivery independently of depletion difficulty;
  the existing resource-flow formula/operation order is retained. Audible car
  simulations feed that law into the exhaust model once per physical tick.
- `ExhaustMixtureModel` is the shared eight-zone residual fuel/oxygen transport,
  gas/wall heat, concentration eligibility, seeded ignition and energy ledger.
  The Editor preview is now an adapter, not a second chemistry implementation.
  Fuel/ignition cuts, actual contribution/fueling condition, airflow, charge heat,
  exhaust heat and anti-lag affect inputs. No fresh fuel means no new supplied fuel;
  already retained mixture can still ignite while its material/heat permit it.
- Events carry sequence, simulation time, zone/location, consumed fuel/oxygen,
  released/acoustic energy, temperatures, RPM, cut context, boost and seed.
  A stamped 4,096-slot history publishes values without exposing mutable vehicle
  state to DSP. Consumers retain multiple events between UI frames. Overwritten
  history and exhausted voice budgets are counted explicitly, never silently
  labeled lossless outside their tested bounds.
- `AfterfireAudioSynth` prepares 64 independently decaying voices, source-relative
  installed exhaust filters, turbine response, cavity responses and reflection
  histories outside the callback. The complete reviewed pressure/body/grit,
  secondary texture, shell/cavity and reflection source generates 12 shared noise
  banks. Per-event seed selects/interpolates banks and varies stretch/color/gain;
  event energy sets amplitude and gas temperature affects propagation delay.
  A bank cache bounds memory and avoids generating large waveform arrays while
  playing. Its finite noise realizations are a real-time implementation choice,
  not newly simulated forces or evidence of measured exhaust geometry.
- The previous `exhaustPopCount` edge/timer requests remain only for compatibility
  with old input traces/diagnostics. Live frames use the funded chemical history;
  manually changing a legacy counter cannot create a gameplay explosion. Legacy
  isolated synth callers retain their explicitly tested old pop contract.
- BOV requests also enter a stamped bounded history, with simulation timestamps
  and 16 independent prepared release voices. Valve response families/induction
  physics stay intact; releases between frame publications no longer collapse.
- Intake uses the reviewed opening-driven turbulence, with runtime gain **0.21715**,
  about **36 dB below** the 13.7013 diagnostic calibration and about 21 dB below the
  quieter prior candidate. This lowers only intake, before the common mix/output
  protection; there is no loudness normalization that raises it again. Filters,
  zero-mean texture and 75 ms envelope remain. Pause clears/rebases its excitation.

The existing combustion/engine bed, bank stereo, fitted exhaust effects, cylinder
wear, limiter/cut rules, turbo/blower simulation, category settings and per-wheel
tire acoustics remain authoritative. No generic backfire sound is invented to
fill a quiet configuration. Finished running lanes remain audible; there is no
new finish-line sound cutoff. Stopped/failed engines supply no fresh combustion
fuel, while finite retained afterfire tails can still sound. Pause/test Stop mute
and rebase without replaying delivered events. Reconfiguration captures new
hardware and discards stale transient history. Copies/saves do not share or
serialize mutable live acoustic transport. The debug bench UI is untouched; its
existing physical tick feeds the same observation law for its audio.

## Accuracy and platform limits

This remains an explicitly calibrated reduced-order model, not CFD, measured
combustion chemistry, calibrated microphone SPL or proof of real-car timbre.
Existing authored assumptions remain: gas holdup, constant heat capacity,
chamber heat partition, wall capacities, hydrocarbon flammability surrogate,
ignition hazard, acoustic coupling and closed-throttle pumping proxy. Diesel uses
separate gross fuel constants but not detailed droplet evaporation/CO/H2 chemistry.
Cylinder burn input is the actual aggregate contribution/cut state, not newly
measured per-cylinder chemical packets. Detailed catalyst chemistry, flame-front
propagation, shell structural mechanics and odd-fire geometry remain outside this
checkpoint. Acoustic source texture is not extra chemical combustion energy.

No Unity APIs, file I/O, source construction or managed allocation belong in the
warmed callback. Models/queues are session-local; synthesis does not change torque,
wear, resources, career randomness or saves. Editor CPU/allocation measurements
are not Android device performance certification. Listening approval remains a
separate user review, even after automated gates pass.

## Verification and listening

`RuntimeAudioIntegrationVerification.FinalAndRaces` runs focused causality,
conservation, seed, no-allocation, event delivery, pause, BOV and physics-parity
checks; existing engine/pop/induction/powertrain/contact/tire/pressure/preferences
regressions; then regenerates and processes every one of the 47 real race inputs
through production DSP. Historical reports are preserved where practical; current
concise results are collected in this folder. Raw WAVs/logs/fixtures remain local.

`Tools/Test-RaceAudio.ps1` builds/launches the real Windows Development player.
The accelerated verification pass checks all fixtures and live category isolation,
rival silence, Stop/Resume, UI bounds and callback output; it is not a normal-speed
listening/timing test. The full normal-speed pass is then launched for the user,
with test-only category/Stop/Next controls and no player-save modifications.


## Verified results

- Shared mixture core: **49** causality/conservation/seed gates pass.
- Final runtime integration: **26** focused delivery, gain, no-allocation, seed,
  fuel-cut, resource/dynamics parity and pause gates pass at 44.1/48 kHz.
- Existing regression suites pass: **276 engine, 64 isolated legacy pop, 346
  induction, 116 powertrain, 39 contact/effects, 97 tire DSP, 116 pressure,
  78 acoustic refinement, 19 transition and 10 shared preference checks**.
- **All 47** real race input pipelines pass after the continuous-burn correction:
  every chemical event and BOV release delivered, zero history overflows or tail
  steals, finite PCM/headroom and unchanged original elapsed/request traces.
- Final gain/tail/cause refinements receive a focused rerun on turbo 03, anti-lag
  26, chambered V8 30 and open V8 31, plus the 26 integration gates. See
  `race-pipeline-focused.txt` for actual callback timings. The prior broad results
  are retained rather than falsely claiming a second full 47-case rerun.

The initial 47-case run exposed real tail stealing in case 03 (796 delivered
reactions, 686 stolen tails). Reactions close together in the same physical zone
now share a pressure front, with subsequent energy feeding a seeded diffuse burn
tail. Each chemical reaction/energy ledger remains intact; chemistry is not
silenced or altered to hide the budget problem. A new front still forms when the
continuous window/gap expires. The final front/tail gain contract is explicitly
checked; tail endings fade rather than cutting abruptly.

A stale pause fixture that manually changed an old pop counter was changed to an
explicitly isolated legacy-DSP test. A new gameplay negative gate proves those
counters cannot invent an explosion without fuel-funded events. The BOV fixture
also needed its required turbo hardware enabled; the production path retains that
hardware constraint. The existing scripted DSP audition now advances shared
acoustic time/mixture so its valve examples remain meaningful.

Current rerun receipts are collected under `regressions/`; historical tracked
report bytes are preserved, and unrelated user modifications are excluded.


## Delivery and listening — 2026-10-21

The Windows Development build completed successfully. The existing local Android
ARM64 build also reports success; its metadata contains every new runtime audio
type and its package timestamp is newer than the source. Binary hashes and the
exact evidence boundary are in `build-verification.txt`. Windows assembly freshness
is verified likewise. No physical phone performance/listening is implied.

The **normal-speed visible 47-case game audition is running**, launched with
`Tools/Test-RaceAudio.ps1 -SkipPrepare -SkipBuild`, in the isolated directory
`reports/player-fixtures/live-audio-20261021-064401-048`. Initial telemetry confirms
advancing race input and positive engine output, with tires/rival silent in the
engine example. This run is for user listening and is not falsely reported as a
completed accelerated packaged verification pass. Normal-speed completion remains
pending while the player works through the full approximately 28-minute sequence.
Use test-only Stop/Resume or Next car; Esc opens the existing limited sound controls.

Auditory review: quiet intake (especially Timber carburetor), no bell-like intake
tone, fuel-versus-ignition limiter behavior, Bison chambered/open afterfires, Tempest
continuous two-step/anti-lag reactions, distinct release valves and blower setups,
and independent tire-only slip examples. Report the displayed case and stage for
any unexpected sound. This is the requested stopping point for human review.
Unrelated dirty reports, Tests.ps1, remote attachments and other report folders
remain outside this commit. No later architecture/UI phase is started.
