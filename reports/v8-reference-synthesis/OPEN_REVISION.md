# Direct-pressure / clearer texture audition

2026-10-19. User rejected J as synthetic and muffled. No audio candidate has
listening acceptance; all changes here remain offline, outside the game.

## Change in approach

J routed its excitation through low-frequency sine resonators and successive
low-pass stages. Only about 0.033% of measured idle energy remained above 640 Hz,
while its envelope variation exceeded the steadier reference. Those observations
are consistent with the user's criticism, not a substitute for listening.

The optional `open-exhaust` source uses direct pressure transients and short
damped reflections instead of the ringing sine-kernel bank. A separate, gated
coloured-texture path restores exhaust detail without a constant noise bed. Its
bandwidth is independent of the low body layer. The second bank contributes more
and the cycle-variation amount decreases from 0.19 to 0.075. Short pulse tails
are tapered. RPM, firing order, throttle trace and all healthy firing events remain.

Reference audio is used for comparison/measurements only; none is sampled into the
generator. Neutral revs remain an extrapolation from the idle reference. These
parameters are authored DSP choices, not measured hardware of the source vehicle.

## Review

Final local page: `reports/v8-youtube-reference-20261018-l/index.html`.
It includes J-to-L idle/rev comparisons, reference audio and separate revised clips.
Comparison sections play 12 seconds each, with a short gap and equal 0.10 RMS.
This lower export level retains the pressure transients without compressing their
peaks or exceeding headroom. Perceived loudness can still differ between versions.

Idle envelope CV changes from about 0.559 to 0.283 (steady reference: 0.249).
Energy above 640 Hz increases to about 4.29% at idle and 6.05% during neutral revs.
Spectral centroids are 240.12 / 274.68 Hz respectively. The prior <170 Hz / <1%
brightness ceilings were calibration choices and are inappropriate for this
requested clearer audition; this profile instead checks <320 Hz and 0.5-10% upper
energy. These are regression bounds, not evidence of naturalness or an exact match.
Listening acceptance is pending. See the adjacent matrix for exported WAV values.

## Verification and limits

Passed the complete standalone render: same-seed exact replay, other-seed variation,
finite unclipped exports, eight-cylinder sequence/bank count, RPM-linked events,
unchanged J idle firing times, load-strength versus timing isolation, production
source hashes and coarse reference-band improvement over the production baseline.
Additional checks covered 750/2400/5000 RPM firing rates, Python syntax and
PCM-byte-identical historical F/I/J idle outputs through their original profiles.

No Unity source/asset changes or game build. Real-time/mobile cost and actual
simulation integration remain unverified. Existing unrelated files are preserved.
Generated media stays ignored; profile, tools and concise evidence are versioned.

```powershell
python tools/audio-audition-matrix/reference-pressure-preview.py `
  --reference reports/v8-youtube-reference-20261018-a/reference-analysis.wav `
  --output reports/v8-youtube-reference-20261018-next-open `
  --config tools/audio-audition-matrix/reference-pressure-open-profile.json `
  --previous reports/v8-youtube-reference-20261018-j
```
