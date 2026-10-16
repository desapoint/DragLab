using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator PartsRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("Windows Parts controls / OS mouse events / isolated save.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            string carId = game.Profile.SelectedCar.instanceId;
            string oldId = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId;
            game.Profile.parts.Find(p => p.instanceId == oldId).condition.health = .42f;
            game.ShowParts(PartCategory.Pistons); yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForSecondsRealtime(1);
            using (var mouse = new WindowsKeyboardProbe())
            {
                string definition = game.Profile.catalog.Id("starter-hatch", PartCategory.Pistons, 1);
                int startCash = game.Profile.cash;var offer=game.Profile.catalog.Find(definition);int purchasePrice=PartsMarket.Quote(game.Profile,offer,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer)).cost;
                yield return ClickPart(mouse, "fit:" + definition);
                string forgedId = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId;
                require(game.Profile.parts.Find(p => p.instanceId == forgedId).definitionId == definition && game.Profile.cash == startCash - purchasePrice, "Actual Buy + install purchases forged pistons directly.");
                require(game.Profile.parts.Find(p => p.instanceId == oldId).condition.health == .42f && game.Profile.parts.Find(p => p.instanceId == oldId).location == PartLocation.Inventory, "Original worn pistons remain loose and unhealed.");
                yield return Capture("parts-forged-installed");
                yield return ClickPart(mouse, "remove");
                game.StartPractice(); require(game.ScreenName == "Parts", "Actual removal leaves an owned incomplete build that cannot race.");
                yield return ClickPart(mouse, "install:" + oldId);
                require(game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId == oldId, "Actual Install / reuse downgrades to the original identity.");
                int cash = game.Profile.cash;
                yield return ClickPart(mouse, "sell:" + forgedId);
                yield return ClickPart(mouse, "quote-sell-preview"); yield return ClickPart(mouse, "quote-sell-confirm"); game.ShowParts(PartCategory.Pistons); yield return new WaitForSecondsRealtime(.2f);
                require(game.Profile.parts.Find(p => p.instanceId == forgedId) == null && game.Profile.cash > cash, "Actual Sell loose removes the forged identity and credits provisional value once.");
                var reloaded = ProfileStore.Load();
                require(reloaded.SelectedCar.instanceId == carId && reloaded.parts.Find(p => p.instanceId == oldId).condition.health == .42f && reloaded.parts.Find(p => p.instanceId == forgedId) == null, "Mouse workflow persists IDs, cash and worn condition on disk.");
                yield return ClickPart(mouse, "safe");
                require(game.Profile.parts.Find(p => p.instanceId == oldId).condition.health == .42f, "Safe defaults do not repair components.");
                require(!game.CommitParts(game.Profile.revision - 1, p => p.cash += 999), "Stale runtime action rejected.");
                game.StartPractice();
                require(!game.CommitParts(game.Profile.revision, p => p.cash += 999), "Garage transaction boundary locks parts during a real active race.");
                game.ReturnToGarage(); game.ShowParts(PartCategory.Pistons); yield return Capture("parts-downgraded-reloaded");
                game.Profile.cash = 20000; ProfileStore.Save(game.Profile);
                string originalBox = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Gearbox").partInstanceId;
                game.ShowParts(PartCategory.Gearbox); yield return new WaitForSecondsRealtime(.2f);
                string sequential = game.Profile.SelectedCar.vehicleId + "/Gearbox/SequentialManual";
                yield return ClickPart(mouse, "preview:" + sequential);
                require(game.ProposedInspection.transmissionName == "Sequential manual" && game.ProposedInspection.shiftSeconds < game.SavedInspection.shiftSeconds, "Shop preview names sequential manual hardware and shows reduced shift time.");
                yield return Capture("parts-sequential-preview");
                yield return ClickPart(mouse, "fit:" + sequential);
                game.StartPractice();
                require(game.Race.player.transmission.type == TransmissionType.Manual && game.Race.player.transmission.manualLayout == ManualGearboxLayout.Sequential, "Actual Buy + install carries sequential manual hardware into the real race.");
                game.ReturnToGarage(); game.ShowParts(PartCategory.Gearbox); yield return new WaitForSecondsRealtime(.2f);
                var savedGearbox = ProfileStore.Load();
                require(MechanicsOwnership.Resolve(savedGearbox, savedGearbox.SelectedCar, game.Profile.catalog).spec.transmission.manualLayout == ManualGearboxLayout.Sequential, "Sequential gearbox identity and selector behavior persist on disk.");
                yield return ClickPart(mouse, "install:" + originalBox);
                require(game.SavedInspection.transmissionName == "H-pattern manual", "Reusing the retained original gearbox restores H-pattern behavior.");
                string model = game.Profile.SelectedCar.vehicleId;
                string originalInduction = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Induction").partInstanceId;
                game.ShowParts(PartCategory.Induction); yield return new WaitForSecondsRealtime(.2f);
                string fixedTurbo = model + "/Induction/TurboLargeFixed";
                yield return ClickPart(mouse, "preview:" + fixedTurbo);
                require(game.ProposedInspection.source.build.tune.TurboSize == 1.4f && !game.ProposedInspection.source.build.tune.partBoostAdjustable, "New large fixed turbo preview derives its physical size and tuning restrictions from the selected part.");
                yield return Capture("parts-fixed-turbo-preview");
                yield return ClickPart(mouse, "fit:" + fixedTurbo);
                game.OpenTuning(); yield return new WaitForSecondsRealtime(.25f);
                require(!game.tuneRects.ContainsKey("turbo-size") && !game.tuneRects.ContainsKey("boost"), "Fixed turbo tuning page has no physical-size or boost slider.");
                game.ReturnToGarage(); game.ShowParts(PartCategory.Induction); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse, "fit:" + model + "/Induction/TurboLargeAdjustable");
                game.OpenTuning(); yield return new WaitForSecondsRealtime(.25f);
                require(game.tuneRects.ContainsKey("boost") && !game.tuneRects.ContainsKey("turbo-size"), "Adjustable kit unlocks boost only; physical turbo size remains a hardware choice.");
                float savedBoost = game.Profile.SelectedCar.tune.boostSetting;
                game.ScrollTuningTo("boost"); yield return new WaitForSecondsRealtime(.2f);
                yield return MoveTuneSlider(mouse, "boost", .9f);
                yield return Capture("parts-adjustable-boost-draft");
                require(game.DraftDirty && game.DraftValues.boostSetting > 1.1f && game.Profile.SelectedCar.tune.boostSetting == savedBoost, "Actual boost slider edits preview only until Apply. [dirty=" + game.DraftDirty + ", draft=" + game.DraftValues.boostSetting + ", saved=" + game.Profile.SelectedCar.tune.boostSetting + ", baseline=" + savedBoost + "]");
                game.ScrollTuningTo("apply-top"); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickTune(mouse, "apply-top");
                game.StartPractice();
                require(game.Race.player.tune.boostSetting > 1.1f && game.Race.player.tune.TurboSize == 1.4f, "Applied adjustable boost and fitted turbo size carry into actual race physics.");
                game.ReturnToGarage(); game.ShowParts(PartCategory.Induction); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse, "install:" + originalInduction);
                require(MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, game.Profile.catalog).tune.TurboSize == 1, "Reinstalling retained OEM induction restores factory physical sizing.");
                // Seed an owned carbureted car; every subsequent conversion action uses the real shop controls.
                var carbSpec=game.Profile.catalog.Spec("v8-muscle");
                var carbCar=new OwnedCar{vehicleId="v8-muscle",tune=new Tune{finalDrive=carbSpec.finalDrive,shiftRpm=carbSpec.redlineRpm-200}};
                game.Profile.cars.Add(carbCar);game.Profile.cash=100000;MechanicsOwnership.Ensure(game.Profile,game.Profile.catalog);
                game.Profile.selectedVehicleId=carbCar.vehicleId;game.Profile.selectedOwnedVehicleId=carbCar.instanceId;game.ReturnToGarage();
                string originalCarb=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="FuelSystem").partInstanceId;
                game.ShowParts(PartCategory.FuelSystem);yield return new WaitForSecondsRealtime(.2f);
                string efi="v8-muscle/FuelSystem/EfiThrottleBody";
                yield return ClickPart(mouse,"preview:"+efi);
                require(game.ProposedInspection.fueling=="Injection" && !game.ProposedInspection.source.build.Drivable && game.ProposedInspection.source.build.errors.Exists(e=>e.Contains("injection-capable")),"EFI shop preview shows injection with explicit missing-controller warning.");
                require(game.StatsSummaryForVerification.Any(s=>s.Contains("Measured wheelspin") && s.Contains("Unavailable")),"Blocked EFI conversion preview reports unavailable wheelspin rather than a false traction improvement.");
                yield return Capture("parts-efi-kit-preview");
                yield return ClickPart(mouse,"fit:"+efi);game.StartPractice();
                require(game.ScreenName=="Parts","Installed conversion cannot race on carb mechanical controls.");
                game.ShowParts(PartCategory.Controls);yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"fit:v8-muscle/Controls/EfiStreet");
                yield return Capture("parts-efi-controller-installed");
                require(game.SavedInspection.fueling=="Injection" && game.SavedInspection.source.build.Drivable,"Actual EFI controller purchase completes runnable injection conversion. [fueling="+game.SavedInspection.fueling+"; errors="+string.Join("; ",game.SavedInspection.source.build.errors)+"]");
                game.StartPractice();
                require(game.ScreenName=="Race" && game.Race.player.engine.data.fueling==FuelSystem.Injection && game.Race.player.state.engineSupplyAvailable && game.Race.player.spec.sound.cylinders==8,"EFI shop purchases reach real race physics with V8 engine character.");
                game.ReturnToGarage();var efiSave=ProfileStore.Load();
                require(MechanicsOwnership.Resolve(efiSave,efiSave.SelectedCar,game.Profile.catalog).spec.engineBehavior.fueling==FuelSystem.Injection && efiSave.parts.Find(p=>p.instanceId==originalCarb).location==PartLocation.Inventory,"EFI hardware and retained original carb persist on disk.");
                game.ShowParts(PartCategory.FuelSystem);yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"install:"+originalCarb);
                require(game.SavedInspection.fueling=="Carburetor","Actual retained-carb Install restores carbureted behavior.");
                // Seed isolated service faults; use real OS mouse actions for every repair/top-up.
                var owned=game.Profile.SelectedCar;owned.fluids.oilLitres=1;owned.fluids.coolantLitres=2;owned.fluids.oilCondition=.3;
                string radiator=owned.installed.slots.Find(b=>b.slot=="Cooling").partInstanceId;
                game.Profile.parts.Find(p=>p.instanceId==radiator).condition.leakSeverity=.6;
                game.ShowRepairs();yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"fluids-fill");
                owned=game.Profile.SelectedCar;
                require(owned.fluids.oilLitres==5 && owned.fluids.coolantLitres==7.5 && owned.fluids.oilCondition==.3,"Actual fluid top-up fills installed capacities without renewing aged oil.");
                require(game.Profile.parts.Find(p=>p.instanceId==radiator).condition.leakSeverity==.6,"Top-up leaves the radiator leak fault intact.");
                yield return ClickPart(mouse,"service:Reseal:"+radiator);
                require(game.Profile.parts.Find(p=>p.instanceId==radiator).condition.leakSeverity==0 && game.Profile.parts.Find(p=>p.instanceId==radiator).condition.health==1,"Actual reseal action repairs only the radiator leak.");
                string oilFilter=game.Profile.SelectedCar.installed.slots.Find(b=>b.slot=="OilFilter").partInstanceId;
                game.Profile.parts.Find(p=>p.instanceId==oilFilter).condition.deposits=.8;
                yield return ClickPart(mouse,"oil-change");
                require(game.Profile.SelectedCar.fluids.oilCondition==1 && game.Profile.parts.Find(p=>p.instanceId==oilFilter).location==PartLocation.Inventory && game.Profile.parts.Find(p=>p.instanceId==oilFilter).condition.deposits==.8,"Actual oil/filter service renews oil and retains the used filter.");
                yield return Capture("parts-fluids-serviced");
                game.ShowParts(PartCategory.Camshaft);yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"preview:v8-muscle/Camshaft/2");
                require(game.ProposedInspection.warnings.Any(w=>w.title.Contains("Cam needs stronger")),"Race cam preview warns when factory springs cannot control its profile.");
                yield return Capture("parts-cam-spring-warning");
                yield return ClickPart(mouse,"fit:v8-muscle/Camshaft/2");
                game.ShowParts(PartCategory.ValveSprings);yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"fit:v8-muscle/ValveSprings/1");
                game.StartPractice();
                require(game.Race.player.spec.valvetrain.camTorqueBias>.1 && game.Race.player.spec.valvetrain.springSafeRpm>game.Race.player.spec.valvetrain.profileDemandRpm && game.Race.player.state.fluids.oilLitres==5,"Installed cam/spring hardware and serviced fluids reach the same race solver.");
                game.ReturnToGarage();var serviceSave=ProfileStore.Load();
                require(serviceSave.SelectedCar.fluids.oilCondition>.99 && serviceSave.parts.Find(p=>p.instanceId==radiator).condition.leakSeverity==0,"Fluid service and repair history persist after race/garage save.");
                // These faults/deadline are seeded only in the fixture's isolated profile.
                var elementCar=game.Profile.SelectedCar;string pistonSet=elementCar.installed.slots.Find(b=>b.slot=="Pistons").partInstanceId;var pistonItem=game.Profile.parts.Find(p=>p.instanceId==pistonSet);string element=pistonItem.condition.elements[0].id,other=pistonItem.condition.elements[1].id;pistonItem.condition.elements[0].health=.2f;EngineElementService.RefreshAggregate(pistonItem.condition);game.ShowParts(PartCategory.Pistons);yield return new WaitForSecondsRealtime(.2f);
                int elementCash=game.Profile.cash,unitCost=PartsMarket.Quote(game.Profile,game.Profile.catalog.Find(pistonItem.definitionId),pistonItem.condition.elements.Length,true).cost;yield return ClickPart(mouse,"element-new:"+element);
                var renewed=game.Profile.parts.Find(p=>p.instanceId==pistonSet);require(game.Profile.cash==elementCash-unitCost&&renewed.condition.elements[0].health==1&&renewed.condition.elements[1].id==other,"Actual individual-piston button charges unit cost and retains neighbouring parts.");require(game.Profile.looseEngineElements.Any(p=>p.element.id==element&&p.element.health==.2f),"Removed individual piston retains its identity and wear.");yield return Capture("parts-individual-piston");
                yield return ClickPart(mouse,"element-sell:"+element);yield return ClickPart(mouse,"quote-sell-preview");yield return ClickPart(mouse,"quote-sell-confirm");game.ShowParts(PartCategory.Clutch);yield return new WaitForSecondsRealtime(.2f);require(!game.Profile.looseEngineElements.Any(p=>p.element.id==element),"Actual individual inventory sale removes one retained piston.");
                game.ShowParts(PartCategory.Clutch);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"fit:v8-muscle/Clutch/WetMultiOrganic");game.StartPractice();require(game.Race.player.mechanics.Definition(game.Race.player.mechanics.Part(PartCategory.Clutch)).clutchEnvironment==ClutchEnvironment.Wet&&game.Race.player.mechanics.Part(PartCategory.Clutch).condition.clutchOilLitres==2,"Actual wet multi-plate purchase reaches shared race coupling with dedicated oil bath.");game.ReturnToGarage();game.ShowRepairs();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"engine-rebuild-start");
                var shop=game.Profile.SelectedCar.engineRebuild;require(shop!=null&&shop.active&&shop.readyUtcTicks>DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,"Actual paid rebuild button creates persisted timed reservation.");game.StartPractice();require(game.ScreenName=="Parts","Reserved engine cannot start a real race.");yield return Capture("parts-engine-rebuild-waiting");
                shop.startedUtcTicks=DragLab.Domain.RuntimeEntropy.UtcNow.AddMinutes(-30).Ticks;shop.readyUtcTicks=DragLab.Domain.RuntimeEntropy.UtcNow.AddSeconds(-1).Ticks;ProfileStore.Save(game.Profile);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"engine-rebuild-collect");require(game.Profile.SelectedCar.engineRebuild==null&&ProfileStore.Load().SelectedCar.engineRebuild==null,"Actual Collect completes overdue fixture job exactly once and persists availability.");yield return Capture("parts-engine-rebuild-collected");


            }
            require(errors == 0, "Parts UI has no runtime/GUI errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Parts UI probe requires Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator ClickPart(WindowsKeyboardProbe mouse, string key)
        {
            if(game.ScreenName=="Parts") {
                if((key.StartsWith("install:")||key.StartsWith("preview-item:")||key.StartsWith("sell:")||key.StartsWith("part-quote:"))&&game.PartsPage!="Inventory")game.SelectPartsPage("Inventory");
                else if((key.StartsWith("buy:")||key.StartsWith("fit:")||key.StartsWith("preview:"))&&game.PartsPage!="Shop")game.SelectPartsPage("Shop");
                yield return new WaitForEndOfFrame();
            }
            game.ScrollToPart(key); yield return new WaitForSecondsRealtime(.12f);
            yield return new WaitForEndOfFrame();
            Rect rect; Check(game.partRects.TryGetValue(key, out rect) && rect.height > 0 && rect.center.y >= 125 && rect.center.y < Screen.height - 70, "Visible part control: " + key);
            File.AppendAllText(Path.Combine(output, "mouse-controls.txt"), key + " / " + rect + " / " + Screen.width + "x" + Screen.height + "\n");
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.25f);
            // Transactions run in Update; derived inspection is refreshed by OnGUI.
            // Assertions inspect the rendered committed state, regardless of frame cost.
            yield return new WaitForEndOfFrame();
            if(game.ScreenName=="Parts"||game.ScreenName=="Tuning"||game.ScreenName=="Garage"||game.ScreenName=="Stats")yield return WaitForInspection();
        }
#endif
    }
}
