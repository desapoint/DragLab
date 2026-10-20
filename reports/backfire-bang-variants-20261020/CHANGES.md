# Louder, longer Bison backfire previews — 2026-10-20

The user requested a louder bang with a longer tail after reviewing the restrained A/B/C previews. This checkpoint renders D/E/F only for listening review. No production sound, runtime asset, player preference, save or game build changes. The architecture plan remains held pending the sound decision.

`BisonBackfirePreview.RunBang` reuses the original simulation fixtures and exact two physical pop triggers from cases 29 and 30, cropped at 17.900–20.100 seconds. Original and no-pop controls remain sample-identical to the prior render. The previous `Run` entry point and A/B/C synthesis remain available unchanged.

| Variant | Direction | Turbulent-tail decay constant |
| --- | --- | --- |
| D — sharp bang | Strong initial crack and a short, audible exhaust tail | 42 ms |
| E — deep bang | Wider pressure impulse with deeper body and longer tail | 75 ms |
| F — long bark | Rougher, more sustained exhaust tail | 105 ms |

Each burst combines a fast broadband blast, a pressure impulse/rarefaction and decaying filtered turbulence. Two feed-forward reflections at 13 and 31 ms use the actual burst samples, with no pitched oscillator or recirculating feedback. The 650 ms synthesis window is a maximum lifetime, not a sustained explosion. The burst passes through the same exhaust transfer and DC rejection, and recombines at the existing output saturation stage. Only the proposed burst is louder; there is no whole-clip gain, pitch change or normalization.

Unity batch rendering completed successfully and verified original PCM16 fidelity, two physical events, finite/bounded audio and the unchanged pre-pop engine bed for both exhausts. `tools/Verify-BackfireBangPreview.py` independently verifies controls and pre-event identity, six distinct-length alternatives, unclipped peaks, increased pop-layer RMS and increasingly strong 50–250 ms tails. Mixed peaks range from 0.569 to 0.668. Pop-layer differences measure 18.1–20.3 dB above prior B; these are comparative preview levels, not calibrated real-world sound pressure or final production gain.

Six four-repeat listening files are 10-second, 48 kHz stereo PCM16 with 0.4-second gaps and the existing 5 ms outer-edge fades. The single clips and controls remain available locally. WAVs/logs remain ignored; renderer, checker and concise evidence are tracked. Listening approval and Android realtime integration/performance are not claimed.

Reproduce after the original case 29/30 fixtures and identified source WAVs exist:

```powershell
unity run Game --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.BisonBackfirePreview.RunBang
python tools/Verify-BackfireBangPreview.py
```

The CLI update check could not reach its CDN; rendering used the installed CLI 1.0.0-beta.13 and Unity 6000.6.4f1. No running Editor was found and no live audition player was interrupted.
