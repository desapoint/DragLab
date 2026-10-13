# All-mode AWD and independent tire geometry

2026-10-13. One user-requested simulation checkpoint; existing tuning/bench controls and explicit Apply remain. No later UI phase advances.

All six AWD modes resolve actual per-corner radius, inertia, rim/tire mass, contact slip and loaded traction. Fixed/open center splits remain mechanical torque allocation; equal torque can produce different force because force = torque / rolling radius. Active bias now combines its preference/bounds/response with usable axle torque capacity and slip feedback. Capacity uses actual static mass/placement, acceleration weight transfer, compound/condition, pressure/width, surface, differential behavior and each axle radius at a comparable torque port. On-demand preload also reacts to secondary capacity; reactive engagement uses linear tire/road slip, not raw unequal tire RPM.

On-demand now feeds the primary input and transmits bounded torque through a real secondary clutch impulse. Smaller/faster secondary tires can return torque rather than receive fictitious propulsion. Engaged on-demand coupling retains its last commanded torque reference through an open gearbox clutch, so a shift preserves wheel-to-wheel momentum transfer. Center LSD and on-demand transfer dissipate actual axle-speed-difference work: the ledger closes, transfer-case heat uses that work, and branch stress reads actual shaft torque. Failure isolation and the open-center free-shaft path remain. The corrected mass model sums each individual rim/tire once; trace wheel energy uses each wheel's own inertia and angular speed. Existing Coupling graph/CSV channels add AWD slip work kJ and AWD heat kW, within the shared twelve-channel group limit.

Warnings distinguish locked scrub and engaged slipping-coupling heat. Free/open centers and the independently biased center abstraction are not falsely labelled locked. A specialty locked gear ratio is only an option for that hardware, not the only AWD configuration capable of calculating unequal tire sizes.

## Verification

Passed on this checkpoint: focused all-mode geometry/energy/repeatability matrix **487/0**; contact-core matrix **595**; AWD contact **9**; component/branch failures **93**; driving/contact/serialized replay **455**; mass/load **212**; axle/wheel integration **421/0**; shared bench/rules/abuse **309** (71 + 23 + 215); integrated thirteen-car tools/scenarios/telemetry/replay **2,462**; category comparisons **1,256/0**. Windows `BuildPlayerOnly` succeeded. Counts overlap and are not an aggregate coverage score.

The final 487/455/2,462/1,256 checks and Windows build include the latched on-demand shift handover. Historical native menu 1,524/0 and Build/cart 586/0 remain retained baselines, not rerun claims; no layout/theme/control behavior changed. Physical bench review of the two reused Coupling trace channels remains a user review item.

The focused matrix covers all six modes, equal and unequal/reversed tire sizes, 30/70 and 70/30 starting weight, split grip, both on-demand primary axles, preemptive/reactive/disengaged engagement, actual load-driven allocation, power/shift/lift/brake runs, energy closure, repeatability, coupling heat and truthful warnings. The failure-stress fixture now supplies measured shaft torque rather than only an unconnected engine torque field; production stress consumes real transmitted branch torque. No player assets are used.

## Limits and review

This remains a straight-line four-wheel model, with no lateral steering, carcass deflection or detailed internal planetary gear/clutch pack geometry. Active bias is the existing independently controlled center abstraction; on-demand is a primary/secondary clutch topology. Normal secondary command remains bounded by authored settings, while isolated-failure fallback retains its existing gradual rerouting semantics. Front bias shows requested allocation; actual signed shaft torque/power and axle force can reveal backdrive or circulating torque.

Physics identity is now `draglab-variety-v18` and contact core v5. Old recorded inputs are retained but require rerecording; ownership, progression, parts, wear, settings and tunes keep their existing save schema. Contact work includes coupling loss; do not add the separate coupling ledger to total loss again. Wear/thermal difficulty remains authoritative.

Review in the physical bench: use different front/rear sizes with each center mode; compare actual wheel RPM versus linear road slip, axle loads/forces, shaft Nm/kW, AWD heat and transfer-case temperature. For on-demand compare primary selection, zero/preemptive preload and low/high secondary capacity. With active bias compare front/rear mass placement and controller bounds. With fixed/open hardware compare equal torque against unequal-radius force. Preserve the current build via copied bench state.

Physical reference: BorgWarner describes [primary/secondary multi-disc AWD coupling](https://www.borgwarner.com/docs/default-source/default-document-library/next-generation-awd-actuators.pdf) and [load-aware preemptive locking torque](https://www.borgwarner.com/newsroom/press-releases/2019/08/22/brushless-dc-motor-brings-amazing-performance-to-borgwarner-s-new-awd-coupling). Game calibrations remain fictional; these sources support the architecture, not manufacturer-specific numbers.
