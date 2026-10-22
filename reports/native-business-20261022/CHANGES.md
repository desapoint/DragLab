# Native Business / idle checkpoint — 2026-10-22

User authorizes the existing idle component in the new interface after the race checkpoint. The career expansion is already validated: 702 new-pack, 1,088 career, 332 reward and 70 native race-selection checks; Windows and Android players were built at `94025d7`. Those historical gates are retained, not rerun for this presentation change.

## Scope

- Business opens **Overview**, **Display jobs**, and **Recovery**, using the existing panels, joined choices, action surfaces, modal confirmations and thin scrollbars.
- Overview shows the captured hourly contract, offline cap, claimable income and garage expansion price/benefits. Claims retain fractional dollars; expansion settles old income before capturing new terms. Car storage remains unrestricted.
- Jobs select an actual owned instance locally without changing the globally selected driving car. Offers show eligibility, required garage/REP/power/drivetrain, reward and duration. Active contracts show the captured car/reward and real deadline, with explicit claim/cancel confirmation. Cars remain reserved after completion until released. Display work consumes no resources or wear.
- Recovery is native, including the no-car route. Existing bounded real-time work and recent committed operations remain accessible. Garage and Build links lead to existing service/sales tools.
- Timers and pending earnings update twice a second in place. Structure/eligibility rebuild only on changed profile revision, local selection, subpage or policy. Dynamic decorative surfaces are released on rebuild.
- Every confirmed action uses the existing save-before-publication transaction, captured revision and captured policy checks. Rejected or failed writes retain review/error and do not credit/release assets. Desktop F8 and clean Settings return preserve the Business subpage; fresh Business navigation resets Overview, car picker and scroll. Mobile has no legacy fallback.

## Verification and builds

- Existing roster-wide business domain suite: **863 passed**, including caps at all four levels, fractional claims, old/new rate settlement, rollback clock, exact car/engine/component reservations, forbidden race/tune/service/disposal actions, reload, failed publication, once-only payout, cancellation and independent duplicate-model assignments. [Receipt](domain/verification.txt), [car/offer matrix](domain/business-matrix.csv).
- Packaged Windows real-input suite: **83 checks, 0 runtime errors**, including the shared onboarding/starter-purchase prelude and 43 Business-specific gates. Real 30-second display and 60-second recovery deadlines run without backdating those jobs. Browsing/picker/F8/Settings leave profile JSON and saved bytes unchanged; timers retain element instances. Stale terms/revisions, failed durable write/retry, expansion, reservation, complete/cancel, no-car recovery and actual SQLite reload pass. Compact 1024×768, 1920×1080, offscreen 3840×2160 and simulated touch layout/access are covered. [Receipt](native-verification.txt).
- Fresh **Windows and Android ARM64 development builds** succeed in Unity 6000.6.4f1. APK ZIP integrity, ARM64-only native libraries, signature and 16 KB page alignment pass. [Hashes and sizes](build-manifest.txt). Executable/APK, raw captures and logs stay local/ignored.
- PowerShell launcher syntax passes. UTF-8, explicit staging and staged whitespace checks are checked before commit.
- Visually inspected Overview, active contracts, Recovery, expansion confirmation, compact jobs and simulated-touch recovery screenshots. Current screenshots are in `reports/interface-screenshots/`; alternate resolutions are in its `resolution-tests/` subfolder. This suite clears/replaces its previous images through the existing screenshot-run helper.

Reproduce:

```powershell
& 'C:/Users/jason/AppData/Local/Unity/bin/unity.exe' run "$PWD/Game" --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.GarageBusinessVerification.RunStandalone --garage-business-report "$PWD/reports/native-business-20261022/domain"
& 'C:/Users/jason/AppData/Local/Unity/bin/unity.exe' run "$PWD/Game" --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.PlayerBuild.BuildPlayerOnly
./tools/Test-NativeMenu.ps1 -FixtureName native-business-new-run -BusinessOnly
& 'C:/Users/jason/AppData/Local/Unity/bin/unity.exe' run "$PWD/Game" --timeout 1800 --no-tail -- -nographics -buildTarget Android -executeMethod DragLab.Editor.PlayerBuild.BuildAndroid
```

Use a fresh disposable fixture name for each full test: the suite completes transactions and sells its isolated last car. The actual successful fixture is `reports/player-fixtures/native-business-20261022-r1/`. No physical phone input, suspend/resume session or human usability acceptance is claimed. Existing economy calibration is preserved and may be reviewed separately.

## Interface review checklist (user acceptance pending)

- [ ] Overview: rate/cap, income amount and expansion cost/benefits are clear.
- [ ] Jobs: exact job car, restrictions, occupied bays, countdown and reservation/release consequences are clear.
- [ ] Recovery: no-car flow and recent operation amounts are understandable.
- [ ] Scrollbars, shared dialogs/hover/pressed styles and compact/1080p/4K layouts are comfortable.
- [ ] Physical Android: scrolling, safe areas, dialog buttons and suspended/offline timers work on the phone.

This is the requested existing Business migration, not approval of all Slice 4K gates or a request to begin Data/bench changes. No new income rates, job rules, simulation/audio or market behavior are introduced.


## Preserved pre-existing local changes

The unrelated modified receipts `reports/new-parts-supply/verification.txt`, `reports/parts-market/verification.txt`, `reports/race-audio-audition/coverage-matrix.csv`, `reports/race-audio-audition/verification.txt` and `reports/sqlite-state/verification.txt` were not included. Existing untracked `.codex-remote-attachments/`, `Tests.ps1`, `reports/race-exit-regression/` and `reports/shop-responsive-20261019/` remain intact.
