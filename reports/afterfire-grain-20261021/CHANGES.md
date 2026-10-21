# Afterfire grain-only listening refinement — 2026-10-21

The user describes the balanced backfires as gunfire-like and low-quality/noisy,
then explicitly limits this increment to reducing grain. Do not change the
pressure attack, reaction cadence, chemical simulation, global afterfire level,
burn/decay parameters, installed exhaust or wall/outdoor paths.

`ReactivePressureBurst` now rounds only the stochastic body after waveshaping,
where extra high harmonics had been introduced. An 80% single-pole smoothed / 20%
direct texture mix uses the existing body cutoff. The small high-grit coefficient
drops from .012 to .006. Pressure and all timing/envelope parameters are retained;
no whole-mix gain is changed. Removing texture energy does reduce its RMS, rather
than compensating by increasing another layer.

The source's optional `smoothTexture: false` constructor argument retains the
previous texture for deterministic diagnostic comparisons. Production always
uses the smoother default; there is no new gameplay setting or UI. Both paths
consume identical seeded noise and reaction packets.

`AfterfireGrainVerification.VerifyAndBuild` checks two burn lengths and a primary,
residual and renewed-pressure sequence at 24 / 44.1 / 48 kHz. It verifies exact
pressure/funding parity, retained low/mid body, reduced high-frequency texture,
finite seeded output, reset behavior and zero warmed allocations. Band measures
are weighted one-pole analysis bands, not calibrated perceived loudness or a
claim of physical exhaust accuracy. The existing source/wall, outdoor, mixture
and runtime gates are reused without overwriting historical receipts. Five
production replays cover Bison soft ignition, diesel fuel cut, Tempest two-step
and absorptive/open exhaust. The earlier full 47-case path baseline is retained;
it is not claimed as rerun for this texture-only adjustment.

## Technical results

- **187** checks pass: 33 grain A/B, 46 source/wall, 33 outdoor, 26 runtime and
  49 mixture gates. In all six controlled cases the pressure waveform and reaction
  funding remain bit-identical. The weighted high-texture energy falls roughly
  6–9 dB while the low/mid texture remains within the required 2.3 dB band.
- All five focused production race replays pass with unchanged ET and reaction
  counts, no lost events or stolen tails, and finite output. Maximum mixed peak
  is **0.3058**; largest local callback p95 is **1.0283 ms** per 400 stereo frames
  against an **8.333 ms** budget. This is not a phone benchmark.
- Windows Development build succeeds. Runtime assembly SHA256:
  `49E211CEAC71206A995A647346A8CA3D14351191609FF01D674B2F7E5A37DBAC`.
- Reopen only `26-launch-hard-ignition` through the normal live game test, with
  engine-only audio and the rival muted. A progress snapshot records playback;
  this launch is not claimed as a completed packaged automated verification.

User listening acceptance remains pending; no other sound layer or phase advances.
Android is not rebuilt by this Windows listening checkpoint. Pre-existing report
changes, remote attachments, `Tests.ps1` and race/shop report folders remain intact.
