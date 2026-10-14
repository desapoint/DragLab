# 4J native Race selection — 2026-10-14

Race now provides Career, Free race, Practice and Records in the shared native shell. Career has searchable tier-filtered two-column cards, actual rival/car names, entry/reward/first-win terms, claimed status and authoritative eligibility reasons. Hidden selected events clear staging selection. The current-car button selects actual owned instances. Free race retains its existing opponent roster and three distances; Practice remains quarter mile/no payout. Available personal bests, last pass, existing in-session previous-pass comparison and recent completed outcomes use actual records and configured speed units.

Shared entry confirmation captures the build/profile, event terms, mode, opponent and distance, then invokes the existing race creation and entry transaction. Stale/changed/failed entries do not start a race; repeat confirmation while racing cannot enter twice. Practice/Career do not overwrite the separate Free race setup. A failed preliminary durable save now returns visibly instead of continuing race construction. No physics, opponent behavior, rewards or career content is duplicated/rebalanced. Eligibility is cached until its context changes, not calculated every idle frame; preview/search/F8/Settings/record browsing never save.

## Executed checks

Windows BuildPlayerOnly passed. Final packaged native **49 checks / 0 runtime errors** passed: every authored event/reason, search, blocked/eligible selection, car picker, captured terms/Cancel, Free distance/opponent configuration, F8 and Settings return, compact/1080p/true 4K bounds, records/read-only state, stale and locked entry rejection, real Practice/Career/Free staging, configured opponent/distance, retained Free configuration and SQLite entry reload. [Evidence](01-native.txt). Entry cash assertion uses the current starter event's authored fee; broader positive-fee/reward/balance matrices remain the historical domain gates. No complete races/listening/controller/Android runs are claimed.

One final attempt failed an older starter-filter click before Race checks began. A fresh isolated rerun of the same build passed the starter baseline and all Race checks. Prior successful Race runs are diagnostic history, not substituted for the final source result. Rendered Career/Free pages were reviewed; local captures are under reports/player-fixtures/race-selection-release/ and the current screenshot gallery.

## Review

Open Race, choose an owned car/event, check eligibility and captured cost/rewards, then Cancel or Stage. Try Free opponent/distance, Practice and Records; F8 and Settings should preserve the selection. Destructive race behavior remains the existing shared simulation. This completes the explicitly authorized Race-selection checkpoint technically; visual acceptance remains separate. Saved-setup rename/delete is authorized next. Later Business/HUD/bench pages are not advanced.
