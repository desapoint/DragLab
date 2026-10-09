# 3F — Garage business and idle integration (2026-10-09)

Four garage levels now expand income, offline caps and simultaneous job bays without restricting an existing car collection. The saved accrual contract retains rate, cap, epoch and fractional cash. Expansion and authoring-rate changes settle at the captured old rate before adopting new rates. Legacy higher-level saves retain their previous eight-hour cap for the first claim. Clock rollback cannot reset that epoch or release a job early.

Four authored engine-off display offers use installed HP/drive, reputation and garage-level eligibility. They reserve exact owned instances, engines and installed parts, preserving fuel, fluids, tune and wear snapshots. Rewards/deadlines remain captured across offline time and policy edits. Claim/cancel releases atomically; failed writes leave the unpaid reservation intact. Completed unclaimed jobs stay reserved. Cancellation pays nothing. Another owned instance remains usable.

Reservations are enforced at shared race/bench eligibility, tuning Apply, hardware/service, engine removal and disposal. The verification found and fixed missing reservation marking on nested chassis quotes. Car/engine/chassis quotations are now inspection-only while assigned. Post-release salvage leaves no dangling job references. Recovery work remains available without a car.

Validation:

- **159 targeted checks**, **32 car/offer matrix rows** across eight models, four-level caps, fractions, old-rate upgrades/repricing, legacy cap migration, backwards clocks/reload, overflow, duplicate/early claims, failed writes, all reservations, cancellation, concurrent duplicate-model cars and post-release salvage.
- Paid-operation regression **226 checks**, saved-setup/assembly regression **179 checks**; overlapping ownership checks are not summed as unique cases.
- Scene integration and Windows development build passed.
- **11 actual Windows semantic checks across two separate player processes**, preserving an assigned car/deadline offline, waiting for the real 30-second deadline, claiming/releasing, canceling and claiming a simulated offline accrual epoch. The epoch backdate exists only in the isolated verification fixture; the production clock was not changed. **Zero runtime/GUI/audio errors.**

Controls and authoring: [garage business](../../../docs/GARAGE_BUSINESS.md). Source and full Windows checkpoint ZIPs stay local and immutable. Generated asset/meta whitespace is normalized before the Git checkpoint without altering values or GUIDs. Phase 3G supplies economy scenario balance/recovery; 3H remains the integrated release audit. Used dealer rotation, driving customer jobs, fifth-level workshop and storage limits remain later content.
