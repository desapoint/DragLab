# 4D.1 — Owned-car grid (2026-10-10)

First user-requested Garage control checkpoint. Technical completion and human acceptance remain separate. This is not completion of all 4D; search/filtering, sorting, item selection/actions, dealership and inspection drawer await separate instructions.

- Garage displays a responsive, internally scrolling grid of actual owned instances. Cards show model, short identity, layered paint/wheel preview, HP/torque/mass, current-build class estimate, drivetrain/fuel and condition. The actual current car alone is marked CURRENT.
- Duplicate models retain separate full instance IDs and condition. Worn parts/service faults, failed components, missing engine, reservation and empty fuel remain visible; an empty garage says No owned cars. Cards are read-only in this checkpoint; clicking does not select or mutate a car.
- Use shared palette/font/panel surface/scrollbar and existing source art. No additional title/subtitle, alternate theme, focus decoration, Tab navigation, new drawer or dealership tab. Existing header/Settings and footer joins remain.
- Static UI previews use the existing LayeredCarView applied geometry, then retain its sprites/tints in UI Images. Temporary preview sources are disposed; no camera, sound listener, physics/input owner or particle simulation is created. Optional slip-effect construction defaults to the existing enabled behavior in races/benches.
- The existing CarInspection prepares copies on the main thread and runs quarter-mile class estimates serially in one background worker. Pending/unavailable class states are explicit; estimates never advance the owned car. Cancel obsolete generations and cache cards/geometry until profile identity/revision, selected instance or displayed torque unit changes. Class completion updates text without rebuilding cards. F8, Settings/Cancel and resizing retain scrolling where valid.

## Verification

- Unity 6000.6.4f1 Windows development player build: passed (`tools/Invoke-Unity.ps1 -Action BuildPlayerOnly`). Updated local executable and support files under `Game/Builds/Windows`.
- Native player probe: **256 checks, 0 runtime errors** (`tools/Test-NativeMenu.ps1 -FixtureName native-owned-car-grid-final`). [Retained verification](01-native-verification.txt) covers nine real owned instances/eight definitions, duplicates, worn pistons, failed brakes, detached engine, reservation, empty tank and empty garage. Background class labels finish without rebuilding cards; browsing/F8 do not select, spend, refill, heal, replace assets or write the durable save.
- Standalone comparison probe: **48 checks, 0 runtime errors** (`tools/Test-InterfaceComparison.ps1 -FixtureName comparison-owned-grid -ShowPlayer`). [Retained verification](01-comparison-verification.txt). Existing route/toggle test captions mentioning empty replacements are historical wording; Garage now contains this grid and Settings retains its implemented controls.
- Checked reflow at 1024×768 and 1920×1080, true 3840×2160 offscreen target at shared 2× UI scale, no Tab traversal, cached card geometry and scroll retention through Settings/Cancel. Inspected populated 1280 and 4K captures, including truthful detached-engine/empty-fuel states and the current marker. Raw captures/logs remain ignored under `reports/player-fixtures/native-owned-car-grid-final`.
- Static checks: UTF-8 text, report links, matching metadata and whitespace checks. Existing source art is reused; two new scripts have new metadata, with existing asset GUIDs preserved.

Limits: 4K verification uses an offscreen render target, not a physical 4K monitor/input session. Class is a copied-build automatic quarter-mile estimate, not a recorded personal best; inability to finish displays Class unavailable. Broader mechanics/economy acceptance gates remain historical baselines and were not rerun for this presentation checkpoint. Human visual acceptance is pending.

## Interface review

- F8 into the new Garage: review a card's preview, concise facts, identity and CURRENT marker.
- With multiple owned copies, review spacing, wrapping and internal scrolling; resize the window.
- Review failed/worn/reserved/incomplete cards and the empty state in captured isolated fixtures.
- Cards currently display only. Validate this grid before adding search/filter, sort or selection/details.
