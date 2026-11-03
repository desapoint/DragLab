# L1D physical inspection closure — open audit

2026-11-03; baseline `e261080`, design `a75964d`. Root read-only source audit during the isolated reference rerun. **Not an edit allowlist, closure approval or candidate implementation.** The separately reviewed frozen `Preview` liveness reaches `InspectionBuild.Capture` and build resolution, but does not calculate the requested inspection DTO. The finite physical method closure and its independent reference treatment must therefore be reviewed before production edits.

The existing full 266-file pre-query Domain manifest pins the bytes being inspected. It does not by itself prove a transitive call graph or authorize every method in those files.

## Observed routes and obligations

- `CarInspection.PrepareCar` constructs `CarSimulation` from the resolved spec, tune and components, then applies starting fuel mass and fluid values. `CalculatePrepared` reaches `BuildLimits`, mechanical preparation, service/induction calculations, transmission/wheel/flywheel quantities and diagnosis. When `runProbe` is true, it additionally reaches `CarSimulation.Stage`, `Launch` and `Step`; testing only static dyno quantities would miss this branch.
- `CarSimulation` is partial across `DragSimulation.cs`, `FuelDynamics.cs`, `RoadContactCar.cs` and `ServiceDynamics.cs`. Its constructor validates and copies the spec, tune and driver, clamps assisted targets, constructs induction/engine/transmission/wheel/AWD profiles, initializes fluids/mass, constructs `MechanicalStress`, computes shift points, prepares rules and publishes flywheel quantities. These calls and reached property accessors must appear in the final method manifest.
- `MechanicalStress` creates a catalogue from the spec when `hasInstalledParts` is false, and creates simulation parts when no installed components were supplied. L1D must prove those fallbacks are unreachable on its concrete installed-build path, or explicitly review their full closure. They cannot silently substitute for actual owned hardware.
- The stress constructor and `Prepare` reach `EngineElements.Ensure` and `ClutchHardware.Ensure` on private simulation components. A future candidate must validate existing-source completeness before this copy/preparation path, preserve every existing identity/condition, and distinguish genuinely new-part initialization from reconstruction/healing of persisted records. Keeping source objects unmodified alone does not prove correct physical output.
- `VehicleSpec.Copy`, `Tune.Copy`, `DriverSettings.Copy` and the constructor's nested copies must be audited through their actual reached typed copy methods; the direct copy call is not sufficient no-JSON evidence. Similarly, `MechanicalStress` copies fitted/corner definitions and installed components; the bounded concrete-build dependency must not become a profile/catalogue mirror.
- `CalculatePrepared` with a caller-provided steady-output delegate follows a different branch and suppresses that reuse when running the physical probe. Preserve the authoritative simulation and every requested DTO field, warning and array; do not use a static estimate as proof of probe equivalence.

## Remaining gates

The exact method inventory below is a lexical source capture for review, not a closed semantic graph. Missing receiver bindings, property accessors, constructors, extension/LINQ callback calls and cross-file partial methods remain to be expanded explicitly. Each final reached method needs its exact path/signature/range/body hash, file hash, purpose and reuse/reader-overload/frozen-reference treatment. Proposed paths outside the design's initial table require explicit root/high expansion before edits. The catalogue, simulation and source-input invariants must be tested independently of query Total/Fee/Apply liveness.

No source, simulation, UI, worker, persistence or caller is changed by this audit. Luna-high review remains pending; no S06/S07 advancement or global no-JSON completion is claimed.

## Root-method lexical capture

| Exact path under Domain | Declaring type and signature | Lines | Declaration/body SHA-256 | File SHA-256 |
|---|---|---:|---|---|
| CarInspection.cs | public static InspectionBuild InspectionBuild::Capture(PlayerProfile profile, OwnedCar car, Tune adjustments = null, string definitionId = null, string instanceId = null, PartCategory? remove = null, IEnumerable<PartCartLine> cart = null, bool healthy = false) | 14–50 | FA0CB33DDC1E49CB53A79A00FE8F4E40B3BBDFF95C0A4208338F68A83980C05D | 3296D682D165191D3D950DD61C14836DC55114E96E75736CEE9C26F3241703E5 |
| CarInspection.cs | public static CarSimulation CarInspection::PrepareCar(InspectionBuild source) | 80–87 | D4B668A169A635BD306457827AEA4B4557E88642F023F111536CCA00FF67A2B5 | 3296D682D165191D3D950DD61C14836DC55114E96E75736CEE9C26F3241703E5 |
| CarInspection.cs | public static CarInspection CarInspection::Calculate(InspectionBuild source) | 88–88 | F5721E33EA8F5C4B50DF3E57A8BE5342FCB6DF302D9EC453DFAA6A9F2AED003B | 3296D682D165191D3D950DD61C14836DC55114E96E75736CEE9C26F3241703E5 |
| CarInspection.cs | public static CarInspection CarInspection::CalculatePrepared(InspectionBuild source,CarSimulation car,bool runProbe=true,Func<double,InspectionSteadyPoint> steadyOutput=null) | 90–149 | 282D5518E737A5F1C05E3EA4976B6C41E78A890377C71B1311C10C4F1932E47E | 3296D682D165191D3D950DD61C14836DC55114E96E75736CEE9C26F3241703E5 |
| CarInspection.cs | void CarInspection::Diagnose(CarSimulation car) | 150–195 | 055638EE1E5DA4CD86D0155A975BC169439F10FC4C34A5329539C11032AEE6D2 | 3296D682D165191D3D950DD61C14836DC55114E96E75736CEE9C26F3241703E5 |
| DragSimulation.cs | public void CarSimulation::SetMassInputs(double driverKg, double payloadKg, double positionM, double heightM, double fuelLitres) | 539–543 | AD62EB13ED79EA0E89BDBE7EEF4C2E945F3AB65CC09F7392F482BB34E4279E8A | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | void CarSimulation::UpdateMass() | 544–544 | 9C0F5F970AD6E6CCB6426FA755A4655A1FE69EAE265884F91B4ED127F10F3969 | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | public double CarSimulation::ShiftPoint(int gear) | 547–547 | 1C963D1AACC5AA226F5D407D7CDDC25F7B9795FC2544C8DF3D21F015CF223B8A | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | public CarSimulation::CarSimulation(VehicleSpec spec, Tune tune, DriverSettings driver, IEnumerable<Mechanics.PartInstance> components = null) | 555–588 | 50B20E1D249FE350F4C31AB386D0C07CB9F52D90FE38349BB3911EA57880916C | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | void CarSimulation::PrepareSimulationRules() | 610–616 | 113B78D522302E97C54EF0516029631E46F78366F8BF56105A0389A6CFE0D4FB | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | public void CarSimulation::Stage(double dt, DriverInput input) | 618–645 | 7ECBBEF3CFA0724BD519F7C7BFDE7B0002BF7F22E14838AB0120206C8102E189 | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | public void CarSimulation::Launch(double timeSinceGreen) | 647–655 | 2E78EDE052926616A8922E63CEFFB455C6DFB47EBCA1896B1E64B796E3D25261 | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| DragSimulation.cs | public void CarSimulation::Step(double dt, DriverInput input) | 684–754 | 80F7E0D4FCD545144AF1D89B335632EAEAFBB89E8B2B7BFBE7279077714D7076 | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| MechanicalStress.cs | public MechanicalStress::MechanicalStress(VehicleSpec spec, Tune tune, IEnumerable<PartInstance> installed) | 19–33 | 747B8B060D6846A040BE94B51E1A76CF0E9DA456DEDD71FAEB75BCA8421EAC13 | 793600F5964ED661424A3F63F8199E80D790ECC6AC76EA1E10599208431A70BE |
| MechanicalStress.cs | public PartDefinition MechanicalStress::Definition(PartInstance part) | 37–37 | 0936339E5C898792FBF1CB6D85414EF1C0433C186B2BBB0E753FFAEE3DB86A88 | 793600F5964ED661424A3F63F8199E80D790ECC6AC76EA1E10599208431A70BE |
| MechanicalStress.cs | public PartDefinition MechanicalStress::TireDefinition(int corner) | 38–38 | 8746929FA9420107B6959C60BAAD2606A3DCC219CD14393BC099931E162B07BA | 793600F5964ED661424A3F63F8199E80D790ECC6AC76EA1E10599208431A70BE |
| MechanicalStress.cs | public void MechanicalStress::Prepare(CarState s) | 40–85 | 26BC7B638594A35D6E8785199E79A58828D30F176F21700950E4F60F10F77E44 | 793600F5964ED661424A3F63F8199E80D790ECC6AC76EA1E10599208431A70BE |
| ServiceDynamics.cs | public void CarSimulation::SetFluids(FluidState fluids) | 140–140 | 2272DF5506A7848CEC7D748A0BAB7735E0AFEC38F8D6572C42AE94FF6BA780F7 | 3B4295E3211BBC27E4C21BB4D8CD426DEEFF7403B20063A1C244F57E4FCEBB34 |


## Semantic expansion, still pending review

The root-only lexical table above is now supplemented by the [semantic audit](../../reports/save-refactor/S06D2B1L1D_SEMANTIC_CLOSURE_AUDIT.md): two identical zero-error compiler-based runs, 21 language/input/schema probes and an exact joined 428-member/43-file draft physical inventory. It records explicit virtual/interface/delegate and implicit-disposal gaps, plus a conditional path to authored JSON loading when `hasInstalledParts` is false. Existing typed capture/reader seams and their marker-setting route are identified for review. Neither the unpruned graph nor the compact TSVs are an approved edit allowlist; the complete production gate remains open.
