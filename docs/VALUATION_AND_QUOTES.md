# Valuation and captured quotes — 3C foundation / 3D transactions

Open **Garage → Value car / engine / chassis / loose parts**. Collection, Parts inventory and stored-engine rows also link to the exact owned asset. Select a buyer to compare offers. **Selected car**, **Chassis / keep engine** and **Attached engine** inspect different actual ownership trees. Stored engines, loose packs and singular engine elements are listed below.

The summary shows the offer, comparable loose-part total, chassis-only base, completeness, faults, excluded parts, policy version and expiry. **Show item values and every price adjustment** explains catalogue basis, quality demand, wear, individual damage, use/service history, buyer assembly context, model demand and incomplete/fault discounts. **Choose individual fitted parts to keep** excludes that pack and its children from a new preview. Missing assets contribute no value. Optional removals, such as stripped interior, do not create a missing-required-hardware discount.

The original 3C checkpoint introduced read-only quotes. Current 3D uses those immutable quotes for explicitly confirmed sales and chassis salvage with retained engines. Paid fuel/services and dealer purchases use captured payment quotes; configuration purchases retain their atomic captured-price Apply. See [paid operations and recovery](PAID_OPERATIONS_AND_RECOVERY.md). Full-loop balance belongs to **3G**.

## Value calculation

Each actual pack starts at authored catalogue price × used-market fraction. Authored quality demand then applies. Condition uses pack health and the actual individual cylinder/valve/tire condition; failed individual elements cannot hide behind a healthy aggregate. Deposits, leaks and adjustment error reduce condition value. A failed pack becomes a low-value recovery core. Bounded distance/runtime/abuse/reconditioning discounts account for use history; a service counter alone grants no resale bonus. Service that restores actual condition affects the same condition model used by driving.

Engines start with their actual loose-component subtotal, then apply buyer/model assembly demand and incomplete/fault risk. They have no invented empty-core base. A builder can offer more than the loose-part subtotal for an assembled engine, while a wholesaler can offer less for exactly the same hardware. Chassis start with an authored bare-chassis identity value plus their actual road/control components. Whole-car context applies to actual attached components and chassis identity, not a whole-car purchase price plus those same parts. Stored/loose/retained assets are excluded. Engine and chassis included identities partition the car's actual tree without overlap.

Fuel, engine oil and coolant state are captured to prevent stale transactions, but priced at $0 in this phase. Detached engine quotes leave external ECU/coupling requirements with the chassis. Completeness reports required hardware, with the shared resolver checking whole-car running dependencies; it is not a promise that a broken car is roadworthy. Fault risk is separately visible, including road component faults that do not stop combustion.

## Quote validity

Quote schema 1 receipts have immutable totals, lines and included/retained IDs, an issue/expiry time, profile revision and policy ID/version. Requests are copied; changing the caller's retained list cannot alter a receipt. Default expiry is five minutes. Clock rollback, expiry, revision changes, altered condition/hardware/resources/tuning, ownership moves, catalogue changes and policy changes invalidate the quote. Unversioned authored edits also invalidate it.

The snapshot hash conservatively covers the entire authoritative owned profile and its relevant catalogue definitions, so an unrelated garage/preference change may also require refresh. UI drafts are excluded. **Refresh quote** explicitly recaptures state; stale amounts remain visible until refreshed. Exact unchanged JSON reloads can be checked against the receipt. Quotes remain in-memory offers; 3D confirmation validates against the authoritative profile inside the existing candidate/save transaction, then persists matching cash/ownership and a committed operation receipt. Rebuild reservations and unsettled runs permit inspection but block disposal validation.

## Authoring and verification

Edit `Game/Assets/DragLab/Resources/Data/Economy/ValuationPolicy.asset` in Unity's Inspector: policy ID/version, quote lifetime, wear/history factors, quality demand, buyer contexts and per-model chassis/demand values. Bump the policy version for intentional revisions. Unknown authored qualities use neutral demand; unknown models use the explicit default chassis value. Invalid, duplicate or non-finite policy entries are rejected. **DragLab → Prepare Valuation Policy** creates the initial asset only when absent and preserves subsequent edits.

**DragLab → Verify Valuation and Quotes**, or `tools/Invoke-Unity.ps1 -Action ValuationVerify`, runs the owned-asset matrix and exports `reports/phases/3c/valuation-matrix.csv` plus the detailed verification log. `tools/Test-WindowsSlice.ps1 -ValuationOnly -ShowPlayer -FixtureName <name>` exercises actual buttons with isolated saves in the Windows development player. The verification gate does not modify the player's normal save.

Prices are deliberately provisional. Cheap starter purchase prices and component sums already differ in the earlier catalogue; valuation makes those differences visible. 3D adds transaction safeguards and a default dealer stock-disposal floor; final economy balance and broader recovery testing remain 3G. Every phase preserves its separate source/player checkpoint.
