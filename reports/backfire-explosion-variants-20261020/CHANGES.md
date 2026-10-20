# Separate longer-afterfire listening prototype — 2026-10-20

The user requested a longer explosion rather than a loud breaking/cracking impact,
and then specified that combustion location and intervening fitted hardware must
affect the generated sound. This is one preview checkpoint. Production event
generation, DSP, saves/preferences and builds remain unchanged; AP-00 remains held.

`BisonBackfirePreview.RunAfterfireStudies` renders both this G/H/I envelope study
and the J/K/L source-location study. Original simulation case 29/30 events are
retained, now in a three-second 17.900–20.900 excerpt so the later tail is audible.
There are still exactly two real triggers. Original PCM matches its source exactly.

G is a fuller afterfire, H a deeper pressure bloom, and I a longer rolling tail.
A distinct prepared cloud renderer replaces the amplified short-crack approach
only in these preview files. It has a softer onset, broad decaying pressure and
filtered turbulent energy, overlapping smooth release lobes and no pitched
oscillator. Four bounded voices allow the second event without truncating the
first. Cloud shape uses actual event strength, engine displacement, airflow and
boost plus existing pop gain/decay/sharpness. Coefficients are acoustic proxies,
not simulated trapped-fuel mass or measured explosion energy.

The same fitted exhaust transfer and upgrade level process the source. Explicit
650 K gas and 3.2 m remaining-path assumptions add approximate transit delay and
one small reflection; there is no claim these are authored Bison measurements.
The independent source-location study shows how only the remaining hardware
should shape the outlet response. The fuller required contract and missing
simulation inputs are recorded in [the audio direction](../../docs/ENGINE_AUDIO_PIPELINE.md).

Checks: Unity compilation/rendering, original source fidelity, actual event count,
finite bounded PCM and original pre-pop bed checks; independent PCM checks verify
controls, unchanged pre-event engine samples, stronger 250–500 ms tail energy,
distinct outputs and six 13.2-second repeats. No whole-clip gain/pitch/normalization.
This is technical preview evidence; listening approval, production integration and
Android realtime cost remain unverified. WAVs and raw logs remain local/ignored.

Reproduce using existing original fixtures/source WAVs:

```powershell
unity run Game --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.BisonBackfirePreview.RunAfterfireStudies
python tools/Verify-BackfireExplosionPreview.py
```
