# Part statistics: physical consumers corrected — 2026-10-21

Implements the four software-contract findings in the preceding
[read-only audit](../part-statistics-audit-20261021/CHANGES.md), as requested.
Audio generators, acoustic presets, the browser workbench and packaged players
are unchanged. No player profile is opened by the focused/catalogue checks;
transaction and recovery suites use isolated fixtures.

## Contracts

- The naturally aspirated steady-torque fast path now applies the same minimum
  fuel/ignition power capacity as steady output and running torque. The existing
  torque curve and below-capacity output are preserved. This fixes estimates and
  clutch/default-sizing consumers without lowering actual race output to hide
  the old helper discrepancy.
- Fitted flywheel `massKg` and `inertiaKgM2` are independently authoritative.
  Mass changes translating load and its placement once; inertia changes engine
  angular momentum, stored energy, synchronizing torque and actual rev response.
  Inspection reads the same helpers. Zero absolute fields retain the scale-only
  legacy fallback; missing hardware contributes zero. Unresolved development
  specifications retain their existing tuning-scale behavior. A per-specification
  nonserialized lookup avoids a repeated fitted-array scan in hot paths.
  Ambiguous edits to legacy `massScale` on absolute-rated flywheels are rejected
  with an instruction to edit the physical fields, rather than silently ignored.
  Part summaries show physical mass/inertia instead of the obsolete scale row.
- The resolved global overrev threshold uses the weakest positive core RPM
  rating, including ratings below factory redline. Validation accepts a finite
  positive ratio rather than imposing the former 1.01 floor. Individual stress
  and condition/cam-dependent spring limits remain their existing separate
  physical mechanisms; a mechanical rating is not a new electronic limiter.
- All 104 factory/reinforced blocks now carry torque/RPM strength. Blocks enter
  resolver, inspection and browser strength comparisons. Service wear adds
  squared torque overload and overspeed exposure using the same condition-based
  torque-strength function as other internals, without duplicating thermal wear.
  Disabling wear remains authoritative. Reinforcement adds no horsepower and
  does not repair existing condition.
  Comparison influence filters include their torque headroom, RPM and assisted
  shift-target effects, so real changes remain visible.

Block ratings are conservative **fictional game calibration**: factory blocks
retain the previous forged-core envelope (1.2 × factory redline and 2.6 × authored
peak torque), reinforced blocks provide 1.4 × and 3.5 ×. Current factory output
is not rebalanced. These are not manufacturer-validated material failure limits.
Captured older unrated blocks retain their previous no-additional-rating
behavior; a failed block still has zero usable headroom. Zero-rated pistons,
rods or crankshafts retain zero headroom; the unrated fallback applies only to
legacy blocks. Final review added three explicit zero-core guards and reran
the focused suite after tightening that distinction. The positive authored
catalogue and the other completed suites are unaffected by that narrow guard.

Content revision advances from 7 to 8; physics identity advances from v21 to v22.
Old driving-input recordings are explicitly rejected. Known v18–v21 resource
journals may still recover monotonic wear/fluids into their existing open run,
with the existing ownership/revision checks; their obsolete physics is never
replayed. Save schema and component identities are unchanged.

## Verification

Final results:

- **4,743 focused checks** across all 52 configurations: independent mass/inertia
  edits and actual rev response, lower RPM ratings for each core category,
  block torque/overspeed damage, wear-off behavior, legacy fallbacks, resource
  journal recovery, capacity limits and exact serialized replay.
- **146,542 catalogue/control checks**, 9,294 definitions / 49 categories /
  26,102 fitting replacements. Zero invariant failures. The same eight expected
  incomplete EFI/controller or cam/follower combinations remain rejected.
- **36,812 existing regression checks**: part effects 26,720; mass/load 836;
  individual components 3,532; recipe/calibration 375; race/bench parity and
  simulation rules 289; recovery 1,924; physical replay 475; async save 2,661.
- **84 focused native comparison/cache checks**, covering flywheel/block scopes
  and four representative rating platforms. Other categories retain their
  influence maps; the full catalogue audit covers their numerical invariants.
- **14 independent Python gates**, including a before/after catalogue fingerprint
  and preserved stock output/mass. All four former software-contract finding
  groups are absent. The remaining informational group documents the existing
  generic exhaust-mass calibration; it is not claimed to be manufacturer data.

Final receipts and catalogue matrices accompany this report. Full logs,
catalogue JSON, replacement CSV and save/replay fixtures remain local artifacts.
The original audit matrices are retained as the before baseline. No visual UI
layout or later architecture phase is implemented here.
The audit's default output now points to this fix checkpoint, so a normal rerun
does not overwrite the earlier before matrices. An explicit output override
remains supported.
The versioned `baseline-contracts.json` records the original catalogue hash
(excluding only the intentionally changed block fields); independent checks do
not require an ignored pre-fix catalogue dump on a fresh checkout.

The initial combined regression harness reached its 1,200-second process limit
during the per-car durable-recovery suite. Four completed suite logs were
independently counted; 38 complete car recovery blocks were preserved in
`recovery-completed-before-timeout.csv`. The remaining 14 cars run through the
same recovery method with an explicit selection predicate and a separate output
folder. The timed-out process itself is not counted as a pass. Later changes in
this split concern displayed stat scopes and test infrastructure, not physics.
Full fresh reproduction uses the longer time limit below.

One existing short driveshaft-failure fixture was stale: Fjord Sport retains
0.0002668998 health after five seconds when starting at 0.001. A controlled run
restoring legacy scale-only flywheel and unrated-block semantics gives the same
survival and wear (within 1e-7). This is not a newly broken drivetrain model.
The failure fixture now starts at 0.000001, matching the existing recovery suite;
real transmitted torque depletes it and disconnects drive while the engine
continues. Gameplay wear rates and driveshaft ratings are unchanged. Native
comparison fixtures also account for block strength when calculating the
expected minimum, instead of assuming only three internals can limit torque.

Reproduction through the installed Unity CLI:

```powershell
unity run ./Game --timeout 1200 --log-file ./reports/part-statistics-fixes-focused-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsFixVerification.Run
unity run ./Game --timeout 3600 --log-file ./reports/part-statistics-fixes-regression-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsFixVerification.RunRegressions
unity run ./Game --timeout 1200 --log-file ./reports/part-statistics-fixes-comparison-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsFixVerification.RunPresentation
unity run ./Game --timeout 1200 --log-file ./reports/part-statistics-fixes-audit-20261021.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PartStatisticsAudit.Run --draglab-stats-audit-output ./reports/part-statistics-fixes-20261021
python ./tools/Verify-PartStatisticsAudit.py ./reports/part-statistics-fixes-20261021 --expect-fixed
```

This validates software consistency and selected physical sensitivities, not
every multi-part/wear permutation or real-world calibration. No audio acceptance
or physical Android performance claim is made. Existing unrelated report edits,
Tests.ps1 and attachment/test folders are preserved and excluded from this work.

For interface review in the Editor, inspect a flywheel's physical mass/inertia
and a worn block's torque headroom in part comparisons. No control layout/theme
changed. Compilation and domain/model checks passed; no visual screenshot pass,
new packaged player or physical-device test was run for this stat checkpoint.
