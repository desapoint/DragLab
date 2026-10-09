# Shared inventory and transaction cart

Approved post-3H extension, 2026-10-09. Preserve the paid economy, actual ownership, individual condition, same race/bench simulation and explicit Apply behavior. This does not start another planned slice.

## Authoritative records and containers

`SharedInventory` exposes Storage, Car, Engine, Seller and Cart containers. Player parts and loose units remain their existing authoritative records; installed slot bindings and engine parents remain authoritative. Views reference those records rather than duplicating them. Garage shelves and Storage shelves are initially available. Storage moves persist a location identifier; legacy blank locations mean Garage. Detached engines retain their complete child tree, fluids and tuning when moved or attached. Installed parts cannot be sold as loose items.

New catalog offers are virtual seller offers. Sold loose sets and individual units move into persisted dealer stock with the same item/descendant identities, damage and history. Buying back moves those original records into Garage storage or their checked fitted positions. Complete car/chassis/engine sale, retained salvage and assembly valuation continue through the existing captured-quote interface; those disposal markets are not a used-vehicle dealer rotation.

Dealer records and optional tire-unit records have explicit type discriminators, because Unity JSON can instantiate empty objects for null serialized fields. Normalization clears only inactive representation fields, never condition, quantities or missing installed slots. No migration grants parts or renews wear. Duplicate identities across player and dealer records are invalid.

## Sets and individual units

The default offer is a complete set. One piston set contains the selected engine's cylinder count; one valve/spring/seal/follower set uses its valve topology. Ignition sets also include the applicable coil topology. A tire set contains four tires. Shop/cart labels state set size and distinguish set quantities from unit quantities. Quantity one never means buying eight piston sets for a V8.

Individual units can be purchased without selecting a cylinder. Currently individually modeled engine categories (pistons, rods, rings, bearings, valves, springs, seals, followers, ignition and appropriate injection hardware) and tires support this path. Where a category contains different roles, such as intake/exhaust valves or plugs/coils, choose the role. Quantity controls buy units of that role. Whole wheel/brake hardware remains a corner-set offer because those hardware definitions do not model independent mixed corner specifications.

To install individual purchased units, select fitted positions in the cart. Checked positions consume that many purchased units; extras remain loose. Replacement requires the fitted definition and role to match, preserving the existing matched-mass/profile rule; changing specification uses a complete kit. Individual inventory spares can be reused in explicitly selected positions. Other components keep their identities, wear and temperature. Replaced originals remain loose inventory. A tire replacement preserves the other tires' life and heat and retains the old tire's physical condition.

## Pricing and confirmation

`InventoryPricing` supplies new-set/unit pricing, dealer buyback pricing and the existing condition-aware valuation quotes for every supported valuation kind. New sets use catalog prices; individual pricing retains the existing rounded per-set allocation. Selling uses buyer/quality/condition/history/completeness valuation rather than a flat universal discount. Assembly values remain independent of loose-component totals.

The valuation policy authors dealer resale markup and handling fee (initially 35% and $1). Dealer purchase price is its captured acquisition cost plus that spread; condition remains unchanged. Detailed buyer explanations remain available through the valuation screen. Cart rows show their charges/credits, and the basket shows its net payment and resulting cash. A negative net means the player receives money.

Cart commands cover complete-set purchase, individual purchase, used-stock purchase, loose-set/unit sale and moves to storage/car/stored-engine containers. It remains an unpersisted session plan scoped to the selected owned car. Merely adding, inspecting, changing quantities, selecting positions or previewing spends/saves nothing. Installed service and immediate replacement shortcuts remain available; complete-set shortcuts are explicitly labelled.

Quote validation precedes candidate mutation: profile/catalog/policy, availability, fitment, reputation, quantity bounds, unique owned-item use, conflicting slots/positions, reservations, active runs, timestamps and affordability. Captured sales fund purchases in the same transaction. One recoverable save publishes all cash, ownership and a receipt containing disposed/moved IDs. A failed operation or write leaves the original profile unchanged. Expired, changed or replayed quotes cannot commit. Engine/car moves retain existing reservation guards.

## Performance preview and limits

Cumulative installed stats use the same cart operations on an isolated profile copy, then the same condition-aware inspection used by the saved car. Background driving probes and no-wear references remain in place. Offer templates, cart totals and sale prices are cached rather than recalculated each repaint. Complete saves still occur at definitive confirmation. Large inventory rendering and save transactions are not promised to be allocation-free or hitch-free.

No shift-light HUD change is included in this inventory increment. Slice 3's operating-loop systems remain accepted through 3H; human balance/listening, final presentation and real-device acceptance remain separate gates. See [implementation evidence](../reports/inventory-commerce/CHANGES.md).
