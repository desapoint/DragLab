# Approved backfire sound reference

`backfire_lab_v3.html` is the user's supplied, unmodified reference, received
2026-10-21 from `E:/Téléchargements/backfire_lab_v3.html`.

SHA256: `6E97825426EA143E4CF102CDB66A39E35E9D4D7D684FF531DE2A5D8D46A5CBBE`.

The user approved applying its sound behavior and using its presets to calibrate
engine/exhaust combinations. Keep the original here to make later comparisons
possible. Its web interface is a laboratory reference, not a requested game UI.

The native port is `AfterfireLabSource` with `AfterfireLabSettings`. Ten sound
presets are transcribed. Hardware selects an exact preset: turbo systems use
Turbo Pop & Flame; large V8s use Big-Block; carbureted engines and chambered V8s
use Old-School; high-revving naturally aspirated engines use High-RPM; the
remaining engines use Street. Explicit native calibration can select any of the
ten presets. The simulation controls onset and strength, without compressing
pulse/decay times, adding a Shotgun blend or replacing the six preset envelopes.

The port preserves the V3 nonperiodic shock, four cascaded noise bands, broad
overdamped coloration, body-only saturation, DC blocking and 2x oversampling with
the same 33-tap anti-alias FIR (1x at 96 kHz and above). Live processing adds eight
output frames of causal FIR delay below 92 kHz, with a silent startup matching the
HTML's time-zero crop. Existing installed exhaust paths, wall radiation and
trackside propagation handle the spatial result.

The reference's count/spacing/pattern/RPM-sync are **not gameplay triggers**.
The within-burst follower and seeded micro-pressure texture ARE retained as part
of the approved sound; they neither create extra chemical reactions nor consume
fictional fuel. Its stereo delays,
demo room reflections, peak normalization and browser compressor are also not
added on top of the existing game mixer and propagation. There is no separate
reference/live envelope mode. One transported chemical flame shares one running
preset across zones. Every zone retains its own transfer path and cumulative
simulated acoustic-energy gain. Continuing numerical reaction packets do not
restart attacks, even when their pressure rises. Gain changes fade over 5 ms;
the first funded onset retains its full attack. The explosion envelope is finite;
continuing steady flame funding does not start a synthetic repeating sequence.

This is a reduced-order procedural acoustic model, not resolved fluid dynamics or
an absolute sound-pressure calibration. A preset's label never produces fuel,
selects limiter behavior, or changes car physics. No engine-bed, induction, tire,
save, or simulation-fuel code is replaced by this checkpoint.

Reproduce the numerical reference with
`node tools/Verify-AfterfireLabReference.cjs reports/afterfire-preset-fidelity-20261021`,
then run `DragLab.Editor.AfterfireLabVerification.Run` in Unity. Comparisons cover
both the source and actual history-driven gameplay/installed-exhaust/outdoor path.
Generated PCM and JSON stay local; current concise receipts are in
`reports/afterfire-preset-fidelity-20261021`. The earlier V3 report remains a
historical checkpoint whose live adaptation the user rejected.
