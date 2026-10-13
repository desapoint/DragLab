# Staggered wheels and mixed tires

2026-10-13. Final integration checkpoint for the first requested feature. Technical validation and user acceptance remain separate; the user's subsequent wheel-selection/AWD/comparison requests follow as separate commits.

Previously, contact speed/slip/heat/wear were per corner, but radius, width, pressure and compound were shared. Tuning → Wheels now uses **Both / Front / Rear** for independent outer radius, width and pressure. Selection is local view state; edits remain drafts until Apply. Right-drag fine tuning, middle reset, numeric entry, units, Revert, F8 and legacy controls retain their existing behavior. The bench exposes the same optional axle fields; zero inherits the common value in older saves.

Individual tire purchases can select FL/FR/RL/RR or Front/Rear pairs while keeping complete-set buying the default. Compatible mixed compounds retain actual definitions, identities, wear and outgoing originals. Whole-car blueprints retain the corner specifications; restored units are owned or explicitly priced, never free. Cart/dyno previews remain copies. Quick Buy same preserves mixed specifications at the actual paid cost, retaining the worn assembly.

Authoritative race/physical-bench physics uses individual geometry/mass/inertia, axle loads/ride-height/center of gravity, pressure/width friction, driven gearing, brake torque, heat and wear. Lighter non-driven fronts reduce physical and rotational load; larger driven rears change gearing and are not always beneficial. Actual corner compounds control grip/thermal/life ratings. Locked AWD constrains angular shaft speed, so unequal rolling sizes can scrub. Shared stats expose Front/Rear geometry and actual corner specifications; sprites show different diameter and stance. Normalization derives missing condition records from retained assembly condition without granting hardware or healing wear.

## Checks

| Gate | Result |
| --- | --- |
| Thirteen-car geometry/mixed-purchase/save/blueprint/maintenance and unequal-inertia energy matrix | 305 / 0 failures |
| Contact/energy regression on first geometry checkpoint | 595 passed |
| Mass/load/ride-height regression | 212 passed |
| Shared driving/contact/shifts/braking/neutral/checkpoint/replay | 455 passed |
| Cart transactions | 102 passed |
| Final shop cache/single-tire regression | 130 passed |
| Final saved-setup previews | 208 passed |
| Final paid operations/recovery | 351 passed |
| Final async race-save/checkpoint/reset recovery | 417 passed |
| Shared plain-language stat help | 157 passed |
| Final development bench | 309 passed (71 shared + 23 rules + 215 abuse) |
| Packaged native Tuning / input / reload / compact and 4K layouts | 772 / 0 runtime errors; 36 fresh images |
| Windows development player | Passed final rebuild |

Counts overlap. [Feature matrix](geometry-verification.txt). Existing phase archives retain their prior baseline; new raw outputs stay locally under ignored player-fixture folders. Earlier native attempts exposed verifier assumptions about pressure quantization, stale rebuilt stats, scroll position and saved F8 routes, which were corrected. The broad native run then found an absent-tire label regression; the production comparison now correctly says Not installed. The legacy-settings bench fixture also now explicitly creates its owned-car baseline rather than assuming a fresh game already owns one.

## Review

- Set Front narrow/small and Rear wide/large, switch back, fine-adjust/reset, Apply/reload and Revert. Inspect axle stats and the layered preview.
- Purchase a rear pair of drag tires, retain the road fronts, inspect exact cart positions and retained originals, then check Buy same.
- Compare RWD launches and AWD size mismatch warnings with actual mass/inertia/grip readings.
- Review normal images in `reports/interface-screenshots`; other dimensions belong in `resolution-tests`. Feature captures: `tuning-staggered-front.png`, `tuning-staggered-rear.png`, `tuning-axle-stats.png`, `purchase-mixed-rear-tires.png`.

Outer tire radius remains inches and width the existing relative multiplier; separate rim diameter/aspect ratio and absolute section width are not fabricated. At this checkpoint rim construction remains a common kit; the user's next request adds individual wheels. A pure side view shows diameter/stance, not tread width. This is a calibrated straight-line model, not lateral steering, a full carcass model or shaft torsional wind-up. User acceptance is pending.
