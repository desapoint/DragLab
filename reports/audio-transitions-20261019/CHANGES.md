# Mobile audio transitions and post-finish playback

Requested bug-fix checkpoint, 2026-10-19.

The controller previously marked any limiter engagement as a full cut, including
soft limiting with nonzero physical combustion. The pressure core then suppressed
every cylinder. It now distinguishes actual complete combustion/shift cuts from
partial limiting; the simulation's combustion fraction still controls gain.
Skipping zero-contribution pressure-kernel writes reduces work during full cuts.
The existing firing order, individual wear and induction physics are unchanged.

Rapid exhaust-pop retriggers previously restarted the pressure envelope at zero
while the preceding event could still be audible. A short preallocated 2 ms decay
preserves the outgoing value across retriggers. Clear/mute removes this tail.
Intake gulp, hesitation, overrun, blower level and frame-volume changes now
interpolate at audio rate rather than switching abruptly at publication frames.
Real physical pops, limiter events, shifts and blow-off effects remain present.

Finished races no longer mark either running engine inactive. Both lanes continue
their actual shutdown/idle audio while results are displayed. Pause, leaving the
race, stalling or engine failure retain their own silence rules.

## Verification

- 19 new transition checks: partial/full/shift cuts, overlapping-pop continuity,
  clear, and intake/volume transitions at 22.05/44.1/48/96 kHz.
- Existing 276 audio, 64 exhaust-pop and 346 induction checks pass.
- Standalone production coverage passes 382 checks: 52 cars, 288 synthetic
  engine/induction combinations, determinism, callback boundaries, rates and zero
  warmed allocations. Naturally aspirated steady output still matches the
  approved core. The old discontinuous overrun transition is intentionally no
  longer an exact reference for the entire old 14-second sequence.
- Windows development player built successfully. Packaged results validation
  passed **53 checks / zero runtime errors**, including live DSP after the full
  Finished state, failed-save retry, export and race-again. See `player-verification.txt`.
- Android repackaging/device limits are recorded with the subsequent requested
  shop responsiveness checkpoint. No phone listening/underrun claim is made here.

Editor checks used `DragLab.Editor.PressureAudioVerification.Transitions`.
Standalone reproduction uses `verify-pressure-live.py`; generated WAVs and logs
remain local. Debug bench controls and the authoritative turbo model are intact.
