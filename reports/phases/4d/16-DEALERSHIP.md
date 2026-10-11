# 4D.9 — Native dealership (2026-10-11)

## Scope

The user requested the next Garage step after owned-car browsing. Implement the dealership workflow as one review checkpoint: Owned cars / Dealership grouping, stock model grid, full-page model details and explicit purchase confirmation. Paint, the inspection drawer and later screens remain separate requests. Technical completion and user acceptance are tracked separately.

## Changes

- Two accented, rounded model cards per row reuse the Garage theme and layered previews. Prices use the existing dealer component-value floor and shared money formatter; cards show HP, torque, mass, class/PP, drivetrain/type/transmission, owned count, reputation and purchase availability.
- Reuse the browsing controls for search, filters and sorting. Independent owned/model queries retain their state across switching, Back, F8, Settings and resizing. Query gestures only hide/reorder cached cards.
- Stock inspection is built on an isolated factory profile through the real OEM ownership initializer and shared CarInspection. Main-thread preparation precedes a cached worker-domain quarter-mile estimate. Existing owned parts, wear and resources are never normalized for preview. Details reuse the dyno with a Stock series and list engine/induction, included fuel/oil/coolant and completed estimated quarter-mile results.
- Buy opens the shared confirmation with total, balance, remaining cash and required reputation. Cancel/X/Escape discard it. Confirm uses PaidEconomy quotes and the existing atomic MechanicsTransactions/ProfileStore path; quote snapshot/revision/catalog/policy/expiry and cash/REP/run guards remain authoritative. Failed saves publish no partial car/parts/cash changes. Errors remain visible and require reopening a fresh confirmation.
- Suppress the legacy P-to-practice shortcut while a native text field is being edited or the dealership is visible. Typing pickup search terms must not start/save a run.
- A successful purchase creates independent car/engine/component identities, selects the new car according to existing dealership behavior and opens its owned details. Multiple copies remain supported. Browsing does not save; each confirmed transaction saves once.

## Verification

- Unity 6000.6.4f1 Windows development build passed; executable/supporting files updated. Final build log contains no compiler or USS errors.
- Focused dealership probe: **49 checks / 0 runtime errors**, PASS ([evidence](16-dealership-focused-verification.txt)). Final full native probe: **573 checks / 0 runtime errors**, PASS ([evidence](16-native-verification.txt)). This retains all Settings, owned browsing/stat/dyno/naming/selection/maintenance gates and adds actual model search/filter/switch/Back/F8/Settings/Buy/Cancel/Confirm gestures; exact dealer prices, OEM isolation, cash/REP restrictions, changed live balance/offer, failed-save rollback, successful independent purchases, replay rejection and preserved existing identities/conditions/resources.
- Reviewed actual grid/details/confirmation captures at 1280×720 and the grid at 1024×768/1920×1080. True 3840×2160 render-target scaling/grouped-navigation gates pass. Physical 4K display and human visual acceptance remain open.
- Earlier full attempts exposed a verification click-delivery/timing defect before Garage. The probe now separates switch down/up edges across frames and observes reversal at the input event, retaining intermediate/endpoint/direction checks instead of comparing stale wall-time samples. No production switch behavior changed. A copied test profile initially had JsonUtility empty inline jobs; canonicalize only this isolated fixture and assert ownership/economy validity before exercising transactions. The focused probe then exposed the actual P shortcut/search regression; the production guard is fixed and the final full/focused runs pass.
- Added optional `./tools/Test-NativeMenu.ps1 -FixtureName native-dealer-check -DealershipOnly` for quick isolated development checks. Its profile-folder/development-build safety remains; default execution still runs the full suite. Factory ratings are cached across browsing gestures and refresh when the authoritative catalog is replaced by a definitive save/load.
- UTF-8/new GUID uniqueness/whitespace checks passed. Build-only font serialization whitespace normalized; assets/fonts/theme remain unchanged. Raw captures, logs, build and disposable saves stay ignored.
- Existing full physics/economy audits are retained; this increment migrates presentation and reuses their transaction/simulation rules. Technical completion does not imply user acceptance.

## Review

Open Garage → Dealership (F8 if still viewing the old interface). Search/filter/sort; inspect a model and its Stock dyno. Review price/REP/shortfall states, Back and F8/Settings return. Open Buy and Cancel/X/Escape, then confirm a purchase in a disposable save; check the new selected car, preserved existing assets and a second distinct copy. Review grouping, card spacing, details and confirmation at your normal window size. Stop before paint or other Garage additions until user validation.
