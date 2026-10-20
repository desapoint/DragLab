# Isolated source-shape comparison, then one common exhaust — 2026-10-20

The user requested three isolated examples at equal loudness, then the same three
through the same exhaust, delivered as WAVs only. No game change is authorized.
This is an Editor-only listening checkpoint: no production DSP, authored car,
player resource/save/settings, build or HTML changes. Previous auditions remain
intact. AP-00 and production integration remain held for explicit approval.

## Six controlled recordings

Each 5.4-second WAV contains three identical repetitions at 0.6, 2.2 and 3.8 s:

1. Sharp snap, isolated source.
2. Ragged combustion burst, isolated source.
3. Fuller bang, isolated source.
4. The snap through the common exhaust.
5. The ragged burst through the common exhaust.
6. The fuller bang through the common exhaust.

No engine, tire, turbo, background sound or added environment reverb masks the
source comparison. These are laboratory shape examples, not a simulated race or
predictions of an identified production vehicle.

## Shared source mechanism

One kernel computes a normalized heat-release pulse for each assumed fuel packet,
accumulates pressure in a 1.2 L gas cell and relaxes it through a vent-time proxy.
A smoothed pressure derivative plus a small pressure contribution yields the source
waveform, with a 45 Hz DC-removal pole. Every complete event releases 200 J of
inferred combustion energy. There is no white-noise emitter, independently added
metal rattle or different DSP recipe for each label. Only the release-time table
and gas-pressure relaxation change.

| Shape | Release packets | Packet fractions | Burn constants | Vent constant |
| --- | --- | --- | --- | --- |
| Snap | 0 ms | 1.0 | 0.10 ms | 1.8 ms |
| Ragged | 0 / 3.7 / 8.9 / 15.8 / 26 ms | .28 / .23 / .20 / .17 / .12 | .20 / .12 / .16 / .13 / .19 ms | 3.2 ms |
| Fuller | 0 / 3.2 ms | .35 / .65 | 1.5 / 4.5 ms | 28 ms |

The packet timing is an explicitly authored listening hypothesis for distributed
heat release, not measured combustion data, a validated fuel-reservoir calculation
or a new gameplay pop schedule. No source is claimed to reproduce a real recording.
The earlier audition's single dominant primary pulse made its first ragged attempt
too similar to the snap; the similarity gate rejected it. Redistribution changed
the temporal source structure rather than reducing the gate's threshold.

## Identical exhaust

All three source arrays pass through the existing Editor preview duct solver at
48 kHz: 0.45 m of 48 mm pipe, 0.65 m of 60 mm pipe, one 0.65 m / 130 mm chambered
silencer and 0.8 m of 60 mm outlet pipe; assumed gas temperature 850 K. The source
is 0.12 m from the inlet, at an exact prepared junction. All use the same
`ChamberedSilencer` acoustic definition at stage zero. There is no turbine,
preview wall/crackle addition or exterior-reflection network. These choices
isolate the exhaust response, not recreate the earlier Tempest configuration.

The dry arrays feed this path directly. Python independently convolves each dry
array with the captured common impulse response and requires its wet array to
match within numerical tolerance. Thus there is no hidden per-source exhaust/EQ
adjustment. Source energy and hardware attenuation remain in the raw evidence;
listening copies receive individual constant gains only to compare at equal level.

## Loudness matching and evidence

Use FFmpeg's input loudness and true-peak meter (BS.1770/EBU gated measurement),
then multiply each complete array by a constant scalar in Python. The normalization
filter's processed audio is discarded; no limiter, compressor or dynamic EQ is
used in the delivered files. Choose one common target, no louder than -24 LUFS,
lowered if necessary to keep every source's true peak below -2 dBTP without
flattening its attack. Remeasure the delivered PCM16 stereo WAVs and require all
six within 0.10 LU. Matching measured loudness is an estimate, not a guarantee of
identical subjective impact for transients. See [ITU-R BS.1770-5](https://www.itu.int/rec/R-REC-BS.1770-5-202311-I/en).

Core checks cover equal heat accounting, settled sources, deterministic rerenders,
finite values, zero-input silence and no pre-echo. Packaging checks the independent
common-exhaust convolution, source shapes after gain and up to +/-5 ms alignment,
identical format/duration, unchanged waveforms under constant gain, loudness,
true peak, silent starts/ends and six distinct hashes. Source shape similarity
must stay below 0.85; this is a sanity check, not acoustic acceptance.

```powershell
unity run Game --timeout 1200 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.IsolatedBackfireAudition.Run
python tools/Package-IsolatedBackfireAudition.py
```

Uses the installed Unity CLI and `C:/tools/FFMPEG/ffmpeg.exe`; NumPy is required
for the independent packaging/FFT checks. No extra Python packages were installed.
Raw double arrays, manifest JSON, WAVs and logs remain local; source/tools/metadata
and concise evidence are versioned. Unrelated pre-existing work is preserved.
No worktree, branch switch, push or game integration.

Actual gate results and selected common loudness are in `verification.txt`.
Listening approval remains pending; no Android realtime claim is made for these
offline examples. The earlier 97 shared-source gate is historical, not rerun here.


## Final results

Unity batch rendering passed **43** isolated-source/path checks. Python packaging
passed the common-exhaust convolution check for all three sources. Maximum
normalized source similarity after up to +/-5 ms alignment is 0.82523; no pair
can pass as a simple level-adjusted duplicate under the configured sanity gate.
The fuller candidate was also widened before final rendering so it was not just
a softened copy of the snap. The gate was tightened from 0.92 to 0.85.

All six delivered WAVs measure **-32.05 LUFS**. The unusually short snap sets the
crest-factor limit, so the common target is lower than the initial -24 LUFS ceiling;
no limiter or compressor makes its attack fit. True peaks range from -2.20 to
-9.44 dBTP. All outputs pass constant-gain waveform, silent lead/tail, distinct-hash
and identical-duration/channel/rate checks. These gates do not establish realism
or listener preference. Only the requested six listening WAVs are handed over.
