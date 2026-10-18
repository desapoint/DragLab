# Starting variety and onboarding - 2026-10-18

Requested checkpoint: expand distinct models and starter choices, add independent used-component wear, increase initial funds, restore opening first-win bonuses, increase seeded used-parts selection, and introduce preset/market onboarding.

## Content and starter checkpoint

25 distinct model families / 52 authored configurations, at least two trims per model. Existing IDs and assets are retained. New models vary displacement, torque curve, drive layout, mass, weight distribution, body type, gearing and budget. They currently reuse the existing vehicle artwork families; these are new mechanical models, not 18 newly illustrated bodies. See roster.csv.

A new save receives three distinct models from a 15-model low-budget pool, selected from its persisted initial seed. Reloading/browsing preserves the shortlist, exact part identities, condition, mileage and price. Existing starter selections and owned cars are not rerolled. Initial cash is $6,500, with most options leaving maintenance/upgrade funds; the serviced Sable costs nearly the entire budget. No career outcome is forced.

Generated used condition is no longer clustered near an overall average: component selection mixes older, intermediate and recently replaced hardware. Repeated components can differ individually. Every generated starter contains a component/member near 30-40% and another above 90%. Starter engines are inspected/serviceable and the replacement clutch is disclosed; worn chassis, cooling/exhaust/brake/tire equipment and individual engine members remain visible. The near-budget serviced option retains a stronger engine. Regular used cars retain concealed-history/fault and variable-fluid rules and can be worse purchases. New generation rules never normalize or heal existing assets.

New-player defaults are Shift myself / Simulation / Normal market. The two presets apply first, then the independent market choice. Existing players and progression resets retain saved preferences. Early first-clear bonuses are $150 / $225 / $300 / $400; repeat rewards and later-event bonuses retain their prior pacing. Captured race terms and previously recorded wins are preserved.

Validation and remaining UI/market work are recorded as each checkpoint passes. User visual/phone acceptance is separate from automated checks.

Content checkpoint checks: Unity compilation and 360 roster/32-seed/actual-race assertions pass, including all 15 starters, wide per-component/member wear, full-wear clean passes and a real opening win for the near-budget Sable. The deterministic clean-driver fixture uses automatic shift, traction and stall assistance with full wear/consumption/condition rules; it is an achievable benchmark, not an override of player difficulty or a guaranteed outcome. First-win settlement checks: 164 pass, including repeat-win/captured-term consistency. Existing UI tests are updated in the following onboarding checkpoint. No packaged build claimed at this intermediate commit.
