# Pressure combustion and turbo acoustics integration

2026-10-19. Requested checkpoint: apply the reviewed pressure-event core to all
engines, preserve existing audio/gameplay systems, and add missing turbo acoustics
from the supplied Python reference. No subsequent interface/content phase is
advanced. Listening acceptance of the earlier three-car preview is retained;
physical-device acceptance of this integration remains separate.

## Delivered

- Cylinder firing events, rarefaction pulses, unequal runners, bank headers,
  collector reflections and separate block/intake response replace the harmonic
  combustion core inside the existing synth. Current mechanical firing order and
  actual per-cylinder contribution remain authoritative.
- Added pressure-derived exhaust/turbine response, shaft-following compressor
  partials, actual-airflow texture and boost-dependent exhaust response. The
  supplied Python turbo integrator and recording normalization are not imported.
- Existing installed exhaust filters/upgrades, diesel/clatter, wear, combustion
  and shift/limiter cuts, pops, superchargers, blow-off modes and master gain remain.
- `InductionDynamics`, powertrain, fitting, physics and content data are unchanged.
  Spool/coast, flow limits, efficiency/temperature/intercooling, backpressure,
  twin/twincharged bypass and supercharger losses still come from that simulation.
- Pulse tables/coefficients and per-voice buffers are prepared before publication
  to the DSP callback. Tables share a bounded rate cache. Device-rate changes
  prepare a new audio session on the main thread, retaining the controller and
  latest frame; a mismatched interim callback is silent rather than allocating.
- Development callback counters are available for profiling. The debug bench UI
  is intact. The new opt-in playback test reuses isolated verification saves and
  preferences and requires no keyboard or historical slider layout.

Architecture and limitations: [audio pipeline](../../docs/ENGINE_AUDIO_PIPELINE.md).
The exact supplied turbo script is retained under
`tools/audio-audition-matrix/reference-v8-turbo-user.py` as a reference, not runtime
code. Runner/bank acoustics are authored approximations, not measured geometry.

## Checks actually run

| Gate | Result |
|---|---|
| Standalone production DSP | 380 checks; 52 authored configurations; 288 synthetic cylinder/layout/induction combinations |
| Reviewed naturally aspirated PCM | Juniper and Bison sample-exact against the frozen approved preview core |
| Determinism | Same-seed repeat and changed callback boundaries sample-exact; different seed varies output |
| Prepared sample rates | 8 / 22.05 / 32 / 44.1 / 48 / 96 kHz finite and bounded |
| Unity pressure checks | 116 passed |
| Existing engine audio | 276 passed |
| Shared audio / exhaust pops / induction | 7 / 64 / 346 passed |
| Debug bench/exhaust / engine concert | 599 / 159 passed |
| Unity total | **1,567 passed**, full run completed; expensive scenario/concert baselines rerun, not assumed |
| Packaged Windows live playback | **19 checks passed; zero runtime/DSP/GUI errors**; three cars, real listener PCM, master scaling/mute, pause/resume, actual AudioSource callbacks |
| Windows package | Build succeeded |
| Android package | ARM64 IL2CPP build succeeded; APK CRC, v2 signature and 16 KB page alignment checks passed |

Reproduction:

```powershell
python tools/audio-audition-matrix/verify-integration.py
# Editor execute methods, in order:
# DragLab.Editor.PressureAudioVerification.Run
# DragLab.Editor.EngineSoundVerification.RunStandalone
./tools/Test-WindowsSlice.ps1 -PressureAudioOnly -FixtureName pressure-playback-review
```

The former harmonic-only spectral expectations did not describe the approved
pressure voice. They now check correct crank/firing orders against off-order
energy; the idle gate retains lower-band dominance without forcing away the
approved pulse transients. Wear, gain, cut and hardware tests remain. Failures
are collected and reported before the suite throws, so an early tone assertion
cannot hide later failures.

The bench report's historical literal “four cars” was corrected to the actual
catalogue count (52) after this run; its loop already visited every definition.
This reporting-only correction was statically checked; the 599-case result is
from the completed full run, not an additional replay after that wording change.

The old `-AudioLevelsOnly` test passed actual listener volume scaling but then
failed an obsolete legacy Settings slider-rectangle assertion (the initial hidden
window attempt also could not focus its OS keyboard probe). It is not counted as
a passing UI test. The dedicated audio-path test above passed without changing
bench UI or removing that older test. Its fixture is
`reports/player-fixtures/audio-pressure-20261019-playback/` (raw logs ignored).

## Performance and limits

See [callback matrix](callback-matrix.csv). At 48 kHz/512 stereo frames, the signal
budget is 10.6667 ms. Standalone .NET p95 was 0.148 ms for a turbo four, 0.128 ms
for an NA V8, and 0.242 ms for a twincharged 16-cylinder/12,000 RPM stress voice.
All three warmed loops allocated **0 managed bytes**. Unity Editor stress p95 was
0.6697 ms, max 0.7531 ms, also zero allocations. Real Windows playback recorded
1,008 callbacks and a peak observed callback of 1.8472 ms while packaging ran.
These measurements are desktop observations, not a phone guarantee.

No Android device was attached. On-device listening, audio-device interruptions,
thermal/CPU budget and underrun measurements remain to be checked on the phone.
No new-game reset, player migration, active save edit or physics-version change
is needed for this audio checkpoint.

Unity's incidental texture metadata/font formatting and platform-filtered URP
serialization changes were archived locally and removed from the source diff.
No visual/rendering settings change is part of this audio checkpoint. Pre-existing
maintenance/shop reports and unrelated untracked files are preserved separately.

Review on phone: idle and neutral blips, loaded acceleration, shifts/cuts and
lift-off on an inline four, turbo four, V8 and diesel; compare exhaust upgrades,
shared volume/mute and pause/resume; check for crackles after background/resume
or output-device changes. APK and hashes are listed in the artifact manifest.
