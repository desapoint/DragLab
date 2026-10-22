# Corrected-stat audio comparison — 2026-10-21–22

User requests a fresh fixed-tuning comparison after `14d2703`, keeping changes
inside the workbench/diagnostic tools. Work remains on main in the existing
checkout. No production sound generator, audio controller, simulation source,
car/part asset, settings, save or Windows/Android player changes in this step.

## Result

Open the local `index.html`. Tempest turbo and Bison carbureted V8 each have:

- Original Unity throttle/shift inputs followed by full Unity simulation through
  the same original Free sound preset, separated by one second of silence.
- All 14 cumulative fixed-tuning diagnostic passes and adjacent A/Bs, plus
  shorter neutral-rev/limiter and acceleration/shift chapter sequences.
- RPM, effective pedal, published load and full-cut graphs on the original clock.
- Separate production-DSP source/propagated engine/tire/combined references.

Fresh Unity 6000.6.4f1 captures use `draglab-variety-v22`, source `14d2703` plus
Editor-only capture tooling. Reconstruct current authored hardware, require it
to match the controlled configurations, replay every original command and
recompute expected outcomes before exporting. Do not substitute a version field
or reuse old numerical frames as the fresh capture. Keep the old output folders.

**All eight native WAVs are byte-identical to their retained pre-stat reference;
all 28 freshly rendered fixed-tuning diagnostic WAVs also match exactly.**
Hardware, tuning, acoustic seeds, driver ticks, every physical sound frame and
all chemical/release events match. Tiny JSON/runtime differences in reported
elapsed decimals are below 3e-15 s and do not change any PCM sample. These healthy
stock-derived fixtures do not exercise the NA capacity ceiling, non-default
fitted flywheel values, lower core RPM rating or block damage cases fixed in the
preceding checkpoint. The prior focused stat regressions remain the evidence
for those fixes; they were not rerun as part of this audio-only diagnosis.

See `baseline-contracts-matrix.csv`, `reference/rebased-inputs-matrix.csv`,
`measurement-matrix.csv` and `signal-source-matrix.csv`. SHA256s and settings
hashes are retained; generated WAV/HTML/JSON/PNG remain local ignored artifacts.

## What explains the remaining difference

The preferred input-only mode has different driving dynamics, not just a
different way of reading identical engine conditions. It replays throttle and
shift commands into the browser Race model. It deliberately omits Unity's
neutral/countdown states and automatic launch behaviour. For example, Unity's
effective gas rises during the countdown while the original driver command is
still zero. The browser stays in Race drive and uses its own gear ratios,
coupling, RPM response and limiter. This comparison must not be mistaken for two
equivalent physical performances.

Over the common 5–9 s neutral-rev/limiter window:

| Configuration/source | Mean RPM | Published load | Full-cut frames |
| --- | ---: | ---: | ---: |
| Tempest, Unity | 6,907 | 0.08 | 71.7% |
| Tempest, browser inputs/Race | 5,923 | 0.98 | 24.0% |
| Bison, Unity | 6,227 | 0.08 | 87.9% |
| Bison, browser inputs/Race | 5,496 | 0.98 | 25.0% |

During acceleration the browser spends about 41% of frames at a full cut for
both cars; Unity is 0% for Tempest and 4.1% for Bison. The apparent reversal
between revving and acceleration is visible in the graphs. Source driving,
RPM and cut cadence are therefore substantial confounders when judging timbre.
The high Unity neutral limiter duty is a concrete lead to inspect against the
intended limiter design; these measurements alone do not prove the simulation
is incorrect or justify altering car statistics to improve sound.

At fixed acoustic settings the cumulative signal/layer changes remain:

| Added input/layer | Tempest RMS change | Bison RMS change |
| --- | ---: | ---: |
| Aggregate combustion fraction | -2.53 dB | -1.07 dB |
| Actual cuts/two-step/shift target | +3.72 dB | +3.02 dB |
| Intake/induction/ancillary layers | +2.83 dB | +0.49 dB |
| Actual backfires/BOV | +2.13 dB | +0.02 dB |
| Outdoor processing | -4.25 dB | -3.11 dB |

The spectral power above 2 kHz drops from 22.93% to 14.67% for Tempest when
restoring outdoor processing; Bison drops from 0.309% to 0.146%. This supports
outdoor filtering as a muffling lead, not a verdict that propagation should be
removed. These are ratios of spectral power, not loudness percentages.

Prioritize listening to passes 06–08 (combustion/cut interaction), 08–09
(induction layering), and 11–12 (propagation), while comparing the RPM/phase
graphs. Published load is a transmitted-torque ratio and `.08 × gas` in neutral;
the present adapter treats effective gas as throttle plate. Those mappings may
need a clearer physical excitation contract. Keep authentic RPM, cylinder
combustion, cuts and exhaust chemistry; do not copy browser driving physics or
weaken car performance merely to reproduce a preferred timbre. No candidate
mapping or gain/filter adjustment is implemented here.

These are cumulative diagnostic hybrids; incompatible intermediate cut/RPM
states can sound unnatural. RMS/waveform/spectral differences are objective
leads, not realism scores. Per-cylinder contributions can supersede aggregate
fraction in the pulse path; do not assume double attenuation without inspection.

## Tooling correction and checks actually run

Unity JsonUtility normalizes null stage markers to empty strings. The old
exporter overwrote the current phase with those empty markers. Retain the last
non-empty label and assert every new frame has one. This fixes phase reporting
and meter labels, with all physical data and native PCM proved unchanged. The
new comparison excludes only the corrected textual stage label when checking
old/new physical frames.

- 39 Unity capture/replay gates pass: current-hardware reconstruction, unchanged
  commands/source files, completed races, lossless frame/event serialization,
  exact native dry/propagated/tire replay, event funding/overflow and global RNG.
- 21 independent reference artifact/replay gates pass for these two cars.
- 14 mode/driver gates pass; Free remains sample-identical to the supplied DSP,
  and inputs mode excludes captured state/events while preserving shift timing.
- 12 diagnostic gates cover all 28 fresh finite/unclipped renditions, frozen
  tuning, exact cached source, deterministic rerenders, substitution boundaries,
  source immutability and rejected mismatches.
- 52 fresh-vs-retained-baseline contracts pass, including eight native WAVs,
  28 diagnostic WAVs, corrected phase labels and exact natural-level A/B assembly.
- Ten reference UI/AudioWorklet and eight diagnostic UI contracts pass. Four
  complete Free sequences retain sample equality with the pre-diagnostic DSP;
  all four supplied drivetrain/limiter/shift/tire regression suites pass.
- Python/JavaScript/PowerShell syntax and scoped diff checks pass. Generated
  listening links and WAV headers are checked before delivery.

The shell's old Node 16 lacks the global Blob required by the diagnostic UI
checker; the final UI run uses bundled Node 24.19.0. Run embedded-module sync
first: checkout line-ending differences otherwise cause a textual embedding
assertion despite identical normalized JavaScript. An initial missing Editor
namespace import was corrected before the final successful capture. The final
phase-label correction required recapturing references; its equality gates prove
the already rerendered diagnostic PCM is still applicable.

No subjective improvement is claimed, and no phone profiling, full 47-case live
audition or new player build was performed. Listen in the new review page, then
choose the next mapping/processing investigation explicitly. Do not advance
the architecture plan or integrate sound changes at this checkpoint.

Reproduction is in [the diagnostic guide](../../docs/AUDIO_SIGNAL_DIAGNOSTICS.md).
Pre-existing market/save/audition report changes, Tests.ps1, attachment and
unrelated regression/report folders are preserved and excluded from this commit.
