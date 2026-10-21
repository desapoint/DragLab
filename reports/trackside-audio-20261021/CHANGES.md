# Live pressure sources and outdoor trackside listening — 2026-10-21

Requested checkpoint: remove the canned quality from afterfires, let a consumed
fuel pocket produce an initial pressure bang and smaller subsequent burns when
the actual mixture supports them, and make all vehicle audio sound external to
the car through a common atmosphere/trackside path. Stop for human listening
review after verification, build and the visible game audition.

## Findings and source change

The prior runtime afterfire source replayed twelve prepared generated waveforms,
with event-dependent stretching/filtering and a diffuse continuation tail. That
could preserve a recognizable burst shape and turn later combustion energy into
air-like noise. The game also had no shared source-to-listener outdoor path:
small internal exhaust reflections were present, but engine and tire AudioSources
were effectively dry stereo sources.

`ReactivePressureBurst` replaces that runtime waveform bank with a continuously
evolving pressure rise, expansion/rarefaction, broadband pressure texture and
gas-flow body. Each real chemical event funds the pressure/flow excitation.
Changes in the simulated pressure influence how much excites a coherent front;
later reactions do not merely switch on a generic hiss. Seeded texture evolves
continuously inside the front, rather than selecting a stored explosion clip.
The first excitation is tracked independently of audio travel time, so packets
arriving before the front becomes audible are not all mistaken for primary bangs.

Declining reaction energy/pressure can create smaller later disturbances. Fresh
fuel or a newly accumulating pressure pocket can produce another larger bang;
there is no artificial rule that every subsequent bang must be weaker. A physical
isolated-pocket check proves the sequence depletes/stops without fresh fuel. A
separate controlled source test checks a larger pressure event followed by two
weaker events, plus a stronger replenished event, using only funded event energy.

Installed source-relative exhaust/turbine filtering, authored gain/sharpness/decay,
combustion wear/cuts, induction and individual-tire source generation remain.
There is no new fuel, combustion trigger, limiter-flag bang or artificial pop train.
The engine combustion oscillator and vehicle simulation are unchanged.

## Common listener path

`TracksidePropagation` processes the complete engine mix (combustion, mechanical,
intake, induction/BOV, afterfires) and the separate tire mix. It provides:

- Metre-based direct travel time and 1/r pressure spreading, with a 6 m reference.
- Temperature-dependent sound speed and ISO 9613-1 pure-tone atmospheric
  absorption using temperature, humidity and pressure, approximated by four
  smoothly complementary frequency bands during playback.
- Image-source ground and opposite-barrier reflections, a quieter stand return,
  and two low-level diffuse returns. Echoes follow longer paths; air absorption
  itself is not used to invent echoes.
- Fractional stereo ear delays, lateral balance and continuous changing-delay
  Doppler. Unity stays in 2D AudioSource mode to avoid applying spatial/Doppler
  effects a second time.
- Immutable geometry/coefficient publication; prepared delay buffers and
  allocation-free callback processing. Geometry/air calculations and device-rate
  reallocation stay outside the audio callback.
- Pause/session generation resets so old echoes cannot replay on resume even if
  Unity did not process an inactive/virtualized source while muted.

The virtual listener follows the race view outside the near lane, about 12 m away
and 1.65 m high. This is an external tracking spectator perspective, not one fixed
person at the starting line. Opponents use their actual relative distance and lane;
the old additional opponent-distance gain is replaced by the common propagation
model while the configured opponent volume remains. The bench inherits a stationary
external listener without changing its debug controls. Tires use a representative
0.08 m source height and engine/exhaust audio 0.55 m. Existing per-wheel sound
generation and stereo distinctions remain upstream of this category-level path.

Default humidity is 50%; temperature/pressure follow the authored induction
atmosphere. There is no new weather setting or UI. The geometry API also supports
a fixed listener for future recording/camera modes without changing the sources.

## Basis, calibration and boundaries

[ISO 9613-1](https://cdn.standards.iteh.ai/samples/17426/3a2d69b767024b74805b83b063a91445/ISO-9613-1-1993.pdf)
supplies the pure-tone loss calculation. The humidity conversion uses percentage
molar water concentration; the 20 C / 50% RH / sea-level 1 kHz check prevents a
factor-of-100 unit error. Temperature-dependent sound speed follows the
[ideal-gas relation](https://www.grc.nasa.gov/WWW/K-12/BGP/sound.html).
[Outdoor image/specular/diffuse boundary concepts](https://doc.comsol.com/6.3/doc/com.comsol.help.aco/aco_ug_geometrical_aco.11.02.html)
motivate the sparse reflection path, not a claim that this renderer is a full
ray-traced or ISO 9613-2-compliant field solver.

This is still reduced-order game audio, not calibrated absolute SPL or a nonlinear
pressure-wave/CFD solution. Pressure-to-audible excitation, material reflection
coefficients, representative emitter heights, diffuse stand paths and four-band
air rendering require listening/measured calibration. It does not model full
HRTFs, per-wheel outdoor emitter positions, crowds as resolved scatterers,
wind/temperature gradients, terrain diffraction or obstructing 3D structures.
Changing listener geometry rather than attaching a microphone to the car changes
the heard sound without changing engine physics. No guarantees of perceived
realism are inferred from automated checks.

At 48 kHz the four-second stereo delay storage is about 2 MiB per category/vehicle
(about 8 MiB for two cars), prepared outside playback; removal of the shared burst
bank saves its previous runtime storage/preparation. Physical Android CPU/speakers
remain a separate device gate. This checkpoint rebuilds Windows, not Android.

## Verification

`DragLab.Editor.CrowdAudioVerification.Checkpoint` checks outdoor travel, reflection
arrival, inverse-distance pressure, atmospheric units, callback block invariance,
pause reset, Doppler, seeded pressure sources, primary/residual/replenished events,
finite fuel-pocket depletion, preserved authored acoustic controls and allocation
budgets. Existing condition, mixture, runtime, tire and contact gates are reused.
The 47 production-DSP race replays include the outdoor stage in the measured path.
Receipts distinguish chemical reactions from pressure-source voices.

The initial pass completed all 47 replays. The final pass reruns after the source
parameter/first-excitation refinements; the later presentation-only pause generation
fields are validated in the rebuilt game. The first combined build encountered an
Editor/player layout mismatch because those fields were added while the batch
Editor was running. This was a stale loaded-type issue, not a completed build;
rebuilding from a fresh process aligns both type layouts. No source changes are
made while that fresh build runs. Final build/live receipts are recorded below.

Historical report files and unrelated existing report changes, `Tests.ps1`, remote
attachments and the earlier race/shop report folders are preserved. No raw audio,
screenshots, logs, build products or test-save databases are versioned here.

## Final results and delivery

- **33** outdoor/live-pressure checks, **20** condition/causality checks, **26**
  runtime integration gates, **49** shared mixture/conservation gates, **97** tire
  DSP checks and **39** contact/effects gates pass (**264** total).
- All **47** final real-race production-DSP pipelines pass with the outdoor stage
  included, unchanged elapsed/legacy physical traces, all **117,150** chemical
  events delivered and **662** pressure voices. Zero event overflows/tail steals;
  finite output with headroom. See `race-pipeline.txt`.
- Six-path outdoor processing alone measures about **0.12 / 0.15 ms p95** per
  400-frame stereo block at 44.1 / 48 kHz in the local Editor. Both it and the live
  pressure source have zero warmed callback allocations. Full pipeline timings
  are retained per race. These are local observations, not phone guarantees.
- A fresh Windows Development build succeeds, with final runtime assembly SHA256
  `A6C1A0949D8E409317DDFF3D621B4EA5247FE3A01734B1C473B84528921034E4`.
- The actual packaged launch case passes with **5,387/5,387** reactions rendered,
  positive external engine output, tire/rival silence, Stop/Resume and unchanged
  ET/shift/cut traces (`live-launch.txt`).
- The actual packaged tire-only burnout passes with positive stereo tire output,
  exact engine/rival silence, Stop/Resume and unchanged physical traces
  (`live-tires.txt`). These game checks include the final pause generation fields.

The full visible 47-configuration listening suite is launched at normal speed,
PID **49168**, in `reports/player-fixtures/live-audio-20261021-084742-341`. Use
Stop/Resume, Next car and the existing solo controls. The rival remains muted.
This launch is not reported as a completed packaged 47-case verification run;
human listening acceptance remains pending.

Unrelated pre-existing files remain untouched: the supply/parts-market/SQLite
verification reports, `Tests.ps1`, remote attachments and the earlier race-exit /
shop-responsive report folders. No unrelated implementation, data or report is
included in the audio commit.
