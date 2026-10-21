# Fixed afterfire envelope — preview only, 2026-10-21

User request: preserve the HTML burst sound while simulation sets timing and
strength; provide a before/after WAV beginning before the burst. Do not change
game audio yet. This checkpoint adds only an Editor audition and independent
reference/artifact verifier. Runtime Domain code, HTML reference, audio settings,
player resources, Windows/Android players and later phases remain unchanged.

## Correction and controlled listening

The existing live source starts a finite HTML envelope at ignition, but continues
increasing its gain as chemical packets arrive, with 5 ms smoothing. This can
suppress the initial bang relative to the already-playing tail. The preview
freezes one gain for the entire envelope:

`gain = sqrt(sum(actual flame acousticJ) / 0.2) * profile.popGain * 0.35`.

The chemistry, native C# pressure/noise generator, parameters, seeded texture,
first ignition time and total physical acoustic energy are retained. No extra
bangs, filters, saturation, EQ, fades, time stretching or mode-flag triggers are
introduced. All eight actual reaction packets from the first Tempest flame fund
one complete burst. No independent event is invented for each numerical packet.

This is an **offline energy-frozen prototype**, not an integrated causal runtime
fix. Complete recorded flame energy is available before rendering. A later live
implementation must explicitly obtain an onset energy estimate or use a bounded
collection delay; it must not read future chemistry, silently delay race events,
or discard later energy. That work is not approved by accepting this preview.

Local files (ignored audio):

- `01-tempest-envelope-before-after-matched.wav`: 6.5 seconds. Before chapter at
  0.00 s, first bang at 0.75 s; after chapter at 3.75 s, bang at 4.50 s.
  Both complete 2.75 s windows have a 750 ms silent lead-in and full finite decay;
  a one-second silent separator lies between them. One whole-chapter scalar
  matches RMS, then one shared peak guard protects both chapters. No dynamic
  processing is used to match loudness.
- `02-tempest-envelope-before-after-natural.wav`: identical events/windows with
  one shared peak guard and no relative loudness adjustment. This exposes the
  actual level change from removing the intra-burst gain ramp.

Both are raw mono sources duplicated to stereo. HTML stereo width/reflections,
installed path loss/delay/shell, bus DC/limiting and outdoor propagation are
excluded from **both** chapters to isolate the requested envelope correction.
The matched recording isolates timbre; the natural recording exposes level.
Neither is a recording of the full race mix or browser compressor.

## Actual captured case

Re-run the unchanged `26-launch-hard-ignition` Tempest fixture through the real
simulation. Its SHA256 remains
`6906758BF5F0DF0087E4BF62D5D5C4207C746455D20A77FFF479862F34E203E6`.

Select its first physical flame, ID 1, rather than crop an already audible
segment. The eight chemical continuations span 11.63452380952379 to
11.642857142857123 s, in one exhaust zone, supplying 0.13033320421170963 acoustic
joules. The original folded source seed is 422528400. Both versions start at the
same native sample (558800), using Turbo Pop & Flame preset 3. The before is
rendered by the **unchanged production** history reader, burst funding and raw
source tap. Actual per-tick packet availability is replayed as well as chemical
timestamps. Its PCM is bit-identical to the real full-engine replay until the
next independent flame starts; the rest of this isolated flame is rendered alone.

The initial 10 ms / following 500 ms energy ratio increases from 0.3131541 to
0.6793335. This is a genuine envelope change, independent of global loudness.
The matched file applies 1.1422242 to the complete before chapter and a common
1.4403823 peak guard to both; the natural file uses only the latter.

## Checks actually run

- Unity Editor compiles and executes the offline replay: **14 gates pass**,
  including original physical elapsed/pop trace, complete chemical energy,
  no dropped events/duplicate flames/stolen tails, actual native PCM agreement,
  seeded PCM determinism,
  square-root energy scaling, exact zero-energy silence, silent lead-ins and
  untruncated tails. An initial capture omitted the controller's initial idle
  binding and failed before any gates; it was corrected to match the existing
  audition. No production behavior changed.
- Independent Node verifier: **16 gates pass**. Execute the original HTML DSP
  verbatim with the same actual seed/preset; compare its entire mono burst,
  including padded tail, against revised native PCM times the fixed strength.
  Maximum difference 3.6614e-8, relative RMS 3.6188e-8 after accounting for the
  existing eight-frame causal FIR latency. Verify both 48 kHz/24-bit stereo RIFFs,
  chapter lengths, exact silent lead-ins/separator, scalar-only conversion,
  unclipped peaks and equal RMS of the matched chapters.
- Existing packaged Windows `Assembly-CSharp.dll` SHA256 is still
  `540C8BFC8E90831173E00EE7E978CAE4CAF665DC0805F69AF7B25AA1947086F8`.
  No player build, full 47-case suite or Android/device test was needed/run for
  this Editor-only preview. Earlier runtime checks remain historical baselines.
- Original HTML and all Domain source paths are unchanged by this checkpoint.
  Unrelated reports, attachments, Tests.ps1 and race/shop outputs are preserved.

Reproduce with Unity CLI execute method
`DragLab.Editor.AfterfireEnvelopePreview.Run`, then
`node tools/Verify-AfterfireEnvelopePreview.cjs`.

The concise reaction matrix and receipts are versioned; WAVs, raw Float32,
receipt JSON and Unity logs stay local. Human listening approval remains pending.
