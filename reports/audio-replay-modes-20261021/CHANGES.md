# Three-mode acoustic comparison — 2026-10-21

User clarified that the desired third mode replays **only throttle and shift
inputs**, rather than importing recorded backfire/BOV events into browser driving.
It is labelled **Unity inputs** to describe that accurately. Free and full Unity
simulation remain available. E now upshifts; Q downshifts, with two visible buttons.

Sound tuning is independent: import retains original Free settings by default;
captured Unity hardware is explicit and reversible. The full reference adapter
can retain browser geometry, seed, gains and afterfire colour while consuming
the same captured physical frames and chemical funding. Its historical default
remains unchanged. Driver-only replay reads actual fixture commands at 120 Hz,
excludes brakes/clutch/launch and captured physics/events, and leaves browser
Race/Neutral selection explicit. It does not claim Unity simulation fidelity.

## Checks and artifacts

- 14 preview/regression gates pass. Full Tempest and V8 Free-script PCM matches
  the supplied `40d475f` model sample for sample. Both historical captured-reference
  WAVs also remain byte-identical. Preserved-tuning full replays have the same
  reaction/release schedule as the historical path.
- Exact driver shift-command consumption, excluded-input/event contamination,
  deterministic restart, malformed input rejection, 44.1 kHz clock/end behavior,
  and all four templates' downshift/busy/gear bounds/held throttle pass.
- 10 UI/actual embedded AudioWorklet contract gates pass: three-mode routing,
  locked controls, restored tuning, input-only processing, sound options, native
  WAV exclusivity and real E/Q handlers including field/replay exclusions.
- All four supplied drivetrain/limiter/shift/tire suites pass. The existing
  full five-case reference verifier also passes all 46 gates again.

`preview_replay_modes.cjs` produces Tempest and V8 Free, full-reference with Free
tuning, driver-only and captured/Free-tuning A/B WAVs. The matched A/B variant
only attenuates to the quieter RMS; ordinary exports are unnormalized. All newly
rendered PCM is finite and unclipped. `listen.html` provides playback and links
to preloaded interactive three-mode pages. Raw audio, JSON, HTML and images are
local ignored artifacts. Concise verification receipts are versioned.

No production C#, player build, game audio, simulation, assets, saves or later
architecture plan changed. No Unity rebuild was needed. Real browser/device
listening and visual acceptance remain manual; Node/worklet checks are not
listening evidence. Existing unrelated report edits, attachments, Tests.ps1,
race-exit/shop artifacts are preserved and excluded from this commit.

## User review checkpoint

Open `index.html`, select each of the three modes and compare the sound with the
original Free tuning. Try captured hardware, then switch back. In Free, hold W
while shifting with E/Q; verify the displayed gear, held pedal and lower/upper
bounds. In Unity inputs, select Race or Neutral and restart; RPM, boost and
backfires must be browser results. Compare Tempest and V8 natural-level and
matched A/B WAVs in `listen.html`. Technical checks do not imply sound approval.
