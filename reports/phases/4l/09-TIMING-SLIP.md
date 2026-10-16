# 4L.9 — paper timing slip and trackside finish board (2026-10-16)

Status: technical checkpoint complete; visual acceptance pending. This implements the user's requested paper/board/conditional stopping group, not further instruments or another interface phase.

## Presentation and behavior

- A fixed off-white paper slip sits on the right, with grain, restrained perforated edges, readable ink and the shared font. Existing grey/red action surfaces, hover/pressed behavior and themed scrolling remain. Run details are collapsible; no menu navigation is added over the track. Native result framing places the car on the left. Window defaults and driving-camera scale are preserved.
- The slip displays actual outcome, each lane's ET, reaction and finish speed, recorded splits, settlement notice, previous-pass comparison and analysis. Existing telemetry export, failure-review, Garage and Race again actions retain their authoritative services. Results wait for both lanes to resolve before announcing an outcome. F8 retains the full legacy results view; pause still contains its limited shared dialogs.
- The billboard is scene geometry behind the track, 152 m beyond the finish. Its posts meet the projected ground. World travel moves it offscreen naturally; it is never pinned to the HUD. Two independent eight-segment readouts (seven bars and decimal) show each lane's ET/RT at three decimal places. Each lane remains unlit until its own finish beam, then holds that beam's recorded times. Either lane may finish first. A DNF does not fabricate a posted time, and posting never waits for the player, rival or arrival at the board.
- Board geometry is cached until the session/finish state changes. Paper content is built per result; ordinary display updates do not calculate vehicle ratings or save a profile. Rendering uses the established URP vertex material and vector geometry; no new shader, pipeline, audio algorithm, database schema or debug-bench UI is introduced.

## Conditional shutdown stop

The existing automatic shutdown controller now aims for 160 m beyond the finish, eight metres past the billboard. Required deceleration follows remaining distance and actual speed, with existing brake-capacity and wheel-lock feedback. Pressure still eases in and is capped at **60%**, with a light hold near the stop. The authoritative contact simulation handles actual deceleration, heat, wear and resources. There is no distance clamp, teleport, fake brake capacity or forced stop. Manual shutdown braking stays manual.

Light stock examples stop around 161 m; faster examples naturally overshoot. The stock supercharged V8 reaches about 184 km/h and stops around 274 m in the matrix. Its billboard can leave view, while the fixed slip and car remain accessible. This is race post-finish assistance policy, not a second physics model or a change to the physical bench's recorded pedal integrator. Frozen elapsed/trap values remain untouched.

## Checks and iteration

- Windows `BuildPlayerOnly` passed; the tested executable is in `Game/Builds/Windows`.
- [Domain matrix](09-domain.txt): **157 checks**, 13 car configurations in both finish orders, including independent posting, blank pending lane, held times, physical stopping and bounded pressure. This gate precedes view-only formatting/layout fixes; shutdown policy has not changed since it passed.
- [Safety matrix](09-safety.txt): **78 checks** with real short passes, manual partial/coasting input, actual failed brake components, continued motion/no engine failure and unchanged beam time. Fixtures are isolated simulation copies.
- [Final native Windows input suite](09-native.txt): **163 checks / 0 runtime errors**. Covers existing gauges/shift hardware/held input/pause, one lane posting before the other, result timing, partial braking/stop composition, F8, both timing columns, compact/1080p bounds, true 3840×2160 paper-panel scaling, telemetry equality, mouse Garage/Race again, unchanged practice cash, blank retry boards and durable exit/reload. Setup checks overlap other suites; counts are not independent gameplay cases.
- Focused `ResultsOnly` also passes **8 checks** on a copy of the failed disposable fixture. The original retry failure was a pre-LateUpdate visibility assertion: diagnostics showed the new race already in Countdown, while the old slip had not yet completed its UI refresh. Waiting for the rendered frame fixes the test; no retry transaction or save behavior was changed to hide it.
- The first independent-finish input fixture gave the fresh rival too little launch delay relative to the worn player car. Its timing separation was made explicit in verification only. Production opponent reactions/strategy are unchanged; the separate domain matrix tests both finish orders.
- Rendered review caught and fixed right-column clipping. Rows now reserve/shrink their real columns without default Label margins. Inactive/active board digits retain fixed decimal positions and support reaction times above nine seconds. No runtime stylesheet warnings/errors were reported. New asset metadata and UTF-8/whitespace checks pass.

Final captures remain local in `reports/player-fixtures/race-slip-4l09-complete-20261016-120131/`; the normal screenshot gallery is refreshed by the existing tooling, with alternate resolutions grouped separately. The 4K paper capture is a UI render target; world views were reviewed in the real window sizes. The accepted render-pipeline comparison baseline remains historical evidence and was not rebaselined to conceal this intentional UI addition. Full economy/mechanical matrices and the deferred debug-bench input issue were not rerun or declared resolved.

The temporary Editor's generated font/asset whitespace and derived URP runtime-stripping cache are restored separately from authored settings after it closes. Pre-existing user test/report changes are preserved.

## Review checkpoint

Review paper proportions, board digits/placement, both timing columns, car visibility and the achievable assisted stop. A faster car or poor braking should pass the board naturally. Confirm that either lane can post first and the other remains blank until its finish. This checkpoint stops here for user review; later gauges, staging presentation and bench redesign remain separate.

Reproduce with `tools/Test-NativeMenu.ps1 -RaceHudOnly -FixtureName <fresh-name>`. `-ResultsOnly` is the narrower export/retry/reset probe; setup check counts vary with a fresh versus copied fixture. Editor matrices are `DragLab.Editor.FinishPresentationVerification.Run()` and `RunSafety()`.
