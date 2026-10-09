# Slice 1 playtest route

Launch `Game/Builds/Windows/DragLab.exe`, or open `Game` in Unity and play `Assets/DragLab/Scenes/FirstSlice.unity`. Use **DragLab → Vehicle Workbench** in Unity for developer experiments. The interface is a temporary systems overlay.

## Try the complete loop

1. In Driving, choose **Drive automatically**. In Career, select **Normal AI → First pass**. Leave gas alone during staging to use the tuned launch RPM. A clean automatic starter hatch should run about 16.312 s over the quarter mile under the current model.
2. Read reaction, ET, splits and trap at the finish. Press **R** to retry or **G** to return to the garage. Rewards settle once; repeat wins pay cash but no new reputation.
3. Buy an engine upgrade and a weight upgrade. Change final drive toward 3.40, save the tune and try **Club run**. More power, grip and gearing changes should have observable consequences rather than just changing a rating.
4. Press **P** in Garage to practice the selected build against its stock version. Practice has no entry fee or payout and saves valid records. Change one adjustment and practice again; results show ET/trap changes from the previous same-distance pass. Stored tune slots restore adjustments without removing installed upgrades.
5. Close and reopen the game. Check cash, parts, tune, driving settings and last pass. Existing user saves are retained; automated verification uses separate workspace fixtures.

## Try driving yourself

Choose **Shift myself**. Hold **W or Space** during the lights: the car stays in **N**, the staging brakes hold it still and the engine revs freely. Press **E** after green to engage first, keep gas held and press **E** again near the displayed shift target. The combined shift-up button does the same; Enter remains an alternate launch key. Early engagement red-lights. CVT uses E to select Drive, then varies ratio continuously. **Q** shifts down when safe. Releasing gas lowers throttle; shifting briefly interrupts shaft torque while the held gas input stays active.

Choose **Full manual** for clutch control. Hold **Left Shift**, set staging RPM with gas, press E after green to select first and release Shift to transmit drive. Keep gas held while holding Shift, pressing E, then releasing Shift for each upshift. The saved digital clutch-release time controls engagement ramp. Traction and rev-match assists remain individually configurable. Holding gas at redline should cycle limiter cut/recovery.

**S** brakes. Automatic shutdown braking is enabled by default; disable it in Driving to test braking yourself after finish. ET and trap stay fixed while the car slows. **Escape** pauses, and **G** returns to Garage while paused, abandoning an unfinished pass without a reward.

## Compare tunes in Unity

Buy an engine stage and compare single/twin turbo, single/twin supercharger and turbo-plus-supercharger kits in Garage. Keep the same tires/gearing to isolate differences, and inspect actual boost/engine output in the HUD and telemetry CSV. Try a larger turbo for later response, a lower boost target for grip, or a faster blower pulley for more boost and drive load. The workbench shows settled dyno curves; export response/race comparisons to see lag. Buy exhaust stages to compare tones, and listen for turbo valve releases on shifts and charged lifts. See `INDUCTION.md` and `ENGINE_AUDIO.md` for specs and WAV export.

Select a vehicle in the Vehicle Workbench, set its experimental upgrades/tune and choose traction control. Click **Compare current build**. The thirteen trials use a captured baseline, five launch RPMs and seven final drives with identical assists. Export the comparison CSV; changing the sliders afterwards does not change the captured results. These experiments do not change your player save or factory definition. Inspector edits change factory data and should be followed by validation.

## Verified starter-car route

The automated route uses automatic driving, Normal AI and earned money only. Launch RPM is 3,200 for the first three wins and 4,100 for the final. Stage order is **engine / tires / weight / gearbox**. Repeat the last beaten event to afford later parts; full purchase/cash history is in `reports/career-path.txt`. The garage uses sliders, so nearby manual settings may produce slightly different times.

| First win | Upgrade stages | Final drive | Modeled player ET | Modeled rival ET |
| --- | --- | --- | --- | --- |
| First pass | 0 / 0 / 0 / 0 | 4.10 | 16.312 s | 16.312 s |
| Club run | 1 / 0 / 1 / 0 | 3.40 | 15.572 s | 15.553 s |
| Coupe rival | 2 / 3 / 2 / 2 | 3.40 | 14.298 s | 14.277 s |
| Local final | 3 / 3 / 3 / 3 | 4.80 | 13.717 s | 13.713 s |

Winner timing includes reaction: automatic player reaction is 0.18 s, Normal AI is 0.22 s, both quantized to fixed ticks. The first event can therefore be won with equal ET, and the final with a slightly slower ET. This route verifies feasibility, not finished economy balance.

## Human review still needed

Try the new control package, gearbox swaps, wheel-size/mass choices and cold-start setting; see `POWERTRAIN.md`. Report which car, upgrades, tune and driving preset you used, and whether launches, clutch engagement, wheelspin or shifts feel unclear or unfair. Export the actual run CSV from results when useful. Physical keyboard rollover, controller support, Android device touch and human audio quality remain acceptance work. Independent wheel-spin/contact integration, tire temperature and persistent repairs belong to later slices; current contact slip is demand-based.
