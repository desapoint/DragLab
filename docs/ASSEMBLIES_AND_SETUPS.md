# Owned engines and saved configurations

Open **Garage → Engine assemblies** or **Garage → Save / load engine configurations and tunes**. These screens extend the existing Parts, Tuning and collection interfaces.

## Assemblies

Detach moves the exact engine identity, engine-owned slot bindings and oil/coolant state into stored engine inventory. The chassis, its fuel tank, road components, gearbox, clutch and controls remain on the owned car. A chassis without an engine remains owned and cannot race. Missing components are never supplied by a reload.

Select a stored engine to inspect its actual component conditions, remove a pack to loose inventory or reinstall a compatible loose pack. Individual piston/valve histories and service faults are retained. Complete and incomplete engines can both be attached to a chassis. An incomplete build still needs the missing components before it can run.

To transfer an engine, detach it, select another owned car in Collection, detach that car's current engine and attach the retained engine. The receiver keeps its chassis/wheel/gearing adjustments; engine adjustments and liquids move with the engine. Controls/ECUs are chassis hardware: an EFI engine can require a compatible ECU on the receiver. Current declared engine mounts permit the same vehicle platform; cross-platform engine conversions require future authored mount/adapter definitions. Individual compatible parts retain their existing cross-platform fitment rules.

**Keep engine / dispose chassis / remaining assets** is a nonmutating part-out preview. Engine descendants are excluded from proposed chassis disposal. Loose items and other stored engines are retained. 3D connects the buyer valuation screen to confirmed paid salvage and engine sale; see [paid operations](PAID_OPERATIONS_AND_RECOVERY.md).

## Setup library

Enter a name, choose Engine + controls, Whole car or Tune only, then Save. This snapshots the **applied** build; apply edits in Tuning before saving. Names may be reused, but each snapshot has its own ID. Delete removes only the blueprint.

- Engine + controls saves engine-owned hardware plus associated ECU/limiter/two-step/shift-light choices and engine adjustments.
- Whole car saves fitted hardware and all user tuning adjustments. Optional hardware absent from the snapshot is removed to inventory when restoring it.
- Tune only saves adjustments and buys no hardware. Existing installed-part tuning permissions remain authoritative.

Load creates a preview. It keeps matching usable installed components, then chooses compatible loose inventory (best health, stable identity order), then quotes new missing components. Failed or individually faulted packs do not satisfy an automatic usable-part request. Other cars and stored engines are never stripped automatically. Their hardware can be detached/moved explicitly first.

The preview lists exact reused items and retained wear, missing purchases, cost, tuning effects and blocking dependencies. **Apply / reuse owned parts** is available when nothing is missing. **Buy missing + apply** explicitly charges the total and installs fresh identities. Replaced components stay in inventory. A blueprint survives selling its hardware; restoring it purchases replacements rather than resurrecting sold items. Fuel, fluids, individual faults, wear, records and paint are never copied from a setup.

Applying uses the existing atomic candidate/save/publish transaction. Stale previews, changed quoted prices, incompatible hardware, insufficient cash, active run receipts and rebuild reservations reject the operation. Failed persistence rolls back cash, parts and adjustments together. A setup cannot create a free engine core: attach a stored engine before restoring engine hardware to an empty chassis.

## Persistence and scope

Player schema 5 migrates schema 4 in place and retains `.legacy-v4` (the earlier schema-3 migration retains `.legacy-v3`); empty garages stay empty and operating receipts/recovery deadlines persist; mechanics schema 1 and shared simulation model v15 remain unchanged. Attached bindings/fluids have one owner in the car; detached bindings/fluids have one owner in the stored assembly. Normalization never repairs worn items or fabricates missing descendants. Progress reset clears library snapshots and stored assemblies while retaining driving/audio/unit preferences.

Catalog prices remain provisional. Buyer valuations and captured quote explanations are now available through the assembly, collection and inventory interfaces; see [valuation and quotes](VALUATION_AND_QUOTES.md). Quote-backed paid sale/salvage and fuel/service pricing are delivered in 3D, with full-loop balance in 3G. The four new cars use existing layered placeholder artwork in this mechanics milestone.

The expanded roster keeps injector-pack fitment cylinder-specific: the original injected four-cylinder pair still shares its package, while three/six/eight-cylinder factory packages require the matching count. Loose inventory with no owner is never initialized as a descendant of an engine-less chassis. Stored/installed element trees retain their existing identities and conditions.

Tuning-only snapshots remain usable on incomplete retained chassis; the preview warns that the build remains unavailable. They grant no hardware or engine-fluid resources.

