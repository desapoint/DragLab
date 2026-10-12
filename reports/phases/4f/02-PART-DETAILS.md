# Part detail layout refinement (2026-10-12)

User-requested checkpoint: improve shop details, with compact Back separated from the title, visible description/specifications/compatibility, no Installed/Inventory/Shop submenu, a right-side dyno and full-width specifications/comparisons below. This refines the shared detail renderer, so inventory/installed details retain consistent controls too. It does not complete 4F/G/H or introduce later shopping/assembly workflows.

## Delivered

- Compact 36-point filled Back icon, 16-point title gutter and a separate compact information action. The source submenu, browsing toolbar and redundant reference line are hidden while details are open; Back restores the retained browser.
- First row: part compatibility, brand/quality, type/stock, complete-set or individual-unit contents, actual condition, adjustment capability and description on the left; shared Current/Planned, With part and Current no wear dyno on the right directly below the title.
- Operating warnings have a separate compact action using the shared modal. They do not change physical fit classification. Installation prerequisites remain explicit when performance cannot be estimated. The main description removes duplicated supplier/internal fitment boilerplate; the full original description and technical specifications remain in the information dialog.
- Full-width four-stat summary bars, selected-part specifications and collapsible complete comparison groups follow the first row. Shared units, min/max bars, favorable/drawback/tradeoff colors, hover information, differences-only filtering and dyno pointer inspection are reused. Disclosure/difference state is retained per part for the UI session.
- Price and cart actions stay outside content scrolling. Add to cart and Add & install retain the existing staged/atomic checkout behavior; a queued indicator confirms staging. Owned individual replacements still use their explicit confirmation and actual chosen position. Deferred different-block preparation remains gated.
- Shared metal surfaces, palette, fonts, corners, hover shine, mouse-down behavior and scrollbars are retained. No focus highlight or Tab navigation. No catalogue, physics, prices or player assets are changed by the layout.

## Verification

- Windows `BuildPlayerOnly`: passed on the final source.
- `Test-NativeMenu.ps1 -FixtureName native-part-details-02 -PartsOnly`: **188 checks, zero runtime errors**. Covers four detail viewports (current window, compact, 1080p and 4K), compact Back/title spacing, hidden browsing submenu, actual description/specifications/fit, right-side dyno, full-width summary/comparison ordering, fixed actions during expansion/scroll, real information input, read-only profile/disk/model caching, thirteen-car/five-system previews, current/planned physical-fit risk semantics, selected cylinder feedback, F8 and atomic checkout failed-save recovery. [Focused evidence](native-part-details-verification.txt).
- Rendered screenshots reviewed at the current window, compact and 1080p; a dedicated 4K detail render was captured and layout-checked. Raw artifacts remain under `reports/player-fixtures/native-part-details-02/` and are ignored.
- UTF-8, documentation-link and staged whitespace checks passed. Unity font changes were verified as whitespace-only before restoring them.

Earlier full native **1,133/0** and cart-domain **102** checks are retained baselines, not rerun in this layout-only checkpoint. Gates overlap and are not summed. Visual acceptance remains for the user.

## Interface review

- Open Shop → a part: check Back spacing, description/compatibility and absence of the source submenu.
- Check the first-row dyno at the current window size, compact mode and 1080p; scroll down to the full-width changes, specifications and grouped comparisons.
- Expand groups/filter differences, then stage a purchase: price/actions stay visible, cart feedback updates and reopening retains local comparison state.
- Open information/warnings and inspect curve points. Risky fitting hardware remains Compatible.

Stop for user visual validation after this locally committed checkpoint. No later control or phase is advanced.
