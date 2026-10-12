# Detailed component replacement selection (2026-10-12)

User-requested checkpoint: show which fitted pistons or other repeated units will be replaced, with number/position, wear, type and relevant faults. The user explicitly confirmed complete-set replacement as the default with optional individual replacements, and requested the same behavior for other applicable components. This refines the purchase/replacement dialog; the cart-page redesign remains pending.

## Delivered

- Add & install on a new offer with individually modeled members opens Replace components. The default remains the complete set, including the existing set quantity, total price, one installed kit and any extra sets stored. Storage-only Add to cart stays unchanged.
- Matching-specification rows can be clicked to switch to an individual replacement purchase. The selected member count and shared per-unit total are explicit; confirmation stages only those purchased role units at their exact fitted positions. Multi-role sets offer actual role choices. Different specifications keep complete-set installation available and disable individual mixing, preserving current mechanics.
- Rows show the real fitted position, wear used, fitted part/type/quality and applicable fault/severity. Pistons show piston number, crown and bore; valves/springs/followers retain cylinder/intake/exhaust/member identity; ignition and injector roles remain actual; tires identify each corner. Unknown condition is unavailable, not fabricated wear.
- The same row renderer is reused by direct individual purchases, the new-unit cart installation picker and owned-spare position selection. Selection is bounded by quantity, selected faults stay readable, and long lists use the shared modal scrollbar. Complete-set/individual choices and rows retain the native surfaces, hover shine, pressed feedback, no-focus/no-Tab contract and palette.
- Cancel/X discards dialog choices. Staging does not heal/charge/save the car; original identities, unselected condition/resources, extras, F8 and atomic checkout remain. No component physics, compatibility policy, inventory schema or new unsupported individual hardware is introduced.

## Checks

- Final Windows `BuildPlayerOnly`: passed.
- `Test-NativeMenu.ps1 -FixtureName native-replacement-final-01 -PartsOnly`: **366 checks, zero runtime errors**, including the existing Build/inventory/position/durable-checkout regression and the new default-kit, per-unit wear/type/fault, partial purchase, cancel/X, mismatch restriction, role filtering and tire-corner controls. This broader run preceded only the final singular-label wording correction.
- Final packaged `Test-NativeMenu.ps1 -FixtureName native-replacement-final-02 -PurchaseOnly`: **302 checks, zero runtime errors** after that wording correction. Includes the thirteen-car model/topology baseline, actual native input, exact set/unit staging, untouched assets/files, selected-unit checkout and compact/1080p/4K purchase layout checks. [Combined evidence](native-replacement-verification.txt).
- Current-window complete-set/individual-valve dialogs and selected-fault contrast visually reviewed. Raw screenshots/profiles/logs remain ignored in the named player-fixture folders.
- UTF-8, documentation links and staged whitespace checks passed. Unity font serialization was verified as whitespace-only and restored.

Earlier full native **1,133/0** and cart-domain **102** are retained baselines, not rerun for this dialog-only checkpoint; the authoritative cart/domain code is unchanged. Gates overlap and are not summed. Earlier failures were test assumptions about casing/which offer differs, plus a premature click on a filtered card still calculating; tests now wait for readiness. Those diagnostic runs are not accepted gates.

## Review

- Shop → fitted pistons → Add & install: all members are selected by default; inspect each member's wear/type/faults and total.
- Click one matching piston, or choose Individual: confirm the explicit member count and price; Cancel/X leaves the basket and purchase draft intact.
- Try a different specification: use a complete matched set; individual mixing remains unavailable.
- Repeat with valves/springs/plugs/injectors or tires. Role selection shows the appropriate positions; extras retain the existing storage behavior.
- Inspect an owned spare's replacement position and the cart's individual Install picker: the same factual rows appear.

Stop for user visual validation after the local commit. Cart-page refinement and later 4F/G/H workflows remain separately reviewed.
