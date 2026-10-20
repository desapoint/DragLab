# Matched afterfire source-location preview — 2026-10-20

This study accompanies [the longer-afterfire prototype](../backfire-explosion-variants-20261020/CHANGES.md).
The same H source, simulation events and unmodified engine bed are held constant.
Each original Bison fixture retains its actual absorptive or chambered silencer.

| Variant | Explicit test source | Remaining acoustic path |
| --- | --- | --- |
| J | Before the silencer | Full fitted silencer transfer; 3.2 m assumed remaining path |
| K | Within the silencer | Half effective silencer amount; 1.3 m assumed remaining path |
| L | After the silencer | No downstream silencer transfer; 0.35 m assumed remaining path |

Transit timing uses an explicit 650 K gas assumption. A small feed-forward reflected
copy approximates a return path. This demonstration is not a solved two-direction
acoustic network; upstream impedance, local combustion chemistry and muffler shell
radiation remain outside the prototype. Source positions/lengths/temperature are
not existing simulated car values. These fixtures do not contain a catalyst or
turbine, so no such response is claimed. See the required integration contract in
[the audio direction](../../docs/ENGINE_AUDIO_PIPELINE.md).

Unity original-fidelity/event checks and independent source/control/PCM checks pass.
The checker verifies distinct location responses and retained 250–500 ms tail
energy; each of six repeated files is 13.2 seconds, stereo 48 kHz PCM16. Production
sound/physics/hardware data are untouched, and no user listening preference is
claimed. Reproduce both studies with `BisonBackfirePreview.RunAfterfireStudies`,
then `tools/Verify-BackfireExplosionPreview.py`. Raw WAVs/logs remain ignored.
