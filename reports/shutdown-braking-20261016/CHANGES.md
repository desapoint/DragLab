# Progressive shutdown braking — 2026-10-16

The user approved allowing up to 90% automatic shutdown pressure when necessary, following healthy faster cars overshooting the billboard at the old 60% cap. This changes the ceiling, not an immediate pedal command. The existing 0.65 pressure/second increase, faster pressure release, wheel-lock feedback and actual brake capacity remain. Slower cars still request only the pressure needed for the existing 160 m target; the board remains 152 m beyond the finish.

Manual input, failed-brake behavior, beam ET/trap values, lane posting, geometry and the shared physical bench integrator are unchanged. There is no teleport, forced stop or guaranteed composition beyond available grip and stopping distance. The simulation remains authoritative in memory; no storage/schema or recording version changes are needed for post-finish assistance policy.

## Verification

The shared simulation matrix runs all 13 stock cars in both finish orders. It checks physical stopping, the 90% bound, gradual pressure rise and frozen results. The safety matrix checks real short passes, unchanged manual partial/coasting input, gradual assist engagement and actual failed brakes that neither stop the car nor break the engine. The domain matrix passes **183 checks** and safety matrix **91 checks**. [Measured distances](COMPARISON.md) compare the retained historical 60% baseline with this run. Peak pressure remains bounded and its maximum per-tick rise is 0.00542 at 120 Hz. Historical 4L.9 reports remain unchanged.

The Windows player was rebuilt and the existing native race/HUD/results suite passes **163 checks / 0 runtime errors** on an isolated profile. Results, retry, export, F8, resolution bounds and durable exit/reload remain working. Build fingerprints are recorded in `build-manifest.txt`. Broader economy/mechanical matrices and the debug bench are outside this narrow policy change.

Most stock cars now stop about 161–166 m beyond the finish. The supercharged V8 improves from 274.48 m to 207.29 m and can still pass the board. The controller remains bounded by real braking/grip and does not promise a stop inside the target. The temporary Editor was closed; only generated whitespace and known derived URP runtime-stripping cache changes were restored. Pre-existing user test/report changes remain untouched.
