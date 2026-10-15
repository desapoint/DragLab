using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DriveType = DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    // Deliberately collects failures rather than stopping at the first configuration.
    // Uses the production simulation and copied parts; never loads a player save.
    public static class SliceTwoAudit
    {
        static readonly FieldInfo[] Numbers = typeof(CarState).GetFields().Where(f => f.FieldType == typeof(double)).ToArray();
        public static void Run() { RunVerified(); }
        public static void ChangedCases(){RunVerified(true);}
        public static int RunVerified(){return RunVerified(false);}
        static int RunVerified(bool changedOnly)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/slice-2-audit"));
            if(changedOnly)folder=Path.Combine(folder,"changed-cases-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var log = new StringBuilder(); var rows = new StringBuilder("car,configuration,scenario,seconds,speed_kmh,rpm,gear,engine_failed,drive_failed,brakes_failed,min_health,failed_parts\n");
            var partRows = new StringBuilder("car,part_id,name,category,quality,stats,build_ready,peak_hp,internal_safe_nm,internal_safe_rpm\n");
            int checks = 0, failures = 0, runs = 0; double seconds = 0;
            Action<bool, string> check = (ok, label) => { checks++; if (!ok) failures++; log.AppendLine((ok ? "PASS " : "FAIL ") + label); };
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v => v.spec.id).ToArray();
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            Action<CarSimulation, string, string, int> exercise = (car, configuration, scenario, ticks) =>
            {
                bool finite = true;
                for (int tick = 0; tick < ticks; tick++)
                {
                    double t = tick * DragMath.FixedStep;
                    bool neutral = scenario == "neutral-blips" || scenario == "staging" || scenario == "idle";
                    double throttle = scenario == "idle" ? 0 : scenario == "neutral-blips" ? (t % 4 < 1 ? .7 : 0) : scenario == "mixed-pedals" ? (t % 8 < 4 ? 1 : 0) : 1;
                    var input = new DriverInput { throttle = throttle, brake = scenario == "mixed-pedals" && t % 8 >= 6 ? .7 : 0 };
                    car.IndependentStep(DragMath.FixedStep, input, neutral, scenario == "staging");
                    finite &= car.state.contact.Validate()==null && Numbers.All(f => MechanicsSchema.Finite((double)f.GetValue(car.state))) && car.mechanics.parts.All(p => p.condition.Validate() == null);
                    if (!finite) break;
                }
                runs++; seconds += ticks * DragMath.FixedStep;
                check(finite && car.state.rpm >= 0 && car.state.speed >= 0, car.spec.id + " / " + configuration + " / " + scenario + " finite state and valid conditions");
                var s = car.state;
                rows.AppendLine(string.Join(",", car.spec.id, Csv(configuration), scenario, F(ticks * DragMath.FixedStep), F(s.speed * 3.6), F(s.rpm), s.gear, s.engineFailed, s.drivetrainFailed, s.brakesFailed, F(car.mechanics.parts.Min(p => p.condition.health)), Csv(string.Join(";", car.mechanics.parts.Where(p => p.condition.failed).Select(p => p.slot + ":" + p.condition.failureReason)))));
            };
            foreach (var v in vehicles)
            {
                var stock = catalog.Resolve(v.spec.id, catalog.Recipe(v.spec.id), v.CreateStockTune());
                if(!changedOnly){
                foreach (var p in catalog.parts.Where(p => catalog.Fits(v.spec, p) && MechanicalCatalog.Slots(v.spec).Contains(p.category)))
                {
                    var map = catalog.Recipe(v.spec.id); map[p.category] = p.id;
                    var build = catalog.Resolve(v.spec.id, map, v.CreateStockTune());
                    if(!CombustionHardware.Diesel(v.spec)&&p.fuelDelivery!=FuelDeliveryHardware.Factory&&!build.Drivable){
                        check(build.errors.Any(e=>e.Contains("injection-capable")),"EFI alone requires a compatible controller: "+p.id);
                        map[PartCategory.Controls]=v.spec.id+"/Controls/EfiStreet";
                        build=catalog.Resolve(v.spec.id,map,v.CreateStockTune());
                    }
                    if(!build.Drivable&&build.errors.Any(e=>e.Contains("followers"))){
                        check(true,"Mismatched cam/followers are explicitly rejected: "+v.spec.id+" / "+p.id);
                        map[PartCategory.Camshaft]=v.spec.id+"/Camshaft/RollerStreet";
                        map[PartCategory.Followers]=v.spec.id+"/Followers/RollerConversion";
                        build=catalog.Resolve(v.spec.id,map,v.CreateStockTune());
                    }
                    check(build.Drivable, "Compatible replacement or required matched pair resolves: " + v.spec.id + " / " + p.id);
                    check(!p.name.Contains("option "), "Part has an explicit functional name: " + p.id);
                    double hp = Enumerable.Range(0, 31).Select(i => { double rpm = build.spec.idleRpm + (build.spec.redlineRpm - build.spec.idleRpm) * i / 30; return DragMath.Torque(build.spec, build.tune, rpm) * rpm / 7127; }).Max();
                    partRows.AppendLine(string.Join(",", v.spec.id, p.id, Csv(p.name), p.category, Csv(p.quality), Csv(string.Join(";", PartSemantics.Stats(p).Select(s => s.label + "=" + F(s.value) + s.unit))), build.Drivable, F(hp), F(build.safeTorqueNm), F(build.safeRpm)));
                    if (build.Drivable) exercise(new CarSimulation(build.spec, build.tune, Driver()), p.id, "mixed-pedals", 1440);
                }
                foreach (TransmissionUpgrade gearbox in Enum.GetValues(typeof(TransmissionUpgrade)))
                foreach (EngineUpgradeInduction induction in Enum.GetValues(typeof(EngineUpgradeInduction)))
                foreach (WearMode wear in Enum.GetValues(typeof(WearMode)))
                {
                    var map = catalog.Recipe(v.spec.id);
                    if (gearbox != TransmissionUpgrade.Factory) map[PartCategory.Gearbox] = v.spec.id + "/Gearbox/" + gearbox;
                    if (induction != EngineUpgradeInduction.Factory) map[PartCategory.Induction] = v.spec.id + "/Induction/" + induction;
                    var build = catalog.Resolve(v.spec.id, map, v.CreateStockTune()); var driver = Driver(); driver.wearMode = wear;
                    var car = new CarSimulation(build.spec, build.tune, driver);
                    exercise(car, gearbox + "/" + induction + "/" + wear, "mixed-pedals", 2880);
                    if (wear == WearMode.None) check(car.mechanics.parts.All(p => p.condition.health == 1 && !p.condition.failed), "None wear prevents new damage: " + v.spec.id + "/" + gearbox + "/" + induction);
                }
                foreach (string scenario in new[] { "idle", "neutral-blips", "staging", "mixed-pedals" })
                    exercise(new CarSimulation(stock.spec, stock.tune, Driver()), "OEM", scenario, 7200);
                // Disconnecting the engine must not heat or wear stationary downstream shafts.
                var disconnected = new CarSimulation(stock.spec, stock.tune, Driver());
                for (int i = 0; i < 2400; i++) disconnected.IndependentStep(DragMath.FixedStep, new DriverInput { throttle = i % 480 < 120 ? .65 : 0 }, true, false);
                check(disconnected.mechanics.parts.Where(p => p.slot == "Driveshaft" || p.slot == "Differential" || MechanicalCatalog.DriveBranch(catalog.Find(p.definitionId).category)).All(p => p.condition.health == 1 && Math.Abs(p.condition.temperatureC - 20) < 1e-6), "Stationary neutral revving cannot heat/wear downstream drive paths: " + v.spec.id);
                }
                // Flags must be authoritative even if a test sets positive health.
                foreach (var original in new CarSimulation(stock.spec, stock.tune, Driver()).mechanics.parts)
                {
                    var car = new CarSimulation(stock.spec, stock.tune, Driver()); var bad = car.mechanics.parts.Find(p => p.slot == original.slot);
                    if(original.slot=="Tires"){foreach(var tire in bad.condition.tires){tire.failed=true;tire.failureReason="Audit injected failure";}TireDynamics.SyncSummary(bad.condition);}
                    else {bad.condition.failed = true; bad.condition.failureReason = "Audit injected failure";}
                    car.mechanics.Prepare(car.state);
                    exercise(car, "failed:" + original.slot, "mixed-pedals", 120);
                    if (original.slot == "Brakes") check(car.state.brakesFailed && !car.state.engineFailed && !car.state.drivetrainFailed, "Brake-only failure preserves combustion and drive: " + v.spec.id);
                    if (original.slot == "Driveshaft" || original.slot == "Gearbox") check(car.state.drivetrainFailed && !car.state.engineFailed, "Drive-only failure preserves engine: " + v.spec.id + "/" + original.slot);
                    check(original.slot=="Tires"?bad.condition.tires.All(t=>t.Failed&&t.failureReason=="Audit injected failure")&&bad.condition.health==1&&!bad.condition.failed:bad.condition.failed && bad.condition.failureReason == "Audit injected failure", "Existing failed condition is retained: " + v.spec.id + "/" + original.slot);
                }
                if(!changedOnly){
                var noWear = new CarSimulation(stock.spec, stock.tune, new DriverSettings { wearMode = WearMode.None });
                var failedPiston = noWear.mechanics.parts.Find(p => p.slot == "Pistons"); failedPiston.condition.failed = true; failedPiston.condition.failureReason = "Existing piston failure";
                noWear.IndependentStep(DragMath.FixedStep, new DriverInput(), true, false);
                check(noWear.state.engineFailed && noWear.mechanics.parts.Where(p => p.slot != "Pistons").All(p => p.condition.health == 1 && !p.condition.failed), "Existing failed internal does not destroy other parts with wear disabled: " + v.spec.id);
                foreach (double radius in new[] { .8, 1.6 })
                foreach (int flywheel in new[] { 0, 1 })
                {
                    var map = catalog.Recipe(v.spec.id); map[PartCategory.Flywheel] = catalog.Id(v.spec.id, PartCategory.Flywheel, flywheel);
                    var tune = v.CreateStockTune(); tune.wheelDiameter = (float)radius;
                    var build = catalog.Resolve(v.spec.id, map, tune);
                    exercise(new CarSimulation(build.spec, build.tune, Driver()), "radius-scale:" + radius + "/flywheel:" + flywheel, "mixed-pedals", 3600);
                }
                }
                foreach (AwdMode mode in Enum.GetValues(typeof(AwdMode)))
                {
                    if (v.spec.drive != DriveType.AWD) continue;
                    var map=catalog.Recipe(v.spec.id);string key=mode==AwdMode.FixedSplit?"FixedSplit35":mode==AwdMode.OpenCenter?"OpenCenter":mode==AwdMode.LimitedSlipCenter?"AdjustableLSD":mode==AwdMode.LockedCenter?"SwitchableLock":mode==AwdMode.OnDemand?"OnDemandController":"ActiveController";
                    map[PartCategory.Differential]=v.spec.id+"/Differential/"+key;var tune=v.CreateStockTune();tune.awdConfigured=true;tune.awd=v.spec.awd.Copy();tune.awd.mode=mode;
                    var build=catalog.Resolve(v.spec.id,map,tune);var car=new CarSimulation(build.spec,build.tune,Driver());
                    check(build.Drivable&&car.awd.mode==mode,"Requested AWD scenario uses matching installed hardware: "+v.spec.id+" / "+mode);
                    exercise(car, "AWD:" + mode, "mixed-pedals", 3600);
                }
            }
            File.WriteAllText(Path.Combine(folder, "verification.txt"), "Model: " + MechanicsSchema.Model + "\nUTC: " + DateTime.UtcNow.ToString("O") + "\nChecks: " + checks + "; failures: " + failures + "; simulation runs: " + runs + "; simulated seconds: " + F(seconds) + "\n" + log);
            File.WriteAllText(Path.Combine(folder, "runs.csv"), rows.ToString());
            File.WriteAllText(Path.Combine(folder, "parts.csv"), partRows.ToString());
            Debug.Log("SLICE 2 AUDIT: " + checks + " checks, " + failures + " failures, " + runs + " simulation runs.");
            if (failures > 0) throw new InvalidOperationException("Slice 2 audit found " + failures + " issues; see reports/slice-2-audit/verification.txt");
            return checks;
        }
        static DriverSettings Driver() { return new DriverSettings { throttleAssist = false, tractionAssist = false, optimalThermals = false, wearMode = WearMode.Full }; }
        static string Csv(string s) { return "\"" + s.Replace("\"", "\"\"") + "\""; }
        static string F(double n) { return n.ToString("F5", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
