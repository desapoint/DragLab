using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    // Additive contracts in 2A. Player-profile migration is deliberately deferred to 2C.
    public static class MechanicsSchema
    {
        public const int Version = 1, TraceVersion = 1;
        public const string Model = "draglab-variety-v19";
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
    public enum PartCategory { Pistons, Rods, Crankshaft, HeadIntake, FuelSystem, Ignition, Cooling, Induction, Exhaust, Clutch, Flywheel, Gearbox, Differential, Wheels, Tires, Brakes, Body, Controls, Driveshaft, FrontAxle, RearAxle, FrontDriveshaft, RearDriveshaft, FrontDifferential, RearDifferential, TransferCase, Interior, Accessories, RevLimiter, ShiftLight, TwoStep, Camshaft, ValveSprings, Valves, Followers, TimingDrive, PistonRings, CylinderBlock, Bearings, ValveSeals, OilPan, OilPump, OilFilter, CoolantPump, CoolingCap, CoolingHoses, OilCooler, SparkPlugs, GlowPlugs }
    public enum PartLocation { Inventory, Vehicle, Assembly }
    public enum RunSegment { Staging, Driving, Shutdown, Neutral, Diagnostic }
    [Serializable]
    public sealed class ComponentCondition
    {
        public float health = 1;
        public bool failed;
        public string failureReason = "";
        public double runningSeconds, distanceM, stressExposure;
        public double temperatureC = 20, slipEnergyJ;
        public double deposits, leakSeverity, adjustmentError, serviceWearRemainder;
        public int serviceCount, reconditionCount;
        public string lastService = "";
        public EngineElement[] elements = new EngineElement[0];
        public int clutchFluidVersion; public double clutchOilLitres, clutchOilCondition=1;
        public TireCondition[] tires = new TireCondition[0];
        public ComponentCondition Copy() { var copy=(ComponentCondition)MemberwiseClone(); if(elements!=null)copy.elements=Array.ConvertAll(elements,e=>e==null?null:e.Copy()); if(tires!=null)copy.tires=Array.ConvertAll(tires,t=>t==null?null:t.Copy());return copy; }
        public string Validate()
        {
            if (!MechanicsSchema.Finite(health) || health < 0 || health > 1) return "Health must be 0-1.";
            if (!MechanicsSchema.Finite(runningSeconds) || !MechanicsSchema.Finite(distanceM) || !MechanicsSchema.Finite(stressExposure) || runningSeconds < 0 || distanceM < 0 || stressExposure < 0) return "Invalid component history.";
            if (failed && string.IsNullOrWhiteSpace(failureReason)) return "Failed component needs a cause.";
            if (!MechanicsSchema.Finite(temperatureC) || temperatureC < -50 || temperatureC > 2000 || !MechanicsSchema.Finite(slipEnergyJ) || slipEnergyJ < 0) return "Invalid component thermal history.";
            if(!MechanicsSchema.Finite(deposits)||deposits<0||deposits>1||!MechanicsSchema.Finite(leakSeverity)||leakSeverity<0||leakSeverity>1||!MechanicsSchema.Finite(adjustmentError)||adjustmentError<0||adjustmentError>1||serviceCount<0||reconditionCount<0||!MechanicsSchema.Finite(serviceWearRemainder)||serviceWearRemainder<0)return "Invalid component service/fault history.";
            if(elements!=null){var ids=new HashSet<string>();if(elements.Length>128)return "Too many engine elements.";foreach(var e in elements)if(e==null||e.Validate()!=null||!ids.Add(e.id))return "Invalid/duplicate engine elements.";}
            if(!MechanicsSchema.Finite(clutchOilLitres)||clutchOilLitres<0||clutchOilLitres>10||(!MechanicsSchema.Finite(clutchOilCondition)||clutchOilCondition<0||clutchOilCondition>1))return "Invalid clutch oil.";
            if(tires!=null&&tires.Length!=0){if(tires.Length!=4)return "Tire pack needs four conditions.";foreach(var t in tires)if(t==null||t.Validate()!=null)return "Invalid tire condition.";}
            return null;
        }
    }
    [Serializable]
    public sealed class PartInstance
    {
        public int version = MechanicsSchema.Version;
        public string instanceId, definitionId, storageId;
        public PartLocation location;
        public string ownerId = "", slot = "";
        public ComponentCondition condition = new ComponentCondition();
        public IEnumerable<string> ItemIds { get {yield return instanceId;foreach(var e in condition.elements??new EngineElement[0])yield return e.id;foreach(var t in condition.tires??new TireCondition[0])if(!string.IsNullOrEmpty(t.id))yield return t.id;} }
        public PartInstance Copy() { var copy = (PartInstance)MemberwiseClone(); copy.condition = condition == null ? null : condition.Copy(); return copy; }
        public string Validate()
        {
            if (version != MechanicsSchema.Version || string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(definitionId)) return "Invalid part identity/version.";
            if (!Enum.IsDefined(typeof(PartLocation), location)) return "Invalid part location.";
            if (location == PartLocation.Inventory && (!string.IsNullOrEmpty(ownerId) || !string.IsNullOrEmpty(slot))) return "Loose part cannot also be installed.";
            if (location != PartLocation.Inventory && (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(slot))) return "Installed part requires owner/slot.";
            return condition == null ? "Missing component condition." : condition.Validate();
        }
    }
    [Serializable]
    public sealed class SlotBinding { public string slot, partInstanceId; }
    [Serializable]
    public sealed class InstalledBuild
    {
        public int version = MechanicsSchema.Version;
        public string instanceId, vehicleDefinitionId;
        public List<SlotBinding> slots = new List<SlotBinding>();
    }
    [Serializable]
    public sealed class FuelSpec
    {
        public int version = MechanicsSchema.Version;
        public float capacityLitres = 50, densityKgPerLitre = .745f, positionFromFrontAxleM = 1.8f, heightM = .4f;
        public float idleLitresPerHour = .8f, bsfcKgPerKwh = .30f; // Zero in older serialized assets uses these authored defaults.
        public double IdleLitresPerHour { get { return idleLitresPerHour > 0 ? idleLitresPerHour : .8; } }
        public double BsfcKgPerKwh { get { return bsfcKgPerKwh > 0 ? bsfcKgPerKwh : .30; } }
        public FuelSpec Copy() { return (FuelSpec)MemberwiseClone(); }
        public string provenance = "Fictional calibration.";
        public string Validate()
        {
            if (version != MechanicsSchema.Version || !MechanicsSchema.Finite(capacityLitres) || capacityLitres <= 0 || !MechanicsSchema.Finite(densityKgPerLitre) || densityKgPerLitre <= 0) return "Invalid tank capacity/density.";
            if (!MechanicsSchema.Finite(idleLitresPerHour) || idleLitresPerHour < 0 || idleLitresPerHour > 30 || !MechanicsSchema.Finite(bsfcKgPerKwh) || bsfcKgPerKwh < 0 || bsfcKgPerKwh > 2) return "Invalid idle/BSFC fuel calibration.";
            if (!MechanicsSchema.Finite(positionFromFrontAxleM) || !MechanicsSchema.Finite(heightM) || heightM < 0) return "Invalid tank position.";
            return null;
        }
    }
    [Serializable]
    public sealed class FuelState
    {
        public double litres, cumulativeUsedLitres, flowLitresPerSecond;
        public FuelState Copy(){return (FuelState)MemberwiseClone();}
        public double MassKg(FuelSpec spec) { return litres * spec.densityKgPerLitre; }
        public string Validate(FuelSpec spec)
        {
            if (spec == null || spec.Validate() != null) return "Invalid fuel specification.";
            if (!MechanicsSchema.Finite(litres) || litres < 0 || litres > spec.capacityLitres || !MechanicsSchema.Finite(cumulativeUsedLitres) || cumulativeUsedLitres < 0 || !MechanicsSchema.Finite(flowLitresPerSecond) || flowLitresPerSecond < 0) return "Invalid fuel quantity/flow.";
            return null;
        }
    }
    [Serializable]
    public sealed class MassBreakdown
    {
        public double chassisKg, installedComponentKg, clutchFluidDeltaKg;
        public double dryInstalledKg, driverKg, payloadKg, fuelKg, frontNormalLoadN, rearNormalLoadN;
        public double engineInertiaKgM2, wheelInertiaKgM2, centerOfGravityFromFrontM, centerOfGravityHeightM, frontWeightFraction;
        public double TranslatingKg { get { return dryInstalledKg + driverKg + payloadKg + fuelKg; } }
        // Rotational inertia is a separate term, never added to gravitational load.
    }
    [Serializable]
    public sealed class RunCheckpoint
    {
        public int version = MechanicsSchema.Version;
        public string runId, ownedVehicleId, model = MechanicsSchema.Model;
        public long revision, tick, thermalUtcTicks;
        public FuelState fuel = new FuelState();
        public bool hasFuel;
        public string closedReason = "";
        public FluidState fluids;
        public List<PartInstance> components = new List<PartInstance>();
        public bool settled;
    }
    [Serializable]
    public sealed class TickInput
    {
        public long tick;
        public DriverInput input;
        public bool neutral;
        public bool staging;
    }
    [Serializable]
    public sealed class RunSnapshot
    {
        public int version = MechanicsSchema.TraceVersion, seed;
        public string model = MechanicsSchema.Model;
        public VehicleSpec vehicle;
        public Tune tune;
        public DriverSettings driving;
        public long startingTick;
        public bool hasStartingDynamics;
        public CarDynamicsCheckpoint startingDynamics;
        public InstalledBuild installed;
        public FuelState startingFuel;
        public FluidState startingFluids;
        public List<PartInstance> components = new List<PartInstance>();
        public List<TickInput> inputs = new List<TickInput>();
    }
}
