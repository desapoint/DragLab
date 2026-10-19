# Uneven-bank V8 growl preview

2026-10-19. The user accepted the lower tone direction but found the darker
audition synthetic, and requested a more uneven growl, especially during revs.
This checkpoint remains preview-only. No game audio or vehicle asset changed.

## What changed

The optional `bank-growl` model replaces the uniform pressure kernel and repeating
cycle-strength sine with correlated, bounded cycle variation, modest cylinder
differences and coloured pressure texture. All eight combustion events remain.
RPM still determines their rate and timing; small acoustic arrival variation does
not change the input RPM or simulate missing firings.

The outlets now have separate broad resonances, path delays and pressure reflections.
One bank is more prominent in this mono pickup, exposing its unequal firing gaps
instead of summing the banks into a nearly uniform buzz. Throttle pressure has
different attack/release time constants, affecting pulse strength and width as the
engine revs up and falls back. No limiter, backfire or misfire events are invented.
The original and darker voice models and profiles remain reproducible.

These are authored acoustic choices, not identified properties of the car in the
video. The reference analysis focuses on idle; the neutral revs are an extrapolation.
No reference waveform, measured impulse response or repeated sampled loop is used.

## Listening checkpoint

Local page: `reports/v8-youtube-reference-20261018-j/index.html`.
It includes reference, production baseline, new idle/revs, and I-to-J comparisons.
Comparison clips play 12 seconds of the prior dark voice, a 0.7 second gap, then
12 seconds of this candidate at equal RMS. Perceived loudness may still differ.

Both scenes keep a spectral centroid below 170 Hz and less than 1% of measured
10-6000 Hz energy above 640 Hz. The new idle centroid is 124.04 Hz; neutral revs
are 130.07 Hz. The 20 ms envelope variation is deliberately stronger (idle CV
0.559 versus prior 0.253 and steady-reference 0.249). This candidate explores
audible bank/cycle unevenness; those measurements do not establish a closer
perceptual match. The user must judge whether the growl or thudding is appropriate.
See the adjacent matrix for both scenes, including peaks.

## Checks and limits

Passed full standalone render: seeded exact replay/variation, finite unclipped
exports, cylinder sequence/bank/event count, previous idle event-time agreement,
load/timing invariance, production source hashes and coarse spectral-error gate
against the production baseline. Additional checks confirmed the 2400 RPM firing
rate, throttle-pressure build-up/decay without changing firing times, unchanged
PCM bytes for the original F and darker I idle profiles, and Python syntax.

No Unity build was required. The generator is offline; CPU cost for a real-time
Unity/Android implementation, production dynamics integration, and listening
acceptance remain unverified. Existing unrelated worktree files are untouched.

```powershell
python tools/audio-audition-matrix/reference-pressure-preview.py `
  --reference reports/v8-youtube-reference-20261018-a/reference-analysis.wav `
  --output reports/v8-youtube-reference-20261018-next-growl `
  --config tools/audio-audition-matrix/reference-pressure-growl-profile.json `
  --previous reports/v8-youtube-reference-20261018-i
```

Generated media remains local and ignored. Tools, profile and concise verification
are versioned. In-game adoption requires a later user-approved checkpoint.
