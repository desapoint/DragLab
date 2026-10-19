# Darker, less noisy V8 audition

2026-10-19. User found the first reference-led preview unclear and too high-pitched.
This is a revised offline audition, not approval to install audio in the game.

The earlier pressure voice combined strong broadband pulse noise, continuous
turbulence and several upper resonances. The revision reduces pulse noise from
0.58 to 0.14 and turbulence from 0.035 to 0.004, softens the pressure attack from
0.25 to 0.9 ms, lengthens the decay from 3.1 to 4.8 ms, and emphasizes broader,
lower exhaust resonances. The low-pass cutoff changes from 3600 to 1100 Hz.
These are model parameters, not measurements of the reference car.

RPM, firing order, event times, cylinder identities, seed and throttle/load trace
stay unchanged. No pitch shifting, invented limiter cuts or missing firings are
used. Original configuration is retained; the optional dark profile selects this
candidate. The original-profile idle export remains PCM-byte-identical to F.

Idle spectral centroid changes from 190.94 to 114.36 Hz; neutral-rev centroid and
upper-band energy also decrease, as recorded in the adjacent measurement matrix.
Those are timbre/brightness measurements, not engine RPM or evidence of subjective
clarity. The user must judge the listening result. Both versions use equal RMS
playback level; their perceived loudness may still differ.

## Review and reproduction

Final local review: `reports/v8-youtube-reference-20261018-i/index.html`.
It includes reference/current/revised playback and previous-to-revised idle/rev
comparisons, 12 seconds each with a short silence between them.

```powershell
python tools/audio-audition-matrix/reference-pressure-preview.py `
  --reference reports/v8-youtube-reference-20261018-a/reference-analysis.wav `
  --output reports/v8-youtube-reference-20261018-next-dark `
  --config tools/audio-audition-matrix/reference-pressure-dark-profile.json `
  --previous reports/v8-youtube-reference-20261018-f
```

Passed: deterministic replay, cylinder/bank/event count, unchanged prior idle
schedule, load/timing invariance, finite unclipped exports, coarse reference-band
improvement over the production baseline, lower brightness in idle and revs,
production source hashes, original-profile compatibility and Python syntax.
Generated audio/reports remain ignored; concise evidence and tools are versioned.
No Unity runtime changes or build; real-time/mobile DSP cost and human listening
acceptance remain unverified. Existing unrelated worktree files are preserved.
