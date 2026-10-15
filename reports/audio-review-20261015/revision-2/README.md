# Revision 2 — retain the current V8's low character

The user rejected the first sound draft as trumpet-like and much higher in frequency. That criticism is supported by its spectral balance: over the same 3–5 second loaded-pull window, spectral centroid rose from approximately 163 Hz in the current algorithm to 284 Hz in the rejected prototype; energy above 700 Hz rose from about 0.17% to 7.5%. These are brightness measurements, not a substitute for listening or a measurement of engine RPM.

The rejected voice replaced the source waveform with sharper pressure pulses and used substantially brighter filtering. Loudness matching made the comparison more even in level, but could not correct that wrong timbre. The original deep voice should have been the reference before trying a wider redesign.

This revision starts from a standalone copy of the **current game DSP**. Keep its oscillator frequencies, firing timing, source cutoff, exhaust chain and induction layers. Adjust only the existing low-bank weights under load, add a mild bounded saturation component before the existing filter, and smooth a small ±1.2% combustion amplitude variation from a separate audio seed. No timing/pitch randomization is added in this revision. The proposal remains deliberately close to the current voice; the next decision is whether its loaded texture is moving in the right direction.

## Listen

Both clips: **current 0–8 s → silence → revised 8.7–16.7 s**. Constant-gain loudness matching targets −20 LUFS with a −3 dBTP ceiling, preserving dynamics within each clip. These use the same scripted RPM/load track as before, not a live race recording.

- [V8 idle and neutral revving](01-v8-idle-rev-revised-AB.wav)
- [V8 loaded acceleration](02-v8-acceleration-revised-AB.wav)
- [Download both](revision-2-pack.zip)

## Checks and boundary

The 3–5 s centroid is 77.30 → 76.86 Hz for the idle/rev script and 163.10 → 160.85 Hz for the loaded script. Loaded energy above 700 Hz is 0.173% → 0.156%. These limited checks confirm that this revision avoids the previous brightness jump; they do not certify a natural growl. Human listening remains the acceptance gate.

The baseline raw PCM matches the first pack's current-DSP baseline exactly. Same-seed revised PCM repeats exactly; changing only its audio seed changes the sound. Renderer compilation/run, finite buffers, PCM clipping limits and loudness matching pass. No Unity or gameplay run is claimed. All runtime code, assets, builds, bench interfaces, settings and player data remain unchanged. Other engine-family, exhaust and induction redesigns are held pending review of the base V8 character.
