# Deeper V8 listening sample — pending approval

The user could not distinguish revision 2 from the current voice and requested a clearly deeper growl sample only. This isolated audition substantially strengthens the lower bank-order voice, with rounded saturation, uneven crank-locked amplitude and small seeded pulse variation. It reduces the upper firing-tone and idle harmonic weights. RPM timing stays unchanged; this is a timbre option, not a globally pitch-shifted recording or a runtime modification.

- `01-deep-v8-idle-rev.wav`: proposed idle and neutral revving, 8 seconds.
- `02-deep-v8-acceleration.wav`: proposed loaded acceleration, 8 seconds.
- `03-current-then-deep-acceleration.wav`: current 0–8 s; silence; deeper proposal 8.7–16.7 s.

All are generated mono 48 kHz PCM auditions using the same scripted input tracks as the earlier packs. Constant gain targets −20 LUFS with a −3 dBTP ceiling; the A/B is level matched. No perceptual acceptance is claimed.

Standalone renderer compilation and render pass. All samples are finite, unclipped, and valid PCM. Same audio seed repeats exactly; a different seed changes the variation. Baseline PCM matches revision 2 exactly. Over the same loaded 3–5 s window, spectral centroid changes from 163.10 to 104.28 Hz and energy below 120 Hz from 19.99% to 78.02%. These measurements establish a substantially lower spectral balance, not realism or user approval. Very low idle components may be less audible on small speakers.

The local `prepare.py` and `package.py` retain the standalone generation recipe; raw buffers, copies of DSP, project outputs and audio remain ignored local review artifacts. No game code, sound assets, bench, settings, saves or build was changed. Integration still requires explicit approval. Unrelated user edits remain untouched. No Unity tests or new game build were required for this sample-only checkpoint.
