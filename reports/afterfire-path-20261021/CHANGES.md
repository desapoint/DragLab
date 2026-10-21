# Constrain afterfires by their installed acoustic paths — 2026-10-21

The user hears an external/free blast and remaining grain. This checkpoint fixes
afterfire acoustic transfer and texture, preserving the engine bed and actual
reaction simulation. Listening acceptance remains separate from technical checks.

## Measured faults

The existing Tempest case generates substantial reaction energy in zones 1–2,
inside the canonical 1.1 m header section. The old audio gate only applied the
turbine before 0.45 m, missing those reactions. The local wall branch contributes
about 8.6% of outlet branch squared-signal energy in this audit, so it is not the
sole cause. `before-paths.txt` retains the actual counts/energies and branch audit.

Internal echoes were generic 3–20 ms taps, independent of actual runner/collector
geometry. The local structural path also used the full stochastic gas signal.

## Correction

- Centralize the existing header/turbine/path dimensions without changing their
  values or arithmetic. Use the real header boundary for pre-turbine sound.
- `AfterfirePipeResponse` prepares separate source-relative outlet and upstream
  return paths. Reflections use existing per-cylinder runner lengths, collector
  scale, resonance scale and source gas temperature. Upstream components are
  traversed twice on a return; waves returning from downstream of the turbine
  receive its round-trip damping. The direct downstream path is not falsely
  sent through upstream hardware.
- Local wall radiation is driven by coherent pressure. It no longer carries raw
  stochastic gas texture around the hardware. It retains the earlier local
  arrival, damped modes and independence from downstream silencing.
- Remove the remaining 20% direct-noise bypass around source texture rounding.
  A small 1.08 texture normalization retains low/mid body; high-grit coefficient
  falls from .006 to .003. Pressure source, reaction cadence, decay parameters
  and global afterfire gain are preserved.

This remains a reduced-order transfer model: reflection strengths and baseline
collector timing are calibrated, not a resolved pipe-network/structural solver.
Delay and turbine coefficients capture gas temperature/boost at the beginning
of each transported flame/zone voice. It does not yet resolve temperature fields
along the acoustic path or update delays continuously during a voice. Metal
material/thickness data is not authored, and pressure-only wall excitation
deliberately omits turbulent structural forcing. Shared crowd propagation still
follows both branches afterward; no new environmental echo is added here.

At 48 kHz the added prepared return history is about 16 KiB per voice, up to
1 MiB per audible car's 64-voice pool. Rate-sized rings also avoid aliasing long
paths at 192 kHz. There are no callback allocations; phone CPU/listening is a
separate device gate.

## Verification and review

`AfterfirePathVerification.AuditBefore` measures the previous branches. Its
`VerifyAndBuild` runs boundary, load-damping, runner-dependence, wall-isolation,
reset and allocation gates at 24 / 44.1 / 48 / 192 kHz, repeats the actual two-car
audit, and checks exact per-zone reaction counts and chemical acoustic energies.
It reruns source/grain, fuel/mixture and integration checks, all 47 production-DSP
race replays, then builds Windows. Historical report directories are preserved.

## Technical results

- **201** gates pass: 47 path/audit, 33 grain A/B, 46 source/wall, 26 runtime and
  49 mixture checks. Old and new source-grain A/B pressure samples remain exact.
- All **47** production-DSP race replays pass, including outdoor propagation,
  unchanged ET/legacy traces, all **117,150** chemical events, no drops and no
  stolen tails. Maximum mixed peak is **0.3394**; largest local callback p95 is
  **2.7362 ms** per 400 stereo frames against an **8.333 ms** budget. This is an
  Editor observation, not a phone performance guarantee.
- The two actual race audits preserve every per-zone count and chemical acoustic
  energy. Wall/outlet squared-signal ratios fall from 4.48% / 8.58% in Bison /
  Tempest to about 0.00035% / 0.023% after removing stochastic structural forcing.
  These ratios are diagnostics, not perceived volume or absolute acoustic power.
- Windows Development build succeeds. Runtime assembly SHA256:
  `47ED556A724E3CE759F78519034F987A6D9AF3CC3C36B375D974370A210303E1`.
- Reopen only `26-launch-hard-ignition` through the normal live game test, with
  engine-only audio and the rival muted. A playback snapshot records progress;
  it is not claimed as another completed automated packaged verification.

No other sound layer, simulation mechanics, save state or later phase advances.
Android is not rebuilt by this Windows listening checkpoint. Pre-existing report
changes, remote attachments, `Tests.ps1` and race/shop reports remain intact.
