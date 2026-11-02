#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>
    /// C1-only pre-refactor catalog behavior for disposable Editor reference profiles.
    /// This adapter deliberately does not call MechanicalCatalog.Find, RebuildIndexes,
    /// Spec, Resolve, LookupScope, or a future reader/core implementation.
    /// </summary>
    internal static class LegacyMechanicalCatalogReference
    {
        const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags NestedMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly FieldInfo SpecsField = RequiredField(typeof(MechanicalCatalog), "specs");
        static readonly FieldInfo AuthoringField = RequiredField(typeof(MechanicalCatalog), "authoring");
        static readonly FieldInfo PartIndexField = RequiredField(typeof(MechanicalCatalog), "partIndex");
        static readonly FieldInfo IndexedPartCountField = RequiredField(typeof(MechanicalCatalog), "indexedPartCount");
        static readonly FieldInfo AuthoringRevisionField = RequiredField(typeof(MechanicalCatalog), "authoringRevision");
        static readonly FieldInfo IndexLockField = RequiredField(typeof(MechanicalCatalog), "indexLock");
        static readonly Type IndexedPartType = typeof(MechanicalCatalog).GetNestedType("IndexedPart", BindingFlags.NonPublic);
        static readonly FieldInfo IndexedValueField = RequiredNestedField("value");
        static readonly FieldInfo IndexedPositionField = RequiredNestedField("position");

        static FieldInfo RequiredField(Type type, string name)
        {
            var field = type.GetField(name, InstanceMembers);
            if (field == null) throw new InvalidOperationException("Frozen MechanicalCatalog field changed: " + name);
            return field;
        }

        static FieldInfo RequiredNestedField(string name)
        {
            if (IndexedPartType == null) throw new InvalidOperationException("Frozen MechanicalCatalog.IndexedPart type changed.");
            var field = IndexedPartType.GetField(name, NestedMembers);
            if (field == null) throw new InvalidOperationException("Frozen MechanicalCatalog.IndexedPart field changed: " + name);
            return field;
        }

        internal static PartDefinition Find(MechanicalCatalog catalog, string id)
        {
            _ = catalog.parts;
            if (id == null) return null;
            if ((bool)AuthoringField.GetValue(catalog)) return catalog.parts.Find(part => part.id == id);

            int indexedCount = (int)IndexedPartCountField.GetValue(catalog);
            var index = PartIndexField.GetValue(catalog) as IDictionary;
            if (index == null || indexedCount != catalog.parts.Count)
            {
                RebuildIndexes(catalog);
                index = PartIndexField.GetValue(catalog) as IDictionary;
            }

            bool indexed = index.Contains(id);
            int position = 0;
            PartDefinition indexedValue = null;
            if (indexed)
            {
                object row = index[id];
                position = (int)IndexedPositionField.GetValue(row);
                indexedValue = IndexedValueField.GetValue(row) as PartDefinition;
            }
            if (indexed && position < catalog.parts.Count && ReferenceEquals(catalog.parts[position], indexedValue) && indexedValue.id == id)
                return indexedValue;

            var found = catalog.parts.Find(part => part.id == id);
            if (found != null || indexedValue != null) RebuildIndexes(catalog);
            return found;
        }

        static void RebuildIndexes(MechanicalCatalog catalog)
        {
            _ = catalog.parts;
            var sync = IndexLockField.GetValue(catalog);
            lock (sync)
            {
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), IndexedPartType);
                var next = (IDictionary)Activator.CreateInstance(dictionaryType, new object[] { StringComparer.Ordinal });
                for (int i = 0; i < catalog.parts.Count; i++)
                {
                    var part = catalog.parts[i];
                    string partId = part.id;
                    if (next.Contains(partId)) continue;
                    object indexedPart = Activator.CreateInstance(IndexedPartType);
                    IndexedValueField.SetValue(indexedPart, part);
                    IndexedPositionField.SetValue(indexedPart, i);
                    next[partId] = indexedPart;
                }
                PartIndexField.SetValue(catalog, next);
                IndexedPartCountField.SetValue(catalog, catalog.parts.Count);
                int revision = (int)AuthoringRevisionField.GetValue(catalog);
                unchecked { AuthoringRevisionField.SetValue(catalog, revision + 1); }
            }
        }

        internal static VehicleSpec Spec(MechanicalCatalog catalog, string id)
        {
            _ = catalog.parts;
            var specs = (Dictionary<string, VehicleSpec>)SpecsField.GetValue(catalog);
            return specs[id].Copy();
        }

        internal static BuildResolution Resolve(MechanicalCatalog catalog, string model,
            IDictionary<PartCategory, string> installed, Tune adjustments = null)
        {
            _ = catalog.parts;
            var specs = (Dictionary<string, VehicleSpec>)SpecsField.GetValue(catalog);
            var result = new BuildResolution
            {
                spec = Spec(catalog, model),
                tune = adjustments == null
                    ? new Tune { finalDrive = specs[model].finalDrive, shiftRpm = specs[model].redlineRpm - 200 }
                    : adjustments.Copy(),
                safeRpm = float.MaxValue,
                safeTorqueNm = float.MaxValue
            };
            var factoryRecipe = catalog.Recipe(model);
            result.spec.combustion.separateSparkPlugs = !CombustionHardware.Diesel(result.spec);
            var tune = result.tune; tune.partsResolved = true;
            result.spec.partMassDeltaKg = result.spec.partLongitudinalMomentKgM = result.spec.partVerticalMomentKgM = 0;
            result.spec.fuelPowerCapacityHp = result.spec.ignitionPowerCapacityHp = 0;
            tune.partGearCapacity = 1; tune.partPower = tune.partGrip = tune.partMass = tune.partShift = tune.partCapacity = tune.partBoost = 1;
            tune.partLimiterAdjustable = tune.partTwoStepAdjustable = tune.partShiftLight = tune.partShiftLightAdjustable = false;
            tune.partTuningConfigured = true;
            tune.partBoostAdjustable = tune.partPulleyAdjustable = tune.partValveAdjustable = tune.partFinalDriveAdjustable = false;
            tune.partTurboSize = tune.partFixedBoost = tune.partFixedPulley = 1;
            tune.partFixedValve = BlowOffType.Factory; tune.partFixedFinalDrive = result.spec.finalDrive;
            tune.engineLevel = tune.tireLevel = tune.weightLevel = tune.gearboxLevel = tune.exhaustLevel = tune.controlLevel = 0;
            tune.inductionUpgrade = EngineUpgradeInduction.Factory; tune.transmissionUpgrade = TransmissionUpgrade.Factory;
            foreach (PartCategory category in MechanicalCatalog.Slots(result.spec))
            {
                string id;
                var part = installed.TryGetValue(category, out id) ? Find(catalog, id) : null;
                if (part == null)
                {
                    if (category == PartCategory.Body) result.spec.hasStructuralBody = false;
                    string effect = RemovalEffect(category, result.spec, installed, catalog);
                    if (Optional(category, result.spec, installed, catalog))
                    { if (category == PartCategory.Cooling) result.warnings.Add(effect); PartSemantics.ApplyRemoval(category, result.spec, tune); }
                    else (MechanicalCatalog.RoadOnly(category) ? result.warnings : result.errors).Add(effect);
                    if (factoryRecipe.ContainsKey(category))
                    {
                        var factoryPart = Find(catalog, factoryRecipe[category]);
                        PartSemantics.ApplyMassDelta(result.spec, factoryPart, -Find(catalog, factoryRecipe[category]).massKg);
                    }
                    continue;
                }
                if (part.category != category || !catalog.Fits(result.spec, part))
                { result.errors.Add("Incompatible " + category); continue; }
                if (PartSemantics.EngineStrengthRated(category))
                {
                    // Earlier captured blocks were unrated; zero retains that contract.
                    if (part.rpmLimit > 0 || PartSemantics.EngineInternal(category)) result.safeRpm = Math.Min(result.safeRpm, part.rpmLimit);
                    if (part.torqueLimitNm > 0 || PartSemantics.EngineInternal(category)) result.safeTorqueNm = Math.Min(result.safeTorqueNm, part.torqueLimitNm);
                }
                PartSemantics.Apply(part, result.spec, tune, factoryRecipe.ContainsKey(category) ? Find(catalog, factoryRecipe[category]) : null);
                PartTuning.Project(part, tune, specs[model]);
                switch (category)
                {
                    case PartCategory.HeadIntake: tune.partPower *= part.power; tune.engineLevel = part.stage; break;
                    case PartCategory.Tires: tune.partGrip = part.grip; tune.tireLevel = part.stage; break;
                    case PartCategory.Body: tune.partMass = part.massScale; tune.weightLevel = part.stage; break;
                    case PartCategory.Gearbox: tune.partShift = part.shiftScale; tune.partGearCapacity = part.capacityScale; tune.gearboxLevel = part.stage; tune.transmissionUpgrade = part.transmission; result.spec.transmission.manualDownshiftInterlock = part.manualDownshiftInterlock; result.spec.transmission.manualLayout = part.manualLayout; result.spec.transmission.sequentialShiftScale = part.sequentialShiftScale; break;
                    case PartCategory.Clutch: tune.partCapacity *= part.capacityScale; break;
                    case PartCategory.Induction: tune.inductionUpgrade = part.induction; break;
                    case PartCategory.Exhaust: tune.exhaustLevel = part.stage; break;
                    case PartCategory.Controls: tune.controlLevel = part.stage; break;
                    case PartCategory.RevLimiter:
                        tune.partLimiterAdjustable = PartTuning.Allows(part, PartTuningOptions.Limiter);
                        if (result.spec.engineBehavior.limiter == LimiterStyle.None) result.spec.engineBehavior.limiter = LimiterStyle.Hard;
                        break;
                    case PartCategory.TwoStep: tune.partTwoStepAdjustable = PartTuning.Allows(part, PartTuningOptions.TwoStep); result.spec.engineBehavior.twoStep = true; break;
                    case PartCategory.ShiftLight: tune.partShiftLight = true; tune.partShiftLightAdjustable = PartTuning.Allows(part, PartTuningOptions.ShiftLight); break;
                    case PartCategory.Flywheel: tune.flywheelMass = part.massScale; break;
                    case PartCategory.Wheels: tune.wheelMass = part.massScale; break;
                    case PartCategory.Brakes: result.spec.brakeDecelerationMps2 = (float)(result.spec.BrakeDeceleration * part.capacityScale); break;
                }
            }

            if (result.spec.engineBehavior.fueling == FuelSystem.Carburetor)
            { result.spec.engineBehavior.cut = result.spec.engineBehavior.launchCut = CutMethod.Ignition; tune.limiterCut = tune.twoStepCut = CutMethod.Ignition; }
            else
            {
                if (CombustionHardware.Diesel(result.spec))
                { result.spec.engineBehavior.cut = result.spec.engineBehavior.launchCut = CutMethod.Fuel; result.spec.engineBehavior.antiLag = result.spec.engineBehavior.launchPreload = false; tune.limiterCut = tune.twoStepCut = CutMethod.Fuel; }
                string controllerId; var controller = installed.TryGetValue(PartCategory.Controls, out controllerId) ? Find(catalog, controllerId) : null;
                if (controller != null && !FuelingHardware.ControllerSupports(controller, result.spec))
                    result.errors.Add("EFI requires injection-capable Controls compatible with this engine's cylinder count: install the fixed EFI controller or programmable engine controller.");
            }
            result.spec.valvetrain.camTimingDegrees = result.spec.valvetrain.timingAdjustable ? (float)DragMath.Clamp(tune.camTimingDegrees, -6, 6) : 0;
            tune.camTimingDegrees = result.spec.valvetrain.camTimingDegrees;
            ValidateCombination(result, installed, catalog);
            tune.ClampTo(result.spec);
            // Preserve authored factory calibration; fitted boost headroom is independent of old engine ranks.
            if (result.safeRpm == float.MaxValue) result.safeRpm = result.spec.redlineRpm * 1.08f;
            if (result.safeTorqueNm == float.MaxValue) result.safeTorqueNm = 0;
            result.spec.engineBehavior.safeRpmMultiplier = result.safeRpm / result.spec.redlineRpm;
            tune.shiftRpm = Math.Min(tune.shiftRpm, result.safeRpm - 100);
            if (tune.revLimitRpm > result.safeRpm) tune.revLimitRpm = result.safeRpm;
            result.spec.fittedParts = installed.Values.Select(id => Find(catalog, id)).Where(part => part != null).Select(part => part.Copy()).ToArray();
            result.spec.hasInstalledParts = true;
            AwdHardware.Enforce(result);
            return result;
        }

        static TransmissionType Transmission(VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            string id; var part = installed.TryGetValue(PartCategory.Gearbox, out id) ? Find(catalog, id) : null;
            return part == null || part.transmission == TransmissionUpgrade.Factory ? spec.transmission.type : (TransmissionType)((int)part.transmission - 1);
        }

        static FuelSystem FuelingType(VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            string id; var part = installed.TryGetValue(PartCategory.FuelSystem, out id) ? Find(catalog, id) : null;
            return part != null && part.fuelDelivery != FuelDeliveryHardware.Factory ? FuelSystem.Injection : spec.fuelSystemConverted ? spec.originalFueling : spec.engineBehavior.fueling;
        }

        static bool Optional(PartCategory category, VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            var type = Transmission(spec, installed, catalog);
            return ServiceHardware.Optional(category) || category == PartCategory.RevLimiter || category == PartCategory.ShiftLight || category == PartCategory.TwoStep || category == PartCategory.Interior || category == PartCategory.Accessories || category == PartCategory.Exhaust || category == PartCategory.Induction || category == PartCategory.Cooling ||
                category == PartCategory.Controls && FuelingType(spec, installed, catalog) == FuelSystem.Carburetor || category == PartCategory.Clutch && (type == TransmissionType.Automatic || type == TransmissionType.CVT);
        }

        static string RemovalEffect(PartCategory category, VehicleSpec spec, IDictionary<PartCategory, string> installed, MechanicalCatalog catalog)
        {
            switch (category)
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
                case PartCategory.Controls: return FuelingType(spec, installed, catalog) == FuelSystem.Carburetor ? "Optional controller removed: mechanical carb/ignition remain; added limiter/launch functions unavailable." : "Missing EFI controller: install injection-capable Controls; this injected engine cannot run without them.";
                case PartCategory.Clutch: return Optional(category, spec, installed, catalog) ? "Separate friction clutch omitted: installed converter/CVT coupling remains." : "Missing clutch: no power transfer through this manual/DCT gearbox.";
                case PartCategory.Body: return "Missing structural chassis/body: build is incomplete; weight reduction does not remove the chassis.";
                default: return "Missing " + category + ": " + (MechanicalCatalog.RoadOnly(category) ? "its drive/braking path is unavailable; engine may still run." : "build is incomplete.");
            }
        }

        static void ValidateCombination(BuildResolution build, IDictionary<PartCategory, string> map, MechanicalCatalog catalog)
        {
            var spec = build.spec;
            PartDefinition Part(PartCategory category) => map.TryGetValue(category, out var id) ? Find(catalog, id) : null;
            var cam = Part(PartCategory.Camshaft); var followers = Part(PartCategory.Followers); var spring = Part(PartCategory.ValveSprings);
            if (cam != null && followers != null && !string.IsNullOrEmpty(cam.followerInterface) && cam.followerInterface != followers.followerInterface)
                build.errors.Add("Camshaft requires " + cam.followerInterface + " followers; fitted " + followers.followerInterface + " cannot mate with it.");
            if (cam != null && spring != null && cam.camLiftMm > spring.springMaxLiftMm)
                build.warnings.Add("Cam lift exceeds spring travel: coil binding can damage cam/followers. Fit springs rated for at least " + cam.camLiftMm.ToString("F1") + " mm lift.");
            if (spec.valvetrain.profileDemandRpm > spec.valvetrain.springSafeRpm)
                build.warnings.Add("Cam RPM demand exceeds spring control rating: valve float reduces power and can damage valves. Use matched springs or shift earlier.");
            spec.combustion.valveClearanceMm = spec.combustion.referenceValveClearanceMm + (spec.combustion.valveReliefMm - 2.3f) * .6f - (spec.valvetrain.liftMm - 8) * .35f - Math.Abs(spec.valvetrain.camTimingDegrees) * .07f;
            if (spec.combustion.valveClearanceMm < 1.2)
                build.warnings.Add("Low estimated piston-to-valve clearance: " + spec.combustion.valveClearanceMm.ToString("F2") + " mm. Use a lower-lift cam, deeper piston reliefs or less timing offset; interference contact can stop the engine.");
            if (!CombustionHardware.Diesel(spec) && spec.combustion.compressionRatio > spec.combustion.referenceCompression + .65)
                build.warnings.Add("Compression exceeds the factory fuel margin: knock is possible under load/boost. Use dished pistons, lower boost or suitable higher-octane fuel.");
        }
    }
}
#endif
