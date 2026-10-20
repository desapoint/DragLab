# Bison lift/backfire identification excerpts — 2026-10-20

User requested an isolated repeating Unity segment, then prioritized playable audio files for identification. This checkpoint supplies the files first; it does not change production synthesis or start AP-00. The user subsequently confirmed the middle ding; [preview variants](../backfire-pop-variants-20261020/CHANGES.md) are the next listening checkpoint, with final sound selection still pending.

The existing Unity `RaceAudioAuditionVerification.Run` renderer regenerated cases 29 and 30 through the unchanged production DSP and their real simulation inputs (`--draglab-audio-case 29-,30- --draglab-audio-wav`). Both complete renders passed their audio/simulation assertions. These are deterministic synthesized excerpts, not a recording of the earlier desktop session.

Crop: **17.900–20.100 seconds**. The actual lift/backfire is at approximately 18.817 seconds; gas reapplication/backfire is at 19.475 seconds. All engine/exhaust layers remain together so the user can identify whether this interval contains the ding before any layer is blamed or adjusted.

- `29-street-lift-repeat.wav`: street absorptive exhaust, four repetitions.
- `30-chambered-lift-repeat.wav`: sport chambered exhaust, four repetitions.

Each file is 10 seconds of 48 kHz stereo PCM16, with 0.4-second gaps. Only a 5 ms fade at the excerpt edges prevents crop clicks, far from the suspected events. No normalization, EQ, pitch, pop, mechanical or resonance adjustment is applied. Samples between the edge fades are verified against the full source byte-for-byte; all four copies match and the gaps are silent. See verification.txt.

`tools/Extract-RaceAudioSegment.py` reproduces extraction using Python's standard library. Example from the repository root:

```powershell
python tools/Extract-RaceAudioSegment.py reports/audio-ding-excerpts-20261020/29-exhaust-1.wav reports/audio-ding-excerpts-20261020/29-street-lift-repeat.wav --start 17.9 --end 20.1 --repeats 4
```

The complete 47-case manifest, coverage and verification metadata, selected input fixtures and any pre-existing selected WAVs were backed up and restored exactly around the partial render. No running player was stopped. Raw audio remains local and unversioned; the extraction tool and concise evidence are versioned. The requested in-game loop has not been implemented by this file-first checkpoint.
