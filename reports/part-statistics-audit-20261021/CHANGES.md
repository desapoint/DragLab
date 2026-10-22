# Part-statistics audit — 2026-10-21

Read-only validation requested after the audio signal comparison. This checkpoint
adds an Editor audit and evidence, **not fixes or calibration changes**. No runtime
source, authored parts/cars, user saves, physical bench, game audio or packaged
Windows/Android players were changed. Existing unrelated report edits,
Tests.ps1 and attachment/test folders remain untouched.
The runtime/content baseline is `8db01d3` on main; only this read-only Editor
audit, its independent verifier and concise evidence were added for the check.

## Coverage and result

The actual Unity catalogue/resolver was evaluated: **52 configurations, 9,294
part definitions, 49 categories, 26,102 physically fitting single-slot replacement
combinations**. The audit completed **146,350 baseline/control checks with zero
invariant failures**, plus the explicit sensitivity findings below. Those findings
are not counted as proof that everything is correct.

- All authored definitions pass the current override validator and expose finite
  relevant stats. IDs are unique; all stock builds are drivable and valid.
- Resolved stock torque curves match their authored configurations on the same
  25-point RPM grid. There were no stock operating warnings.
- All 26,094 accepted single-slot combinations have finite nonnegative sampled
  output and positive mass/inertia. Parts outside engine-output categories do not
  change the steady engine torque curve. Strength upgrades do not create free HP.
- Eight physically fitting single-slot combinations are intentionally rejected:
  four EFI conversions missing matching Controls and four cam/follower mismatches
  on the carbureted Timber/Bison configurations. Fit does not promise a complete,
  functioning combination; these are not numerical or market bugs.
- 37,440 real shared-car simulation/audio-publication ticks check finite RPM,
  bounded load/combustion and valid active airflow at idle, throttle, drive, shifts
  and lift. This six-second probe does **not** claim a limiter/audibility or full
  durability test. It observed no limiter cuts in these trajectories.
- The catalogue and Unity gameplay RNG are unchanged after the audit. No player
  profile/store is loaded. Authoring probes use detached catalogue instances.
- The existing PartEffectsVerification suite passes **26,720 checks**, with its
  complete log/catalogue dump isolated from historical reports. Nine independent
  Python cross-checks validate catalogue/matrix coverage, preserved stock output,
  finite accepted results and the measured capacity/flywheel findings.

Stock, category, sensitivity and grouped-finding matrices are versioned. Raw
catalogue JSON, the 26,102-row part-effects CSV, audio-signal CSV and Editor logs
remain ignored local evidence. Generic DTO defaults in the category matrix are
not all relevant/consumed stats; PartSemantics defines applicable fields.

## Confirmed existing-content inconsistency

**The naturally aspirated torque helper can bypass fuel/ignition power capacity.**
`InductionDynamics.SteadyTorque` has an NA fast path that returns the multiplied
authored curve without `Capacity`. The running induction path and full steady
output apply the cap. Using actual fitting catalogue parts (highest-flow head,
lowest-capacity fitting fuel system), **16 configurations** reproduce disagreement.
An independent check confirms the running torque path agrees with capped output.

Examples, at the same sampled RPM:

| Actual build | Torque helper | Capacity-limited engine |
| --- | ---: | ---: |
| Aster Sport, race head + Finch OEM fuel system | 221.96 HP | 194.44 HP |
| Sable Sport, race head + Finch OEM fuel system | 250.45 HP | 194.44 HP |

These are helper-derived estimates, not proof of extra real race output. Main car
inspection already uses `SteadyOutput`; the affected helper is consumed by safe
defaults, clutch-sizing calculations, older sound auditions and fallback torque
paths. Fix the shared helper/capacity contract before adjusting part ratings;
do not globally lower engine torque to hide this disagreement.

## Confirmed authoring/extension gaps

1. **Absolute flywheel mass and inertia are editable but ignored by runtime.**
   For all 52 configurations, an accepted +2 kg `massKg` edit changes actual car
   mass by zero; an accepted +50% `inertiaKgM2` edit leaves actual inertia unchanged.
   Runtime derives both from the vehicle flywheel and `massScale`. A positive
   control changing `massScale` changes both quantities as expected. This proves
   an authoring contract problem; it does not prove the existing catalogue's
   derived flywheel numbers are all wrong. Choose authoritative fields or make
   derived fields explicitly read-only, with legacy handling, before new content.
2. **The global overrev threshold has a floor above factory redline.** A detached
   internal rated to 85% of factory redline lowers `BuildResolution.safeRpm`, but
   `safeRpmMultiplier` is clamped to at least 1.01. All 52 probes reproduce it.
   Individual component stress still uses the real lower rating, and the live
   BuildLimits display also reads actual component/spring limits. This is a
   disagreement between protection thresholds, not complete immunity to damage.
   No current authored single-part combination triggered the lower-rating case.
3. **Cylinder blocks have no torque/RPM strength ratings.** All 104 stock/reinforced
   block definitions carry zero for those fields and are outside the three core
   internal-strength categories. Reinforced blocks do gain service-life/thermal
   tolerance through existing service hardware; they do not add a modeled block
   torque-strength ceiling. That is an incomplete capability, not a zero-Nm engine.

## Calibration limits

Current numbers are fictional game calibration. Examples of deliberately generic
authoring include a 55 kg block, 4 kg piston set and 22 kg factory exhaust across
different engine families. The baseline vehicle mass already includes factory
hardware, so these values do not simply add those masses again. Replacement and
removal deltas still make their accuracy matter. This audit establishes software
consistency, **not manufacturer/engineering validation of every strength, material,
thermal capacity, grip or life rating**. Costs are checked for valid values, not
career-economic balance. All multi-part/tuning/wear permutations are not exhausted.

There is no evidence here supporting a broad rewrite of HP/torque numbers to
improve audio. The stock baselines are consistent; the current audio load remains
a transmitted-torque-derived estimate, whose acoustic suitability is a separate
mapping issue. Correct the specific contracts above, then consider a controlled
per-category physical calibration pass independently of sound preference.

## Reproduce

With the installed Unity CLI, from the repository root:

```powershell
unity run ./Game --timeout 1200 --log-file ./reports/part-statistics-audit-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsAudit.Run
unity run ./Game --timeout 900 --log-file ./reports/part-statistics-existing-effects-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsAudit.RunExistingEffects
python ./tools/Verify-PartStatisticsAudit.py
```

The existing-effects wrapper isolates its reports under this audit rather than
overwriting historical receipts. Its outcome is recorded separately in
existing-effects-verification.txt. The audit reports observed invariant failures
and review findings separately; a process exit code alone is not a clean bill
of health. User decisions on gameplay changes remain a later checkpoint.
