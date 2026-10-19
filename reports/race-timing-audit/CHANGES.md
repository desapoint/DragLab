# Race timing and retry audit — 2026-10-18

## Finding and interface change

Elapsed time (ET) starts at each car's launch; reaction is recorded separately. `RaceSimulation` decides the winner using **reaction + ET** and interpolates the finish crossing within the physics tick. Car placement uses the two actual travelled distances. The rival receives the same fixed-step simulation as the player; there is no forced 0.3-second result margin.

The paper slip now also shows **Total · s** and the actual **Finish gap** once both lanes finish. Unfinished rival totals remain blank, and new races rebuild the slip and clear the board. The existing ET/RT billboard convention stays intact. The Run details disclosure explains the distinction.

## Independent timing gate

748 checks passed across 93 runs: three representative cars with 0.18/3.18/6.18-second reactions, plus all 21 career rivals with two captured driver seeds and two player delays. The test independently observes each crossing against the clock from green, checks RT+ET within one physics tick, compares finish order/winner, and constructs fresh repeat captures. The longest-delay representative cases also use the rival worker.

For identical controlled launch conditions, waiting three/six seconds changes total by exactly three/six seconds; ET remains unchanged. Career rival ETs ranged 13.522–19.326 seconds and total margins 0.145–7.443 seconds. Some rival ETs change with the visible gap because their authored risk/driver behavior reacts to it. Fixed hardware/wear and recorded seeds remain reproducible.

An initial timer-isolation fixture incorrectly floored the accelerator throughout neutral staging. The Bison's ET changed 14.474→14.983 seconds after a longer wait because physical launch conditions changed; its independent total still correctly lost by 3.373 seconds. The controlled fixture now holds the same automatic launch target until reaction expires. This was a test assumption, not evidence of a timer reset bug; the independent crossing assertions were retained.

No race-clock, opponent-physics, AI performance calibration or win/loss rule was changed. Without the affected car/event/save or a recording, the user's exact anecdotal race cannot be reproduced. This audit establishes the tested cases rather than claiming every possible player setup is correct.

## Related performance issue

The raster Race HUD run exposed a real 293.590 ms paid stock-refresh step; the full finish-pacing gate remains failed. It is separate from the ET calculation and was not hidden by relaxing its threshold. See [measured market diagnosis and recommended checkpoint](../market-refresh-audit/RECOMMENDATION.md). Market-generation changes were requested as a recommendation and are not implemented here.

Windows development build passed. Real-player Results checks: 52 passed, including failed-save recovery, current totals/margin, exact telemetry export, a fresh retry and cleared board lanes. Real-player early-exit checks: 59 passed, including exits/retries before the rival finish, one durable payout/checkpoint, retained dashboard and 1024/1280/1920 layouts. Both runs had zero runtime errors. Inspected the actual slip capture; the shared scroll area accommodates the additional timing rows.

Local player evidence: `reports/player-fixtures/race-timing-results-20261018-a` and `race-timing-exit-20261018-a`. These focused gates do not replace the separate failing full finish-pacing gate described above.

## Manual-launch follow-up

The user suspected ET-only wins, then accepted the reaction-inclusive total convention. Twelve additional runs exercise Assisted/Sequential and Manual/ManualClutch launches on the hatch, 5.0 V8 and supercharged V8, with 0.2/5.2-second requested delays. All 54 checks pass: the delay is present in reaction, totals match the independently observed green-to-finish clock, and every five-second late start loses against the same healthy rival. Source and existing Android/Windows runtime are unchanged by this Editor-only follow-up.

The initial manual test controller requested upshifts by engine RPM while the launch clutch was still open, causing a legitimate failed launch. Corrected the controller to require speed, a released clutch and synchronized clutch RPM before requesting the next gear. No game stall/shift behavior was weakened to make the fixture finish.
