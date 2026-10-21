# Afterfire simulation and audio direction

Recorded 2026-10-21 at the user's request.

## Current integration scope

Use the supplied `references/audio/backfire_lab_v3.html` as the pressure/noise
timbre reference. Keep its clear compression attack, broad body, correlated
turbulence and exhaust coloration. The live sound must be causal and seeded:
actual fuel/oxygen combustion supplies acoustic energy; pressure rise, reacting
volume, temperature and continued burning shape the attack, body and decay.
Limiter/two-step/anti-lag flags alone never trigger an explosion.

The fixed complete-flame-energy auditions are tonal references, not the live
algorithm. Runtime cannot read future fuel burn or apply a permanent canned
envelope. Do not grow one external gain over an already decaying burst, restart
an attack for each numerical reaction packet, or restore muffling by cascading
another general low-pass/muffler over the already voiced HTML source.

Preserve fitted exhaust/induction hardware, source position, physical losses,
travel, wall radiation, ambient propagation, wear, cuts and conserved resources.
Keep the ongoing engine/induction generators intact during this checkpoint.

## Deferred: shared gas-pressure/flow coupling

The user asked to record this direction for subsequent work, not implement a new
whole-engine gas solver during the immediate backfire integration.

- Pressure disturbances must propagate through connected exhaust volumes with
  direction-dependent travel, impedance/reflection and energy loss.
- Couple local pressure differences to gas transport, retaining fuel, oxygen,
  heat and mass balances. Pressure-wave travel and bulk mixture movement are
  different quantities; a returning wave does not automatically carry flame.
- Before-turbine reactions may affect turbine loading and shaft energy. Reactions
  downstream have a different, attenuated upstream interaction.
- Cylinder scavenging and intake influence depend on open valves/overlap and
  actual connections, not a global "backfire affects everything" multiplier.
- All affected sound layers should observe the same physical state. Shared bus
  limiting is not a substitute for pressure/flow feedback.
- Use a bounded, reduced-order implementation suitable for Android. Validate
  causality, conservation, stable substeps and numerical-partition independence
  before changing player physics or replay compatibility.

Current gap: the mixture model computes local reaction pressure and the audio
routes source-relative losses/wall response, but afterfire pressure does not feed
back into the main induction/turbo gas solver. The turbo acoustic collector feed
currently contains the engine pressure core, excluding afterfire. Do not report
this deferred feedback as implemented by an envelope or mixing change.
