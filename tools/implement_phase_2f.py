from pathlib import Path
root=Path(__file__).resolve().parent.parent
pending={}
def edit(p,old,new):
 f=root/p;s=pending[p] if p in pending else f.read_text(encoding='utf-8-sig');assert old in s,(p,old[:90]);pending[p]=s.replace(old,new)
def write(p,s):
 pending[p]=s
d='Game/Assets/DragLab/Scripts/Domain/'
edit(d+'MechanicsContracts.cs','public double engineInertiaKgM2, wheelInertiaKgM2;', 'public double engineInertiaKgM2, wheelInertiaKgM2, centerOfGravityFromFrontM, centerOfGravityHeightM, frontWeightFraction;')
edit(d+'MechanicsContracts.cs','draglab-audited-stress-v6','draglab-mass-load-v7')
edit(d+'DragSimulation.cs','public float massKg = 1080;', 'public Mechanics.FuelSpec fuel = new Mechanics.FuelSpec();\n        public float partLongitudinalMomentKgM, partVerticalMomentKgM;\n        public float massKg = 1080; // OEM curb mass including full fuel, excluding driver/payload.')
edit(d+'DragSimulation.cs','copy.gears = gears == null', 'copy.fuel = fuel == null ? new Mechanics.FuelSpec() : new Mechanics.FuelSpec { capacityLitres = fuel.capacityLitres, densityKgPerLitre = fuel.densityKgPerLitre, positionFromFrontAxleM = fuel.positionFromFrontAxleM, heightM = fuel.heightM, provenance = fuel.provenance };\n            copy.gears = gears == null')
edit(d+'DragSimulation.cs','public float launchRpm = 3200;', 'public float driverMassKg = 75, payloadMassKg, payloadPositionM = 1.5f, payloadHeightM = .45f;\n        public float launchRpm = 3200;')
edit(d+'DragSimulation.cs','awd = other.awd == null', 'driverMassKg = other.driverMassKg; payloadMassKg = other.payloadMassKg; payloadPositionM = other.payloadPositionM; payloadHeightM = other.payloadHeightM;\n            awd = other.awd == null')
edit(d+'DragSimulation.cs','if (float.IsNaN(launchRpm)', 'driverMassKg = (float)DragMath.Clamp(driverMassKg, 0, 200); payloadMassKg = (float)DragMath.Clamp(payloadMassKg, 0, 1000); payloadPositionM = (float)DragMath.Clamp(payloadPositionM, -.5, spec.wheelbaseM + .5); payloadHeightM = (float)DragMath.Clamp(payloadHeightM, 0, 2);\n            if (float.IsNaN(launchRpm)')
edit(d+'DragSimulation.cs','public readonly WheelProfile wheels;', 'public readonly WheelProfile wheels;\n        public Mechanics.MassBreakdown mass { get; private set; }\n        public void SetMassInputs(double driverKg, double payloadKg, double positionM, double heightM, double fuelLitres)\n        {\n            if (!Mechanics.MechanicsSchema.Finite(driverKg) || !Mechanics.MechanicsSchema.Finite(payloadKg) || !Mechanics.MechanicsSchema.Finite(positionM) || !Mechanics.MechanicsSchema.Finite(heightM) || !Mechanics.MechanicsSchema.Finite(fuelLitres) || driverKg < 0 || driverKg > 200 || payloadKg < 0 || payloadKg > 1000 || positionM < -.5 || positionM > spec.wheelbaseM + .5 || heightM < 0 || heightM > 2 || fuelLitres < 0 || fuelLitres > spec.fuel.capacityLitres) throw new ArgumentException("Invalid mass inputs.");\n            tune.driverMassKg = (float)driverKg; tune.payloadMassKg = (float)payloadKg; tune.payloadPositionM = (float)positionM; tune.payloadHeightM = (float)heightM; state.fuelLitres = fuelLitres; UpdateMass();\n        }\n        void UpdateMass() { mass = MassDynamics.Resolve(spec, tune, wheels, state.fuelLitres); wheels.vehicleMass = mass.TranslatingKg; wheels.effectiveMass = mass.TranslatingKg + wheels.totalInertia / (wheels.radius * wheels.radius); state.translatingMassKg = mass.TranslatingKg; state.effectiveRollingMassKg = wheels.effectiveMass; state.massCgHeightM = mass.centerOfGravityHeightM; state.massCgPositionM = mass.centerOfGravityFromFrontM; }')
edit(d+'DragSimulation.cs','mechanics = new MechanicalStress(spec, this.tune, components);', 'state.fuelLitres = spec.fuel.capacityLitres; UpdateMass();\n            mechanics = new MechanicalStress(spec, this.tune, components);')
edit(d+'DragSimulation.cs','state.wearRate = driver.WearRate;', 'UpdateMass(); state.wearRate = driver.WearRate;')
edit(d+'DragSimulation.cs','mass * state.acceleration * spec.centerOfGravityM', 'mass * state.acceleration * this.mass.centerOfGravityHeightM')
edit(d+'DragSimulation.cs','mass * DragMath.Gravity * spec.frontWeightFraction - transfer', 'mass * DragMath.Gravity * this.mass.frontWeightFraction - transfer')
# Add mass fields to the state, not to a GUI-only copy.
edit(d+'DragSimulation.cs','public double exhaustPopStrength, cvtRatio', 'public double translatingMassKg, massCgHeightM, massCgPositionM, fuelLitres;\n        public double exhaustPopStrength, cvtRatio')
edit(d+'PowertrainDynamics.cs','public readonly double radius, assemblyMass, totalInertia, effectiveMass, vehicleMass, gripScale;', 'public readonly double radius, assemblyMass, totalInertia, gripScale;\n        public double effectiveMass, vehicleMass;')
edit(d+'PowertrainDynamics.cs','vehicleMass = spec.massKg * tune.MassMultiplier + spec.partMassDeltaKg + (spec.flywheel.massKg * tune.FlywheelMass - spec.flywheel.massKg) + 4 * (assemblyMass - data.assemblyMassKg);', 'vehicleMass = MassDynamics.Resolve(spec, tune, this, spec.fuel.capacityLitres).TranslatingKg;')
write(d+'MassDynamics.cs', '''using System;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain
{
    public static class MassDynamics
    {
        // Curb reference includes factory wheels, flywheel and a full tank. Replace
        // their deltas once; rotating inertia is never a gravitational load.
        public static MassBreakdown Resolve(VehicleSpec spec, Tune tune, WheelProfile wheels, double fuelLitres)
        {
            var tank = spec.fuel; double referenceFuel = tank.capacityLitres * tank.densityKgPerLitre;
            double baseDry = (spec.massKg - referenceFuel) * tune.MassMultiplier;
            double flywheelDelta = spec.flywheel.massKg * (tune.FlywheelMass - 1);
            double wheelDelta = 4 * (wheels.assemblyMass - spec.wheels.assemblyMassKg);
            double dry = baseDry + spec.partMassDeltaKg + flywheelDelta + wheelDelta;
            double baselineX = (spec.massKg * spec.wheelbaseM * (1 - spec.frontWeightFraction) - referenceFuel * tank.positionFromFrontAxleM) * tune.MassMultiplier;
            double baselineHeight = (spec.massKg * spec.centerOfGravityM - referenceFuel * tank.heightM) * tune.MassMultiplier;
            double fuel = fuelLitres * tank.densityKgPerLitre;
            double total = dry + tune.driverMassKg + tune.payloadMassKg + fuel;
            double xMoment = baselineX + spec.partLongitudinalMomentKgM + flywheelDelta * spec.wheelbaseM * .15 + wheelDelta * spec.wheelbaseM * .5 + tune.driverMassKg * spec.wheelbaseM * .45 + tune.payloadMassKg * tune.payloadPositionM + fuel * tank.positionFromFrontAxleM;
            double heightMoment = baselineHeight + spec.partVerticalMomentKgM + flywheelDelta * .4 + wheelDelta * wheels.radius + tune.driverMassKg * .65 + tune.payloadMassKg * tune.payloadHeightM + fuel * tank.heightM;
            double fraction = DragMath.Clamp(1 - xMoment / (total * spec.wheelbaseM), .05, .95);
            return new MassBreakdown { dryInstalledKg = dry, driverKg = tune.driverMassKg, payloadKg = tune.payloadMassKg, fuelKg = fuel, centerOfGravityFromFrontM = xMoment / total, centerOfGravityHeightM = heightMoment / total, frontWeightFraction = fraction, frontNormalLoadN = total * DragMath.Gravity * fraction, rearNormalLoadN = total * DragMath.Gravity * (1 - fraction), engineInertiaKgM2 = spec.RotationalInertia(tune), wheelInertiaKgM2 = wheels.totalInertia };
        }
    }
}
''')
edit(d+'MechanicalCatalog.cs','public float rpmLimit, torqueLimitNm, massKg, inertiaKgM2;', 'public float rpmLimit, torqueLimitNm, massKg, inertiaKgM2;\n        public float positionFromFrontAxleM, heightM = .4f;')
edit(d+'MechanicalCatalog.cs','result.spec.partMassDeltaKg = 0;', 'result.spec.partMassDeltaKg = result.spec.partLongitudinalMomentKgM = result.spec.partVerticalMomentKgM = 0;')
edit(d+'PartSemantics.cs','p.massKg - stock.massKg;', 'p.massKg - stock.massKg;')
edit(d+'PartSemantics.cs','if (p.category == PartCategory.Interior || p.category == PartCategory.Accessories || IsDifferential(p.category)) spec.partMassDeltaKg += p.massKg - stock.massKg;', 'if (p.category == PartCategory.Interior || p.category == PartCategory.Accessories || IsDifferential(p.category)) { double delta = p.massKg - stock.massKg; spec.partMassDeltaKg += (float)delta; spec.partLongitudinalMomentKgM += (float)(delta * p.positionFromFrontAxleM); spec.partVerticalMomentKgM += (float)(delta * p.heightM); }')
edit(d+'PartSemantics.cs','if (label.Length > 0) p.name', 'p.positionFromFrontAxleM = p.category == PartCategory.Interior ? spec.wheelbaseM * .6f : p.category == PartCategory.Accessories ? spec.wheelbaseM * .5f : p.category == PartCategory.RearDifferential ? spec.wheelbaseM : 0;\n            p.heightM = p.category == PartCategory.Interior || p.category == PartCategory.Accessories ? .65f : .35f;\n            if (label.Length > 0) p.name')
edit(d+'PartSemantics.cs','if (c == PartCategory.Interior) spec.partMassDeltaKg -= 60;', 'if (c == PartCategory.Interior) { spec.partMassDeltaKg -= 60; spec.partLongitudinalMomentKgM -= 60 * spec.wheelbaseM * .6f; spec.partVerticalMomentKgM -= 60 * .65f; }')
edit(d+'PartSemantics.cs','if (c == PartCategory.Accessories) spec.partMassDeltaKg -= 20;', 'if (c == PartCategory.Accessories) { spec.partMassDeltaKg -= 20; spec.partLongitudinalMomentKgM -= 20 * spec.wheelbaseM * .5f; spec.partVerticalMomentKgM -= 20 * .65f; }')
r='Game/Assets/DragLab/Scripts/Runtime/'
edit(r+'TuningGarage.cs','dirty |= TuneSlider("launch",', 'dirty |= TuneSlider("driver-mass", "Driver mass (kg)", ref tune.driverMassKg, 0, 200, "F0", 75);\n            dirty |= TuneSlider("payload-mass", "Payload / ballast mass (kg)", ref tune.payloadMassKg, 0, 1000, "F0", 0);\n            dirty |= TuneSlider("payload-position", "Payload position from front axle (m)", ref tune.payloadPositionM, -.5f, Selected.spec.wheelbaseM + .5f, "F2", 1.5f);\n            dirty |= TuneSlider("payload-height", "Payload height (m)", ref tune.payloadHeightM, 0, 2, "F2", .45f);\n            dirty |= TuneSlider("launch",')
edit(r+'PhysicalDrivingBench.cs','public sealed class PhysicalDrivingBench', 'public sealed partial class PhysicalDrivingBench')
edit(r+'PhysicalDrivingBench.cs','GUILayout.Label("Radiator rejecting "', 'MassControls();\n            GUILayout.Label("Radiator rejecting "')
write(r+'PhysicalBenchMass.cs','''using System;
using UnityEngine;
namespace DragLab.Runtime
{
    public sealed partial class PhysicalDrivingBench
    {
        bool MassSlider(string label, ref float value, float min, float max, string format, float initial) { GUILayout.Label(label + " " + value.ToString(format)); return TuningSlider.Draw(ref value, min, max, initial); }
        void MassControls()
        {
            var car = Session.car; var m = car.mass;
            GUILayout.Label("MASS / dry " + m.dryInstalledKg.ToString("F1") + " kg / driver " + m.driverKg.ToString("F1") + " / payload " + m.payloadKg.ToString("F1") + " / fuel " + m.fuelKg.ToString("F1") + " / total " + m.TranslatingKg.ToString("F1"));
            GUILayout.Label("CG " + m.centerOfGravityFromFrontM.ToString("F2") + " m from front / height " + m.centerOfGravityHeightM.ToString("F2") + " m / static axle loads " + m.frontNormalLoadN.ToString("F0") + " : " + m.rearNormalLoadN.ToString("F0") + " N");
            float driver = car.tune.driverMassKg, payload = car.tune.payloadMassKg, position = car.tune.payloadPositionM, height = car.tune.payloadHeightM, fuel = (float)car.state.fuelLitres;
            bool changed = false;
            changed |= MassSlider("Test driver (kg)", ref driver, 0, 200, "F0", 75);
            changed |= MassSlider("Test ballast (kg)", ref payload, 0, 1000, "F0", 0);
            changed |= MassSlider("Ballast position (m)", ref position, -.5f, car.spec.wheelbaseM + .5f, "F2", 1.5f);
            changed |= MassSlider("Ballast height (m)", ref height, 0, 2, "F2", .45f);
            changed |= MassSlider("Test fuel mass input (litres)", ref fuel, 0, car.spec.fuel.capacityLitres, "F1", car.spec.fuel.capacityLitres);
            if (changed) { Archive(); car.SetMassInputs(driver, payload, position, height, fuel); config.tune = car.tune.Copy(); Session.snapshot.tune = car.tune.Copy(); Session = Session.ContinueRecording(Recording); notice = "Mass changed; movement and pedal source retained. Recorded segment continued from a physical checkpoint."; }
        }
    }
}
''')
write('Game/Assets/DragLab/Editor/MassLoadVerification.cs','''using System;
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
            }
            Directory.CreateDirectory(Path.Combine(root,"reports/phases/2f"));File.WriteAllText(Path.Combine(root,"reports/phases/2f/verification.txt"), log + "Checks: " + checks);return checks;
        }
    }
}
''')
edit('Game/Assets/DragLab/Editor/Verification.cs','count += CarInspectionVerification.Run(root);','count += CarInspectionVerification.Run(root);\n            count += MassLoadVerification.Run(root);')


edit(d+'MechanicalCatalog.cs','var t = result.tune; t.partsResolved = true;', 'var factoryRecipe = Recipe(model);\n            var t = result.tune; t.partsResolved = true;')
edit(d+'MechanicalCatalog.cs','PartSemantics.ApplyRemoval(category, result.spec, t);', 'PartSemantics.ApplyRemoval(category, result.spec, t); if (factoryRecipe.ContainsKey(category)) PartSemantics.ApplyMassDelta(result.spec, Find(factoryRecipe[category]), -Find(factoryRecipe[category]).massKg);')
edit(d+'MechanicalCatalog.cs','PartSemantics.Apply(part, result.spec, t, Find(Id(model, category)));', 'PartSemantics.Apply(part, result.spec, t, factoryRecipe.ContainsKey(category) ? Find(factoryRecipe[category]) : null);')
edit(d+'PartSemantics.cs','if (c == PartCategory.Interior) { spec.partMassDeltaKg -= 60; spec.partLongitudinalMomentKgM -= 60 * spec.wheelbaseM * .6f; spec.partVerticalMomentKgM -= 60 * .65f; }','// Optional removal mass uses the actual OEM reference in the resolver.')
edit(d+'PartSemantics.cs','if (c == PartCategory.Accessories) { spec.partMassDeltaKg -= 20; spec.partLongitudinalMomentKgM -= 20 * spec.wheelbaseM * .5f; spec.partVerticalMomentKgM -= 20 * .65f; }','')
edit(d+'PartSemantics.cs','if (p.category == PartCategory.Interior || p.category == PartCategory.Accessories || IsDifferential(p.category)) { double delta = p.massKg - stock.massKg; spec.partMassDeltaKg += (float)delta; spec.partLongitudinalMomentKgM += (float)(delta * p.positionFromFrontAxleM); spec.partVerticalMomentKgM += (float)(delta * p.heightM); }', 'ApplyMassDelta(spec, p, p.massKg - (stock == null ? 0 : stock.massKg));')
edit(d+'PartSemantics.cs','public static void Apply(PartDefinition p,', '''public static void ApplyMassDelta(VehicleSpec spec, PartDefinition part, double delta)
        {
            if (part.category == PartCategory.Wheels || part.category == PartCategory.Tires || part.category == PartCategory.Flywheel || part.category == PartCategory.Body) return;
            spec.partMassDeltaKg += (float)delta; spec.partLongitudinalMomentKgM += (float)(delta * part.positionFromFrontAxleM); spec.partVerticalMomentKgM += (float)(delta * part.heightM);
        }
        public static void Apply(PartDefinition p,''')
edit(d+'PartSemantics.cs','p.positionFromFrontAxleM = p.category == PartCategory.Interior ?', '''if (p.massKg <= 0) {
                switch (p.category) {
                    case PartCategory.Pistons: p.massKg = 4 * (upgrade ? 1.02f : 1); break;
                    case PartCategory.Rods: p.massKg = 3 * (upgrade ? 1.02f : 1); break;
                    case PartCategory.Crankshaft: p.massKg = 15 * (upgrade ? 1.02f : 1); break;
                    case PartCategory.HeadIntake: p.massKg = 25 + n * 2; break;
                    case PartCategory.FuelSystem: p.massKg = upgrade ? 10 : 8; break;
                    case PartCategory.Ignition: p.massKg = 2; break;
                    case PartCategory.Cooling: p.massKg = upgrade ? 22 : 18; break;
                    case PartCategory.Induction: p.massKg = p.induction == EngineUpgradeInduction.Factory ? (spec.induction.factory == InductionType.NaturallyAspirated ? 2 : 14) : p.induction == EngineUpgradeInduction.TwinTurbo ? 24 : p.induction == EngineUpgradeInduction.TwinSupercharger ? 28 : p.induction == EngineUpgradeInduction.Twincharged ? 30 : p.induction == EngineUpgradeInduction.Supercharger ? 18 : 14; break;
                    case PartCategory.Exhaust: p.massKg = new[] { 22f, 18f, 12f, 8f }[n]; break;
                    case PartCategory.Clutch: p.massKg = upgrade ? 10 : 8; break;
                    case PartCategory.Gearbox: p.massKg = 45 + n * 2 + (p.transmission == TransmissionUpgrade.Automatic || p.transmission == TransmissionUpgrade.DCT ? 15 : p.transmission == TransmissionUpgrade.CVT ? 10 : 0); break;
                    case PartCategory.Wheels: p.massKg = 4 * (spec.wheels.assemblyMassKg - 6) * p.massScale; break;
                    case PartCategory.Tires: p.massKg = 24; break;
                    case PartCategory.Brakes: p.massKg = upgrade ? 32 : 28; break;
                    case PartCategory.Controls: p.massKg = 1.5f; break;
                    case PartCategory.RevLimiter: case PartCategory.TwoStep: case PartCategory.ShiftLight: p.massKg = .2f; break;
                    case PartCategory.FrontAxle: case PartCategory.RearAxle: p.massKg = upgrade ? 10 : 8; break;
                    case PartCategory.TransferCase: p.massKg = upgrade ? 18 : 14; break;
                    case PartCategory.Driveshaft: case PartCategory.FrontDriveshaft: case PartCategory.RearDriveshaft: p.massKg = upgrade ? 6 : 4; break;
                }
            }
            p.positionFromFrontAxleM = p.category == PartCategory.Interior ?''')
edit(d+'PartSemantics.cs','p.category == PartCategory.RearDifferential ? spec.wheelbaseM : 0;', 'p.category == PartCategory.RearDifferential || p.category == PartCategory.RearAxle ? spec.wheelbaseM : p.category == PartCategory.Driveshaft || p.category == PartCategory.TransferCase || p.category == PartCategory.FrontDriveshaft || p.category == PartCategory.RearDriveshaft ? spec.wheelbaseM * .5f : spec.wheelbaseM * .15f;')

# Keep every edit atomic at the source-staging level: assertions above cannot leave
# a half-applied migration if a source signature has moved.
for relative, content in pending.items(): (root/relative).write_text(content,encoding='utf-8')
