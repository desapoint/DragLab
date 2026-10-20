# Engine acoustic refinement checkpoint

## Starting point and build gate

The user requested updating the existing checkout to main, preserving newer work,
and building both platforms before changing audio. This checkout fast-forwarded
to `6a19b79`; no F.7 branch work was merged. The three existing modified verification
reports and pre-existing untracked files were preserved. No managed worktree is used.

Baseline Windows development build succeeded. Actual Windows playback passed
19 checks with zero runtime errors (fixture `audio-main-baseline-20261020`).
Android ARM64 IL2CPP development build also succeeded. There is no connected Android
device, so packaging is separate from device performance and listening acceptance.

## Requested acoustic scope

- Retain independent bank signals through collector, acoustic response and
  installed exhaust filters. Mono must fold down safely, inline engines remain
  centered, and stereo must not advance combustion twice.
- Derive pulse excitation/width from smoothed actual combustion load and turbo
  pressure. Do not use throttle alone as a substitute for cylinder pressure.
- Make hot-side attenuation respond continuously to the existing turbo pressure
  and flow. Do not introduce a competing spool/boost model or simplify physics.
- Allow compressor airflow hiss to be suppressed independently of the whistle,
  turbine response and pressure-release events. Reuse the shared Sound slider
  with draft/Apply/Cancel semantics and durable separate preferences.
- Provide reusable engine acoustic presets and optional authored overrides for
  runner lengths, header/collector propagation, resonances and pulse envelope.
  Capture immutable values before playback, precompute shared pulse tables,
  and retain zero managed allocation in warmed callbacks.

Wear, cylinder contributions, ignition/fuel cuts, exhaust hardware, induction
topologies, BOV choices, superchargers, held throttle and post-finish audio remain
authoritative. New voice tuning must not change simulation, rewards or saves.

## Review and validation plan

Use production-code checks across authored engines and sample rates, distinct
bank/mono tests, deterministic block-size invariance, load/boost/hiss isolation,
fault/cut preservation and callback memory/time measurements. Verify real
AudioSource playback, Settings draft/commit/recovery, then rebuild both platforms.
Desktop callback timings are not a physical Android performance claim.

User review remains required for listening, stereo balance and Android behavior.

## Implemented behavior and checks

Independent bank collectors/resonances and installed exhaust filters now reach
stereo PCM. Inline voices remain centered; mono folds the final pair down. One
firing scheduler still applies actual cylinder contributions and cuts. Automatic
engine presets plus authored runner, propagation, resonance and envelope overrides
are captured before playback. A bounded rate/envelope table cache shares kernels.

Actual load shapes the pressure envelope, while pressure and flow interpolate
hot-side damping without changing `InductionDynamics`. The first calibration made
neutral revs too quiet; the final calibration retains a tested V8 neutral/loaded
RMS ratio of **0.938** at 4,500 RPM. Existing wear/cuts/exhaust/pop/induction gates
remain in force. The old test requiring duplicated mono channels was updated for
independent bounded channels; missing-cylinder audibility is tested by normalized
waveform change rather than assuming comb-filtered RMS always decreases.

**Compressor airflow** is a shared 0–100% Sound slider with numeric entry,
confirmed Apply, Cancel/Revert, reload and recovery. It affects airflow noise only;
whistle, turbine response, BOV and blower layers remain. Existing/pause preference
commits preserve it; old files without the field default to 100%.

- Unity: **78** refinement, **116** pressure, **19** transition, **276** audio,
  **64** exhaust-pop and **346** induction checks pass (**899** total).
- Production-code standalone matrix: **382** checks pass across 52 authored cars,
  288 cylinder/layout/induction combinations, multiple rates, private-seed and
  block-size determinism, bounded output and zero warmed callback allocations.
- Editor worst voice: 16-cylinder twincharged / 12,000 RPM / 512 stereo frames,
  **0.7611 ms p95**, **0.8146 ms max**, versus **10.6667 ms** signal duration;
  zero managed bytes allocated. Desktop measurements are not phone guarantees.
- Real visible Windows AudioSource playback: **20 / 0 runtime errors**, including
  actual stereo callbacks, master scaling, mute, pause/resume and three engines.
  Observed peak callback **2.8596 ms** during real rendering.
- Real Settings/onboarding flow: **169 / 0 runtime errors**, including the new
  shared slider, draft isolation, confirmation, cancel, durable reload, write
  failure/undo recovery, reset retention, compact and 4K layouts. Sound screenshot
  inspected: same sliders, label alignment, number fields and spacing.
- Final unchanged race-results probe on the saved fixture: **15 / 0 runtime
  errors**, including post-finish DSP, injected save-error visibility, recovery,
  authoritative total times, telemetry, retry and no duplicate payout. Its first
  fresh run during concurrent Android compilation failed the save-notice assertion
  after 43 checks. A diagnostic run and the final original probe passed; no
  production results/settlement code or final probe expectations were changed.
  The first timing-sensitive failure is retained as a verification limitation,
  not counted as a passing run or asserted to be an acoustic rendering defect.

An initial hidden-window playback run stalled before completing startup; it was
stopped and is not counted as passing. The visible player completed normally.
The final preparation-only change also moves initial installed-exhaust/pop
coefficients out of the live callback. The 382-case matrix passes again, and all
three review engines retain **sample-exact stereo PCM** across that relocation.

Both final development packages include the preparation change:

- Windows: `Game/Builds/Windows/DragLab.exe` and its adjacent data folder.
  Managed game assembly SHA-256
  `062988a96330b5d187552f06a0133ffbf653fc0ebe882afed5ef526f6b9d86ce`.
- Android: `Game/Builds/Android/DragLab.apk`, ARM64 IL2CPP, v2 debug signature
  verified and `zipalign -c -P 16 4` passed. SHA-256
  `3018cf261e669d965a951dbcf00bf3560166ff3df90a3fbe2f31c9a816e1f5c6`.

Raw logs, PCM, comparison WAVs and screenshots remain local. A three-engine
before/after listening page is `reports/audio-refinements-20261020/index.html`.
Review on a phone: idle/rev/load transitions, limiter, lift/BOV, 0/100% airflow,
headphone bank balance, post-finish idle, and sustained thermal/audio performance.
No physical Android device is connected; device acceptance remains pending.

The retained shop responsiveness gate reached 61 checks without runtime errors,
then failed its combined-cart preview assertion on this baseline. Investigate that
failure before treating the older shop checkpoint as complete. It is not caused
by the requested acoustic changes, which have not started at this build gate.

### Shop continuation resolved before audio

Reusing the original failing saved fixture reproduced `Duplicate new offer`.
Restored complete-set rows can contain an empty role string rather than null;
native/legacy staging now matches complete sets by product/action, retaining exact
roles for individual units. This prevents a duplicate row from invalidating the
whole cart preview. The packaged regression explicitly exercises that restoration
case, then all 38 checks on the original saved fixture pass with zero runtime
errors. A separate fresh diagnostic fixture passed 74 checks before the fix but
could not establish correctness for restored carts; the saved-fixture regression
does. Price ordering, tab reuse, stat sorting, actual durable purchase and reload
are included. Windows rebuild succeeded. This narrow fix changes no bench UI.

Baseline Android APK passed v2 signature verification and 16 KB zip alignment;
SHA-256 `322e731175b6462513c9de7f2caba286581dd209aa88d81a9ac4bf4c7f05fea3`.
Retained locally as `Game/Builds/Android/DragLab-before-acoustic-refinement.apk`.
