# 4D.10 — Compare with the selected car (2026-10-11)

The user requested car comparison in the dealership and for owned cars other than the selected instance. Add one shared inline comparison workflow on both details pages, without selecting, purchasing or modifying a car.

## Delivered

- Compare sits beside Buy / Use this car. Hide it on the selected owned instance. Disable it with an honest tooltip if either engine/build inspection is unavailable; retained engine failure/wear can still be inspected when its assembly exists.
- A concise name line establishes selected → viewed direction. Basic HP, torque, weight, condition and class/PP show both figures; numeric improvement/drawback colors follow the existing conventions. Unknown ratings remain pending/unavailable instead of becoming zero.
- The existing dyno overlays actual selected and viewed builds, with distinct labeled HP/torque curves, common axes, peak legends and pointer samples. Comparison does not substitute a no-wear baseline. Toggling off restores normal Stock or Current/No wear curves.
- Reuse all eight collapsible stat groups and their nested gears, exhaust sections, installed components, individual elements, saved tuning and warnings. Existing current-to-proposed rows supply units, values, ranges and better/worse/tradeoff delta bars. Info shows each side's explanation. Comparison rows wrap long pairs within the shared theme.
- Reuse the owned inspection cache by profile identity/revision/car and the existing stock estimates. Performance obtains cached copied-build estimates for both sides on demand. Its asynchronous refresh preserves both snapshots and the union of warnings. Browsing, hovering, disclosure and repeated comparison never save, select, spend, heal, refill or run a race.
- Add the same Stats button/full list to stock details. Stats opens when enabling comparison and can be independently hidden; disclosures are retained locally. F8 and Settings Cancel preserve comparison; Back closes it. Normal details/maintenance/purchase guards remain.
- Update rating/PP/stock estimate labels in place when background jobs finish. A focused run exposed the owned rating refresh replacing an open comparison panel and leaving its detached Performance polling pending; fixed that behavior. The corresponding stock completion also updates in place. Scheduled UI callbacks never drive the authoritative player car.

## Checks and limits

- Final Unity 6000.6.4f1 Windows development build passed; player/supporting files updated.
- Final focused native dealership/comparison probe: **107 checks / 0 runtime errors**, PASS ([evidence](18-dealership-focused-verification.txt)). This covers both actual Compare buttons, a worn selected engine versus a stock pickup, a different worn owned build versus a freshly selected model, exact summary/dyno/stat correspondence, both hover samples, copied driving estimates, full-group availability, info, toggle/cache behavior, selected-instance hiding, F8/Settings return and byte-exact asset/save preservation. It also retains dealership pricing/locks/quote/rollback/multiple-copy gates.
- Final full native regression: **631 checks / 0 runtime errors**, PASS ([evidence](18-native-verification.txt)). Retains Settings drafts/units, owned inspection/stat groups, naming/selection/maintenance, query filters and dealership transactions. The expanded developer-only input probe has a bounded six-minute watchdog; race/bench limits are unchanged.
- UTF-8/LF, new Unity GUID uniqueness and diff whitespace checks pass. Final build log contains no compiler or USS errors. Build-only font serialization whitespace normalized without content changes.
- Reviewed actual dealership and owned comparison captures at 1280×720. Focused compact/1080p/4K render-target grid gates pass; human comparison readability/scroll review at those display sizes remains open. Raw media, fixtures, logs and build remain local/ignored.
- First focused launch lost foreground before UI checks; retried without relaxing the input probe's own-window guard. One intermediate compile needed the existing Mechanics namespace import. Neither is reported as a passing gate.
- Existing full domain physics/economy audits remain historical baselines; no simulation rules or economy changed.

## Review

Open a dealer model and click Compare. Review selected → viewed summary, both dyno curves and hover figures; scroll through Stats and Performance. Repeat on a non-selected owned car, including a worn build, and open component/gear groups. Compare again should restore the normal curves. F8 and Settings Cancel should return to the comparison; Back should return to the grid with its query retained. Inspect your normal window and 1080p/4K scaling. This requested checkpoint is ready for user review; paint, drawer and other Garage steps remain gated.
