from pathlib import Path
root=Path(__file__).resolve().parent.parent
pending={}
def edit(p,old,new):
 text=pending.get(p,(root/p).read_text(encoding='utf-8-sig'));assert old in text,(p,old[:75]);pending[p]=text.replace(old,new)
d='Game/Assets/DragLab/Scripts/Domain/'
edit(d+'DragSimulation.cs','public float partLongitudinalMomentKgM, partVerticalMomentKgM;', 'public float partLongitudinalMomentKgM, partVerticalMomentKgM;\n        public bool hasStructuralBody = true;')
edit(d+'MechanicalCatalog.cs','if (part == null) {', 'if (part == null) {\n                    if (category == PartCategory.Body) result.spec.hasStructuralBody = false;')
edit(d+'MechanicalCatalog.cs','PartSemantics.ApplyRemoval(category, result.spec, t); if (factoryRecipe.ContainsKey(category)) PartSemantics.ApplyMassDelta(result.spec, Find(factoryRecipe[category]), -Find(factoryRecipe[category]).massKg);', 'PartSemantics.ApplyRemoval(category, result.spec, t);')
edit(d+'MechanicalCatalog.cs','else (RoadOnly(category) ? result.warnings : result.errors).Add(effect);\n                    continue;', 'else (RoadOnly(category) ? result.warnings : result.errors).Add(effect);\n                    if (factoryRecipe.ContainsKey(category)) PartSemantics.ApplyMassDelta(result.spec, Find(factoryRecipe[category]), -Find(factoryRecipe[category]).massKg);\n                    continue;')
edit(d+'MassDynamics.cs','public static MassBreakdown Resolve(', 'public static double ReferenceBodyMass(VehicleSpec spec) { return (spec.massKg - spec.fuel.capacityLitres * spec.fuel.densityKgPerLitre) * .35; }\n        public static MassBreakdown Resolve(')
edit(d+'MassDynamics.cs','double baseDry = (spec.massKg - referenceFuel) * tune.MassMultiplier;', 'double bodyReference = ReferenceBodyMass(spec);\n            double bodyDelta = spec.hasStructuralBody ? bodyReference * (tune.MassMultiplier - 1) : -bodyReference;\n            double baseDry = spec.massKg - referenceFuel + bodyDelta;')
edit(d+'MassDynamics.cs','- referenceFuel * tank.positionFromFrontAxleM) * tune.MassMultiplier;', '- referenceFuel * tank.positionFromFrontAxleM) + bodyDelta * spec.wheelbaseM * .5;')
edit(d+'MassDynamics.cs','- referenceFuel * tank.heightM) * tune.MassMultiplier;', '- referenceFuel * tank.heightM) + bodyDelta * .6;')
edit(d+'MassDynamics.cs','dryInstalledKg = dry,', 'chassisKg = spec.hasStructuralBody ? bodyReference * tune.MassMultiplier : 0, installedComponentKg = dry - (spec.hasStructuralBody ? bodyReference * tune.MassMultiplier : 0), dryInstalledKg = dry,')
edit(d+'MechanicsContracts.cs','public double dryInstalledKg,', 'public double chassisKg, installedComponentKg;\n        public double dryInstalledKg,')
edit(d+'PartSemantics.cs','case PartCategory.Brakes: p.massKg = upgrade ? 32 : 28;', 'case PartCategory.Body: p.massKg = (float)MassDynamics.ReferenceBodyMass(spec) * p.massScale; break;\n                    case PartCategory.Brakes: p.massKg = upgrade ? 32 : 28;')
edit(d+'PhysicalDrivingSession.cs','public double frontLeftSlip,', 'public double translatingMassKg, dryMassKg, driverMassKg, payloadMassKg, fuelMassKg, cgHeightM, cgPositionM, frontLoadN, rearLoadN;\n        public double frontLeftSlip,')
edit(d+'PhysicalDrivingSession.cs','var sample = samples[samples.Count - 1];', 'var sample = samples[samples.Count - 1]; sample.translatingMassKg = car.mass.TranslatingKg; sample.dryMassKg = car.mass.dryInstalledKg; sample.driverMassKg = car.mass.driverKg; sample.payloadMassKg = car.mass.payloadKg; sample.fuelMassKg = car.mass.fuelKg; sample.cgHeightM = car.mass.centerOfGravityHeightM; sample.cgPositionM = car.mass.centerOfGravityFromFrontM; sample.frontLoadN = s.frontLoadN; sample.rearLoadN = s.rearLoadN;')
edit(d+'PhysicalDrivingSession.cs','return enriched.ToString();', '''var massRows = enriched.ToString().TrimEnd('\\n').Split('\\n');
            var massCsv = new StringBuilder(massRows[0] + ",mass_translating_kg,mass_dry_kg,driver_kg,payload_kg,fuel_mass_kg,cg_height_m,cg_position_from_front_m,front_load_n,rear_load_n\\n");
            for (int i = 0; i < samples.Count; i++) { var s = samples[i]; massCsv.Append(massRows[i + 1]); massCsv.AppendFormat(CultureInfo.InvariantCulture, ",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R}\\n", s.translatingMassKg, s.dryMassKg, s.driverMassKg, s.payloadMassKg, s.fuelMassKg, s.cgHeightM, s.cgPositionM, s.frontLoadN, s.rearLoadN); }
            return massCsv.ToString();''')
edit('Game/Assets/DragLab/Scripts/Runtime/PhysicalBenchMass.cs','config.tune = car.tune.Copy();', 'config.startingFuel = new DragLab.Domain.Mechanics.FuelState { litres = fuel }; config.tune = car.tune.Copy();')
edit(d+'PhysicalDrivingSession.cs','if (snapshot.hasStartingDynamics) car.RestoreDynamics(snapshot.startingDynamics);', 'if (snapshot.startingFuel != null) car.SetMassInputs(car.tune.driverMassKg, car.tune.payloadMassKg, car.tune.payloadPositionM, car.tune.payloadHeightM, snapshot.startingFuel.litres);\n            if (snapshot.hasStartingDynamics) car.RestoreDynamics(snapshot.startingDynamics);')
for p,t in pending.items():(root/p).write_text(t,encoding='utf-8')
