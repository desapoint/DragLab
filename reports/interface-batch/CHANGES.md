# Sale, Race selection and setup management batch — 2026-10-14

Delivered in the user-authorized order. Each implementation checkpoint reached a passing Windows build/native gate and was locally committed before the next began. Technical completion is separate from user visual acceptance; later interfaces remain gated.

| Checkpoint | Commit | Packaged native checks |
|---|---|---|
| Sale page / shared Back | 60c137e | 32 / 0 runtime errors |
| Actual valuation and contents | d3192d1 | 35 / 0 |
| Engine/component retention | 9eedfac | 37 / 0 |
| Explicit durable sale confirmation | ff41f32 | 50 / 0 |
| Career / Free / Practice / records | 9846dbe | 49 / 0 |
| Saved-setup rename / delete | 873bfb6 | 48 / 0 |

Counts include overlapping starter/shared gates and are not a unique summed total. [Sale report](../phases/4i/04-CONFIRMATION.md), [Race report](../phases/4j/01-RACE-SELECTION.md), [setup management](../phases/4e/06-SETUP-MANAGEMENT.md) describe exact checks and limits. Earlier physics, full-career economy and listening/controller/Android results remain historical baselines, not reruns of this UI batch.

A final integration correction keeps covered Garage details/source tabs hidden directly in their refresh predicates while sale/salvage is open, avoiding redundant class removal/re-addition every frame. The final integrated Windows build and sale suite passed **50 / 0** again; [evidence](integration-native.txt). Assets/folder metadata and GUID uniqueness were checked; generated whitespace-only font serialization was restored. Required implementation, metadata, tools and concise reports are versioned; build/save/log/image artifacts stay local.

Review Garage → car → Sell / Salvage, Race's four modes and Tuning → Load setup → selected-name pencil/Delete. Check cancellation, retained worn parts/engine liquids and preserved tuning/cart drafts. Current build: Game/Builds/Windows/DragLab.exe. Review captures remain in reports/player-fixtures/disposal-final/, race-selection-release/ and setup-management-final/; the gallery exporter keeps alternate resolutions in its resolution-tests subfolder.
