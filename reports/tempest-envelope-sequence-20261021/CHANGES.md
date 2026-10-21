# Full Tempest envelope A/B — preview only, 2026-10-21

User request: hear the complete Tempest sequence with engine sounds and the
reviewed fixed-envelope backfires, retaining before/after comparison and leaving
the game unchanged. This is another Editor-only listening checkpoint, not runtime
integration or progression to another phase.

Local recording: `tempest-full-sequence-before-after.wav`, 48 kHz / 24-bit stereo.
Total duration **73.316667 s**. Original complete engine mix starts at **0:00**;
the revised complete mix starts at **0:37.158333**. Each sequence lasts 36.158333 s,
including two seconds holding the final active engine frame for its audio/tails.
There is a one-second digital-silence separator. The earlier isolated A/B WAVs
are retained and reproduced with identical hashes.

Each half contains the same idle, throttle blips, neutral rev/limiter, two-step
countdown, launch, acceleration/shifting, loaded lift/BOV and finish/shutdown
sequence. Only the player engine is audible; no rival voice or artificial tire
slip is added. The original traction-assisted physical trace remains unchanged.

## Controlled mix

- Capture the original native engine premix, afterfire path output and final
  engine samples once from the real fixture. Retain engine combustion/exhaust,
  controls/cuts, wear, mechanical/intake sounds, turbo layers and BOV verbatim.
- Reconstruct the old afterfire independently from the same actual chemical
  packets, tick availability, seeds, hardware parameters, 5 ms gain updates,
  installed source path and retirement rules. It matches the captured old
  afterfire output over the **entire** race with **zero PCM difference**.
- Use the previously previewed fixed-strength envelope for **all 54 real
  flames**. Total recorded acoustic joules supply one scalar per flame, with the
  complete seeded HTML source shape. All 5,387 packets contribute once, every
  burst starts at its original native sample, and actual fitted hardware,
  source-relative travel/attenuation/turbine loss/shell paths remain intact.
- Replace only the old afterfire contribution **before** shared DC/soft limiting.
  Reprocess both mixes through the same native bus, then the same native 12 m
  trackside air absorption, stereo delays and reflected paths. Do not subtract
  sources from an already nonlinear limited recording or filter only one half.
- Preserve the physical relative levels. There is one shared output gain, **1**,
  and no per-half/per-event normalization or extra backfire EQ/compression/fades.
  The revised whole-mix difference is consequently audible in context, including
  natural shared-limiter interaction. This is distinct from the earlier isolated
  loudness-matched audition. The maximum WAV peak is 0.4689345.

The camera/listener follows longitudinally at a fixed lateral distance, matching
the earlier processing audition. There is no uncontrolled camera Doppler change.
Browser/OS/speaker processing is outside the WAV. No HTML demo room is added over
the native outdoor path. Existing engine and induction generators are untouched.

This remains an **offline** prototype using complete recorded flame energy.
It does not solve the separate causal onset-energy estimate/bounded collection
needed for future live integration. Listening acceptance does not silently apply
it to the game.

## Checks actually run

- **15 full-sequence Unity gates pass**: original 5,387 reactions / 54 flames;
  actual one-zone paths; no lost or double-counted continuation energy; complete
  original source/path PCM reconstruction; unchanged native start samples;
  deterministic full replay; native engine bus reconstruction; unchanged engine
  bed; no premature afterfires; nonzero complete output.
- All recorded acoustic energy, 132.02810505063448 J, is retained. Original elapsed
  time and physical pop trace are also checked in the shared capture.
- Old afterfire reconstruction error **0**; old complete native engine bus
  reconstruction error **0**. Engine-bed isolation error 1.8042e-16.
- **121 independent Node gates pass**: execute the supplied original HTML DSP
  verbatim for each of the **54** actual seeds/hardware parameter sets. Check
  complete source envelopes against revised C# PCM with a single fixed physical
  gain, including padded tails and existing eight-frame FIR latency. Maximum
  absolute error 3.5353e-7; maximum relative RMS 3.7269e-8. Validate all flame
  counts/energy, unchanged engine opening, stereo RIFF format/size, complete A/B
  durations, separator, finite unclipped PCM and shared scalar-only conversion.
- **14 Unity + 16 independent gates** for the original isolated preview are rerun
  because its capture gained optional context callbacks/tail support. Both short
  WAV hashes remain identical to commit `66a525f`; their tracked evidence does
  not change.
- The explicit Editor-only source-control DTO allows reference verification
  without adding serialization attributes to runtime Domain structs. The first
  verifier attempt correctly rejected missing metadata; that export contract was
  repaired without tuning audio.
- No player build or Android test is run for this preview. Packaged Windows
  `Assembly-CSharp.dll` is still SHA256
  `540C8BFC8E90831173E00EE7E978CAE4CAF665DC0805F69AF7B25AA1947086F8`.
  All Domain sources, the supplied HTML, player settings/resources and existing
  players remain unchanged. Unrelated report/test/attachment changes are retained.

Input fixture SHA256:
`6906758BF5F0DF0087E4BF62D5D5C4207C746455D20A77FFF479862F34E203E6`.

WAV SHA256:
`32d1e53fd705a44856c37c94b22d7c9b4c01b9280f97e75e7c73bb4822e72073`.

Reproduce with Unity CLI execute method
`DragLab.Editor.TempestEnvelopeSequencePreview.Run`, followed by
`node tools/Verify-TempestEnvelopeSequence.cjs` and
`node tools/Verify-AfterfireEnvelopePreview.cjs`.

Authored Editor tools/verifiers, this report, the 54-flame matrix and concise
verification receipts are tracked. WAVs, raw Float32 sources, JSON metadata and
Unity logs stay local and ignored. Stop for user listening review.
