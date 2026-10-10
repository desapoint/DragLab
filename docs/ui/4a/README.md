# Slice 4A — Layout and control coverage review

**Historical review proposal: web layouts declined by the user.** Source baseline: `1fe619b`, runtime baseline `e5eed86`. This archived artifact and its source hashes describe that checkpoint, not later runtime changes. The user now directs native layouts one control at a time; see [the current contract](../../SLICE_4_PLAN.md). No Unity UI, input packages, scenes, physics, game data or saves were changed by 4A.

Open [the clickable review](index.html) in a browser. It works locally without installation or a server. The top controls select 16 screen layouts, five representative ownership/condition states, compact width and annotation visibility. Buttons demonstrate navigation, draft labels, dialogs and local presentation changes only. **Every price, curve, car measurement and timer is illustrative**, not a simulation result. The preview does not play audio, execute jobs/transactions or reproduce Unity input.

## What to review

1. Start on **Garage**: selected-car identity, summary, fuel/condition and quick paths should be understandable at a glance. Switch to **Empty garage** and check the recovery route.
2. Open **Settings**: review Audio, Units, Driving, Simulation, Controls and Reset. Toggle Heat management and inspect the distinct starting-temperature choices. Reset confirmation retains preferences and starts the existing authored new-game baseline, rather than inventing an empty save.
3. Open **Tuning**: edit a slider, observe UNAPPLIED, Revert and Apply. Summary/dyno stays separate from the scrollable technical controls. Loading configurations has its own kept/reused/missing-purchase breakdown.
4. Review **Installed/service**, **Inventory**, **Shop/cart** and **Engines/valuation** in order: finding a failure, using a spare, buying a full kit or single unit, comparing cumulative changes and understanding kept/sold assets should be direct.
5. Review **Race selection**, **Business**, **Race HUD** and **Results**: entry terms, reservations, neutral launch, shift light and failure/repair navigation should have clear priority.
6. Review **Sound bench** and **Physical bench** separately. Their label and control hierarchy must distinguish fixture overrides from the real shared-road model. Config tabs remain editable during keyboard control; pedal feedback is read-only until explicit slider takeover.
7. Select **960 compact** and resize the window. Check reading order and the comparison/cart panels moving below lists rather than disappearing. Use **Control coverage** to find an advanced control such as `twoStep`, `camOverlap`, `exhaust`, `clutchOil` or `fuel`.

Approval covers organization, primary/advanced information, workflow hierarchy, interaction conventions and the proposed framework. Typography/color is functional scaffolding; car art and detailed polish retain Slice 5. **4B cannot begin until the user explicitly validates 4A.** Requested layout corrections remain 4A.

## Navigation and information hierarchy

Persistent header: exact selected owned car, cash/REP, Settings. Primary navigation: Garage, Cars, Workshop, Inventory, Shop/cart, Tuning, Race, Business. Developer benches have a separated entry. Settings may be opened from pause or either bench without hiding the shared nature of preferences.

Workshop is a family of workflows rather than one long page: Installed/service → Storage/spares → Shop/trade cart → Engines/valuation. The prototype exposes direct entry points for review; production navigation may group them under Workshop while keeping tabs and quick actions. One session cart survives workshop tab/category changes, remains scoped to the current selected car and clears under the existing owner-change behavior. It is not a second inventory.

Keep core power/torque/mass/standard-probe estimate and relevant warnings above detailed scrolling. Component changes show strength, cooling, wear, inertia or tuning access when HP is unchanged. A forged-piston example deliberately retains healthy-engine HP while showing increased mechanical strength and mass. Stats use a shared minimum-to-maximum display range, grey current baseline, colored change segment and current → proposed values; label tradeoffs and failure states beyond color.

Race primary layer: gear, speed, RPM, installed shift lamp, starting lights and requested pedals. Optional technical layer: engine vs shaft torque, wheel/clutch slip, branch failures, fluids/heat and converter/CVT. An installed shift lamp must have visible armed/threshold status, including an explanation when auto shifts occur first; no fake numbered CVT gear alerts.

Bench primary layer: mode, input owner, live physical feedback, graph and manual/scenario transport. Secondary pages contain advanced configuration. Do not bury controls in one unbounded inspector. A searchable complete advanced field list preserves the raw experiments; presets do not replace access to fields.

## Proposed layout sizes and interaction conventions

- Desktop reference: **1280 × 720**, verified layout variants at 1440/1280/960/800/640 browser widths. Unity Canvas scaling is a proposal for 4B, not certified by browser checks. Desktop primary type starts around 14–16 px and controls have visible focus. Final text-scale accessibility remains 4O.
- Wide workshop: offer/list column, comparison column, cart/detail column. At compact widths use two columns then stack details/cart with explicit entry buttons; preserve summary/actions and allow independent list scrolling. Do not shrink labels into unreadable controls.
- Landscape mobile candidate: **960 × 540 logical layout**; condense navigation into a route drawer, switch between list/detail/comparison/cart panels, keep a persistent basket/Apply summary and reserve safe-area margins. Actual density, touch sizes and usable area depend on real hardware; acceptance is 4P.
- Numeric controls: labelled units, precise text entry, slider, Fine and Reset actions. Middle-click restores the field's default; right-drag gives the existing 10× finer adjustment. Controller/touch use explicit Fine/Reset equivalents, not a required special mouse gesture. Display ranges are not physical safe limits; show installed hardware constraints separately.
- Menus: numeric/name entry consumes normal navigation/typing keys; submitting an entry does not confirm a purchase. Tab order follows visible reading order. Dialogs capture focus and restore it to the invoking action on cancel. Clicking a disabled action explains its eligibility/fitment/reservation dependency.
- Race/benches: pedal state is separate from momentary shifts and unrelated UI commands. UI refresh uses silent setters. Input cancellation on pause/focus loss clears held pointer commands; regain focus never auto-resumes. Do not globally release gas for any slider/button click.
- Bench **Keyboard** owner: operating sliders show read-only live feedback until **Use sliders** is explicitly selected. **Sliders** owner: assigned pedals/RPM fixture targets remain latched until reset or intentional drive-key takeover. **Scenario** owner: captured timeline updates feedback; manual intervention follows the existing interruption rules. Hardware, turbo, exhaust, mix, audio and test setup actions preserve the owner and held pedal.
- Retain the acoustic bench's intentional W/Space takeover even after numeric focus, Enter to leave text editing, and suppression of other shortcuts while typing. Ordinary menus and rebinding dialogs do not use that bench-specific exception. Rebinding, numeric focus and touch cancellation need native checks in their migration phases.
- Persist only definitive commands and completed preference gestures. Preview, hover, selection, graph toggles and binding refresh do not save/spend/repair. Existing full transaction/run boundaries and asynchronous race journals remain authoritative.

## Framework decision proposed for approval

Use **uGUI + the Unity Input System**, reusable Canvas prefabs/presenters and one EventSystem/input module per active presentation. Keep the authoritative simulation, inspection/pricing services, transactions and captured bench sessions independent from views. Share semantic actions but keep drive input arbitration explicit. Keyboard, controller navigation and each touch pointer must converge on the same command/input boundaries without duplicate callbacks.

This is a project choice, not a claim that UI Toolkit cannot do these screens. Canvas layout, custom graph geometry and independent pedal pointer handlers fit the race/bench needs; common cards, scroll lists, dialogs and navigation fit the same stack. Unity's [UI comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html) discusses both retained runtime options. Its [Input System UI support documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/UISupport.html) explains the UI module, pointer/navigation channels and the need to arbitrate UI versus gameplay input. The latter is essential here; installing a module alone does not protect held throttle.

4B will verify editor-compatible package versions, record installed dependencies and build the common widgets/action adapter. No versions are pinned or packages installed by this review. Keep existing IMGUI Editor tools unless separately migrated; the player-screen migration does not require rewriting every authoring tool.

## Control and workflow coverage

[SOURCE_INDEX.md](SOURCE_INDEX.md) and [control-inventory.json](control-inventory.json) provide **827 explicit UI/feedback call sites, 183 reflected editable field bindings and 41 keyboard occurrences across 30 runtime UI/input source files**. The inventory records source locations/expressions, destination phases, action context, owner/persistence/permission contracts and pending review. Reflection expansion follows `BenchFieldControls.Draw` public-field filtering/skip lists, including enum, numeric, bool, ratio-array and torque-point templates. Individual items, catalogs, cylinders, valves, tires, exhaust-chain elements, graph channels and scenarios are loops, not a fixed number of final controls.

The scanner is a conservative source inventory, **not a C# compiler or a native parity certificate**. Shared helper rows are templates and can have multiple destination phases. The workflow matrix below supplies reviewed semantic contracts; exact listeners/enable rules and dynamic expansion must still be checked against the linked source in the destination phase. Metadata is not executable permission logic. Source IDs are audit locators; 4B creates stable product action IDs independent of line numbers.

| Workflow / existing controls | Source anchor | Phase / destination | Action, ownership and persistence to preserve |
|---|---|---|---|
| Root tabs, route/back, notice, stats overlay | `PrototypeGame.DrawMeasuredGUI` / `CarStatsOverlay` | 4B shell | Navigation only; discard player previews under existing rules; no duplicate presentation listeners |
| Garage practice P / parts / tune / stats / collection / benches | `PrototypeGame.GarageGUI` | 4D overview | Operate the exact selected instance; bench launch copies installed state; shared settings are the only shared bench preferences |
| Owned instances, repeat purchase, package/REP/affordability | `CollectionGarage.CollectionGUI` | 4D cars | Selection and confirmed purchases retain independent identities; no OEM reconstruction on inspection |
| Power/torque/weight/ET summary, full bars, warning/rating disclosure | `CarStatsSummary` / `CarStatsView` | 4D/4E/4H stats | Read-only shared inspection, unavailable estimates explicit; preview has no resource writes |
| Dyno engine/shaft interpretation and healthy reference | `CarStatsView.DrawDyno` / `PartsCartGarage` | 4D/4H graph | Current/proposed/no-wear same hardware/tune; never refill or renew player condition |
| Launch/shift RPM, tire pressure, mass/ballast placement, flywheel/wheel display | `TuningGarage.TuningGUI` | 4E draft | Units and physical dimensions labelled; edits are draft until atomic Apply |
| Boost, pulley, turbo/valve kit, fixed/adjustable distinction | `TuningGarage` / `EquipmentTuningView.DrawHardware` | 4E tuning | Physical kit sizes require part swaps; only installed adjustable hardware exposes tuning |
| Cam, limiter/two-step style/threshold/cut, preload/ALS, shift light | `EquipmentTuningView.Draw` | 4E controls | ECU capability and carb/injection cut permissions remain; disabled hardware explains requirement |
| AWD center mode, torque bias/coupling controls | `AwdSettingsView.Draw` | 4E/4N drivetrain | Hardware/drive layout gated; tune draft in garage and isolated configuration in bench |
| Apply/Revert, conservative defaults, tune slots, paint preview, leave/discard | `TuningGarage` / `PartsGarage` | 4E draft | Loading/reset/paint/default preview does not publish; failed/stale Apply leaves saved car intact |
| Slider fine adjustment/reset | `TuningSlider.Draw` | 4B/4E/4N widget | Right-drag 0.1 sensitivity, middle-click default; silent feedback and other input alternatives |
| Named Engine/Whole car/Tune-only setup save/delete/load | `AssemblySetupGarage.SetupGUI` | 4E library | Blueprints contain desired definitions, not live IDs/health/fuel; save applied state |
| Setup kept/reused/missing list, owned-only Apply, buy-missing Apply | `AssemblySetupGarage.SetupGUI` | 4E library | Inspect total/errors; one validated revision-aware transaction, no automatic donor theft |
| Installed category/slot view, removal effect and missing slots | `PartsGarage.PartsGUI` | 4F installed | Required/removable equipment distinction retained; removal returns original real item |
| Failed/service summary, alternatives, same-new/spare shortcuts | `ComponentMaintenance` | 4F maintenance | Failure-first affected installed parts only; failures do not imply whole-engine destruction |
| Individual cylinder/valve/element selection, new replacement and spare reuse | `IndividualEngineView` | 4F repair | Replace selected identity at modeled unit cost; all other wear remains |
| Wet clutch oil service and real-time rebuild pay/collect/reservation | `IndividualEngineView` / `PaidOperationsGarage` | 4F service | Paid quote/deadline; exact engine locked until collection; unrelated parts remain separate |
| Add litres text/slider, fuel free-space cap, full tank and starvation | `FuelView` | 4F/4N fuel | Garage paid; physical test free; validate quantity twice, preserve momentum/input on bench refill |
| Oil/coolant top-up, change oil/filter, eligible service and auto top-up | `FluidServiceView` | 4F fluids | Paid player service; top-up only adds quantity; leaks/faults/condition remain independent |
| Storage tabs, category/all/compatible filters and actual loose IDs | `PartsCartGarage.InventoryDestinationGUI` / `PartsGarage` | 4G storage | Container view, no duplication; blank legacy locations retain Garage meaning |
| Car/stored-engine destinations, staged move/install and donor use | `PartsCartGarage` / `PartsGarage` | 4G transfers | Existing compatibility and reservation guards; preserve condition/tree/resources; donor may be incomplete |
| Individual loose units/tires, position-targeted reuse | `PartsCartGarage.InventoryUnitsGUI` | 4G spares | Role/definition match, real unit IDs; unaffected cylinders/corners keep wear/heat |
| Shop category, offers, rank/REP/fitment and detailed part stats | `PartsGarage.PartsGUI` | 4H offers | Direct compatible upgrade/downgrade; no intermediate-rank requirement; preview only |
| Complete-set default/count, optional individual role, quantity | `PartsCartGarage.OfferModeGUI` | 4H quantities | One piston set contains engine cylinder count; buy loose without picking a cylinder |
| Install checkbox, selected unit positions, extra loose units | `PartsCartGarage.CartGUI` | 4H installation | Full set swaps specification; singular reuse matches current definition/role; extras remain loose |
| Retained dealer stock inspect/buyback | `PartsCartGarage.DealerGUI` | 4H used parts | Return exact sold IDs/condition at existing directional pricing; not a rotating used-car market |
| Cart +/-/remove/clear, preview combined, charges/credits/net cash | `PartsCartGarage.CartGUI` | 4H cart | Session-only selected-car plan; no saving/spending from selections; sale credit may fund purchase |
| Confirm all, stale/quote/conflict/expired/replay/write-failure handling | `PartsCartGarage.CheckoutCartNow` | 4H commit | Existing quote/operations/receipt; publish one candidate/save or nothing |
| Detach/attach, partial engine inspection and loose-part reassembly | `AssemblySetupGarage.AssembliesGUI` | 4I engines | Actual descendants, liquids/tune/faults retained; no fabricated missing OEM hardware |
| Car/chassis/attached/stored engine/loose/element quote selection | `ValuationGarage.DrawValuation` | 4I valuation | Actual asset/buyer/condition/history/assembly demand captured |
| Refresh/validate captured quote, item adjustments, retained-part selection | `ValuationGarage` | 4I explanations | Expiry/revision/IDs/policy guards; included/kept assets explain valuation |
| Confirm sale, cancel, keep engine / chassis salvage | `PaidOperationsGarage.QuoteSaleGUI` | 4I disposal | Remove/pay quoted included assets once; keep engine subtree; incomplete/last-car sale has recovery |
| Distance/opponent/AI, event eligibility/fees, stage race | `PrototypeGame.FreeRaceGUI/CareerGUI/DifficultyGUI` | 4J selection | Installed-build restrictions and current event roster; AI separate from physics assists |
| Last pass/records/splits and telemetry export | `PrototypeGame.RaceGUI` / history | 4J/4L records | Existing available records, no invented race-history system; once-only result/settlement |
| Income claim, garage expansion, captured accrual/cap | `GarageBusinessView.BusinessGUI` | 4K business | Old-rate settlement/caps/rollback/failed-save semantics retained |
| Offer/owned-car job assignment, claim/cancel/deadline | `GarageBusinessView` | 4K jobs | Reserve exact instance/tree, no driving resource use; release and payout atomic |
| Recovery start/claim, idle claim, replacement/service paths and receipts | `PaidOperationsGarage.DrawRecovery` / `EconomyRecoveryView` | 4K recovery | Works without selected/drivable car; bounded deadlines, paid operations and actual receipts |
| W/Space/Shift/S, E/Q, Enter, touch/mouse pedals | `RaceInputAdapter` | 4L/4O/4P driving | Held inputs separate from edge commands; existing pointer-ID support is preserved, real-device verification still open |
| P/G/R/Escape, neutral staging, optional warm-up/line lock/Ready | `PrototypeGame` / domain warm-up rules | 4L flow | E engages under launch settings; warm-up physics consume resources and preserve contact state |
| RPM/gear/speed/slip/boost/shift target and installed shift feedback | `PrototypeGame.RaceGUI` | 4L HUD | Visible hardware-aware lamp adds presentation; CVT/converter and clutch/wheel slip are truthful |
| Pause/resume/audio/garage, result/retry/export/repair routes | `PrototypeGame.RaceGUI` | 4L result | Active-run journal and full boundaries retained; focus return does not auto-resume |
| Master slider, 25/50/75/100 shortcuts, opponent slider and output RMS | `AudioSettingsView.Draw` | 4C/4M/4N audio | Shared saved gains; mute/post-synthesis ramp; preference gesture completes save |
| Speed/pressure/torque unit selection | `UnitSettingsView.Draw` | 4C/4M/4N units | Same preference everywhere; boost bar/psi, outer rubber radius in inches; CSV engineering units stay fixed |
| Driving preset, launch/shifting, throttle/clutch/traction/rev-match/stall | `PrototypeGame.DrivingGUI` / `SimulationSettingsView` | 4C driving | Separate operation/AI/hardware; changing assist never grants hardware or repairs parts |
| Wear/fuel/fluid/aging/condition modes and heat/warm-up rules | `SimulationSettingsView` / `FluidServiceView` | 4C simulation | Independent None/25%/Full variants; continuous optimum disables remembered start preference only |
| Reset request/cancel/confirm | `GameReset.ResetSettingsGUI` | 4C reset | Clear progression and rebuild authored fresh baseline; retain driving/audio/units, later binding preferences |
| Sound car selector, tabs, valid draft Apply/restore | `EngineTestScene.OnGUI` | 4M setup | Copied definitions; current debounce/Apply/reset behavior retained; no player tuning Apply substituted |
| Keyboard/sliders/scenario ownership, neutral/gear/reset/pause/held pedals | `EngineTestScene` / `EngineBenchControls` | 4M operation | Keyboard Drive feedback read-only; other config editable; held gas and numeric-focus exceptions preserved |
| Fixed/target RPM, dyno load, roller speed/slip, engine/launch/failure/warmth overrides | `EngineTestScene` / reflected `controls.*` | 4M fixture | Explicit diagnostic overrides/protection; not shared road dynamics |
| Engine/cylinder/layout/displacement/torque points, core/flywheel inertia | reflected `vehicle.*`, `flywheel.*`, `sound.*` | 4M engine | All currently editable bindings indexed; authoring fields stay test-only |
| Carb/injection/cam/idle/cold/hesitation/clatter/cuts/ALS/limiter | reflected `behavior.*`, `tune.*` | 4M behavior | Existing acoustic/physical profile parameters, protected test semantics labelled |
| Single/twin turbo/blower/mixed, flow/efficiency/spool/BOV | reflected `induction.*` | 4M induction | Complete editable fields retained; changing arrangement doesn't claim pedal ownership |
| Exhaust presets, ordered add/remove/move, kind/amount/filter/resonance | `EngineTestScene.ExhaustGUI` / `part*.{field}` | 4M exhaust | Amount/attenuation/softening/resonance/Q are numeric, not presence booleans |
| Gearbox type/selector/coupling/converter/CVT, ratios and wheel specs | reflected `transmission.*`, `wheels.*`, ratios | 4M driveline fixture | Adjustable copied specs; hardware selector distinct from operation preset |
| Sound engine/accessory/idle/bass/raw/pop/gain profiles | reflected `sound.*` | 4M mix | Gain/capture reference distinct from shared master; concerts preserve/restore temporary state |
| Scenario selection/run/repeat/A/B, two-step/matched-clutch diagnostic shortcuts | `EngineTestScene.ScenariosGUI` | 4M repeatability | Same captured initial state/inputs; no real player hardware purchase |
| File path/JSON import-export/current and A/B WAV, CSV dyno export | `EngineTestScene.FilesGUI/ExportDyno` | 4M files | Explicit exports only, model/config attached; WAV exact audition vs live DSP timing distinct |
| Dyno time/RPM axis, HP/Nm/shaft toggles, run/history/clear/car view/concert | `EngineBenchDynoView` / `EngineTestScene` | 4M graph | Chronological samples and full shift/lift tail; previous runs/layers retained |
| Physical car/preset, isolated parts/category/remove, AWD/equipment rules | `PhysicalDrivingBench.DrawMeasuredGUI` | 4N setup | Same race solver, copied actual initial build; hardware changes retain declared reset semantics |
| Record OFF, scenarios/seed/replay/import/export/end/unbounded manual | `PhysicalDrivingBench` | 4N recording | Optional recording; ten-minute segments continue without resetting dynamics; user can test indefinitely |
| Keyboard/sliders, held pedals, E/Q/N/Enter/R/Escape | `PhysicalDrivingBench` | 4N operation | Persistent source, latched sliders, exact physical gear/RPM/inertia and protections |
| Graph groups/channels/history, detailed diagnostics | `PhysicalDrivingBench` / `DrivingTraceView` | 4N telemetry | Engine/shaft/contact/converter/flywheel/mass/fuel/fluid/thermal/failure channels, all tails |
| Per-part health/temperature, per-wheel life/heat, failure inspect/reset | `PhysicalDrivingBench.WearSetup` | 4N condition | Assign paused/pre-engagement; shared destructive behavior; reset wear doesn't refill fluids |
| Individual fault/health/severity/replace, wet clutch liquid/condition | `IndividualEngineView` physical partial | 4N faults | Deterministic unit consequences; other elements retained, no random intermittent faults added |
| Driver/ballast/location/height/fuel input, per-corner grip | `PhysicalBenchMass` / `PhysicalDrivingBench` | 4N mass/contact | Change real copied mass/contact inputs; momentum and pedal owner retained where currently supported |
| Fuel/top-up, oil/coolant/condition/leaks/deposits/clearance/cooldown/service | `FuelView` / `FluidServiceView` physical partial | 4N service | Free isolated operations; resource/fault/heat distinctions remain; multiplier affects wear only |

## Outstanding acceptance

4A technical checks validate the source map and the local review layouts, not actual uGUI bindings, controller/multitouch delivery, performance on Unity or sound quality. Those remain their lettered technical gates. The framework, navigation and layouts above are proposed and await explicit user validation. No unresolved game-mechanic change is required for this handoff. 4B is the next phase only after that validation.
