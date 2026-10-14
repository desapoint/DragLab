# Part brands and upgrade focus

Each supplier has one declared upgrade focus. Shop, inventory and installed-part filters support one focus and any selected brands; multiple brands form an OR selection, combined with the other filters. No brand selection means all suppliers. Apply confirms a filter draft; Cancel leaves the previous filter intact. Clear resets the query. Search also recognizes focus labels. Mixed wheel/tire sets match the actual suppliers on their corners, with brand and focus required to match the same constituent.

| Focus | Suppliers | What to expect |
| --- | --- | --- |
| OEM | Atlas OE | Factory-spec replacements, including factory-forged or performance equipment. |
| OEM+ | Relay Components | Stock-style replacements and longer-service alternatives. Inspect the actual specification; regular cast replacements do not inherit forged strength. |
| Street | Redline Engineering, IronTrail, Trackline | Mild road upgrades; IronTrail focuses on work vehicles, Trackline on road tires. |
| Tuning | Stratos Performance, Vortex Dynamics, IonWorks, LobeWorks, IronTrail Diesel, Trackline Circuit | More substantial build changes; general performance, induction, electronics/fueling, valvetrain, diesel and track tires respectively. |
| Competition | Apex Forge, Vector Motorsport, Trackline Racing, Apex Competition | Forged internals, specialist AWD/sequential equipment, race tires and competition hardware. |

These are supplier focuses, not a universal ranking or a tuning permission. Competition does not promise extra HP, longer life, easier driving or suitability for every build. Condition, physical compatibility, price, actual stat changes and listed adjustments remain authoritative. A tuning-focused fixed part can have no editable settings. Buying a supplier's part never grants capabilities the hardware lacks.

OEM definitions are identified by their actual stock `/0` identity rather than by forged/cast material or fuel type. Independent long-service and regular replacement variants use Relay Components. Higher-tier and specialty variants are assigned after all definitions (including AWD and long-service parts) have been authored. Existing model/part IDs, mechanical specifications, price, wear, faults, tuning permissions and ownership are unchanged. Brands do not add duplicate products. Saved markets retain their seeds, epoch and quantities; no restock is forced.

`PartBrands` is the shared supplier registry and assignment policy. Extend its profiles/assignment when authoring a new supplier. Explicit project catalog overrides run after default assignment and retain their brand/name choices. Unknown override brands appear under Custom instead of being hidden or rejected. Catalog content revision is 6; the save schema and physics identity do not change.
