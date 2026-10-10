# 4A — Coverage map and annotated layouts (2026-10-09)

**Ready for user review; acceptance pending. 4B–4Q have not started.** Source baseline `1fe619b`; runnable runtime baseline `e5eed86`. This checkpoint changes review documents and development tools only. The existing Windows player at `Game/Builds/Windows/DragLab.exe` remains the game checkpoint; no new player was built.

## Delivered

- [Clickable layout review](../../../docs/ui/4a/index.html), [review guide and workflow contracts](../../../docs/ui/4a/README.md), [source index](../../../docs/ui/4a/SOURCE_INDEX.md) and machine-readable control inventory.
- Sixteen screens: garage, cars, tuning, configuration library, installed/service, inventory, shop/cart, assemblies/valuation, career, business, race HUD, results, settings, acoustic bench, physical bench and searchable coverage. Five representative state selections: ready, worn/failed, incomplete, reserved and empty garage. These are illustrative layout examples, not complete simulated state models.
- Persistent selected-instance identity, summary before details, explicit draft Apply/Revert, failure-first service navigation, complete-set quantities, cumulative trade/cart comparison, exact retained-item labels and recovery routes. Stat bars distinguish the current baseline and delta with current → proposed values. The forged-piston example shows strength/mass changes without inventing additional healthy-engine horsepower.
- Separate isolated acoustic and physical bench layouts. Live keyboard/scenario feedback cannot claim slider ownership; explicit slider takeover is available. Hardware/configuration tabs remain editable. Recording is optional and the chronological graph includes shifts, lift and coast.
- Framework proposal: runtime uGUI, Unity Input System, reusable presenters and shared action boundaries. No packages are installed or versions pinned. Framework, hierarchy and interaction conventions await review before 4B.
- Reproducible static coverage generator: **827 explicit UI/feedback call sites, 183 reflected editable bindings and 41 keyboard occurrences across 30 runtime UI/input files**. Source hashes, expressions, locations, destination phases and context contracts are retained. Dynamic catalogs, component positions and scenarios are templates rather than fixed counts of rendered controls.
- Updated objective, implementation plan, game plan and status to distinguish delivered 4A review from human acceptance and future runtime migration.

## Checks actually run

- `python tools/Build-Slice4A-Review.py --check`: generated JSON, browser coverage index and Markdown match current source. Unique locator IDs, reflected call-site references, mapped destinations and source hashes checked.
- Node syntax checks for the review JavaScript, generated browser index and browser verification script; Python generator syntax compiled in memory.
- `tools/Verify-Slice4A-Review.cjs` against installed Chrome with an isolated headless browser: **1,233 checks passed, zero failures, zero browser errors**. Sixteen screens × five state selections × five browser widths (1440/1280/960/800/640), plus draft/Revert, cart/dialog, heat dependency, bench ownership/config/tabs, coverage search and annotation interactions. [Recorded result](layout-verification.txt).
- Eleven local screenshot captures retained under ignored `local/`. Final shop, physical bench and compact tuning screenshots inspected visually. Corrected an SVG grid fill and misleading piston power comparison during layout review; the final passing capture contains those corrections.
- UTF-8/garbled-punctuation scan, local Markdown-link targets, required layout artifacts, documentation status consistency and Git whitespace checks. No paths under `Game/` changed.

## Review and limits

Open the HTML locally in a browser and follow the seven-step checklist in the guide. Review the screen organization, primary/advanced hierarchy, comparison placement, draft/cart clarity, bench distinction and framework proposal. Use the screen/state/width selectors and annotation toggle. Buttons demonstrate local presentation behavior only; prices, curves, measurements and timers are explicitly illustrative. There is no live simulation, economy, persistence or audio connection.

The source scanner is not a C# compiler or a proof of native UI parity. Exact bindings, dynamic enable rules, input ownership, held-throttle behavior, performance, save/transaction effects and audible gain must pass their destination migration gates. Browser layout checks do not certify Unity Canvas scaling, controller focus, phone safe areas or multitouch. No Unity import/build, gameplay matrix, real-device or listening gate was rerun for this documentation-only increment; previous evidence remains historical.

**Human validation: pending.** Stop here after the local commit. Requested corrections remain 4A; start 4B only after explicit user validation, as required by the repository instructions and Slice 4 review contract.
