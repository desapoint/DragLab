using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class PartHardwareVerification
    {
        public static void RunStandalone() { Debug.Log("Part hardware checks: " + Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../..")))); }
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder(); var csv = new StringBuilder("vehicle,kit,rpm,torque_nm,hp,boost_bar,spool_02s\n");
            Action<bool,string> check = (ok,label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var catalog = MechanicsTransactions.Catalog();
            check(catalog.parts.Select(p => p.id).Distinct().Count() == catalog.parts.Count, "Every catalog ID is unique; old IDs remain addressable.");
            foreach (var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                string id = v.spec.id; var map = catalog.Recipe(id);
                foreach (var p in catalog.parts.Where(p => catalog.Fits(v.spec,p))) {
                    check(!string.IsNullOrWhiteSpace(PartTuning.Summary(p)) && PartSemantics.Stats(p).All(s => MechanicsSchema.Finite(s.value)), id + "/" + p.id + ": tuning permissions and stats are explicit and finite.");
                    var m = catalog.Recipe(id); m[p.category] = p.id; var b = catalog.Resolve(id,m,v.CreateStockTune());
                    if(p.fuelDelivery!=FuelDeliveryHardware.Factory) { m[PartCategory.Controls]=id+"/Controls/EfiStreet"; b=catalog.Resolve(id,m,v.CreateStockTune()); }
                    if(!b.Drivable&&b.errors.Any(e=>e.Contains("followers"))){check(true,id+"/"+p.id+": mismatched cam/followers rejected explicitly.");continue;}
                    var car = new CarSimulation(b.spec,b.tune,new DriverSettings { wearMode = WearMode.None, throttleAssist = false }); car.Launch(0);
                    for (int t = 0; t < 360; t++) car.Step(DragMath.FixedStep,new DriverInput { throttle = 1 });
                    check(b.Drivable && b.spec.Validate() == null && MechanicsSchema.Finite(car.state.rpm) && MechanicsSchema.Finite(car.state.speed) && MechanicsSchema.Finite(car.state.engineTorqueNm), id + "/" + p.id + ": compatible hardware runs through shared physical simulation.");
                }
                foreach (var topology in new[] { EngineUpgradeInduction.Turbo, EngineUpgradeInduction.TwinTurbo, EngineUpgradeInduction.Twincharged }) {
                    BuildResolution small = null, large = null;
                    foreach (string size in new[] { "Compact", "Street", "Large" }) {
                        string key = id + "/Induction/" + topology + size;
                        map[PartCategory.Induction] = key + "Fixed";
                        var hostile = v.CreateStockTune(); hostile.turboSize = 1.6f; hostile.boostSetting = 1.2f; hostile.superchargerPulley = 1.3f; hostile.blowOffValve = BlowOffType.Flutter;
                        // Pretending to own capabilities in a saved tune cannot grant them.
                        hostile.partsResolved = hostile.partTuningConfigured = hostile.partBoostAdjustable = hostile.partPulleyAdjustable = true;
                        var fixedBuild = catalog.Resolve(id,map,hostile); var part = catalog.Find(key + "Fixed");
                        check(fixedBuild.tune.TurboSize == part.turboSize && Math.Abs(fixedBuild.tune.BoostSetting - .8) < 1e-6 && fixedBuild.tune.SuperchargerPulley == 1 && fixedBuild.tune.blowOffValve == BlowOffType.Recirculated && !fixedBuild.tune.partBoostAdjustable, key + ": fixed hardware ignores old sliders, presets and forged capability flags.");
                        fixedBuild.tune.turboSize = 1.6f; fixedBuild.tune.boostSetting = 1.2f; fixedBuild.tune.ClampTo(fixedBuild.spec);
                        check(fixedBuild.tune.turboSize == part.turboSize && fixedBuild.tune.boostSetting == part.fixedBoost, key + ": direct race/snapshot clamp keeps physical dimensions and fixed boost.");
                        if (size == "Compact") small = fixedBuild; if (size == "Large") large = fixedBuild;
                        map[PartCategory.Induction] = key + "Adjustable"; hostile.boostSetting = .35f; hostile.superchargerPulley = .75f;
                        var adjustable = catalog.Resolve(id,map,hostile);
                        check(adjustable.tune.partBoostAdjustable && adjustable.tune.boostSetting == .8f && adjustable.tune.turboSize == part.turboSize && adjustable.tune.blowOffValve == BlowOffType.Flutter, key + ": adjustable boost respects the spring floor; size stays physical and valve is configurable.");
                        hostile.boostSetting = 2; hostile.superchargerPulley = 2; var upper = catalog.Resolve(id,map,hostile);
                        check(upper.tune.boostSetting == 1.2f && upper.tune.SuperchargerPulley <= 1.2 + 1e-6, key + ": installed boost/pulley upper bounds are enforced.");
                        var profile = new InductionProfile(fixedBuild.spec,fixedBuild.tune); var rotor = new CarState { rpm = v.spec.redlineRpm - 100 };
                        InductionDynamics.Step(profile,rotor,1,1,false,false,.2);
                        for (int rpm = 1500; rpm <= v.spec.redlineRpm; rpm += 500) {
                            var output = InductionDynamics.SteadyOutput(profile,rpm);
                            csv.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R}",id,topology+size,rpm,output.engineTorqueNm,output.engineHorsepower,output.boostBar,rotor.turboSpool));
                        }
                    }
                    var a = new InductionProfile(small.spec,small.tune); var z = new InductionProfile(large.spec,large.tune);
                    double testRpm = a.data.turboThresholdRpm + 700;
                    check(a.TurboEnvelope(testRpm) > z.TurboEnvelope(testRpm), id + "/" + topology + ": compact hardware develops boost earlier in RPM envelope.");
                    var sa = new CarState { rpm = v.spec.redlineRpm - 100 }; var sz = new CarState { rpm = sa.rpm };
                    InductionDynamics.Step(a,sa,1,1,false,false,.2); InductionDynamics.Step(z,sz,1,1,false,false,.2);
                    check(sa.turboSpool > sz.turboSpool && a.data.turboUnitFlowKgS * a.size < z.data.turboUnitFlowKgS * z.size, id + "/" + topology + ": compact transient is faster; large unit has greater airflow capacity.");
                }
                foreach (var topology in new[] { EngineUpgradeInduction.Supercharger, EngineUpgradeInduction.TwinSupercharger }) {
                    map[PartCategory.Induction] = id + "/Induction/" + topology + "FixedDrive";
                    var knobs = new Tune { superchargerPulley = 1.3f,boostSetting = .35f };
                    var fixedSc = catalog.Resolve(id,map,knobs);
                    check(Math.Abs(fixedSc.tune.SuperchargerPulley - .9) < 1e-6 && fixedSc.tune.BoostSetting == 1 && !fixedSc.tune.partPulleyAdjustable, id + "/" + topology + ": fixed street blower has no electronic boost or pulley tuning.");
                    map[PartCategory.Induction] = id + "/Induction/" + topology + "PulleySet";
                    var adjustableSc = catalog.Resolve(id,map,knobs);
                    check(adjustableSc.tune.partPulleyAdjustable && adjustableSc.tune.superchargerPulley == 1.2f && !adjustableSc.tune.partBoostAdjustable, id + "/" + topology + ": pulley set unlocks drive ratio only.");
                }
                map = catalog.Recipe(id); var altered = new Tune { finalDrive = 2 };
                check(catalog.Resolve(id,map,altered).tune.finalDrive == v.spec.finalDrive, id + ": OEM fixed gearset cannot be rewritten by a saved ratio.");
                map[PartCategory.Gearbox] = id + "/Gearbox/ShortFixed";
                check(catalog.Resolve(id,map,altered).tune.finalDrive == catalog.Find(map[PartCategory.Gearbox]).fixedFinalDrive, id + ": fixed short gearset sets real wheel gearing.");
                map[PartCategory.Gearbox] = id + "/Gearbox/Interchangeable";
                var gears = catalog.Resolve(id,map,altered); check(gears.tune.partFinalDriveAdjustable && gears.tune.finalDrive == 2.5f, id + ": included gearsets enforce their range independently of stage.");
                var player = PlayerProfile.New(DateTime.UtcNow.Ticks); player.cars.Clear(); player.cars.Add(new OwnedCar { vehicleId = id,tune = v.CreateStockTune() }); player.selectedVehicleId = id; player.cash = 100000;
                MechanicsOwnership.Ensure(player,catalog); var owned = player.SelectedCar;
                string original = owned.installed.slots.Find(s => s.slot == "Induction").partInstanceId;
                string purchase = PartOperations.Buy(player,catalog,owned.instanceId,id + "/Induction/TurboLargeFixed",true);
                var draft = new TuneDraft(owned,player.revision); draft.values.turboSize = .7f; draft.values.boostSetting = 1.2f; draft.ApplyTo(player);
                check(owned.tune.turboSize == 1.4f && owned.tune.boostSetting == .8f && player.parts.Find(p => p.instanceId == original).location == PartLocation.Inventory, id + ": Apply sanitizes disallowed values and installation retains original hardware.");
                var saved = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(player)); MechanicsOwnership.Ensure(saved,catalog);
                check(MechanicsOwnership.Resolve(saved,saved.SelectedCar,catalog).tune.TurboSize == 1.4f && saved.parts.Any(p => p.instanceId == purchase), id + ": new physical part identity and fixed settings survive save/reload.");
                PartOperations.Install(saved,catalog,saved.SelectedCar.instanceId,original);
                check(MechanicsOwnership.Resolve(saved,saved.SelectedCar,catalog).tune.TurboSize == 1, id + ": reusing OEM restores OEM compressor size, without free upgrades.");
                var capture = new RunSnapshot { vehicle = gears.spec,tune = gears.tune,driving = new DriverSettings { wearMode = WearMode.None } };
                var session = new PhysicalDrivingSession(capture);
                for (int i=0;i<360;i++) session.Step(new TickInput { tick = session.Tick,input = new DriverInput { throttle = 1 },neutral = i<60 });
                var replay = new PhysicalDrivingSession(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot)));
                foreach (var input in session.snapshot.inputs) replay.Step(input);
                if(session.Csv()!=replay.Csv()){Directory.CreateDirectory(Path.Combine(root,"reports/service-fluids"));File.WriteAllText(Path.Combine(root,"reports/service-fluids/original.csv"),session.Csv());File.WriteAllText(Path.Combine(root,"reports/service-fluids/replay.csv"),replay.Csv());}
                check(session.Csv() == replay.Csv(), id + ": hardware permissions/settings are captured and replay exactly in shared physical bench.");
                var rawTune = new Tune { turboSize = 1.6f, boostSetting = .4f,superchargerPulley = 1.3f,engineLevel=1,inductionUpgrade=EngineUpgradeInduction.Twincharged };
                var raw = new EngineTestBench(v.spec,rawTune);
                check(raw.tune.TurboSize == 1.6f && raw.tune.boostSetting == .4f && raw.tune.superchargerPulley == 1.3f, id + ": unrestricted raw sound-engineering bench still supports arbitrary A/B configurations.");
            }
            string folder=Path.Combine(root,"reports/part-hardware"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"verification.txt"),log + "Checks: " + count + "\nCatalog definitions: " + catalog.parts.Count + "\n");
            File.WriteAllText(Path.Combine(folder,"turbo-curves.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(folder,"catalog.md"),string.Join("\n\n",catalog.parts.Select(p => "### " + p.id + "\n" + p.name + "\n\n" + p.description + "\n\n" + PartTuning.Summary(p))));
            return count;
        }
    }
}
