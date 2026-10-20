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

The retained shop responsiveness gate reached 61 checks without runtime errors,
then failed its combined-cart preview assertion on this baseline. Investigate that
failure before treating the older shop checkpoint as complete. It is not caused
by the requested acoustic changes, which have not started at this build gate.
