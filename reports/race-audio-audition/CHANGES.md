# Live race audio audition and category controls

Requested checkpoint, 2026-10-20. The listening test launches the actual Windows
player with the normal road, cars, gauges and production EngineAudio/TireAudio.
It does not play WAVs or open a browser. The earlier offline-only approach is
superseded; its deterministic DSP recordings remain regression artifacts.

## Run and listen

```powershell
# Generate/validate the inputs, build Windows, then launch visible live playback:
.\tools\Test-RaceAudio.ps1
# Replay in the existing up-to-date game without preparation/build:
.\tools\Test-RaceAudio.ps1 -SkipPrepare -SkipBuild
# Hear only the single-wheel/open-differential case:
.\tools\Test-RaceAudio.ps1 -SkipPrepare -SkipBuild -Case "18-"
# Accelerated live-player verification, not the normal listening speed:
.\tools\Test-RaceAudio.ps1 -SkipPrepare -SkipBuild -VerifyOnly
```

The game cycles 18 configurations (about 10m42s total), starting from a true neutral
idle. Engine cases cover idle, throttle blips, neutral limiting, launch, multiple
real shifts, loaded throttle lift/release and six seconds of running-engine
shutdown after finishing. Seven cylinder counts (3/4/5/6/8/10/12), injection,
carburetor, diesel and all six induction topologies are represented. Selected
5/10/12-cylinder/conversion cases are labelled laboratory configurations rather
than added catalogue products. Diesel does not invent a petrol-style BOV.

Four tire cases provoke physical street-tire wheelspin, wide drag-tire wheelspin,
a line-lock burnout and open-differential front-left-only wheelspin. Actual tire contact must be sustained and audible;
traction/temperature/wear are never fabricated for the audio. Those cases default
to **Tires only**; the engine cases default to **Engine only**. Rival engine and
tires are always muted. **Mixed / Engine only / Tires only**, **Stop/Resume** and
**Next car** remain available before finishing. At the end, Resume/restart starts
the suite again. Escape opens the normal limited pause Sound dialog.

Normal listening advances at wall-clock speed. The accelerated check uses the
same fixed 120 Hz driver commands and simulation, checking exact elapsed time,
shift/limiter/release counts, real AudioSource output and solo silence. Prepared
inputs are not cached sound/telemetry playback. Wear/consumption are disabled only
in these test fixtures. The required --draglab-verify directory isolates saves,
preferences, logs and captures; no career settlement or ownership changes occur.

## Shared Sound controls

Main Sound and pause Sound have separate **Engine volume** and **Tire volume**
sliders/number inputs, with the existing theme, draft state and explicit Apply.
Engine includes combustion/exhaust/induction. Tire includes wheelspin, burnout
and braking contact. Master and rival levels still apply. Cancel/Back discards
drafts; save failure retains the applied mix. Existing files missing these fields
default both to 100%. Unrelated settings commits retain both values.

Audition solo buttons are transient test controls, independent of saved gains.
For example, an engine category deliberately saved at zero remains muted when
selecting Engine only. Audition preferences are local to its isolated fixture.

## Tire sound parameters

Existing per-corner slip work, effective friction, normal load and radius remain
authoritative. Prepared acoustic metadata adds the fitted compound and existing
front/rear width multipliers. Wider/softer rubber gently changes resonance and
rubber/squeal texture, alongside radius and actual contact behavior. One explicit
**Asphalt** acoustic surface is defined. These are bounded acoustic calibrations,
not measured tire recordings or changes to grip/handling. The DSP keeps reusable
buffers and reads published gains without settings I/O or Unity calls.

## Checks and limits

- All **18 deterministic simulation/DSP cases** pass, including sustained audible
  contact in the four tire cases; a repeated turbo race is byte-identical.
- Independent Python WAV/input integrity, finite nonclipping PCM, audible phases,
  cylinder/induction coverage and neutral-idle checks pass for all 18 fixtures.
- Tire DSP **97**, retained tire contact/effects **39**, legacy/category preference
  **10**, real native Settings **173/0 errors**, and race pause **74/0 errors** pass.
- The packaged live suite checks all 18 races, muted rival/non-selected category,
  pre-finish Stop/Resume and visible nonoverlapping toolbar controls. Detailed
  current output is in live-verification.txt; captured examples remain local.
- Windows Development build succeeded. The normal Sound and pause captures were
  visually inspected; final human sound/compound calibration remains for review.
  Physical Android listening/CPU performance was not rerun for this checkpoint;
  no new Android package is claimed. Earlier pressure-core/Android gates are
  retained historical evidence, not newly rerun results.

Raw WAVs, JSON inputs, screenshots, profiles and logs stay unversioned. Review
engine/tire isolation, the tire textures and single-wheel sound, Stop/Resume, and Apply/Cancel.
No bench redesign or later UI phase is included.

## Per-tire follow-up, 2026-10-20

The existing production path already reads each wheel's angular speed, slip,
contact force, load and radius and owns separate noise/filter/envelope state for
FL, FR, RL and RR. It does not use aggregate car slip to trigger race tire voices.
The authored aggregate-slip fallback is retained for the acoustic debug bench.
Four logical voices share one AudioSource to avoid four extra Unity audio chains.

Corrected the stereo map from front/rear to left/right and made the tire clock
stereo so Unity actually delivers both channels. Mono synthesis remains supported.
No handling, wear, differential torque allocation or bench UI was changed.

New tests cover every single corner at 44.1/48 kHz, mono/stereo: exactly three
planted contacts are silent even when aggregate car slip is high; full-frame PCM
is byte-identical to an isolated single-wheel frame; left/right placement is
correct and all-grip output is zero. The real wheel-contact solver with an open
differential produces 235 single-wheel-only ticks out of 240 for each corner.
The added full-car audition produces 3,858 actual front-left-only slip ticks.
The live player additionally asserts a two-channel tire callback.

Current tire checks are 97 plus retained contact/effects 39. Main Settings 173 and
pause 74 remain the previous checkpoint's passing results, not rerun for this
follow-up. Normal gameplay still has no audition controls. Physical Android
performance/listening remains pending.
