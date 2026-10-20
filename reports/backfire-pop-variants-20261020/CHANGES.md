# Backfire pop preview variants — 2026-10-20

The user confirmed the ding in the middle of the repeated Bison excerpts and requested multiple preview alternatives, without applying a game change. This checkpoint remains a listening review; AP-00 is held.

`BisonBackfirePreview.Run` replays the exact existing case 29/30 input fixtures through the production simulation and audio controller. Its original render matches every PCM16 sample of the previously identified 17.900–20.100-second source excerpt. Two actual pop events remain at their original timings and strengths.

The comparison bed is rendered with only `exhaustPopGain` at zero on a private copied acoustic specification. The original engine, induction, mechanical and exhaust setup stays otherwise unchanged. Each proposed pressure/noise transient passes through the same exhaust transfer and DC rejection before recombination with the bed at the existing output saturation stage. The preview contains no pitched burst oscillator, whole-clip EQ, pitch change or normalization.

| Variant | Direction |
| --- | --- |
| A — dry crack | Sharp front, short broadband tail |
| B — deep pop | More pressure/body and a slightly longer low turbulent tail |
| C — raspy bark | Rougher broadband tail with small irregular turbulent reflections |
| No-pop control | Original engine bed with the pop layer absent; identifies whether another layer still contributes to the ding |
| Original | Unchanged reference, independently checked against the identified source |

Both street absorptive (`29-exhaust-1`) and sport chambered (`30-exhaust-2`) versions are provided. Each single excerpt is 2.2 seconds; `-repeat.wav` repeats it four times with 0.4-second gaps, totalling 10 seconds. The existing extraction tool applies only 5 ms crop-edge fades, far from the two events.

Checks: both Unity source/reference comparisons pass exactly; each crop has two real pop triggers; pre-event engine samples match with/without the pop; all source clips are finite/bounded stereo PCM16. Independent Python checks confirm unchanged pre-event samples and distinct alternatives for both exhausts. Peak across all ten source clips is approximately 0.3375, below clipping. No assertion substitutes for the user's acoustic preference.

Reproduce after the original fixtures/WAVs from the preceding identification checkpoint exist:

```powershell
unity run Game --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.BisonBackfirePreview.Run
python tools/Extract-RaceAudioSegment.py reports/backfire-pop-variants-20261020/30-exhaust-2-A-dry-crack.wav reports/backfire-pop-variants-20261020/30-exhaust-2-A-dry-crack-repeat.wav --start 0 --end 2.2 --repeats 4
```

Prototype synthesis is Editor-only and is not a proposed final realtime implementation. No production DSP, vehicle asset, game build, preference or save was changed; no running player was stopped. Any selected direction still requires proper realtime integration and its audio/performance gates. WAVs and raw levels remain local/unversioned; the renderer, Unity metadata and concise evidence are versioned.
