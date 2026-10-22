# More playable career races — 2026-10-22

Add **28 races**, four per F–S tier, for **49 total**. The existing 21 event
assets and their metadata match `de697d5`; old wins, prerequisites, rewards and
captured race entries are preserved. This is one requested content checkpoint,
not an advance to another interface or architecture phase.

## Playable content

- Each tier gets an eighth-mile sprint, two quarter-mile events and a half-mile
  run. The opponents cover 23 existing vehicle/trim configurations and 28 named
  drivers, with NA/turbo/supercharged petrol and diesel hardware represented.
- F events open after First pass / Club run at REP 2/4. Later events unlock
  alongside the ladder, after the preceding tier's main rival, with REP gates.
  None is required to continue the original ladder.
- Existing restrictions provide FWD/RWD/AWD, NA/turbo, small-engine,
  lightweight/heavy and HP-limited challenges. The existing Race page includes
  all events, its tier/search controls and the actual entry distance/requirements.
- The new events use real fitted hardware, fixed starting condition and seeded
  human driving variation. Two attempt seeds produce different measured rival
  totals in all 28 cases while preserving starting components. No target-time
  opponent, forced win or catch-up performance scaling is introduced.
- Repeat take-home ranges from $125–225 in F to $1,600–1,950 in S, before fuel
  and maintenance. First wins give a modest bonus and one REP. Repeats give no
  bonus/REP; paid losses refund at most half entry. Side-event prizes remain
  below their tier's main qualifier prize. The original opening rewards and
  four-to-six repeat wins for a useful approximately $600 upgrade are retained.

[Content guide and authoring](../../docs/CAREER_RACE_PACK.md).
`events-matrix.csv` lists the 28 new definitions; `race-matrix.csv` contains the
56 real measured duels, including reaction plus elapsed totals. Compare times
within the same distance. A clean same-hardware reference winning a race is a
viability check, not a guarantee for arbitrary builds, reaction or shifting.

## Implementation and preservation

New assets have stable `side-*` IDs and Unity GUIDs. An explicit, idempotent
Editor authoring action creates missing assets and refuses to alter original
content. Normal loading does not generate or modify events. No save reset,
schema migration, catalogue revision or physics-version change is needed.

Update verification's old exact-21 assertions to expect the complete 49-event
pack. Reward verification distinguishes the optional branch from the original
strictly increasing main reward curve and uses the same reproducible new-event
defaults. Regression output can be redirected to this checkpoint, preserving
historical reports and unrelated changes.

The legacy career test had a stale assertion that omitted the already approved
first-win cash bonus. Its first pass failed there after the entry/build checks;
correct only the expected cash formula, then rerun the complete suite. The
game's payout code and all original financial assets remain unchanged.

## Checks actually run

- **702** new-pack checks pass. All 28 events have valid captured hardware,
  coherent HP ceilings, reachable/acyclic prerequisites, enforced REP and
  advertised hardware restrictions. All 56 duels finish, and each event has a
  winning clean same-hardware reference. Checks cover seeded behavior and
  unchanged initial hardware, first/repeat/duplicate settlement, bounded reward
  pacing and unchanged global RNG/authored assets.
- **1,088** expanded-career regression checks pass across the current full car
  roster and 49 events, including the original 14 ladder events with both
  diagnostic viable builds, original resource/reward rollback, first/repeat
  settlement, invalid outcome handling and empty new-game progression.
- **332** reward-contract checks pass over all 49 events, including the old
  main curve, early upgrade budget, captured terms surviving definition changes
  and reload, duplicate protection and first-only REP/bonus.
- **70 / 0 runtime errors** in the final Windows native Race selection test.
  It checks all 49 cards, four additions per tier, searching Dockside warm-up,
  actual eligibility, selected-state styling, entry confirmation/rejection,
  fee capture, SQLite reload, free/practice behavior, F8 and compact/default/4K
  layouts. Existing onboarding and starter flow also pass in the isolated player.
- Independent static checks retain all 21 original assets/metadata, confirm
  distinct new IDs/GUIDs and verify two different measured attempt totals per
  event. Repeating Prepare preserves existing assets and manual edits.

These are isolated test profiles, not player saves. The new-event build budgets
are diagnostic; no full funded 49-event ownership route was simulated. The
previous funded main route is historical evidence, not rerun acceptance here.

## Builds and review

Windows development build succeeded at `Game/Builds/Windows/DragLab.exe`; the
native checks above run against that build. Android ARM64 IL2CPP development
build succeeded at `Game/Builds/Android/DragLab.apk` (87,542,512 bytes). APK v2
signature, ZIP integrity, four-byte file / 16 KB native-library alignment and
ARM64 library checks pass. `build-manifest.txt` records the player/resource/APK
hashes; `android-verification.txt` retains packaging evidence. A physical Android
playthrough was not performed in this checkpoint.

Screenshots are under `reports/interface-screenshots` and the isolated fixture
`reports/player-fixtures/career-races-20261022/`; resolution variants use the
existing resolution subfolder organization. The 720p career screenshot was
visually inspected: the new early events appear in the established two-column
list with the existing styling and scroll behavior. This does not claim user
acceptance of pacing or a physical phone playthrough.

Review in Race → Career: win First pass, then search **Dockside** or **Backroad**;
use tier filters for later branches. Existing progressed saves receive the new
events without a reset. Check the advertised restrictions and distance in the
entry dialog, then try multiple attempts to assess driver variation and pacing.

Audio generators/settings, physics, opponents in the original events, debug
benches and other interfaces are unchanged. Pre-existing supply/save/audition
reports, `Tests.ps1`, attachment and unrelated test/report folders are retained
outside the commit.

## Reproduce

Run the following Editor methods through Unity CLI `run Game -- ...`, using
Unity 6000.6.4f1 and the usual `-nographics -buildTarget Win64 -executeMethod`:

1. `DragLab.Editor.CareerRacePack.PrepareAndVerify`
2. `DragLab.Editor.CareerExpansionVerification.RunStandalone`
3. `DragLab.Editor.EconomyBalanceVerification.VerifyRaceRewards`
4. `DragLab.Editor.PlayerBuild.BuildPlayerOnly`

For steps 2/3, append `--draglab-career-report` and a new output directory
(`reports/career-races-20261022/expanded` / `rewards`). The first action creates
missing event assets; subsequent verification is read-only for authored content.
Run `tools/Test-NativeMenu.ps1 -FixtureName career-races-20261022 -RaceSelectionOnly`
for actual player input and screenshots. Android uses `-buildTarget Android`
and `DragLab.Editor.PlayerBuild.BuildAndroid`.
