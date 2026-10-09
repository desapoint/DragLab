# Dyno concert

Short, monophonic, handwritten transcriptions of public-domain classical themes, performed by the Juniper engine's procedural combustion/exhaust voice:

- `ode-to-joy.wav` — Beethoven, Ode to Joy.
- `fur-elise.wav` — Beethoven, Für Elise.
- `mountain-king.wav` — Grieg, In the Hall of the Mountain King.

These contain engine sound driven by the same 120 Hz note/RPM/throttle/load sequence as the bench Easter egg. Octave transposition adapts the score to cylinder count and safe RPM range. The audition uses 44.1 kHz mono PCM with 120 Hz publication; live playback publishes through the scene's render/DSP cycle, so its PCM is not expected to be sample-identical to this offline rendering. Each offline repeat is sample-identical. There are no prerecorded music clips or separate music oscillators.

The button is **Dyno concert / random classic** in EngineTest, beside **Show car / wheels**. Playback stops and restores the original copied bench configuration automatically; W/Space also hands control back to driving. Pause freezes musical time.
