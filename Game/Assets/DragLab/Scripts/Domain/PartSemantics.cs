using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public struct PartStat
    {
        public string key, label, unit, explanation;
        public double value;
        public int direction;
        public PartStat(string key, string label, double value, string unit, int direction, string explanation)
        { this.key = key; this.label = label; this.value = value; this.unit = unit; this.direction = direction; this.explanation = explanation; }
    }
    // Category contracts are shared by authoring, resolver, shop, inspection and bench.
    public static class PartSemantics
    {
        public static string Friendly(string value) { return System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant(); }
        public static bool EngineInternal(PartCategory c) { return c == PartCategory.Pistons || c == PartCategory.Rods || c == PartCategory.Crankshaft; }
        public static bool IsDifferential(PartCategory c) { return c == PartCategory.Differential || c == PartCategory.FrontDifferential || c == PartCategory.RearDifferential; }
        public static bool TorqueRated(PartCategory c) { return EngineInternal(c) || c == PartCategory.Gearbox || c == PartCategory.Driveshaft || c == PartCategory.Differential || MechanicalCatalog.DriveBranch(c); }
        public static bool Wears(PartCategory c) { return ServiceHardware.Wears(c) || EngineInternal(c) || TorqueRated(c) || c == PartCategory.Clutch || c == PartCategory.Cooling || c == PartCategory.Brakes || c == PartCategory.HeadIntake || c == PartCategory.FuelSystem || c == PartCategory.Ignition || c == PartCategory.Induction || c == PartCategory.Exhaust || c == PartCategory.Tires; }
        static TransmissionType Transmission(VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            string id; var p = installed.TryGetValue(PartCategory.Gearbox, out id) ? catalog.Find(id) : null;
            return p == null || p.transmission == TransmissionUpgrade.Factory ? spec.transmission.type : (TransmissionType)((int)p.transmission - 1);
        }
        public static bool Optional(PartCategory c, VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            var type = Transmission(spec, installed, catalog);
            return ServiceHardware.Optional(c) || c == PartCategory.RevLimiter || c == PartCategory.ShiftLight || c == PartCategory.TwoStep || c == PartCategory.Interior || c == PartCategory.Accessories || c == PartCategory.Exhaust || c == PartCategory.Induction || c == PartCategory.Cooling ||
                c == PartCategory.Controls && FuelingHardware.Type(spec, installed, catalog) == FuelSystem.Carburetor || c == PartCategory.Clutch && (type == TransmissionType.Automatic || type == TransmissionType.CVT);
        }
        public static string RemovalEffect(PartCategory c, VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            switch (c)
            {
                case PartCategory.OilCooler: return "Oil cooler removed: residual sump heat rejection remains; hot-load oil temperature can rise.";
                case PartCategory.CoolingCap: return "Pressure cap removed: coolant no longer holds pressure; boiling resistance falls, while the engine can still run.";
                case PartCategory.CoolingHoses: return "Hoses removed: coolant circuit leaks; retain engine operation until heat or low coolant causes damage.";
                case PartCategory.CoolantPump: return "Coolant pump removed: forced circulation stops; residual engine heat transfer remains.";
                case PartCategory.RevLimiter: return "No auxiliary/fitted limiter module: no added rev protection or adjustment.";
                case PartCategory.ShiftLight: return "No shift-light module: numerical tachometer remains, but no shift alert.";
                case PartCategory.TwoStep: return "No two-step module: neutral revving has no launch RPM cut.";
                case PartCategory.Interior: return "Interior stripped: lower weight; no mechanical wear or repair required.";
                case PartCategory.Accessories: return "Comfort accessories removed: lower weight; essential starter/charging equipment retained.";
                case PartCategory.Exhaust: return "Exhaust removed: open acoustic path, no silencer/catalyst filtering; engine still runs.";
                case PartCategory.Induction: return "Induction kit removed: naturally aspirated operation; factory boosted curve is de-rated.";
                case PartCategory.Cooling: return "Cooling removed: residual air cooling only; sustained load can overheat the engine.";
                case PartCategory.Controls: return FuelingHardware.Type(spec, installed, catalog) == FuelSystem.Carburetor ? "Optional controller removed: mechanical carb/ignition remain; added limiter/launch functions unavailable." : "Missing EFI controller: install injection-capable Controls; this injected engine cannot run without them.";
                case PartCategory.Clutch: return Optional(c, spec, installed, catalog) ? "Separate friction clutch omitted: installed converter/CVT coupling remains." : "Missing clutch: no power transfer through this manual/DCT gearbox.";
                case PartCategory.Body: return "Missing structural chassis/body: build is incomplete; weight reduction does not remove the chassis.";
                default: return "Missing " + c + ": " + (MechanicalCatalog.RoadOnly(c) ? "its drive/braking path is unavailable; engine may still run." : "build is incomplete.");
            }
        }
        public static void Author(PartDefinition p, VehicleSpec spec)
        {
            int n = p.stage; bool upgrade = n > 0;
            if (p.category == PartCategory.Controls) { p.injectionControlMetadataVersion = 1; p.supportsInjection = upgrade || spec.engineBehavior.fueling == FuelSystem.Injection; p.controllerCylinderCapacity = p.supportsInjection ? (upgrade ? 12 : spec.sound.cylinders) : 0; }
            if (p.category == PartCategory.Induction) {
                var actual = p.induction == EngineUpgradeInduction.Factory ? spec.induction.factory : (InductionType)(int)p.induction;
                p.turboUnits = actual == InductionType.TwinTurbo ? 2 : actual == InductionType.Turbo || actual == InductionType.Twincharged ? 1 : 0;
                p.superchargerUnits = actual == InductionType.TwinSupercharger ? 2 : actual == InductionType.Supercharger || actual == InductionType.Twincharged ? 1 : 0;
            }
            p.tuning = PartTuningOptions.None;
            if (p.category == PartCategory.Induction && (upgrade || p.induction != EngineUpgradeInduction.Factory)) {
                var topology = p.induction == EngineUpgradeInduction.Factory ? spec.induction.factory : (InductionType)(int)p.induction;
                // Existing upgrade kits retain their controller/pulley adjustment, but size is fixed hardware.
                if (topology == InductionType.Turbo || topology == InductionType.TwinTurbo || topology == InductionType.Twincharged) p.tuning |= PartTuningOptions.Boost | PartTuningOptions.Valve;
                if (topology == InductionType.Supercharger || topology == InductionType.TwinSupercharger || topology == InductionType.Twincharged) p.tuning |= PartTuningOptions.Pulley;
            }
            if (p.category == PartCategory.Gearbox && (upgrade || p.transmission != TransmissionUpgrade.Factory)) p.tuning |= PartTuningOptions.FinalDrive;
            if (p.category == PartCategory.RevLimiter && upgrade) p.tuning |= PartTuningOptions.Limiter;
            if (p.category == PartCategory.TwoStep && (upgrade || spec.engineBehavior.twoStep)) p.tuning |= PartTuningOptions.TwoStep;
            if (p.category == PartCategory.ShiftLight && upgrade) p.tuning |= PartTuningOptions.ShiftLight;
            if (!EngineInternal(p.category)) p.rpmLimit = 0;
            if (!TorqueRated(p.category)) p.torqueLimitNm = 0;
            if (!Wears(p.category)) { p.thermalLimitC = p.wearEnergyJ = p.coolingWattsPerC = 0; }
            string label = "";
            switch (p.category)
            {
                case PartCategory.Pistons: case PartCategory.Rods: case PartCategory.Crankshaft:
                    label = (upgrade ? "reinforced " : "factory ") + p.quality.ToLowerInvariant() + " " + Friendly(p.category.ToString());
                    p.description = "Raises load/RPM strength headroom. Does not create horsepower; another weaker internal still limits a safe tune."; break;
                case PartCategory.HeadIntake: label = new[] { "factory head/intake", "mild flow head/intake", "ported head/intake", "race flow head/intake" }[n]; p.description = "Airflow increases the whole torque curve. Extra output also raises heat, clutch load and wheelspin risk."; break;
                case PartCategory.FuelSystem: case PartCategory.Ignition:
                    float hp = spec.torque.Max(t => t.torqueNm * t.rpm / 7127f);
                    p.maxPowerHp = hp * (upgrade ? 3.5f : 1.8f);
                    label = (upgrade ? "high-capacity " : "factory ") + Friendly(p.category.ToString());
                    p.description = p.category == PartCategory.FuelSystem ? "Fuel delivery limits supported power. A larger system only helps when the old capacity is exceeded; failure stops combustion." : "Ignition capacity limits supported power. A higher-capacity system supports tuned loads; failure stops combustion."; break;
                case PartCategory.Cooling:
                    label = upgrade ? "high-flow radiator and pump" : "factory radiator and pump";
                    p.engineHeatTransferScale = upgrade ? 2.2f : 1;
                    p.fluidCapacityLitres = spec.fluids.coolantCapacityLitres + (upgrade ? 2 : 0);
                    p.coolingWattsPerC = upgrade ? 650 : 300; p.heatCapacityJPerC = upgrade ? 26000 : 18000; p.thermalLimitC = upgrade ? 160 : 140;
                    p.description = "Transfers engine heat into coolant and rejects it to air. More transfer and radiator capacity can sustain a hotter tune; it does not raise engine torque or RPM strength."; break;
                case PartCategory.Induction:
                    label = p.induction == EngineUpgradeInduction.Factory ? (upgrade ? "high-flow factory induction" : "factory induction") : Friendly(p.induction.ToString()) + " kit";
                    p.power = upgrade && spec.induction.factory == InductionType.NaturallyAspirated && p.induction == EngineUpgradeInduction.Factory ? 1.04f : 1;
                    p.boostScale = upgrade && spec.induction.factory != InductionType.NaturallyAspirated && p.induction == EngineUpgradeInduction.Factory ? 1.12f : 1;
                    p.description = "Topology controls lag, compressor flow, charge heat, parasitic blower load and sound. High-flow factory option improves airflow or boost delivery."; break;
                case PartCategory.Exhaust: label = new[] { "factory filtered exhaust", "sport chambered exhaust", "straight-through exhaust", "open race exhaust" }[n]; p.description = "Changes silencer/catalyst/resonator filtering and combustion pulse character. No invented horsepower bonus from removing a silencer."; break;
                case PartCategory.Clutch: label = upgrade ? "high-capacity friction clutch" : "factory friction clutch"; p.description = "Raises transferable torque and friction-energy life. Manual/DCT coupling only; a converter/CVT does not use this separate part."; break;
                case PartCategory.Flywheel: label = upgrade ? "lightweight flywheel/flexplate" : "factory flywheel/flexplate"; p.description = "Changes rotating inertia, stored energy, neutral RPM decay and translating mass. No automatic horsepower or strength bonus."; break;
                case PartCategory.Gearbox:
                    label = p.transmission == TransmissionUpgrade.Factory ? new[] { "factory gearbox", "sport shift gearbox", "competition shift gearbox", "race shift gearbox" }[n] : Friendly(p.transmission.ToString()) + " gearbox";
                    var type = p.transmission == TransmissionUpgrade.Factory ? spec.transmission.type : (TransmissionType)((int)p.transmission - 1);
                    if (type == TransmissionType.Manual) label = (p.manualLayout == ManualGearboxLayout.Sequential ? "sequential manual" : "H-pattern manual") + " gearbox" + (p.transmission == TransmissionUpgrade.Factory ? " / " + new[] { "factory", "sport", "competition", "race" }[n] : "");
                    p.description = "Changes coupling type, shift duration, capacity and downshift protection. Gear ratios remain the car's authored ratios.";
                    if (type == TransmissionType.Manual) p.description += p.manualLayout == ManualGearboxLayout.Sequential ? " Sequential selector: " + (100 * (p.sequentialShiftScale == 0 ? .45 : p.sequentialShiftScale)).ToString("F0") + "% of equivalent H-pattern selection time. Single clutch; Full manual still requires clutch operation. No automatic flatshift cut or downshift interlock is implied." : " H-pattern selector with normal selection time. E/Q controls and clutch assists do not convert this into sequential hardware.";
                    break;
                case PartCategory.Differential: case PartCategory.FrontDifferential: case PartCategory.RearDifferential:
                    p.differential = (DifferentialKind)n; p.torqueBiasRatio = n == 1 ? 3 : 1; p.preloadNm = n == 1 ? 35 : 0;
                    p.massKg = n == 0 ? 12 : n == 1 ? 15 : n == 2 ? 12 : 10;
                    if (n == 2) p.torqueLimitNm *= .8f; if (n == 3) p.torqueLimitNm *= 1.25f;
                    label = new[] { "open differential", "3:1 limited-slip differential", "welded differential", "solid spool" }[n];
                    p.description = n == 0 ? "Equal output torque: the weaker-grip wheel limits useful axle force. No automatic grip increase on an even surface." : n == 1 ? "Up to 3:1 torque bias plus 35 Nm preload supports the better-grip wheel. Does not create tire grip; worn internals lose bias/preload." : n == 2 ? "Outputs permanently locked, combining available straight-line grip. Welded strength is lower than a purpose-built spool; turning scrub is outside drag scope." : "Purpose-built permanent solid connection. Same straight-line locking behavior as welded, with lower mass and higher strength; no corner differentiation."; break;
                case PartCategory.Wheels: label = upgrade ? "lightweight wheels" : "factory wheels"; p.description = "Lowers rotating assembly mass/inertia and scale weight. Tire grip is a separate component."; break;
                case PartCategory.Tires:
                    p.tireLifeDistanceM = new[] {40000000f,25000000f,10000000f,5000000f}[n]; p.wearEnergyJ = new[] {40000000f,30000000f,16000000f,10000000f}[n];
                    p.tireOptimalC = new[] {45f,60f,75f,90f}[n];p.thermalLimitC=130+n*10;p.heatCapacityJPerC=6500;p.coolingWattsPerC=25;p.overloadSeconds=120;
                    label = new[] { "road compound", "sport compound", "drag radial compound", "drag slick compound" }[n]; p.description = "Changes friction independently of wheel mass. Higher grip trades away rolling/sliding lifetime. Each tire heats, wears and loses grip independently; a failed tire does not kill the engine."; break;
                case PartCategory.Brakes: label = upgrade ? "high-capacity brake kit" : "factory brakes"; p.description = "Changes stopping-force rating, heat tolerance and friction-energy life. Grip still limits braking; unused brakes produce no friction heat."; break;
                case PartCategory.Body: label = new[] { "factory structural body", "lightweight body panels", "competition lightweight panels", "race lightweight panels" }[n]; p.description = "Lowers structural build mass. This is the chassis/panels, not removable upholstery; removing it leaves an incomplete car."; break;
                case PartCategory.Controls: label = upgrade ? "programmable engine controller" : "factory/mechanical engine controls"; p.description = upgrade ? "Injection-capable programmable controller, up to 12 cylinders. Enables compatible preload and anti-lag controls. Rev limiter, two-step and shift-light adjustments require their separate fitted modules." : p.supportsInjection ? "Factory injection controller with outputs for " + p.controllerCylinderCapacity + " cylinders. Fixed fuel calibration; programmable limiter/launch adjustments require their separate fitted modules." : "Mechanical carburetor/ignition controls. Cannot run an EFI conversion; install an injection-capable controller. Carbureted mechanical systems can run without this module."; break;
                case PartCategory.RevLimiter: label = upgrade ? "programmable electronic rev limiter" : "fixed electronic rev limiter"; p.price = upgrade ? 500 : 220; p.description = upgrade ? "Unlocks RPM threshold, hard/soft/no cut and compatible fuel/ignition cut. Protects against combustion overrev; cannot stop a mechanical money shift." : "Fixed factory-redline threshold and factory cut behavior. Fit the programmable module to adjust threshold and cut style."; break;
                case PartCategory.TwoStep: label = upgrade || spec.engineBehavior.twoStep ? "programmable two-step launch controller" : "fixed two-step launch controller"; p.price = upgrade ? 550 : 260; p.description = upgrade || spec.engineBehavior.twoStep ? "Unlocks adjustable launch-cut RPM, hard/soft cut and compatible cut method. Boost preload/anti-lag still require suitable turbo and controller hardware." : "Fixed 3500 RPM launch cut. No main rev limiter or shift light included."; break;
                case PartCategory.ShiftLight: label = upgrade ? "programmable shift light" : "fixed shift light"; p.price = upgrade ? 150 : 70; p.description = upgrade ? "Unlocks a separate shift-alert RPM threshold. Does not shift gears or change engine output." : "Lights at factory redline minus 200 RPM. Does not shift gears; fit programmable light to adjust threshold."; break;
                case PartCategory.Interior: p.massKg = upgrade ? 15 : 60; label = upgrade ? "minimal lightweight interior" : "full cabin trim and passenger furnishings"; p.description = "Removable cabin mass. Can strip entirely and reinstall the retained original; driver seating/control safety hardware stays in the chassis. No mechanical wear."; break;
                case PartCategory.Accessories: p.massKg = upgrade ? 8 : 20; label = upgrade ? "lightweight comfort equipment" : "comfort equipment and insulation"; p.description = "Optional nonessential comfort equipment. Can remove entirely for weight reduction; engine cooling, charging and starter stay separate. No mechanical wear."; break;
                default: label = upgrade ? "reinforced " + p.category : "factory " + Friendly(p.category.ToString()); p.description = "Torque-bearing drive path. Stronger hardware survives higher load without an automatic horsepower or traction bonus."; break;
            }
            if (p.category == PartCategory.Differential && spec.drive == DriveType.AWD && n == 0) {
                label = "factory center/coupling hardware (" + spec.awd.mode + ")";
                p.description = "Factory center or coupling topology: " + spec.awd.mode + ". Prototype configurations are shown in AWD settings; front and rear axle differentials are separate parts.";
            }
            if (p.massKg <= 0) {
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
                    case PartCategory.Gearbox:
                        var actualType=p.transmission==TransmissionUpgrade.Factory?spec.transmission.type:(TransmissionType)((int)p.transmission-1);
                        p.massKg = 45 + n * 2 + (actualType==TransmissionType.Automatic||actualType==TransmissionType.DCT?15:actualType==TransmissionType.CVT?10:0);break;
                    case PartCategory.Wheels: p.massKg = 4 * (spec.wheels.assemblyMassKg - Math.Min(6,spec.wheels.assemblyMassKg*.6f)) * p.massScale; break;
                    case PartCategory.Tires: p.massKg = 4*Math.Min(6,spec.wheels.assemblyMassKg*.6f); break;
                    case PartCategory.Body: p.massKg = (float)MassDynamics.ReferenceBodyMass(spec) * p.massScale; break;
                    case PartCategory.Brakes: p.massKg = upgrade ? 32 : 28; break;
                    case PartCategory.Controls: p.massKg = 1.5f; break;
                    case PartCategory.RevLimiter: case PartCategory.TwoStep: case PartCategory.ShiftLight: p.massKg = .2f; break;
                    case PartCategory.FrontAxle: case PartCategory.RearAxle: p.massKg = upgrade ? 10 : 8; break;
                    case PartCategory.TransferCase: p.massKg = upgrade ? 18 : 14; break;
                    case PartCategory.Driveshaft: case PartCategory.FrontDriveshaft: case PartCategory.RearDriveshaft: p.massKg = upgrade ? 6 : 4; break;
                }
            }
            p.positionFromFrontAxleM = p.category == PartCategory.Interior ? spec.wheelbaseM * .6f : p.category == PartCategory.Accessories ? spec.wheelbaseM * .5f : p.category == PartCategory.RearDifferential || p.category == PartCategory.RearAxle ? spec.wheelbaseM : p.category == PartCategory.Driveshaft || p.category == PartCategory.TransferCase || p.category == PartCategory.FrontDriveshaft || p.category == PartCategory.RearDriveshaft ? spec.wheelbaseM * .5f : spec.wheelbaseM * .15f;
            if(p.category==PartCategory.Differential)p.positionFromFrontAxleM=spec.drive==DriveType.RWD?spec.wheelbaseM:spec.drive==DriveType.FWD?0:spec.wheelbaseM*.5f;
            if(p.category==PartCategory.Wheels||p.category==PartCategory.Tires||p.category==PartCategory.Brakes)p.positionFromFrontAxleM=spec.wheelbaseM*.5f;
            p.heightM = p.category == PartCategory.Interior || p.category == PartCategory.Accessories ? .65f : .35f;
            if (label.Length > 0) p.name = spec.displayName + " / " + label + (p.manualDownshiftInterlock ? " / downshift interlock" : "");
        }
        public static void ApplyRemoval(PartCategory c, VehicleSpec spec, Tune tune)
        {
            if (c == PartCategory.RevLimiter) spec.engineBehavior.limiter = LimiterStyle.None;
            if (c == PartCategory.TwoStep) spec.engineBehavior.twoStep = false;
            // Optional removal mass uses the actual OEM reference in the resolver.
            
            if (c == PartCategory.Exhaust) { spec.sound.exhaustSystem = ExhaustSetup.Preset(3); spec.sound.exhaust = 3; }
            if (c == PartCategory.Induction) {
                // De-boost the authored curve before replacing its factory reference.
                spec.torque = spec.torque.Select(p => new TorquePoint(p.rpm, (float)InductionDynamics.UnboostedTorque(spec, new Tune(), p.rpm))).ToArray();
                spec.induction.factory = InductionType.NaturallyAspirated; spec.induction.torqueCurveIncludesFactoryInduction = false;
                spec.engineBehavior.twoStep = spec.engineBehavior.launchPreload = spec.engineBehavior.antiLag = false;
            }
            if (c == PartCategory.Controls) { spec.engineBehavior.twoStep = spec.engineBehavior.launchPreload = spec.engineBehavior.antiLag = false; spec.engineBehavior.limiter = LimiterStyle.None; }
        }
        public static void ApplyMassDelta(VehicleSpec spec, PartDefinition part, double delta)
        {
            if (part.category == PartCategory.Wheels || part.category == PartCategory.Tires || part.category == PartCategory.Flywheel || part.category == PartCategory.Body) return;
            spec.partMassDeltaKg += (float)delta; spec.partLongitudinalMomentKgM += (float)(delta * part.positionFromFrontAxleM); spec.partVerticalMomentKgM += (float)(delta * part.heightM);
        }
        public static void Apply(PartDefinition p, VehicleSpec spec, Tune tune, PartDefinition stock)
        {
            ApplyMassDelta(spec, p, p.massKg - (stock == null ? 0 : stock.massKg));
            ServiceHardware.Apply(p,spec,tune);
            if(p.category==PartCategory.Pistons)CombustionHardware.ApplyPistons(p,spec);
            if (p.category == PartCategory.FuelSystem) spec.fuelPowerCapacityHp = p.maxPowerHp;
            FuelingHardware.Apply(spec, p);
            if (p.category == PartCategory.Ignition) spec.ignitionPowerCapacityHp = p.maxPowerHp;
            if (p.category == PartCategory.Induction) { tune.partBoost = p.boostScale; tune.partPower *= p.power; }
            if (p.category == PartCategory.Exhaust && p.stage > 0) { spec.sound.exhaustSystem = ExhaustSetup.Preset(p.stage); spec.sound.exhaust = 0; }
            if (IsDifferential(p.category)) {
                var data = new DifferentialSpec { kind = p.differential, torqueBiasRatio = p.torqueBiasRatio, preloadNm = p.preloadNm };
                if (p.category == PartCategory.FrontDifferential || p.category == PartCategory.Differential && spec.drive == DriveType.FWD) spec.frontDifferential = data;
                else if (p.category == PartCategory.RearDifferential || p.category == PartCategory.Differential && spec.drive == DriveType.RWD) spec.rearDifferential = data;
                else if (p.stage > 0) { var center = AxleDriveDynamics.Configuration(spec, tune); center.mode = p.stage == 1 ? AwdMode.LimitedSlipCenter : AwdMode.LockedCenter; center.torqueBiasRatio = p.torqueBiasRatio; center.centerPreloadNm = p.preloadNm; tune.awd = center; tune.awdConfigured = true; }
            }
            if (p.category == PartCategory.TransferCase && p.stage > 0) { var data = AxleDriveDynamics.Configuration(spec, tune); data.responseSeconds = Math.Min(data.responseSeconds, .1f); data.isolateFailedAxle = true; tune.awd = data; tune.awdConfigured = true; }
        }
        static bool AllowsBoost(PartDefinition p) { return PartTuning.Allows(p, PartTuningOptions.Boost); }
        public static IEnumerable<PartStat> Stats(PartDefinition p)
        {
            if(p!=null)foreach(var stat in ServiceHardware.Stats(p))yield return stat;
            if (p == null) yield break;
            if (TorqueRated(p.category)) yield return new PartStat("torque", "Torque strength", p.torqueLimitNm, "Nm", 1, "Load headroom at this component's shaft; not engine output.");
            if(PartTuning.Allows(p,PartTuningOptions.CenterRatio)){yield return new PartStat("centerratiomin","Minimum center speed ratio",p.centerRatioMin,":1",0,"Interchangeable center gearsets; speed ratio is distinct from torque bias.");yield return new PartStat("centerratiomax","Maximum center speed ratio",p.centerRatioMax,":1",0,"Front wheel angular speed / rear wheel angular speed. Match rear tire radius divided by front radius for straight-line rolling.");}
            if (EngineInternal(p.category)) yield return new PartStat("rpm", "Internal safe RPM", p.rpmLimit, "RPM", 1, "Internal engine speed limit; not a limit imposed by upholstery or a differential.");
            if (Wears(p.category)) yield return new PartStat("heat", "Heat tolerance", p.thermalLimitC, "°C", 1, "Above this provisional limit, thermal damage accumulates.");
            if(p.category==PartCategory.Clutch){yield return new PartStat("plates","Friction plate count",p.clutchPlateCount,"plates",0,"Matched complete assembly; changing the count requires replacing hardware.");if(p.clutchEnvironment==ClutchEnvironment.Wet)yield return new PartStat("clutch-oil","Dedicated clutch oil capacity",p.clutchOilCapacityLitres,"L",0,"Separate bath from engine oil; low level reduces cooling and coupling capacity.");}
            if (p.category == PartCategory.Clutch || p.category == PartCategory.Brakes) yield return new PartStat("life", "Friction-work life budget", p.wearEnergyJ / 1e6, "MJ", 1, "Larger energy budget lasts longer under equal friction work.");
            if (p.category == PartCategory.Cooling) {
                yield return new PartStat("transfer", "Engine heat transfer", p.engineHeatTransferScale * 100, "%", 1, "Conductance relative to factory cooling; condition can reduce it.");
                yield return new PartStat("radiator", "Radiator heat rejection", p.coolingWattsPerC, "W/°C", 1, "Heat rejected per degree above ambient before road airflow.");
                yield return new PartStat("thermalmass", "Coolant thermal capacity", p.heatCapacityJPerC / 1000, "kJ/°C", 1, "More capacity slows temperature changes; does not eliminate heat.");
            }
            if(p.category==PartCategory.Pistons){yield return new PartStat("crownvolume","Crown volume",p.crownVolumeCc,"cc",0,"Positive dish volume lowers compression; negative dome volume raises it. Compare effective compression on the complete build.");yield return new PartStat("relief","Valve relief depth",p.valveReliefMm,"mm",1,"Provides valve clearance, subject to cam lift and timing.");}
            if (p.category == PartCategory.FuelSystem || p.category == PartCategory.Ignition) yield return new PartStat("powerlimit", "Supported engine power", p.maxPowerHp, "hp", 1, "Output beyond this delivery capacity is capped; below it there is no free HP.");
            if (p.category == PartCategory.Controls && p.supportsInjection) yield return new PartStat("injectorcylinders", "Supported engine cylinders", p.controllerCylinderCapacity, "cylinders", 0, "Authored engine/calibration compatibility, not the number of physical injectors. A throttle-body kit can feed multiple cylinders with shared injectors. Fueling capability does not imply programmable launch/limiter access.");
            if (p.category == PartCategory.FuelSystem && p.fuelDelivery != FuelDeliveryHardware.Factory) {
                yield return new PartStat("idlecontrol", "Injection idle control", p.injectionIdleControl*100, "%", 1, "Stabilizes idle while retaining cam-driven roughness.");
                yield return new PartStat("mixturevariation", "Injection metering variation", p.injectionMixtureVariation*100, "%", -1, "Lower variation smooths mixture transitions; no automatic torque multiplier.");
            }
            if (p.category == PartCategory.HeadIntake || p.category == PartCategory.Induction) yield return new PartStat("airflow", "Airflow torque multiplier", p.power, "×", 1, "Multiplier before boost and fuel/ignition limits.");
            if (p.category == PartCategory.Induction) yield return new PartStat("boost", "Boost delivery multiplier", p.boostScale, "×", 0, "More boost can raise output, heat and stress; topology also changes response.");
            if (p.category == PartCategory.Induction && p.turboUnits > 0) {
                yield return new PartStat("turbohardware", "Turbo hardware size (per unit)", p.turboSize, "× reference", 0, "Physical compressor/turbine size. Replace the kit to change it; larger increases flow and delays spool.");
                yield return new PartStat("spoolscale", "Turbo spool time multiplier", Math.Pow(p.turboSize, 1.7), "×", -1, "Applied to transient rotor response, separately from twin-turbo response factor.");
                yield return new PartStat("boostfloor", "Minimum / fixed boost target", AllowsBoost(p) ? p.boostMin : p.fixedBoost, "×", 0, "Wastegate spring sets the floor. Adjustability cannot command below the installed spring.");
            }
            if (p.category == PartCategory.Induction && p.superchargerUnits > 0) yield return new PartStat("pulley", "Default / fixed blower drive ratio", p.fixedPulley, "×", 0, "Greater drive ratio increases boost and parasitic load. Only an included pulley set allows adjustment.");
            if (p.category == PartCategory.Gearbox && p.fixedFinalDrive > 0) yield return new PartStat("fixedratio", "Fixed final-drive ratio", p.fixedFinalDrive, ":1", 0, "Higher ratio increases shaft torque at the wheels and lowers speed per RPM; no free horsepower.");
            if (p.category == PartCategory.Gearbox && p.manualLayout == ManualGearboxLayout.Sequential) yield return new PartStat("selector", "Sequential manual selection scale", p.sequentialShiftScale == 0 ? .45 : p.sequentialShiftScale, "×", -1, "Multiplies H-pattern selection time in addition to the gearbox shift-duration multiplier; no power gain or extra clutch.");
            if (p.category == PartCategory.Gearbox) yield return new PartStat("shift", "Shift duration multiplier", p.shiftScale, "×", -1, "Multiplies the installed transmission's shift duration.");
            if (p.category == PartCategory.Clutch || p.category == PartCategory.Gearbox || p.category == PartCategory.Brakes) yield return new PartStat("capacity", "Coupling/brake capacity multiplier", p.capacityScale, "×", 1, "Applied to this component's coupling or stopping capacity, subject to grip.");
            if (p.category == PartCategory.Tires) {
                yield return new PartStat("grip", "Compound friction multiplier", p.grip, "×", 1, "Traction at equal load; heat and individual tire condition reduce actual grip.");
                yield return new PartStat("rollinglife", "Rolling life at reference load", p.tireLifeDistanceM/1000, "km", 1, "Fictional reference life; pressure, load and slip can shorten it.");
                yield return new PartStat("slidinglife", "Sliding-work life per tire", p.wearEnergyJ/1e6, "MJ", 1, "Compound lifetime under equal sliding work, independently of peak grip.");
                yield return new PartStat("tireoptimal", "Optimal tire temperature", p.tireOptimalC, "°C", 0, "Too cold or too hot changes grip; higher optimum needs more warm-up.");
            }
            if (p.category == PartCategory.Wheels || p.category == PartCategory.Body || p.category == PartCategory.Flywheel) yield return new PartStat("massscale", "Mass multiplier", p.massScale, "×", -1, "Lowers scale weight; rotating components also lower inertia.");
            if (p.category == PartCategory.Flywheel) yield return new PartStat("inertia", "Flywheel inertia", p.inertiaKgM2, "kg m²", 0, "More momentum smooths engagement; less allows faster RPM changes.");
            if (p.massKg > 0) yield return new PartStat("mass", "Component mass", p.massKg, "kg", -1, "Replacement difference or removal changes the car's translating mass.");
            if (IsDifferential(p.category)) {
                if (p.differential == DifferentialKind.LimitedSlip) { yield return new PartStat("bias", "Maximum torque bias", p.torqueBiasRatio, ":1", 1, "Bounded torque transfer to the wheel with more grip."); yield return new PartStat("preload", "LSD preload", p.preloadNm, "Nm", 0, "Provides coupling even when one output has little resistance."); }
            }
        }
    }
}
