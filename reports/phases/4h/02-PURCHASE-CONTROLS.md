# Shop purchase controls (2026-10-12)

User-approved checkpoint: complete-set/individual selection, quantity and total price in shop details, with explicit positions for individual installation. This is the next small 4H control checkpoint, not completion of the cart page or 4F/G/H.

## Delivered

- New offers default to one complete set. Includes shows the actual cylinder/valve/ignition/corner count. Only categories with individually modeled roles offer Individual; multi-role kits expose the actual role choices. Wheels/brake hardware remain complete corner sets. Retained used stock keeps its existing exact-item identity and quantity.
- Shared joined choices, condensed numeric input and shaded −/+ controls. Quantities are whole numbers in 1–99; finite checks reject fractions/NaN/infinity, and boundaries disable the appropriate stepper. Total price uses the shared set/unit pricing and formatted money. Mode/quantity survives F8 and detail return for that car/offer; no purchase preference enters the profile.
- Add to cart buys the specified sets/role units into storage without selecting a cylinder. Matching new lines merge quantities; overflow rejects the request without changing the staged line. Add & install for individual units asks for explicit matching fitted positions, bounded by the newly purchased quantity. Unselected extras remain loose; full sets continue to change specification. Cancel/X leaves assets and the basket unchanged. Checkout remains the existing durable atomic transaction.
- Individual dyno/full-stat previews use the authoritative replacement operation on copies, retain unselected wear/faults/resources and label the previewed position. The current set card/filter reference stays unchanged. Preview caching excludes quantity-only changes; control objects survive mode changes so hover shine is retained.
- The existing native cart Install toggle for new units uses the same position picker instead of silently choosing the first cylinder. Its existing review shows selected positions and extras. This correctness bridge does not redesign the cart or add its future quantity-edit/destination controls.
- The original shared Back visual, native palette/surfaces/fonts, no-focus/no-Tab contract, mouse-down feedback, F8, explicit confirmations, engine-block preparation gate and old interfaces remain.

## Verification

- Final Windows `BuildPlayerOnly`: passed.
- `Test-NativeMenu.ps1 -FixtureName native-purchase-final-02 -PartsOnly`: **351 checks, zero runtime errors**. Includes thirteen-car topology/role pricing, authoritative single/multiple-unit preview equality, immutable assets/files, actual mouse/keyboard mode/quantity input, 1–99/fraction/non-finite handling, F8, merged lines, explicit positions/cancel, cart-toggle position bridging, failed-save recovery, exact checkout ownership/retained wear/extras, and current/compact/1080p/4K layouts. [Focused evidence](native-purchase-verification.txt).
- `Invoke-Unity.ps1 CartVerify`: **102 checks** rerun with the shared price/quantity, identity, used stock, set/unit/tire replacement, conflicting/stale/failed checkout and receipt regressions. [Domain evidence](../../shop-inventory/cart-verification.txt).
- Current-window individual/position dialog, corrected compact role choices and native 4K purchase renders visually reviewed. Raw images/logs/profiles remain ignored under `reports/player-fixtures/native-purchase-final-02/`.
- UTF-8, new metadata, report links and staged whitespace checks passed. Generated font serialization was verified as whitespace-only and restored.

Earlier full native **1,133/0** remains a historical baseline, not rerun for this shop checkpoint. Gates overlap and are not summed. Earlier failed runs are diagnostic artifacts, not accepted gates. The collapsed choice-container layout and clipped compact role labels found during native input/render testing were corrected. The first individual-preview assertion was corrected to use the exact selected-unit operation rather than renewing every worn member of a used kit. The `PurchaseOnly` tooling option supports focused future control checks without bypassing the final full Parts gate.

## Review

- Shop → pistons: Complete set defaults to one kit; −/+ and typed quantities update the formatted total.
- Switch to Individual; intake/exhaust or other roles appear only for applicable parts. Add to cart needs no cylinder selection. F8 preserves the purchase draft and basket.
- Add & install → select positions. Select no more than the purchased count; cancel/X changes nothing. Confirm staging and review positions/extras before Checkout.
- Compare whole-kit and individual previews; unchanged pistons/tires retain their condition. Review the controls at compact, 1080p and 4K sizes.

Stop for user visual validation after the local commit. Cart-page refinement, storage/donor destinations, service/rebuild migration, configuration libraries and the deferred engine-block wizard remain separate checkpoints.
