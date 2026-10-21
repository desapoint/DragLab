# Approved backfire sound reference

`backfire_lab_v3.html` is the user's supplied, unmodified reference, received
2026-10-21 from `E:/Téléchargements/backfire_lab_v3.html`.

SHA256: `6E97825426EA143E4CF102CDB66A39E35E9D4D7D684FF531DE2A5D8D46A5CBBE`.

The user approved applying its sound behavior and using its presets to calibrate
engine/exhaust combinations. Keep the original here to make later comparisons
possible. Its web interface is a laboratory reference, not a requested game UI.

The native port is `AfterfireLabSource` with `AfterfireLabSettings`. Ten sound
presets are transcribed. Hardware selects a reference preset: turbo systems use
Turbo Pop & Flame; large V8s use Big-Block; carbureted engines and chambered V8s
use Old-School; high-revving naturally aspirated engines use High-RPM; the
remaining engines use Street. Explicit native calibration can select any of the
ten presets. Installed components adjust that generator's air/crack/smoothness,
bark/tube/focus, pulse width and decay controls. The stock Tempest downstream
reference remains the exact Turbo Pop & Flame voice. The calibration anchors
are stock turbo/street, chambered carb and open competition exhausts; these are
explicit acoustic approximations, not manufacturer measurements. The simulation
controls onset and energy without compressing envelopes to numerical reaction
packets or introducing a Shotgun blend.

The port preserves the V3 nonperiodic shock, four cascaded noise bands, broad
overdamped coloration, body-only saturation, DC blocking and 2x oversampling with
the same 33-tap anti-alias FIR (1x at 96 kHz and above). Live processing adds eight
output frames of causal FIR delay below 92 kHz, with a silent startup matching the
HTML's time-zero crop. Runner/collector geometry and actual upstream turbine
loading influence generation. Downstream routing retains only real attenuation,
travel time and separate pressure-driven shell radiation. It does not apply a
second muffler/turbine low-pass, header/collector echo, or generic smoothing to
the already voiced signal. Outdoor propagation still runs once for the vehicle.

The reference's count/spacing/pattern/RPM-sync are **not gameplay triggers**.
The within-burst follower and seeded micro-pressure texture ARE retained as part
of the approved sound; they neither create extra chemical reactions nor consume
fictional fuel. Its stereo delays,
demo room reflections, peak normalization and browser compressor are also not
added on top of the existing game mixer and propagation. There is no separate
reference/live envelope mode. One transported chemical flame shares one running
preset across zones. Its hardware colour is captured at the first funded flame
location; transported zones share that phase and envelope rather than restarting
or re-pitching it. Every zone retains its own arrival/attenuation/shell path and cumulative
simulated acoustic-energy gain. Continuing numerical reaction packets do not
restart attacks, even when their pressure rises. Gain changes fade over 5 ms;
the first funded onset retains its full attack. The explosion envelope is finite;
continuing steady flame funding does not start a synthetic repeating sequence.

This is a reduced-order procedural acoustic model, not resolved fluid dynamics or
an absolute sound-pressure calibration. A preset's label never produces fuel,
selects limiter behavior, or changes car physics. No engine-bed, induction, tire,
save, or simulation-fuel code is replaced by this checkpoint.

Reproduce the numerical reference with
`node tools/Verify-AfterfireLabReference.cjs reports/afterfire-single-stage-20261021`,
then run `DragLab.Editor.AfterfireLabVerification.VerifySingleStageAndBuild`.
Tests independently compare the outlet with unfiltered JavaScript PCM plus only
gain/delay, as well as checking source parity, hardware effects and the full
history/mixer/outdoor path. This catches added filtering that the earlier
same-filter-on-both-sides comparison could not detect. Generated PCM and JSON
stay local; concise receipts are in `reports/afterfire-single-stage-20261021`.
Earlier reports remain historical checkpoints, not listening approval.
