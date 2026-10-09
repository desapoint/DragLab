using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    [Serializable]
    public sealed class PartDefinition
    {
        public string id, name, fitment, quality = "OEM";
        public PartCategory category;
        public float boreMm, power = 1, grip = 1, massScale = 1, shiftScale = 1, capacityScale = 1, boostScale = 1;
        public float rpmLimit, torqueLimitNm, massKg, inertiaKgM2;
        public float positionFromFrontAxleM, heightM = .4f;
        public int price, stage;
        public ClutchEnvironment clutchEnvironment; public ClutchMaterial clutchMaterial; public ClutchActuation clutchActuation;
        public int clutchPlateCount=1; public float clutchOilCapacityLitres, clutchEngagementRpm=1800;
        public PartTuningOptions tuning;
        public float turboSize = 1, fixedBoost = 1, boostMin = .65f, boostMax = 1.2f;
        public float fixedPulley = 1, pulleyMin = .85f, pulleyMax = 1.2f;
        public float fixedFinalDrive, finalDriveMin = 2, finalDriveMax = 6;
        public BlowOffType valve = BlowOffType.Factory;
        public int turboUnits, superchargerUnits;
        public FuelDeliveryHardware fuelDelivery;
        public bool supportsInjection;
        public int injectionControlMetadataVersion;
        public int controllerCylinderCapacity;
        public float injectionIdleControl = .95f, injectionMixtureVariation = .04f;
        public float camLiftMm, camDurationDegrees, camOverlapDegrees, camLobeSeparation, camTorqueBias, valvetrainRpm;
        public bool camTimingAdjustable;
        public float fluidCapacityLitres, fluidMinimumLitres, baffling, pumpFlowScale=1, capPressureBar=1.1f;
        public EngineUpgradeInduction induction;
        public TransmissionUpgrade transmission;
        public bool manualDownshiftInterlock;
        public ManualGearboxLayout manualLayout;
        public float sequentialShiftScale = .45f;
        public float thermalLimitC = 180, heatCapacityJPerC = 12000, coolingWattsPerC = 25, wearEnergyJ = 100000000, overloadSeconds = 3;
        public float tireLifeDistanceM = 40000000, tireOptimalC = 45, tireHeatFraction = .45f;
        public string description;
        public DifferentialKind differential;
        public float torqueBiasRatio = 1, preloadNm, engineHeatTransferScale = 1, maxPowerHp;
        public string provenance = "Fictional part specification; platform interfaces are game-authored, not manufacturer fitment.";
        public PartDefinition Copy() { return (PartDefinition)MemberwiseClone(); }
    }
    public sealed class BuildResolution
    {
        public VehicleSpec spec;
        public Tune tune;
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public float safeRpm, safeTorqueNm;
        public bool Drivable { get { return errors.Count == 0; } }
    }
    // Stable, authored definitions; interchangeable sets are distinct from their owned instances.
    public sealed class MechanicalCatalog
    {
        public const int Version = 2;
        public readonly List<PartDefinition> parts = new List<PartDefinition>();
        readonly Dictionary<string, VehicleSpec> specs = new Dictionary<string, VehicleSpec>();
        public static readonly PartCategory[] EngineSlots = { PartCategory.Pistons, PartCategory.Rods, PartCategory.Crankshaft, PartCategory.HeadIntake, PartCategory.FuelSystem, PartCategory.Ignition, PartCategory.Cooling, PartCategory.Induction, PartCategory.Exhaust, PartCategory.Flywheel };
        public MechanicalCatalog(IEnumerable<VehicleSpec> vehicles, bool applyOverrides=true)
        {
            foreach (var source in vehicles) { var spec=source.Copy(); specs.Add(spec.id,spec.Copy()); Author(spec); }
            if(applyOverrides)PartCatalogOverrides.Apply(this,PartCatalogOverrides.Load());
        }
        public VehicleSpec Spec(string id) { return specs[id].Copy(); }
        public PartDefinition Find(string id) { return parts.Find(p => p.id == id); }
        public static bool EngineSlot(PartCategory category) { return Array.IndexOf(EngineSlots, category) >= 0 || ServiceHardware.IsCategory(category); }
        public static bool DisablingEngineFailure(PartCategory category) { return category == PartCategory.Pistons || category == PartCategory.Rods || category == PartCategory.Crankshaft || category == PartCategory.FuelSystem || category == PartCategory.Ignition || ServiceHardware.CoreFailure(category) || category==PartCategory.ValveSprings || category==PartCategory.Followers; }
        public static bool DriveBranch(PartCategory category) { return category >= PartCategory.FrontAxle && category <= PartCategory.TransferCase; }
        public static bool RoadOnly(PartCategory category) { return DriveBranch(category) || category == PartCategory.Brakes || category == PartCategory.Driveshaft || category == PartCategory.Differential || category == PartCategory.Gearbox || category == PartCategory.Clutch; }
        public static IEnumerable<PartCategory> Slots(VehicleSpec spec)
        {
            foreach (PartCategory category in Enum.GetValues(typeof(PartCategory)))
            {
                if (!DriveBranch(category) || spec.drive == DriveType.AWD || category == PartCategory.FrontAxle && spec.drive == DriveType.FWD || category == PartCategory.RearAxle && spec.drive == DriveType.RWD) yield return category;
            }
        }
        public static float Bore(VehicleSpec spec) { return spec.engineBoreMm > 0 ? spec.engineBoreMm : spec.id == "starter-hatch" ? 78 : spec.id == "sport-coupe" ? 86 : spec.id == "utility-pickup" ? 96 : 99; }
        public static string Interface(VehicleSpec spec, PartCategory category)
        {
            // The two injected four-cylinder engines share a fuel/control connector, not their piston geometry.
            if (category == PartCategory.Controls) return "controls-v1";
            if (category == PartCategory.FuelSystem && (spec.fuelSystemConverted ? spec.originalFueling : spec.engineBehavior.fueling) == FuelSystem.Injection) return spec.sound.cylinders == 4 ? "efi-gasoline-v1" : "efi-gasoline-" + spec.sound.cylinders + "-v1";
            if (category == PartCategory.Ignition && spec.sound.cylinders == 4) return "coil-four-v1";
            return spec.id + "/" + category;
        }
        public string Id(string model, PartCategory category, int stage = 0) { return model + "/" + category + "/" + stage; }
        void Author(VehicleSpec spec)
        {
            float peak = spec.torque.Max(t => t.torqueNm);
            foreach (PartCategory category in Enum.GetValues(typeof(PartCategory)))
            {
                if(ServiceHardware.IsCategory(category))continue;
                int levels = category == PartCategory.HeadIntake || category == PartCategory.Tires || category == PartCategory.Body || category == PartCategory.Gearbox || category == PartCategory.Exhaust || PartSemantics.IsDifferential(category) ? 4 : 2;
                for (int level = 0; level < levels; level++)
                {
                    var p = new PartDefinition { id = Id(spec.id, category, level), category = category, stage = level,
                        name = spec.displayName + " / " + category + " / " + (level == 0 ? "OEM" : "option " + level),
                        fitment = Interface(spec, category), price = level == 0 ? 180 : 600 * level,
                        boreMm = category == PartCategory.Pistons ? Bore(spec) : 0,
                        rpmLimit = spec.redlineRpm * (level == 0 ? 1.08f : 1.2f), torqueLimitNm = peak * (level == 0 ? 1.55f : 2.6f) };
                    if (category == PartCategory.Pistons || category == PartCategory.Rods || category == PartCategory.Crankshaft)
                    {
                        bool forged = level > 0 || spec.id == "sport-coupe";
                        p.quality = forged ? "Forged" : "Cast / standard"; p.name += " / " + p.quality;
                        if (forged) p.torqueLimitNm = peak * 2.6f;
                        if (level == 0 && forged) p.price = 420;
                        // Strength is headroom, never an automatic horsepower bonus.
                    }
                    if (category == PartCategory.HeadIntake) p.power = 1 + level * .16f;
                    if (category == PartCategory.Tires) p.grip = 1 + level * .12f;
                    if (category == PartCategory.Body) p.massScale = 1 - level * .045f;
                    if (category == PartCategory.Gearbox) { p.shiftScale = 1 - level * .18f; p.capacityScale = 1 + level * .15f; p.manualLayout = spec.transmission.manualLayout; p.sequentialShiftScale = (float)spec.transmission.SequentialShiftScale; }
                    if (category == PartCategory.Flywheel) { p.massScale = level == 0 ? 1 : .55f; p.massKg = spec.flywheel.massKg * p.massScale; p.inertiaKgM2 = (float)(spec.flywheel.Inertia * p.massScale); }
                    if (category == PartCategory.Wheels) p.massScale = level == 0 ? 1 : .8f;
                    if (category == PartCategory.Clutch) p.capacityScale = level == 0 ? 1 : 1.6f;
                    if (category == PartCategory.Clutch) { p.thermalLimitC = 260; p.coolingWattsPerC = 10; p.wearEnergyJ = level == 0 ? 30000000 : 65000000; }
                    if (category == PartCategory.Gearbox) { p.thermalLimitC = 140; p.heatCapacityJPerC = 18000; }
                    if (category == PartCategory.Driveshaft || category == PartCategory.Differential) { p.torqueLimitNm *= spec.gears[0]; p.heatCapacityJPerC = 5000; }
                    if (DriveBranch(category)) { p.torqueLimitNm *= spec.gears[0] * (category == PartCategory.FrontAxle || category == PartCategory.RearAxle ? spec.finalDrive : 1); p.heatCapacityJPerC = 5000; }
                    if (category == PartCategory.Brakes)
                    {
                        p.capacityScale = level == 0 ? 1 : 1.2f;
                        // A brake rotor normally runs hotter than an oil-cooled gearbox.
                        // Aggregate four-wheel thermal mass and friction-energy life;
                        // the old generic 180 C / 100 MJ limits killed OEM brakes after a few stops.
                        p.thermalLimitC = level == 0 ? 550 : 650;
                        p.coolingWattsPerC = 60;
                        p.wearEnergyJ = level == 0 ? 3000000000f : 6000000000f;
                    }
                    if (category == PartCategory.FuelSystem && spec.engineBehavior.fueling == FuelSystem.Injection) p.torqueLimitNm = level == 0 ? 650 : 1000;
                    PartSemantics.Author(p, spec);
                    parts.Add(p);
                }
                if (spec.id == "sport-coupe" && (category == PartCategory.Pistons || category == PartCategory.Rods || category == PartCategory.Crankshaft))
                    parts.Add(new PartDefinition { id = spec.id + "/" + category + "/cast", name = spec.displayName + " / " + category + " / regular cast replacement", category = category,
                        quality = "Cast / standard", fitment = Interface(spec, category), boreMm = category == PartCategory.Pistons ? Bore(spec) : 0,
                        price = 180, rpmLimit = spec.redlineRpm * 1.08f, torqueLimitNm = peak * 1.55f });
            }
            foreach (EngineUpgradeInduction kit in Enum.GetValues(typeof(EngineUpgradeInduction)))
                if (kit != EngineUpgradeInduction.Factory) parts.Add(new PartDefinition { id = spec.id + "/Induction/" + kit, name = spec.displayName + " / " + kit + " kit", category = PartCategory.Induction, fitment = Interface(spec, PartCategory.Induction), induction = kit, price = 1800, rpmLimit = spec.redlineRpm * 1.2f, torqueLimitNm = peak * 2.6f });
            foreach (TransmissionUpgrade box in Enum.GetValues(typeof(TransmissionUpgrade)))
                if (box != TransmissionUpgrade.Factory) parts.Add(new PartDefinition { id = spec.id + "/Gearbox/" + box, name = spec.displayName + " / " + box + " gearbox", category = PartCategory.Gearbox, fitment = Interface(spec, PartCategory.Gearbox), transmission = box, price = 2400, stage = 1, shiftScale = .82f, capacityScale = 1.15f, rpmLimit = spec.redlineRpm * 1.2f, torqueLimitNm = peak * 2.6f });
            parts.Add(new PartDefinition { id = spec.id + "/Gearbox/SequentialManual", category = PartCategory.Gearbox, fitment = Interface(spec, PartCategory.Gearbox), transmission = TransmissionUpgrade.Manual, manualLayout = ManualGearboxLayout.Sequential, sequentialShiftScale = .45f, price = 3200, stage = 1, shiftScale = .82f, capacityScale = 1.15f, torqueLimitNm = peak * 2.6f });
            parts.Add(new PartDefinition { id = spec.id + "/Gearbox/ManualInterlocked", name = spec.displayName + " / actuated manual with downshift interlock", category = PartCategory.Gearbox, fitment = Interface(spec, PartCategory.Gearbox), transmission = TransmissionUpgrade.Manual, manualDownshiftInterlock = true, price = 2800, stage = 1, rpmLimit = spec.redlineRpm * 1.2f, torqueLimitNm = peak * 2.6f });
            foreach (var part in parts.Where(p => p.id.StartsWith(spec.id + "/") && p.description == null)) PartSemantics.Author(part, spec);
            CatalogVariants.Add(this, spec);
            FuelingHardware.AddConversions(this, spec);
            ServiceHardware.Add(this,spec);ClutchHardware.Add(this,spec);
        }
        public Dictionary<PartCategory, string> Recipe(string model, Tune legacy = null)
        {
            var result = new Dictionary<PartCategory, string>();
            foreach (PartCategory category in Slots(specs[model])) {
                if(category==PartCategory.OilCooler)continue;
                if (category == PartCategory.ShiftLight || category == PartCategory.RevLimiter && specs[model].engineBehavior.limiter == LimiterStyle.None || category == PartCategory.TwoStep && !specs[model].engineBehavior.twoStep) continue;
                result[category] = Id(model, category);
            }
            if (legacy != null)
            {
                result[PartCategory.HeadIntake] = Id(model, PartCategory.HeadIntake, Math.Max(0, Math.Min(3, legacy.engineLevel)));
                result[PartCategory.Tires] = Id(model, PartCategory.Tires, Math.Max(0, Math.Min(3, legacy.tireLevel)));
                result[PartCategory.Body] = Id(model, PartCategory.Body, Math.Max(0, Math.Min(3, legacy.weightLevel)));
                result[PartCategory.Gearbox] = legacy.transmissionUpgrade == TransmissionUpgrade.Factory ? Id(model, PartCategory.Gearbox, Math.Max(0, Math.Min(3, legacy.gearboxLevel))) : model + "/Gearbox/" + legacy.transmissionUpgrade;
                result[PartCategory.Exhaust] = Id(model, PartCategory.Exhaust, Math.Max(0, Math.Min(3, legacy.exhaustLevel)));
                result[PartCategory.Controls] = Id(model, PartCategory.Controls, Math.Max(0, Math.Min(1, legacy.controlLevel)));
                if (legacy.controlLevel > 0) { result[PartCategory.RevLimiter] = Id(model, PartCategory.RevLimiter, 1); result[PartCategory.TwoStep] = Id(model, PartCategory.TwoStep, 1); }
                if (legacy.inductionUpgrade != EngineUpgradeInduction.Factory) result[PartCategory.Induction] = model + "/Induction/" + legacy.inductionUpgrade;
            }
            return result;
        }
        public bool Fits(VehicleSpec spec, PartDefinition part)
        {
            if(part==null || !Slots(spec).Contains(part.category) || part.fitment!=Interface(spec,part.category) || part.boreMm!=0 && Math.Abs(part.boreMm-Bore(spec))>=.01)return false;
            if(part.category!=PartCategory.Controls)return true;
            if(part.supportsInjection && part.controllerCylinderCapacity<spec.sound.cylinders)return false;
            return spec.engineBehavior.fueling!=FuelSystem.Injection || FuelingHardware.ControllerSupports(part,spec);
        }
        public BuildResolution Resolve(string model, IDictionary<PartCategory, string> installed, Tune adjustments = null)
        {
            var result = new BuildResolution { spec = Spec(model), tune = adjustments == null ? new Tune { finalDrive = specs[model].finalDrive, shiftRpm = specs[model].redlineRpm - 200 } : adjustments.Copy(), safeRpm = float.MaxValue, safeTorqueNm = float.MaxValue };
            var factoryRecipe = Recipe(model);
            var t = result.tune; t.partsResolved = true;
            result.spec.partMassDeltaKg = result.spec.partLongitudinalMomentKgM = result.spec.partVerticalMomentKgM = 0; result.spec.fuelPowerCapacityHp = result.spec.ignitionPowerCapacityHp = 0; t.partGearCapacity = 1; t.partPower = t.partGrip = t.partMass = t.partShift = t.partCapacity = t.partBoost = 1;
            t.partLimiterAdjustable = t.partTwoStepAdjustable = t.partShiftLight = t.partShiftLightAdjustable = false;
            t.partTuningConfigured = true;
            t.partBoostAdjustable = t.partPulleyAdjustable = t.partValveAdjustable = t.partFinalDriveAdjustable = false;
            t.partTurboSize = t.partFixedBoost = t.partFixedPulley = 1;
            t.partFixedValve = BlowOffType.Factory; t.partFixedFinalDrive = result.spec.finalDrive;
            t.engineLevel = t.tireLevel = t.weightLevel = t.gearboxLevel = t.exhaustLevel = t.controlLevel = 0;
            t.inductionUpgrade = EngineUpgradeInduction.Factory; t.transmissionUpgrade = TransmissionUpgrade.Factory;
            foreach (PartCategory category in Slots(result.spec))
            {
                string id; var part = installed.TryGetValue(category, out id) ? Find(id) : null;
                if (part == null) {
                    if (category == PartCategory.Body) result.spec.hasStructuralBody = false;
                    string effect = PartSemantics.RemovalEffect(category, result.spec, installed, this);
                    if (PartSemantics.Optional(category, result.spec, installed, this)) { if (category == PartCategory.Cooling) result.warnings.Add(effect); PartSemantics.ApplyRemoval(category, result.spec, t); }
                    else (RoadOnly(category) ? result.warnings : result.errors).Add(effect);
                    if (factoryRecipe.ContainsKey(category)) PartSemantics.ApplyMassDelta(result.spec, Find(factoryRecipe[category]), -Find(factoryRecipe[category]).massKg);
                    continue;
                }
                if (part.category != category || !Fits(result.spec, part)) { result.errors.Add("Incompatible " + category); continue; }
                if (PartSemantics.EngineInternal(category)) {
                    result.safeRpm = Math.Min(result.safeRpm, part.rpmLimit);
                    result.safeTorqueNm = Math.Min(result.safeTorqueNm, part.torqueLimitNm);
                }
                PartSemantics.Apply(part, result.spec, t, factoryRecipe.ContainsKey(category) ? Find(factoryRecipe[category]) : null);
                PartTuning.Project(part, t, specs[model]);
                switch (category)
                {
                    case PartCategory.HeadIntake: t.partPower *= part.power; t.engineLevel = part.stage; break;
                    case PartCategory.Tires: t.partGrip = part.grip; t.tireLevel = part.stage; break;
                    case PartCategory.Body: t.partMass = part.massScale; t.weightLevel = part.stage; break;
                    case PartCategory.Gearbox: t.partShift = part.shiftScale; t.partGearCapacity = part.capacityScale; t.gearboxLevel = part.stage; t.transmissionUpgrade = part.transmission; result.spec.transmission.manualDownshiftInterlock = part.manualDownshiftInterlock; result.spec.transmission.manualLayout = part.manualLayout; result.spec.transmission.sequentialShiftScale = part.sequentialShiftScale; break;
                    case PartCategory.Clutch: t.partCapacity *= part.capacityScale; break;
                    case PartCategory.Induction: t.inductionUpgrade = part.induction; break;
                    case PartCategory.Exhaust: t.exhaustLevel = part.stage; break;
                    case PartCategory.Controls: t.controlLevel = part.stage; break;
                    case PartCategory.RevLimiter:
                        t.partLimiterAdjustable = PartTuning.Allows(part, PartTuningOptions.Limiter);
                        if (result.spec.engineBehavior.limiter == LimiterStyle.None) result.spec.engineBehavior.limiter = LimiterStyle.Hard;
                        break;
                    case PartCategory.TwoStep: t.partTwoStepAdjustable = PartTuning.Allows(part, PartTuningOptions.TwoStep); result.spec.engineBehavior.twoStep = true; break;
                    case PartCategory.ShiftLight: t.partShiftLight = true; t.partShiftLightAdjustable = PartTuning.Allows(part, PartTuningOptions.ShiftLight); break;
                    case PartCategory.Flywheel: t.flywheelMass = part.massScale; break;
                    case PartCategory.Wheels: t.wheelMass = part.massScale; break;
                    case PartCategory.Brakes: result.spec.brakeDecelerationMps2 = (float)(result.spec.BrakeDeceleration * part.capacityScale); break;
                }
            }

            if (result.spec.engineBehavior.fueling == FuelSystem.Carburetor) { result.spec.engineBehavior.cut = result.spec.engineBehavior.launchCut = CutMethod.Ignition; t.limiterCut = t.twoStepCut = CutMethod.Ignition; }
            else {
                string controllerId; var controller = installed.TryGetValue(PartCategory.Controls, out controllerId) ? Find(controllerId) : null;
                if (controller != null && !FuelingHardware.ControllerSupports(controller, result.spec)) result.errors.Add("EFI requires injection-capable Controls compatible with this engine's cylinder count: install the fixed EFI controller or programmable engine controller.");
            }
            result.spec.valvetrain.camTimingDegrees=result.spec.valvetrain.timingAdjustable?(float)DragMath.Clamp(t.camTimingDegrees,-6,6):0;
            t.camTimingDegrees=result.spec.valvetrain.camTimingDegrees;
            t.ClampTo(result.spec);
            // Preserve authored factory calibration; fitted boost headroom is independent of old engine ranks.
            if (result.safeRpm == float.MaxValue) result.safeRpm = result.spec.redlineRpm * 1.08f;
            if (result.safeTorqueNm == float.MaxValue) result.safeTorqueNm = 0;
            result.spec.engineBehavior.safeRpmMultiplier = (float)DragMath.Clamp(result.safeRpm / result.spec.redlineRpm, 1.01, 1.4);
            t.shiftRpm = Math.Min(t.shiftRpm, result.safeRpm - 100);
            if (t.revLimitRpm > result.safeRpm) t.revLimitRpm = result.safeRpm;
            result.spec.fittedParts = installed.Values.Select(Find).Where(p => p != null).Select(p => p.Copy()).ToArray();
            result.spec.hasInstalledParts = true;
            return result;
        }
    }
}
