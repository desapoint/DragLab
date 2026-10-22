# Additional career races — 2026-10-22

Requested content checkpoint: increase the playable career without changing the
approved driving/audio model or replacing the existing ladder. Author 28 added
events, four per F–S tier, for 49 total. Definitions remain editable Unity assets
under `Game/Assets/DragLab/Resources/Data/Events/40-side-*.asset`.

| Tier | New events | Repeat net before running costs |
| --- | --- | --- |
| F | Dockside warm-up; Backroad hatch meet; Small-capacity cup; Rear-drive rookie run | $125–225 |
| E | Short-shift showdown; All-motor club; Boost street challenge; Touring long pull | $450–550 |
| D | Featherweight sprint; Roadster night; Turbo chase; Workhorse long haul | $600–700 |
| C | Four-wheel launch club; Front-row shootout; Six-pot night meet; After-hours muscle | $750–875 |
| B | Boost launch battle; Street pressure; Big coupe grudge; Heavy express | $950–1,125 |
| A | Flash launch trophy; Track pressure; Blower battle; Grand touring challenge | $1,200–1,400 |
| S | Launch masters; Blower crown; Heavy hitters; Midnight long run | $1,600–1,950 |

Every tier contains one eighth-mile sprint, two quarter-mile races and one
half-mile run. Opponents use the existing roster, including light hatches,
roadsters, NA sixes, turbo coupes/saloons, carbureted muscle, supercharged V8s,
diesel pickups and heavy AWD vehicles. Their real fitted parts, condition and
driver habits feed the same simulation as player cars. No scripted target time,
rubber-banding or forced player win is introduced.

Restrictions use the existing entry system: FWD/RWD/AWD, NA/turbo, displacement,
dry mass and HP ceilings. They are intentionally different across events; no
single starter must fit every challenge. Title themes do not add hidden rules.
The Race page displays authoritative eligibility and already supports tier and
event/rival/vehicle search, so the pack adds content without a new interface.

F additions unlock after First pass or Club run, at REP 2/4. Later additions
open after the previous tier's main rival at the existing tier REP thresholds
(20/28/36/44/52/60, with two events per tier opening two REP later). No added
event becomes a prerequisite for the original main ladder.

Repeat income remains below each later tier's main qualification/rival rewards.
Every added race awards one REP and its modest cash bonus only on the first win;
repeat wins pay the repeat prize. Paid consolation refunds at most half entry,
so deliberate paid losses cannot earn net money. Opening main-event rewards and
the agreed four-to-six repeat wins for an approximately $600 upgrade remain.
Fuel, wear, service costs, genuine wins/losses and normal seeded market refreshes
remain authoritative.

Stable new IDs are additive. Existing earned cash, wins, unlocks and captured
entry terms remain; no save reset or schema/physics migration is needed. New
events are loaded from authored assets rather than generated during save load.

The explicit Editor action **DragLab / Prepare Additional Career Races** creates
missing assets only. It preserves existing GUIDs and manual edits on rerun and
asserts the original content is unchanged. **Verify Additional Career Races**
uses real fixed-step duels and checks eligibility, repeatable/private seeded
variation and payout semantics. `CareerRacePack.PrepareAndVerify` is the batch
entry point. Rival speed/driver calibration remains authored content, not an
audio or simulation workaround.

See [checkpoint results and review](../reports/career-races-20261022/CHANGES.md)
and its race matrix for measured rival totals. Distances must be compared
separately; an eighth-mile result is not a quarter-mile rank. Automated clean
same-hardware reference wins demonstrate a viable race, not guaranteed wins for
starter cars, poor shifts, worn builds or arbitrary difficulty settings.
