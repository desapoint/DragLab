# Tempest processing comparisons

48 kHz / 24-bit stereo PCM. One real 26-launch-hard-ignition replay, player only.
No game sound calibration/settings/save changes. All passes reuse the same captured
physics, events, seeds and PCM. No opponent, UI, music or invented supercharger.

Start with quick/10-backfire-launch-and-lift.wav and quick/22-full-vehicle-launch-and-lift.wav.
Each quick segment is the same six seconds at race time 13.5–19.5 s.

Every full WAV repeats the entire 50.158333333333331 s scenario in the order below,
with exactly one second of digital silence between passes. The original replay plus a two-second end hold is followed by a labelled
14-second test-only low-grip Tempest segment: traction assistance OFF, street
tires, local grip multipliers 0.20; contact audio comes from actual physics.
The first race and its engine configuration remain unchanged. CUE files and embedded cue/LIST labels mark passes.
Per-file *-passes.csv gives exact times and gains; race-timeline.csv gives events
relative to each full segment. The engine-internals file has its own eight passes.

fixed-level/ uses a constant gain within each file, lowered only if needed to avoid
clipping a raw peak. Consult CSV for that gain. Do not compare volume across files
without accounting for the gain. level-matched/ matches whole-segment RMS within
each file, then applies one common peak guard; this is for timbre comparisons.
It can make normally subtle intake/airflow layers much louder than gameplay.
No per-event normalization, extra compressor, fades or dither is added.

Raw means the defined generator output with physical event timing/envelopes and
its intrinsic source synthesis/anti-aliasing intact, before added routing/mix/outdoor
processing. It is not unfiltered white noise or a physics-free oscillator. The
engine-internals WAV additionally reveals the engine generator's internal filters.
Pass 09 removes fitted engine exhaust coloration and backfire source hardware
colour; it does not bypass the engine core's intrinsic pressure model.

Processing is applied at the actual bus boundaries: engine DC/limiter on the engine
sum, tires through their own limiter, engine outdoor at .55 m source height and
tires at .08 m, followed by summation. Reflections include ground/barrier/stands
and diffuse paths. Listener follows the car at 12 m lateral separation with fixed
longitudinal offset, avoiding camera motion as an uncontrolled pitch change.
The WAV includes native DSP, not Windows/phone speaker/driver processing.
The full final pass of 22-full-vehicle uses the actual captured production engine
and tire PCM before the same native outdoor renderer. Other combinations are
rebuilt from captured generators, preserving nonlinear bus placement. Solos
therefore do not simply sum after limiting to reproduce a limited full mixture.
A processing step that does not apply to a source correctly leaves it unchanged.

## Pass order

| Pass | Full-file start | Quick-file start | Processing |
|---|---|---|---|
| 0 | 00:00.000 | 00:00.000 | 00 Raw generators |
| 1 | 00:51.158 | 00:07.000 | 01 Add hardware voicing and fitted exhaust |
| 2 | 01:42.317 | 00:14.000 | 02 Add source power attenuation |
| 3 | 02:33.475 | 00:21.000 | 03 Add source travel and shell radiation |
| 4 | 03:24.633 | 00:28.000 | 04 Add final engine DC filter |
| 5 | 04:15.792 | 00:35.000 | 05 Add engine and tire soft limiters |
| 6 | 05:06.950 | 00:42.000 | 06 Add direct distance delay and stereo |
| 7 | 05:58.108 | 00:49.000 | 07 Add atmospheric absorption |
| 8 | 06:49.267 | 00:56.000 | 08 FULL production processing |
| 9 | 07:40.425 | 01:03.000 | 09 Full WITHOUT hardware coloration |
| 10 | 08:31.583 | 01:10.000 | 10 Full WITHOUT source power loss |
| 11 | 09:22.742 | 01:17.000 | 11 Full WITHOUT source travel |
| 12 | 10:13.900 | 01:24.000 | 12 Full WITHOUT shell radiation |
| 13 | 11:05.058 | 01:31.000 | 13 Full WITHOUT final engine DC filter |
| 14 | 11:56.217 | 01:38.000 | 14 Full WITHOUT soft limiters |
| 15 | 12:47.375 | 01:45.000 | 15 Full WITHOUT outdoor delays and stereo |
| 16 | 13:38.533 | 01:52.000 | 16 Full WITHOUT atmospheric absorption |
| 17 | 14:29.692 | 01:59.000 | 17 Full WITHOUT reflected paths |
| 18 | 15:20.850 | 02:06.000 | 18 Raw plus final DC only |
| 19 | 16:12.008 | 02:13.000 | 19 Raw plus soft limiters only |
| 20 | 17:03.167 | 02:20.000 | 20 Raw plus outdoor only |
| 21 | 17:54.325 | 02:27.000 | 21 Full WITHOUT DC and soft limiters |

## Files

- 01-combustion-exhaust.wav: [fixed levels](fixed-level/01-combustion-exhaust.wav) / [level matched](level-matched/01-combustion-exhaust.wav) — engine
- 02-block-and-manifold.wav: [fixed levels](fixed-level/02-block-and-manifold.wav) / [level matched](level-matched/02-block-and-manifold.wav) — mechanical
- 03-cut-and-control-colour.wav: [fixed levels](fixed-level/03-cut-and-control-colour.wav) / [level matched](level-matched/03-cut-and-control-colour.wav) — controls
- 04-mechanical-clatter.wav: [fixed levels](fixed-level/04-mechanical-clatter.wav) / [level matched](level-matched/04-mechanical-clatter.wav) — clatter
- 05-intake-rush.wav: [fixed levels](fixed-level/05-intake-rush.wav) / [level matched](level-matched/05-intake-rush.wav) — intake
- 06-turbo-whistle.wav: [fixed levels](fixed-level/06-turbo-whistle.wav) / [level matched](level-matched/06-turbo-whistle.wav) — turbo-whistle
- 07-turbo-airflow.wav: [fixed levels](fixed-level/07-turbo-airflow.wav) / [level matched](level-matched/07-turbo-airflow.wav) — turbo-airflow
- 08-turbine-interaction.wav: [fixed levels](fixed-level/08-turbine-interaction.wav) / [level matched](level-matched/08-turbine-interaction.wav) — turbo-turbine
- 09-blow-off-valve.wav: [fixed levels](fixed-level/09-blow-off-valve.wav) / [level matched](level-matched/09-blow-off-valve.wav) — bov
- 10-backfire.wav: [fixed levels](fixed-level/10-backfire.wav) / [level matched](level-matched/10-backfire.wav) — after
- 11-pressure-driven-shell.wav: [fixed levels](fixed-level/11-pressure-driven-shell.wav) / [level matched](level-matched/11-pressure-driven-shell.wav) — after-wall
- 12-tire-fl.wav: [fixed levels](fixed-level/12-tire-fl.wav) / [level matched](level-matched/12-tire-fl.wav) — tire-fl
- 13-tire-fr.wav: [fixed levels](fixed-level/13-tire-fr.wav) / [level matched](level-matched/13-tire-fr.wav) — tire-fr
- 14-tire-rl.wav: [fixed levels](fixed-level/14-tire-rl.wav) / [level matched](level-matched/14-tire-rl.wav) — tire-rl
- 15-tire-rr.wav: [fixed levels](fixed-level/15-tire-rr.wav) / [level matched](level-matched/15-tire-rr.wav) — tire-rr
- 16-all-tires.wav: [fixed levels](fixed-level/16-all-tires.wav) / [level matched](level-matched/16-all-tires.wav) — tire-fl + tire-fr + tire-rl + tire-rr
- 17-engine-and-intake.wav: [fixed levels](fixed-level/17-engine-and-intake.wav) / [level matched](level-matched/17-engine-and-intake.wav) — engine + controls + mechanical + clatter + intake
- 18-engine-and-turbo.wav: [fixed levels](fixed-level/18-engine-and-turbo.wav) / [level matched](level-matched/18-engine-and-turbo.wav) — engine + controls + mechanical + clatter + intake + turbo-whistle + turbo-airflow + turbo-turbine
- 19-engine-and-backfires.wav: [fixed levels](fixed-level/19-engine-and-backfires.wav) / [level matched](level-matched/19-engine-and-backfires.wav) — engine + controls + mechanical + clatter + intake + after
- 20-induction-bov-backfires.wav: [fixed levels](fixed-level/20-induction-bov-backfires.wav) / [level matched](level-matched/20-induction-bov-backfires.wav) — turbo-whistle + turbo-airflow + turbo-turbine + bov + after
- 21-full-engine.wav: [fixed levels](fixed-level/21-full-engine.wav) / [level matched](level-matched/21-full-engine.wav) — engine + controls + mechanical + clatter + intake + turbo-whistle + turbo-airflow + turbo-turbine + bov + after
- 22-full-vehicle.wav: [fixed levels](fixed-level/22-full-vehicle.wav) / [level matched](level-matched/22-full-vehicle.wav) — engine + controls + mechanical + clatter + intake + turbo-whistle + turbo-airflow + turbo-turbine + bov + after + tire-fl + tire-fr + tire-rl + tire-rr
- 23-backfires-and-bov.wav: [fixed levels](fixed-level/23-backfires-and-bov.wav) / [level matched](level-matched/23-backfires-and-bov.wav) — after + bov
- 24-engine-internal-filters.wav: [fixed levels](fixed-level/24-engine-internal-filters.wav) / [level matched](level-matched/24-engine-internal-filters.wav) — eight engine-source boundaries.

All source captures, timestamps, stage filters and packaging are generated by
DragLab.Editor.TempestProcessingAudition.Run. Listening acceptance remains pending.

Export master/category gains are fixed at 1, with compressor-noise control at 1. Stored user preferences are not loaded. These constant gain choices do not add another tone filter. The diagnostic files do not capture OS, speaker or driver processing. Chapter/session boundaries are edits with hard digital-silence gaps; judge source texture inside each repeated section.

Quick excerpts retain the whole-run matching gains. Use the full level-matched recordings for strict equal-RMS comparisons over the whole scenario.
