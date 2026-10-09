using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor
{
    public static class MassLoadVerification
    {
        public static void RunStandalone() { int checks = Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../.."))); Debug.Log("MASS VERIFICATION PASS: " + checks); }
        public static int Run(string root)
        {
            int checks = 0; var log = new StringBuilder();
            Action<bool,string> check = (ok,label) => { if (!ok) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS: " + label); };
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles"); var catalog = new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach (var v in vehicles)
            {
                var b = catalog.Resolve(v.spec.id, catalog.Recipe(v.spec.id), v.CreateStockTune()); var car = new CarSimulation(b.spec,b.tune,new DriverSettings()); var m=car.mass;
                check(Math.Abs(m.frontNormalLoadN+m.rearNormalLoadN-m.TranslatingKg*DragMath.Gravity)<1e-7,"Axle load conservation " + v.spec.id);
                check(Math.Abs(m.TranslatingKg-car.wheels.vehicleMass)<1e-7 && Math.Abs(m.TranslatingKg-m.dryInstalledKg-m.driverKg-m.payloadKg-m.fuelKg)<1e-7,"Mass conservation " + v.spec.id);
                car.state.speed=20;car.state.rpm=4000;car.SetMassInputs(75,100,0,.4,10);double front=car.mass.frontNormalLoadN;double total=car.mass.TranslatingKg;
                car.SetMassInputs(75,100,car.spec.wheelbaseM,.4,10);
                check(car.mass.frontNormalLoadN<front && car.mass.TranslatingKg==total,"Equal ballast mass, different axle placement " + v.spec.id);
                check(car.state.speed==20 && car.state.rpm==4000,"Live mass changes preserve motion " + v.spec.id);
                double heavy=car.mass.TranslatingKg;car.SetMassInputs(75,100,car.spec.wheelbaseM,.4,0);
                check(Math.Abs(heavy-car.mass.TranslatingKg-10*car.spec.fuel.densityKgPerLitre)<1e-6,"Fuel mass counted exactly once " + v.spec.id);
                var map=catalog.Recipe(v.spec.id);map[PartCategory.Flywheel]=catalog.Id(v.spec.id,PartCategory.Flywheel,1);var light=catalog.Resolve(v.spec.id,map,v.CreateStockTune());var lightCar=new CarSimulation(light.spec,light.tune,new DriverSettings());
                check(lightCar.mass.dryInstalledKg<m.dryInstalledKg && lightCar.mass.engineInertiaKgM2<m.engineInertiaKgM2,"Flywheel static replacement and rotational inertia are separate " + v.spec.id);
                foreach(var slot in new[]{PartCategory.Flywheel,PartCategory.Wheels,PartCategory.Tires}) {
                    var incompleteMap=catalog.Recipe(v.spec.id);incompleteMap.Remove(slot);var incomplete=catalog.Resolve(v.spec.id,incompleteMap,v.CreateStockTune());var incompleteCar=new CarSimulation(incomplete.spec,incomplete.tune,new DriverSettings());
                    double removed=slot==PartCategory.Flywheel?b.spec.flywheel.massKg:slot==PartCategory.Tires?24:4*(b.spec.wheels.assemblyMassKg-6);
                    check(!incomplete.Drivable && Math.Abs(m.dryInstalledKg-incompleteCar.mass.dryInstalledKg-removed)<1e-5,"Incomplete mass excludes actually removed " + slot + " / " + v.spec.id);
                }
                var snapshot = new RunSnapshot { vehicle = b.spec.Copy(), tune = b.tune.Copy(), driving = new DriverSettings { wearMode = WearMode.None } };
                var badMass=PhysicalDrivingSession.Copy(snapshot);badMass.tune.payloadMassKg=float.NaN;check(PhysicalDrivingSession.Validate(badMass)!=null,"Nonfinite captured mass rejected " + v.spec.id);
                var run = new PhysicalDrivingSession(snapshot); var commands = PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, 123);
                foreach(var command in commands.Take(500)) run.Step(command);
                string original = run.Csv(); double speedBefore = run.car.state.speed, rpmBefore = run.car.state.rpm;
                run.car.SetMassInputs(100, 200, 2, .8, 10); var next = run.ContinueRecording();
                check(next.car.state.speed == speedBefore && next.car.state.rpm == rpmBefore && next.car.mass.payloadKg == 200, "Mass checkpoint preserves movement and captures actual ballast " + v.spec.id);
                check(original == PhysicalDrivingSession.Replay(run.snapshot).Csv(), "Old trace remains repeatable after live mass change " + v.spec.id);
                for(int tick=0; tick<120; tick++) next.Step(new TickInput { tick=tick, input=new DriverInput { throttle=.6 } });
                check(next.Csv() == PhysicalDrivingSession.Replay(next.snapshot).Csv(), "New mass segment replays exactly " + v.spec.id);
                var decoded = PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(next.snapshot)));
                check(Math.Abs(decoded.car.state.distance-next.car.state.distance)<1e-7 && decoded.car.mass.fuelKg == next.car.mass.fuelKg, "JSON mass continuation preserves fuel and distance " + v.spec.id);
                var bodyMap = catalog.Recipe(v.spec.id); bodyMap[PartCategory.Body] = catalog.Id(v.spec.id,PartCategory.Body,1);
                var body = catalog.Resolve(v.spec.id,bodyMap,v.CreateStockTune()); var bodyCar=new CarSimulation(body.spec,body.tune,new DriverSettings());
                check(Math.Abs(m.dryInstalledKg-bodyCar.mass.dryInstalledKg-MassDynamics.ReferenceBodyMass(b.spec)*(1-body.tune.MassMultiplier))<1e-5, "Body upgrade reduces structure only, not fuel or driver " + v.spec.id);
                var interiorMap=catalog.Recipe(v.spec.id); double interiorKg=catalog.Find(interiorMap[PartCategory.Interior]).massKg; interiorMap.Remove(PartCategory.Interior);var stripped=catalog.Resolve(v.spec.id,interiorMap,v.CreateStockTune());
                check(Math.Abs(m.dryInstalledKg-new CarSimulation(stripped.spec,stripped.tune,new DriverSettings()).mass.dryInstalledKg-interiorKg)<1e-5 && stripped.Drivable, "Interior removal subtracts installed hardware once " + v.spec.id);
            }
            var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);profile.SelectedCar.presets=null;MechanicsOwnership.Ensure(profile,catalog);
            check(profile.SelectedCar.fuelInitialized&&profile.SelectedCar.fuel.litres==catalog.Spec(profile.SelectedCar.vehicleId).fuel.capacityLitres,"Old save initializes fuel once and tolerates omitted presets");
            profile.SelectedCar.fuel.litres=0;profile.SelectedCar.tune.driverMassKg=120;MechanicsOwnership.Ensure(profile,catalog);
            check(profile.SelectedCar.fuel.litres==0&&profile.SelectedCar.tune.driverMassKg==120,"Repeated migration preserves empty tank and custom driver mass");
            var jsonBefore=JsonUtility.ToJson(profile);check(MechanicsOwnership.Validate(profile,catalog)==null&&jsonBefore==JsonUtility.ToJson(profile),"Ownership validation does not change state or refill tanks");
            var standing=new CarSimulation(vehicles[0].spec,vehicles[0].CreateStockTune(),new DriverSettings());check(standing.state.frontLoadN>0&&Math.Abs(standing.state.frontLoadN+standing.state.rearLoadN-standing.mass.TranslatingKg*DragMath.Gravity)<1e-7,"Initial stationary telemetry includes conserved static axle loads");
            Directory.CreateDirectory(Path.Combine(root,"reports/phases/2f"));File.WriteAllText(Path.Combine(root,"reports/phases/2f/verification.txt"), log + "Checks: " + checks);return checks;
        }
    }
}
