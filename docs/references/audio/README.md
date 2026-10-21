# Approved backfire sound reference

`backfire_lab_v3.html` is the user's supplied, unmodified reference, received
2026-10-21 from `E:/Téléchargements/backfire_lab_v3.html`.

SHA256: `6E97825426EA143E4CF102CDB66A39E35E9D4D7D684FF531DE2A5D8D46A5CBBE`.

The user approved applying its sound behavior and using its presets to calibrate
engine/exhaust combinations. Keep the original here to make later comparisons
possible. Its web interface is a laboratory reference, not a requested game UI.

The native port is `AfterfireLabSource` with `AfterfireLabSettings`. Ten sound
presets are transcribed; carburetion, cylinder count/displacement, redline, turbo
hardware, collector geometry and installed silencer/resonator types select and
blend relevant anchors. Actual gas temperature, reacting volume, pressure rise,
burn duration, flow and fitted pop controls refine each flame/zone voice.

The port preserves the V3 nonperiodic shock, four cascaded noise bands, broad
overdamped coloration, body-only saturation, DC blocking and 2x oversampling with
the same 33-tap anti-alias FIR (1x at 96 kHz and above). Live processing adds eight
output frames of causal FIR delay below 92 kHz. Existing installed exhaust paths,
wall radiation and trackside propagation handle the spatial result.

The reference's count/spacing/pattern/RPM-sync, automatic follower and random
secondary ignition sequence are **not gameplay triggers**. Its stereo delays,
demo room reflections, peak normalization and browser compressor are also not
added on top of the existing game mixer and propagation. Reference comparison
mode retains its source follower/micro-pulses so the core can be checked against
the original math. Live mode only renews pressure when simulated pressure rises;
every continuation funds a smooth finite combustion body with actual acoustic
energy. Seeded noise remains continuous within that flame/zone.

This is a reduced-order procedural acoustic model, not resolved fluid dynamics or
an absolute sound-pressure calibration. A preset's label never produces fuel,
selects limiter behavior, or changes car physics. No engine-bed, induction, tire,
save, or simulation-fuel code is replaced by this checkpoint.

Reproduce the numerical reference with `node Tools/Verify-AfterfireLabReference.cjs`,
then run `DragLab.Editor.AfterfireLabVerification.Run` in Unity. Generated PCM and
JSON stay local; concise receipts are in `reports/afterfire-lab-v3-20261021`.
