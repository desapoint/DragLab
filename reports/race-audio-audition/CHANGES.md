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
# Accelerated live-player verification, not the normal listening speed:
.\tools\Test-RaceAudio.ps1 -SkipPrepare -SkipBuild -VerifyOnly
```

The game cycles 17 configurations (about 9m50s total), starting from a true neutral
idle. Engine cases cover idle, throttle blips, neutral limiting, launch, multiple
real shifts, loaded throttle lift/release and six seconds of running-engine
shutdown after finishing. Seven cylinder counts (3/4/5/6/8/10/12), injection,
carburetor, diesel and all six induction topologies are represented. Selected
5/10/12-cylinder/conversion cases are labelled laboratory configurations rather
than added catalogue products. Diesel does not invent a petrol-style BOV.

Three tire cases provoke physical street-tire wheelspin, wide drag-tire wheelspin
and a line-lock burnout. Actual tire contact must be sustained and audible;
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

- All **17 deterministic simulation/DSP cases** pass, including sustained audible
  contact in the three tire cases; a repeated turbo race is byte-identical.
- Independent Python WAV/input integrity, finite nonclipping PCM, audible phases,
  cylinder/induction coverage and neutral-idle checks pass for all 17 fixtures.
- Tire DSP **37**, retained tire contact/effects **39**, legacy/category preference
  **10**, real native Settings **173/0 errors**, and race pause **74/0 errors** pass.
- The packaged live suite checks all 17 races, muted rival/non-selected category,
  pre-finish Stop/Resume and visible nonoverlapping toolbar controls. Detailed
  current output is in live-verification.txt; captured examples remain local.
- Windows Development build succeeded. The normal Sound and pause captures were
  visually inspected; final human sound/compound calibration remains for review.
  Physical Android listening/CPU performance was not rerun for this checkpoint;
  no new Android package is claimed. Earlier pressure-core/Android gates are
  retained historical evidence, not newly rerun results.

Raw WAVs, JSON inputs, screenshots, profiles and logs stay unversioned. Review
engine/tire isolation, the three tire textures, Stop/Resume, and Apply/Cancel.
No bench redesign or later UI phase is included.
