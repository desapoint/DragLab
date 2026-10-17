using System;
using System.Collections.Generic;

// No Unity dependencies: shared by races, AI, dyno tools and headless balance tests.
namespace DragLab.Domain
{
    public enum DriveType { FWD, RWD, AWD }
    public enum CarBodyType { Unknown, Hatchback, Coupe, Sedan, Pickup, SUV, Wagon, Van, Roadster }
    public enum ShiftMode { Automatic, Sequential, ManualClutch }
    public enum LaunchMode { Automatic, Assisted, Manual }
    public enum RacePhase { Countdown, Racing, Finished }
    public enum DrivingPreset { Automatic, Sequential, Manual }
    public enum WearMode { None, Partial, Full }
    // Zero preserves realistic consumption when reading older JSON without this field.
    public enum FuelUsageMode { Realistic, Reduced, None }
    public enum SimulationDifficulty { Arcade, Balanced, Simulation, Custom }

    [Serializable]
    public struct TorquePoint
    {
        public float rpm;
        public float torqueNm;
        public TorquePoint(float rpm, float torqueNm) { this.rpm = rpm; this.torqueNm = torqueNm; }
    }

    [Serializable]
    public class VehicleSpec
    {
        public string id = "starter-hatch";
        public string displayName = "Juniper 1.6";
        public string artId = "hatch";
        public string engineFamily, chassisFamily, trim, modelFamily;
        public bool factoryForgedInternals;
        public Mechanics.CombustionSpec combustion=new Mechanics.CombustionSpec();
        public CarBodyType bodyType;
        public string dataProvenance = "Fictional, modeled values; not manufacturer specifications.";
        public DriveType drive = DriveType.FWD;
        public AwdSpec awd = new AwdSpec();
        public bool hasInstalledParts;
        public bool fuelSystemConverted;
        public FuelSystem originalFueling;
        public float partMassDeltaKg, fuelPowerCapacityHp, ignitionPowerCapacityHp;
        public DifferentialSpec frontDifferential = new DifferentialSpec(), rearDifferential = new DifferentialSpec();
        public Mechanics.IgnitionTopology ignitionTopology;
        public int valvesPerCylinder; public bool interferenceEngine=true;
        public Mechanics.FuelSpec fuel = new Mechanics.FuelSpec();
        public Mechanics.FluidSpec fluids = new Mechanics.FluidSpec();
        public Mechanics.ValvetrainSpec valvetrain = new Mechanics.ValvetrainSpec();
        public Mechanics.NitrousCompatibility nitrousCompatibility = new Mechanics.NitrousCompatibility();
        public float partLongitudinalMomentKgM, partVerticalMomentKgM;
        public bool hasStructuralBody = true;
        public float massKg = 1080; // OEM curb mass including full fuel, excluding driver/payload.
        public float wheelRadiusM = .29f;
        public float wheelbaseM = 2.55f;
        public float centerOfGravityM = .49f;
        public float frontWeightFraction = .62f;
        public float dragAreaM2 = .66f;
        public float rollingResistance = .013f;
        public float drivetrainEfficiency = .88f;
        public float tireFriction = 1.05f;
        public float idleRpm = 900;
        public float redlineRpm = 7000;
        public float finalDrive = 4.10f;
        public float shiftSeconds = .32f;
        // Zero uses modeled crank/core inertia; flywheel inertia is calculated separately.
        public float engineInertiaKgM2;
        public float clutchCapacityNm;
        public float brakeDecelerationMps2;
        public float frontBrakeBias = .65f;
        public float limiterDropRpm;
        public EngineSoundSpec sound = new EngineSoundSpec();
        public InductionSpec induction = new InductionSpec();
        public float displacementLitres;
        public float engineBoreMm; // Zero preserves earlier model-specific fitment.
        public EngineBehaviorSpec engineBehavior = new EngineBehaviorSpec();
        public TransmissionSpec transmission = new TransmissionSpec();
        public WheelSpec wheels = new WheelSpec();
        public FlywheelSpec flywheel = new FlywheelSpec();
        public Mechanics.PartDefinition[] fittedParts = new Mechanics.PartDefinition[0];
        public double DisplacementLitres { get { return displacementLitres > 0 ? displacementLitres : (sound ?? new EngineSoundSpec()).displacementLitres; } }
        public double LimiterDrop { get { return limiterDropRpm > 0 ? limiterDropRpm : 150; } }
        public double BrakeDeceleration { get { return brakeDecelerationMps2 > 0 ? brakeDecelerationMps2 : 8; } }
        // Crank/engine inertia plus the separately calculated flywheel.
        public double EngineCoreInertia { get { return engineInertiaKgM2 > 0 ? engineInertiaKgM2 : .12; } }
        public double EngineInertia { get { return EngineCoreInertia + (flywheel ?? new FlywheelSpec()).Inertia; } }
        public double RotationalInertia(Tune tune) { return EngineCoreInertia + FlywheelDynamics.Inertia(this, tune); }
        public double ClutchCapacity
        {
            get
            {
                if (clutchCapacityNm > 0) return clutchCapacityNm;
                double peak = 0; foreach (var point in torque) peak = Math.Max(peak, point.torqueNm);
                return peak * 1.35;
            }
        }
        public float[] gears = { 3.45f, 2.05f, 1.43f, 1.10f, .88f };
        public Mechanics.PartDefinition[] cornerWheelDefinitions=new Mechanics.PartDefinition[0];
        public Mechanics.PartDefinition[] cornerTireDefinitions=new Mechanics.PartDefinition[0];
        public VehicleSpec Copy()
        {
            var copy = (VehicleSpec)MemberwiseClone();
            copy.cornerWheelDefinitions=cornerWheelDefinitions==null?new Mechanics.PartDefinition[0]:Array.ConvertAll(cornerWheelDefinitions,p=>p.Copy());
            copy.cornerTireDefinitions=cornerTireDefinitions==null?new Mechanics.PartDefinition[0]:Array.ConvertAll(cornerTireDefinitions,p=>p.Copy());
            copy.combustion=combustion==null?new Mechanics.CombustionSpec():combustion.Copy();
            copy.fuel = fuel == null ? new Mechanics.FuelSpec() : fuel.Copy();
            copy.fluids=fluids==null?new Mechanics.FluidSpec():fluids.Copy();
            copy.valvetrain=valvetrain==null?new Mechanics.ValvetrainSpec():valvetrain.Copy();
            copy.nitrousCompatibility=new Mechanics.NitrousCompatibility{wetSystemCompatible=nitrousCompatibility==null||nitrousCompatibility.wetSystemCompatible,drySystemRequiresInjection=true};
            copy.gears = gears == null ? null : (float[])gears.Clone(); copy.torque = torque == null ? null : (TorquePoint[])torque.Clone();
            copy.sound = sound == null ? new EngineSoundSpec() : sound.Copy();
            copy.induction = induction == null ? new InductionSpec() : induction.Copy();
            copy.engineBehavior = engineBehavior == null ? new EngineBehaviorSpec() : engineBehavior.Copy();
            copy.transmission = transmission == null ? new TransmissionSpec() : transmission.Copy();
            copy.wheels = wheels == null ? new WheelSpec() : new WheelSpec { assemblyMassKg = wheels.assemblyMassKg, inertiaFactor = wheels.inertiaFactor };
            copy.flywheel = flywheel == null ? new FlywheelSpec() : flywheel.Copy();
            copy.awd = awd == null ? new AwdSpec() : awd.Copy();
            copy.frontDifferential = frontDifferential == null ? new DifferentialSpec() : frontDifferential.Copy();
            copy.rearDifferential = rearDifferential == null ? new DifferentialSpec() : rearDifferential.Copy();
            copy.fittedParts = fittedParts == null ? new Mechanics.PartDefinition[0] : Array.ConvertAll(fittedParts, p => p.Copy());
            return copy;
        }
        public TorquePoint[] torque = {
            new TorquePoint(900, 85), new TorquePoint(2000, 120), new TorquePoint(3500, 145),
            new TorquePoint(5000, 153), new TorquePoint(6000, 141), new TorquePoint(7000, 115)
        };

        public string Validate()
        {
            if (!Finite(engineBoreMm) || engineBoreMm < 0 || engineBoreMm > 160 || engineBoreMm > 0 && engineBoreMm < 40) return "Invalid engine bore (0 = legacy fitment).";
            if(!Enum.IsDefined(typeof(Mechanics.IgnitionTopology),ignitionTopology))return "Invalid ignition architecture.";
            if(valvesPerCylinder!=0&&(valvesPerCylinder<2||valvesPerCylinder>8||valvesPerCylinder%2!=0))return "Invalid valves per cylinder.";
            if(combustion!=null&&combustion.Validate()!=null)return combustion.Validate();
            if (fuel == null || fuel.Validate() != null) return "Invalid fuel specification.";
            if(fluids!=null&&fluids.Validate()!=null)return fluids.Validate();
            if(valvetrain!=null&&valvetrain.Validate()!=null)return valvetrain.Validate();
            if (frontDifferential == null || rearDifferential == null || frontDifferential.Validate() != null || rearDifferential.Validate() != null) return "Invalid axle differential specification.";
            if (awd != null && awd.Validate() != null) return awd.Validate();
            if (engineBehavior != null && engineBehavior.Validate() != null) return engineBehavior.Validate();
            if (transmission != null && transmission.Validate() != null) return transmission.Validate();
            if (wheels != null && wheels.Validate() != null) return wheels.Validate();
            if (flywheel != null && flywheel.Validate() != null) return flywheel.Validate();
            if (sound != null && sound.Validate() != null) return sound.Validate();
            if (induction != null && induction.Validate() != null) return induction.Validate();
            if (!Finite(displacementLitres) || displacementLitres < 0 || displacementLitres > 12) return "Invalid engine displacement (0 = sound-profile fallback).";
            if (string.IsNullOrWhiteSpace(id)) return "Vehicle needs a stable ID.";
            if (!Finite(massKg) || massKg < 400 || massKg > 4000) return "Mass must be 400-4000 kg.";
            if (!Finite(wheelRadiusM) || wheelRadiusM < .15 || wheelRadiusM > .6) return "Invalid wheel radius.";
            if (!Finite(wheelbaseM) || wheelbaseM < 1.5 || wheelbaseM > 5) return "Invalid wheelbase.";
            if (!Finite(centerOfGravityM) || centerOfGravityM < .1 || centerOfGravityM > 1.5) return "Invalid center of gravity.";
            if (!Finite(frontWeightFraction) || frontWeightFraction < .2 || frontWeightFraction > .8) return "Invalid weight distribution.";
            if (!Finite(finalDrive) || finalDrive < 1.5 || finalDrive > 7) return "Invalid final drive.";
            if (!Finite(tireFriction) || tireFriction < .3 || tireFriction > 2.5) return "Invalid tire friction.";
            if (!Finite(drivetrainEfficiency) || drivetrainEfficiency < .5 || drivetrainEfficiency > 1) return "Invalid drivetrain efficiency.";
            if (!Finite(dragAreaM2) || dragAreaM2 < .1 || dragAreaM2 > 2) return "Invalid drag area.";
            if (!Finite(rollingResistance) || rollingResistance < 0 || rollingResistance > .1) return "Invalid rolling resistance.";
            if (!Finite(shiftSeconds) || shiftSeconds < .04 || shiftSeconds > 1) return "Invalid shift duration.";
            if (!Finite(engineInertiaKgM2) || engineInertiaKgM2 < 0 || engineInertiaKgM2 > 2) return "Engine inertia must be 0 (auto) or up to 2 kg m2.";
            if (!Finite(clutchCapacityNm) || clutchCapacityNm < 0 || clutchCapacityNm > 4000) return "Clutch capacity must be 0 (auto) or up to 4000 Nm.";
            if (!Finite(frontBrakeBias) || frontBrakeBias < .1 || frontBrakeBias > .9) return "Invalid front brake bias.";
            if (!Finite(brakeDecelerationMps2) || brakeDecelerationMps2 < 0 || brakeDecelerationMps2 > 20) return "Brake deceleration must be 0 (auto) or up to 20 m/s2.";
            if (!Finite(limiterDropRpm) || limiterDropRpm < 0 || limiterDropRpm > 1000) return "Limiter recovery must be 0 (auto) or up to 1000 RPM.";
            if (!Finite(idleRpm) || !Finite(redlineRpm) || idleRpm < 400 || redlineRpm < idleRpm + 2000 || redlineRpm > 14000) return "Invalid RPM range.";
            if (!Enum.IsDefined(typeof(DriveType), drive)) return "Invalid drivetrain.";
            if (gears == null || gears.Length < 2 || gears.Length > 8) return "Use 2-8 forward gears.";
            for (int i = 0; i < gears.Length; i++)
                if (!Finite(gears[i]) || gears[i] <= .2 || gears[i] > 6 || (i > 0 && gears[i] >= gears[i - 1])) return "Gear ratios must be positive and descending.";
            if (torque == null || torque.Length < 3) return "Provide at least three torque samples.";
            for (int i = 0; i < torque.Length; i++)
                if (!Finite(torque[i].rpm) || !Finite(torque[i].torqueNm) || torque[i].rpm < 0 || torque[i].torqueNm < 0 || torque[i].torqueNm > 3000 || (i > 0 && torque[i].rpm <= torque[i - 1].rpm)) return "Torque samples must be finite, positive and ordered by RPM.";
            if (torque[0].rpm > idleRpm || torque[torque.Length - 1].rpm < redlineRpm) return "Torque curve must cover idle through redline.";
            return null;
        }
        static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    }

    [Serializable]
    public class Tune
    {
        public float camTimingDegrees;
        public float driverMassKg = 75, payloadMassKg, payloadPositionM = 1.5f, payloadHeightM = .45f;
        public float launchRpm = 3200;
        public float finalDrive = 4.1f;
        public float tirePressurePsi = 30;
        public float shiftRpm = 6800;
        public int engineLevel;
        public int tireLevel;
        public int weightLevel;
        public int gearboxLevel;
        public int exhaustLevel;
        public EngineUpgradeInduction inductionUpgrade;
        public BlowOffType blowOffValve;
        public float turboSize, boostSetting, superchargerPulley;
        public int controlLevel;
        public EquipmentSetting twoStep, launchPreload, antiLag;
        public LimiterStyle limiter, twoStepStyle;
        public CutMethod limiterCut, twoStepCut;
        public float revLimitRpm, wheelDiameter, wheelWidth, wheelMass;
        // Zero inherits the original all-wheel setting, preserving old saves and presets.
        public float frontWheelDiameter, rearWheelDiameter, frontWheelWidth, rearWheelWidth, frontTirePressurePsi, rearTirePressurePsi;
        public double AxleDiameter(bool front)=>front?(frontWheelDiameter>0?frontWheelDiameter:WheelDiameter):(rearWheelDiameter>0?rearWheelDiameter:WheelDiameter);
        public double AxleWidth(bool front)=>front?(frontWheelWidth>0?frontWheelWidth:WheelWidth):(rearWheelWidth>0?rearWheelWidth:WheelWidth);
        public double AxlePressure(bool front)=>front?(frontTirePressurePsi>0?frontTirePressurePsi:tirePressurePsi):(rearTirePressurePsi>0?rearTirePressurePsi:tirePressurePsi);
        public void SetWheelDiameter(int axle,float value){if(axle==0){wheelDiameter=value;frontWheelDiameter=rearWheelDiameter=0;}else if(axle==1)frontWheelDiameter=value;else rearWheelDiameter=value;}
        public void SetWheelWidth(int axle,float value){if(axle==0){wheelWidth=value;frontWheelWidth=rearWheelWidth=0;}else if(axle==1)frontWheelWidth=value;else rearWheelWidth=value;}
        public void SetTirePressure(int axle,float value){if(axle==0){tirePressurePsi=value;frontTirePressurePsi=rearTirePressurePsi=0;}else if(axle==1)frontTirePressurePsi=value;else rearTirePressurePsi=value;}
        public bool coldStart;
        public TransmissionUpgrade transmissionUpgrade;
        public float flywheelMass;
        public AwdSpec awd;
        // Unity serializes a null nested object as default field values; presence needs an explicit flag.
        public bool awdConfigured;
        public float frontLeftGrip = 1, frontRightGrip = 1, rearLeftGrip = 1, rearRightGrip = 1;
        // Set only by the installed-parts resolver. Legacy ranks remain a migration/UI projection.
        public bool partsResolved;
        public bool partLimiterAdjustable, partTwoStepAdjustable, partShiftLight, partShiftLightAdjustable;
        public bool partTuningConfigured, partBoostAdjustable, partPulleyAdjustable, partValveAdjustable, partFinalDriveAdjustable;
        public float partTurboSize = 1, partFixedBoost = 1, partBoostMin = .65f, partBoostMax = 1.2f;
        public float partFixedPulley = 1, partPulleyMin = .85f, partPulleyMax = 1.2f;
        public float partFixedFinalDrive, partFinalDriveMin = 2, partFinalDriveMax = 6;
        public BlowOffType partFixedValve;
        public float shiftLightRpm;
        public double ShiftLightRpm(VehicleSpec spec) { return partShiftLightAdjustable && shiftLightRpm > 0 ? shiftLightRpm : spec.redlineRpm - 200; }
        public float partGearCapacity = 1;
        public double GearCapacityMultiplier { get { return partsResolved ? partGearCapacity : CapacityMultiplier; } }
        public float partPower = 1, partGrip = 1, partMass = 1, partShift = 1, partCapacity = 1, partBoost = 1;
        public double CapacityMultiplier { get { return partsResolved ? partCapacity : 1 + gearboxLevel * .15; } }
        public double FlywheelMass { get { return flywheelMass > 0 ? flywheelMass : 1; } }
        public double WheelDiameter { get { return wheelDiameter > 0 ? wheelDiameter : 1; } }
        public double WheelWidth { get { return wheelWidth > 0 ? wheelWidth : 1; } }
        public double WheelMass { get { return wheelMass > 0 ? wheelMass : 1; } }
        public double TurboSize { get { return turboSize > 0 ? turboSize : 1; } }
        public double BoostSetting { get { return boostSetting > 0 ? boostSetting : 1; } }
        public double SuperchargerPulley { get { return superchargerPulley > 0 ? superchargerPulley : 1; } }
        public float PowerMultiplier { get { return partsResolved ? partPower : 1 + engineLevel * .16f; } }
        public float GripMultiplier { get { return partsResolved ? partGrip : 1 + tireLevel * .12f; } }
        public float MassMultiplier { get { return partsResolved ? partMass : 1 - weightLevel * .045f; } }
        public float ShiftMultiplier { get { return partsResolved ? partShift : 1 - gearboxLevel * .18f; } }
        public Tune Copy() { var copy = (Tune)MemberwiseClone(); copy.awd = awd == null ? null : awd.Copy(); return copy; }
        public void ApplyAdjustments(Tune other)
        {
            camTimingDegrees=other.camTimingDegrees;
            driverMassKg = other.driverMassKg; payloadMassKg = other.payloadMassKg; payloadPositionM = other.payloadPositionM; payloadHeightM = other.payloadHeightM;
            awd = other.awd == null ? null : other.awd.Copy();
            awdConfigured = other.awdConfigured;
            frontLeftGrip = other.frontLeftGrip; frontRightGrip = other.frontRightGrip; rearLeftGrip = other.rearLeftGrip; rearRightGrip = other.rearRightGrip;
            launchRpm = other.launchRpm; shiftRpm = other.shiftRpm;
            finalDrive = other.finalDrive; tirePressurePsi = other.tirePressurePsi;
            blowOffValve = other.blowOffValve;
            boostSetting = other.boostSetting; superchargerPulley = other.superchargerPulley;
            twoStep = other.twoStep; launchPreload = other.launchPreload; antiLag = other.antiLag;
            limiter = other.limiter; limiterCut = other.limiterCut; twoStepStyle = other.twoStepStyle; twoStepCut = other.twoStepCut;
            shiftLightRpm = other.shiftLightRpm; revLimitRpm = other.revLimitRpm; coldStart = other.coldStart;
            wheelDiameter = other.wheelDiameter; wheelWidth = other.wheelWidth; turboSize = other.turboSize;
            frontWheelDiameter=other.frontWheelDiameter;rearWheelDiameter=other.rearWheelDiameter;frontWheelWidth=other.frontWheelWidth;rearWheelWidth=other.rearWheelWidth;frontTirePressurePsi=other.frontTirePressurePsi;rearTirePressurePsi=other.rearTirePressurePsi;
            flywheelMass = other.flywheelMass;
        }
        public void ClampTo(VehicleSpec spec)
        {
            camTimingDegrees=spec.valvetrain!=null&&spec.valvetrain.timingAdjustable?(float)DragMath.Clamp(camTimingDegrees,-6,6):0;
            driverMassKg = (float)DragMath.Clamp(driverMassKg, 0, 200); payloadMassKg = (float)DragMath.Clamp(payloadMassKg, 0, 1000); payloadPositionM = (float)DragMath.Clamp(payloadPositionM, -.5, spec.wheelbaseM + .5); payloadHeightM = (float)DragMath.Clamp(payloadHeightM, 0, 2);
            if (float.IsNaN(launchRpm) || float.IsInfinity(launchRpm)) launchRpm = 3200;
            if (float.IsNaN(shiftRpm) || float.IsInfinity(shiftRpm)) shiftRpm = spec.redlineRpm - 200;
            if (float.IsNaN(finalDrive) || float.IsInfinity(finalDrive)) finalDrive = spec.finalDrive;
            if (float.IsNaN(tirePressurePsi) || float.IsInfinity(tirePressurePsi)) tirePressurePsi = 30;
            launchRpm = (float)DragMath.Clamp(launchRpm, spec.idleRpm, spec.redlineRpm - 200);
            shiftRpm = (float)DragMath.Clamp(shiftRpm, spec.idleRpm + 1500, spec.redlineRpm - 100);
            finalDrive = (float)DragMath.Clamp(finalDrive, 2, 6);
            tirePressurePsi = (float)DragMath.Clamp(tirePressurePsi, 18, 42);
            engineLevel = (int)DragMath.Clamp(engineLevel, 0, 3);
            tireLevel = (int)DragMath.Clamp(tireLevel, 0, 3);
            weightLevel = (int)DragMath.Clamp(weightLevel, 0, 3);
            gearboxLevel = (int)DragMath.Clamp(gearboxLevel, 0, 3);
            exhaustLevel = (int)DragMath.Clamp(exhaustLevel, 0, 3);
            controlLevel = (int)DragMath.Clamp(controlLevel, 0, 1);
            if (!Enum.IsDefined(typeof(TransmissionUpgrade), transmissionUpgrade) || !partsResolved && gearboxLevel == 0) transmissionUpgrade = TransmissionUpgrade.Factory;
            if (!Enum.IsDefined(typeof(EquipmentSetting), twoStep)) twoStep = EquipmentSetting.Factory;
            if (!Enum.IsDefined(typeof(EquipmentSetting), launchPreload)) launchPreload = EquipmentSetting.Factory;
            if (!Enum.IsDefined(typeof(EquipmentSetting), antiLag)) antiLag = EquipmentSetting.Factory;
            if (!Enum.IsDefined(typeof(LimiterStyle), limiter)) limiter = LimiterStyle.Factory;
            if (!Enum.IsDefined(typeof(LimiterStyle), twoStepStyle) || twoStepStyle == LimiterStyle.None) twoStepStyle = LimiterStyle.Factory;
            if (!Enum.IsDefined(typeof(CutMethod), limiterCut)) limiterCut = CutMethod.Factory;
            if (!Enum.IsDefined(typeof(CutMethod), twoStepCut)) twoStepCut = CutMethod.Factory;
            if (float.IsNaN(shiftLightRpm) || float.IsInfinity(shiftLightRpm)) shiftLightRpm = 0;
            shiftLightRpm = shiftLightRpm == 0 ? 0 : (float)DragMath.Clamp(shiftLightRpm, spec.idleRpm + 1000, spec.redlineRpm);
            frontLeftGrip = (float)DifferentialDynamics.Grip(frontLeftGrip); frontRightGrip = (float)DifferentialDynamics.Grip(frontRightGrip); rearLeftGrip = (float)DifferentialDynamics.Grip(rearLeftGrip); rearRightGrip = (float)DifferentialDynamics.Grip(rearRightGrip);
            revLimitRpm = revLimitRpm == 0 ? 0 : (float)DragMath.Clamp(revLimitRpm, spec.idleRpm + 1500, spec.redlineRpm * 1.2);
            wheelDiameter = wheelDiameter == 0 ? 0 : (float)DragMath.Clamp(wheelDiameter, .8, 1.6);
            wheelWidth = wheelWidth == 0 ? 0 : (float)DragMath.Clamp(wheelWidth, .45, 1.6);
            frontWheelDiameter=OptionalWheel(frontWheelDiameter,.8,1.6);rearWheelDiameter=OptionalWheel(rearWheelDiameter,.8,1.6);
            frontWheelWidth=OptionalWheel(frontWheelWidth,.45,1.6);rearWheelWidth=OptionalWheel(rearWheelWidth,.45,1.6);
            frontTirePressurePsi=OptionalWheel(frontTirePressurePsi,18,42);rearTirePressurePsi=OptionalWheel(rearTirePressurePsi,18,42);
            wheelMass = wheelMass == 0 ? 0 : (float)DragMath.Clamp(wheelMass, .6, 3);
            flywheelMass = flywheelMass == 0 ? 0 : (float)DragMath.Clamp(flywheelMass, .25, 2.5);
            if (!Enum.IsDefined(typeof(EngineUpgradeInduction), inductionUpgrade) || !partsResolved && engineLevel == 0) inductionUpgrade = EngineUpgradeInduction.Factory;
            if (!Enum.IsDefined(typeof(BlowOffType), blowOffValve)) blowOffValve = BlowOffType.Factory;
            turboSize = turboSize == 0 ? 0 : (float)DragMath.Clamp(turboSize, .7, 1.6);
            boostSetting = boostSetting == 0 ? 0 : (float)DragMath.Clamp(boostSetting, .35, 1.2);
            superchargerPulley = superchargerPulley == 0 ? 0 : (float)DragMath.Clamp(superchargerPulley, .75, 1.3);
            DragLab.Domain.Mechanics.PartTuning.Enforce(this);
        }
        static float OptionalWheel(float value,double min,double max)=>value==0||float.IsNaN(value)||float.IsInfinity(value)?0:(float)DragMath.Clamp(value,min,max);
    }

    public enum RaceTemperatureMode { FullyCooled, Optimal, Live }
    [Serializable]
    public class DriverSettings
    {
        public ShiftMode shifting = ShiftMode.Automatic;
        public LaunchMode launch = LaunchMode.Automatic;
        public bool throttleAssist = true;
        public bool tractionAssist = true;
        public bool revMatchAssist = true;
        public double reactionSeconds = .18;
        public float clutchReleaseSeconds = .18f;
        // Legacy save field; live audio uses the application-wide master setting.
        public float soundVolume = .35f;
        public bool shutdownBrakeAssist = true;
        public WearMode wearMode = WearMode.Full;
        public FuelUsageMode fuelUsage = FuelUsageMode.Realistic;
        public int guaranteedFittingParts = 5;
        public Mechanics.ConsumptionMode fluidDepletion=Mechanics.ConsumptionMode.Realistic,fluidAging=Mechanics.ConsumptionMode.Realistic;
        public Mechanics.ConditionEffectMode conditionEffects=Mechanics.ConditionEffectMode.Full;
        public Mechanics.ServiceAssist serviceAssist=Mechanics.ServiceAssist.Manual;
        public double FluidDepletionRate {get{return fluidDepletion==Mechanics.ConsumptionMode.None?0:fluidDepletion==Mechanics.ConsumptionMode.Reduced?.25:1;}}
        public double FluidAgingRate {get{return fluidAging==Mechanics.ConsumptionMode.None?0:fluidAging==Mechanics.ConsumptionMode.Reduced?.25:1;}}
        public double ConditionSeverity {get{return conditionEffects==Mechanics.ConditionEffectMode.Assisted?0:conditionEffects==Mechanics.ConditionEffectMode.Reduced?.25:1;}}
        public float wearMultiplier = 1;
        public bool optimalThermals, clutchAssist, stallPrevention;
        public RaceTemperatureMode raceTemperatures = RaceTemperatureMode.FullyCooled;
        public bool warmupBeforeRace;
        public SimulationDifficulty simulationDifficulty = SimulationDifficulty.Custom;
        public double WearRate { get { return wearMode == WearMode.None ? 0 : (wearMode == WearMode.Partial ? .25 : 1) * DragMath.Clamp(wearMultiplier, 0, 1000); } }
        public double FuelUsageRate { get { return fuelUsage == FuelUsageMode.None ? 0 : fuelUsage == FuelUsageMode.Reduced ? .25 : 1; } }
        public void ApplySimulationPreset(SimulationDifficulty preset)
        {
            simulationDifficulty = preset;
            if (preset == SimulationDifficulty.Custom) return;
            guaranteedFittingParts=preset==SimulationDifficulty.Arcade?10:preset==SimulationDifficulty.Balanced?5:0;
            wearMode = preset == SimulationDifficulty.Arcade ? WearMode.None : preset == SimulationDifficulty.Balanced ? WearMode.Partial : WearMode.Full;
            fuelUsage = preset == SimulationDifficulty.Arcade ? FuelUsageMode.None : preset == SimulationDifficulty.Balanced ? FuelUsageMode.Reduced : FuelUsageMode.Realistic;
            fluidDepletion=fluidAging=preset==SimulationDifficulty.Arcade?Mechanics.ConsumptionMode.None:preset==SimulationDifficulty.Balanced?Mechanics.ConsumptionMode.Reduced:Mechanics.ConsumptionMode.Realistic;
            conditionEffects=preset==SimulationDifficulty.Arcade?Mechanics.ConditionEffectMode.Assisted:preset==SimulationDifficulty.Balanced?Mechanics.ConditionEffectMode.Reduced:Mechanics.ConditionEffectMode.Full;
            serviceAssist=preset==SimulationDifficulty.Arcade?Mechanics.ServiceAssist.AutoTopUp:Mechanics.ServiceAssist.Manual;
            optimalThermals = preset == SimulationDifficulty.Arcade;
            raceTemperatures = preset == SimulationDifficulty.Arcade ? RaceTemperatureMode.Optimal : preset == SimulationDifficulty.Balanced ? RaceTemperatureMode.FullyCooled : RaceTemperatureMode.Live;
            clutchAssist = stallPrevention = tractionAssist = revMatchAssist = preset != SimulationDifficulty.Simulation;
        }
        public DriverSettings Copy() { return (DriverSettings)MemberwiseClone(); }
        public void ApplyPreset(DrivingPreset preset)
        {
            if (!Enum.IsDefined(typeof(DrivingPreset), preset)) throw new ArgumentOutOfRangeException("preset");
            shifting = preset == DrivingPreset.Automatic ? ShiftMode.Automatic : preset == DrivingPreset.Sequential ? ShiftMode.Sequential : ShiftMode.ManualClutch;
            launch = preset == DrivingPreset.Automatic ? LaunchMode.Automatic : preset == DrivingPreset.Sequential ? LaunchMode.Assisted : LaunchMode.Manual;
            throttleAssist = preset == DrivingPreset.Automatic;
            tractionAssist = revMatchAssist = preset != DrivingPreset.Manual;
            simulationDifficulty = SimulationDifficulty.Custom;
            // Keep sound, brake preference, release rate and AI difficulty independent.
        }
    }

    [Serializable]
    public struct DriverInput
    {
        public double throttle;
        // 0 = clutch engaged; 1 = clutch pedal fully depressed.
        public double clutch;
        public double brake;
        public bool launch;
        public bool shiftUp;
        public bool shiftDown;
    }

    public static class DragMath
    {
        public const double Gravity = 9.80665;
        public const double FixedStep = 1.0 / 120;
        public static double Clamp(double x, double lo, double hi) { return double.IsNaN(x) ? lo : Math.Max(lo, Math.Min(hi, x)); }
        public static double Torque(VehicleSpec spec, Tune tune, double rpm)
        {
            return InductionDynamics.SteadyTorque(spec, tune, rpm);
        }
        public static double AuthoredTorque(VehicleSpec spec, double rpm)
        {
            var points = spec.torque;
            if (rpm <= points[0].rpm) return points[0].torqueNm;
            for (int i = 1; i < points.Length; i++)
                if (rpm <= points[i].rpm)
                {
                    double t = (rpm - points[i - 1].rpm) / (points[i].rpm - points[i - 1].rpm);
                    return points[i - 1].torqueNm + t * (points[i].torqueNm - points[i - 1].torqueNm);
                }
            return points[points.Length - 1].torqueNm;
        }
        public static double Horsepower(VehicleSpec spec, Tune tune, double rpm) { return Torque(spec, tune, rpm) * rpm / 7127.0; }
        public static double PeakHorsepower(VehicleSpec spec, Tune tune)
        {
            double peak = 0;
            for (double rpm = spec.idleRpm; rpm <= spec.redlineRpm; rpm += 25) peak = Math.Max(peak, Horsepower(spec, tune, rpm));
            return peak;
        }
        public static double ShiftPoint(VehicleSpec spec, Tune tune, int gear)
        {
            if (gear < 1 || gear >= spec.gears.Length) return spec.redlineRpm - 100;
            double current = spec.gears[gear - 1], next = spec.gears[gear];
            for (double rpm = Math.Max(spec.idleRpm + 2000, spec.redlineRpm * .65); rpm < spec.redlineRpm - 100; rpm += 25)
                if (Torque(spec, tune, rpm * next / current) * next >= Torque(spec, tune, rpm) * current) return rpm;
            return spec.redlineRpm - 100;
        }
        public static double GearSpeedKmh(VehicleSpec spec, Tune tune, int gear)
        {
            return spec.redlineRpm / 60 * Math.PI * 2 * WheelProfile.DrivenRadius(spec,tune) / (spec.gears[gear - 1] * tune.finalDrive) * 3.6;
        }
    }

    [Serializable]
    public class CarState
    {
        public double distance;
        public double speed;
        public double rpm;
        public double acceleration;
        public double slip;
        public double wheelAngleDegrees;
        public double frontWheelAngleDegrees;
        public double rearWheelAngleDegrees;
        public double shiftRemaining;
        // Gear 0 is neutral; staging holds the car independently of clutch pressure.
        public int gear;
        public bool stagingBrakesHeld = true;
        public string feedback = "STAGED";
        public double feedbackRemaining;
        public bool launched;
        public bool finished;
        public bool disqualified;
        public double reaction = -1;
        public double elapsed = -1;
        public double trapSpeed;
        public double launchTime;
        public double throttle;
        public double clutch;
        public double brake = 1;
        public double finishDistance;
        public double shutdownTime;
        public int earlyShifts;
        public int lateShifts;
        public double clutchSlipRpm;
        public double transmittedTorqueNm;
        public bool bogging;
        public bool limiter;
        public double engineTime, engineTemperature = 1, engineDamage, exhaustHeat, antiLagTime;
        public double brakeFrictionPowerW, clutchFrictionPowerW, converterLossPowerW, frontContactForceN, rearContactForceN, drivelineWheelRpm;
        public bool warmupLineLock;
        public WheelContactState contact = new WheelContactState();
        public bool wheelContactInitialized;
        public bool stalled, engineFailed, launchControlActive, antiLagActive, previousLimiter, converterLocked;
        public double activeLimitRpm, combustionFraction = 1, fuelingTorqueFactor = 1, exhaustEnergy;
        public double idleRoughness, fuelHesitation, intakeGulp, previousGas, popClock, twoStepPopClock;
        public int exhaustPopCount;
        public double translatingMassKg, massCgHeightM, massCgPositionM, fuelLitres, fuelUsedLitres, fuelFlowLitresPerSecond;
        public bool fuelStarved;
        public double exhaustPopStrength, cvtRatio, converterSlipRpm, effectiveRollingMassKg, wheelRadiusM;
        public double frontWheelRadiusM,rearWheelRadiusM;
        public CutMethod activeCutMethod;
        public bool transmissionTorqueCut;
        public double clutchCapacityFactor = 1, mechanicalPowerFactor = 1;
        public bool engineSupplyAvailable = true;
        public bool openCenterFreeBranch;
        public double frontShaftRpm, rearShaftRpm, frontShaftTorqueNm, rearShaftTorqueNm, frontShaftPowerW, rearShaftPowerW;
        public double wearRate = 1;
        public bool optimalThermals, stallPrevention;
        public bool drivetrainFailed;
        public bool frontDriveAvailable = true, rearDriveAvailable = true, brakesFailed;
        public double brakeCapacityFactor = 1, engineCoolingFactor = 1;
        public double gearCapacityFactor = 1;
        public double deliveryPowerCapacityHp;
        public double frontDifferentialHealth = 1, rearDifferentialHealth = 1;
        public double frontLeftSlip, frontRightSlip, rearLeftSlip, rearRightSlip, coolingHeatRejectedW;
        public double frontTorqueShare = .5, rearTorqueShare = .5, awdSecondaryShare, awdCouplingLossPowerW, awdCouplingReferenceTorqueNm;
        public double frontSlip, rearSlip, frontDriveForceN, rearDriveForceN, frontLoadN, rearLoadN;
        public double flywheelInertiaKgM2, engineRotationalInertiaKgM2, flywheelEnergyJ, flywheelAngularMomentum;
        public double turboSpool, turboBoostBar, superchargerBoostBar, superchargerBypass, boostBar;
        public double engineTorqueNm, engineHorsepower, superchargerDriveTorqueNm, chargeTemperatureK, airflowKgS;
        public int boostReleaseCount;
        public double boostReleaseStrength;
        internal double inductionLastThrottle;
        internal bool inductionWasShifting;
        public int roughEngagements;
        public Dictionary<double, double> splits = new Dictionary<double, double>();
        public Mechanics.FluidState fluids = new Mechanics.FluidState();
        public double conditionSeverity=1, conditionCombustionEfficiency=1, fuelMeteringWaste, extraEngineFrictionNm, valveFloat, lubricationFactor=1, coolantCirculation=1;
        public double[] cylinderContribution=new double[0], cylinderFuelDelivery=new double[0];
        public double cylinderCombustionFactor=1, cylinderFuelFactor=1, individualValveFloat, crankCycles;
        public double clutchViscousDragNm, clutchOilFactor=1;
        public double knockSeverity, valveBind, valveContact, dieselColdFactor=1;
        public CarState Copy() { var copy = (CarState)MemberwiseClone(); copy.cylinderContribution=cylinderContribution==null?new double[0]:(double[])cylinderContribution.Clone();copy.cylinderFuelDelivery=cylinderFuelDelivery==null?new double[0]:(double[])cylinderFuelDelivery.Clone();copy.fluids=fluids==null?new Mechanics.FluidState():fluids.Copy(); copy.contact = contact == null ? new WheelContactState() : contact.Copy(); copy.splits = splits == null ? new Dictionary<double, double>() : new Dictionary<double, double>(splits); return copy; }
    }

    [Serializable]
    public sealed class CarDynamicsCheckpoint
    {
        public CarState state;
        public string exactNumbers;
        public List<Mechanics.PartInstance> components;
        public double engagementPenalty, inductionLastThrottle;
        public bool launchClutch, autoLaunchClutch, revMatchPending, engagementPending, stagingPedal, inductionWasShifting;
        public CarDynamicsCheckpoint Copy() { var copy = (CarDynamicsCheckpoint)MemberwiseClone(); copy.state = state.Copy(); copy.components = components == null ? null : components.ConvertAll(p => p.Copy()); return copy; }
    }

    public sealed partial class CarSimulation
    {
        public readonly VehicleSpec spec;
        public readonly Tune tune;
        public readonly DriverSettings driver;
        public CarState state { get; private set; } = new CarState();
        public readonly MechanicalStress mechanics;
        readonly Mechanics.PartDefinition frictionHardware;
        public readonly InductionProfile induction;
        public readonly EngineBehaviorProfile engine;
        public readonly TransmissionSpec transmission;
        public readonly WheelProfile wheels;
        public Mechanics.MassBreakdown mass { get; private set; }
        public void SetMassInputs(double driverKg, double payloadKg, double positionM, double heightM, double fuelLitres)
        {
            if (!Mechanics.MechanicsSchema.Finite(driverKg) || !Mechanics.MechanicsSchema.Finite(payloadKg) || !Mechanics.MechanicsSchema.Finite(positionM) || !Mechanics.MechanicsSchema.Finite(heightM) || !Mechanics.MechanicsSchema.Finite(fuelLitres) || driverKg < 0 || driverKg > 200 || payloadKg < 0 || payloadKg > 1000 || positionM < -.5 || positionM > spec.wheelbaseM + .5 || heightM < 0 || heightM > 2 || fuelLitres < 0 || fuelLitres > spec.fuel.capacityLitres) throw new ArgumentException("Invalid mass inputs.");
            tune.driverMassKg = (float)driverKg; tune.payloadMassKg = (float)payloadKg; tune.payloadPositionM = (float)positionM; tune.payloadHeightM = (float)heightM; state.fuelLitres = fuelLitres; UpdateMass();
        }
        void UpdateMass() { mass = MassDynamics.Resolve(spec, tune, wheels, state.fuelLitres, state.fluids); if(mechanics!=null)MassDynamics.ApplyClutchFluids(mass,spec,mechanics.parts,frictionHardware); wheels.vehicleMass = mass.TranslatingKg; wheels.effectiveMass = mass.TranslatingKg + wheels.equivalentRotatingMass; state.translatingMassKg = mass.TranslatingKg; state.effectiveRollingMassKg = wheels.effectiveMass; state.massCgHeightM = mass.centerOfGravityHeightM; state.massCgPositionM = mass.centerOfGravityFromFrontM; double transfer = mass.TranslatingKg * state.acceleration * mass.centerOfGravityHeightM / spec.wheelbaseM; state.frontLoadN = DragMath.Clamp(mass.frontNormalLoadN - transfer, 0, mass.TranslatingKg * DragMath.Gravity); state.rearLoadN = mass.TranslatingKg * DragMath.Gravity - state.frontLoadN; }
        public readonly AwdSpec awd;
        readonly double[] shiftPoints;
        public double ShiftPoint(int gear) { return shiftPoints[Math.Max(0, Math.Min(shiftPoints.Length - 1, gear - 1))]; }
        double engagementPenalty;
        bool launchClutch;
        bool autoLaunchClutch;
        bool revMatchPending;
        bool engagementPending;
        bool stagingPedal;

        public CarSimulation(VehicleSpec spec, Tune tune, DriverSettings driver, IEnumerable<Mechanics.PartInstance> components = null)
        {
            string invalid = spec.Validate();
            if (invalid != null) throw new ArgumentException(invalid);
            spec=spec.Copy(); this.spec = spec; this.tune = tune.Copy(); this.driver = driver.Copy();
            this.tune.ClampTo(spec);
            induction = new InductionProfile(spec, this.tune);
            engine = new EngineBehaviorProfile(spec, this.tune);
            // A lowered protection limit must also constrain assisted targets;
            // otherwise automatic shifting can remain on the cut in first gear.
            double assistedCeiling = engine.limiter == LimiterStyle.None ? spec.redlineRpm - 100 :
                Math.Min(spec.redlineRpm - 100, engine.limitRpm - (engine.limiter == LimiterStyle.Soft ? engine.data.softBandRpm + 50 : 100));
            assistedCeiling = Math.Max(spec.idleRpm, assistedCeiling);
            this.tune.shiftRpm = (float)Math.Min(this.tune.shiftRpm, assistedCeiling);
            if (engine.limiter != LimiterStyle.None) this.tune.launchRpm = (float)Math.Min(this.tune.launchRpm, assistedCeiling);
            transmission = TransmissionDynamics.Configuration(spec, this.tune);
            wheels = new WheelProfile(spec, this.tune);
            awd = AxleDriveDynamics.Configuration(spec, this.tune);
            if (awd.Validate() != null) throw new ArgumentException(awd.Validate());
            state.fluids=Mechanics.FluidState.Full(spec.fluids??new Mechanics.FluidSpec());
            state.fuelLitres = spec.fuel.capacityLitres; UpdateMass();
            mechanics = new MechanicalStress(spec, this.tune, components);var frictionPart=mechanics.Part(Mechanics.PartCategory.Clutch);frictionHardware=frictionPart==null?null:mechanics.Definition(frictionPart);UpdateMass(); mechanics.Prepare(state); TireDynamics.ResetContactReference(state,mechanics.parts);
            state.frontTorqueShare = spec.drive == DriveType.FWD ? 1 : spec.drive == DriveType.RWD ? 0 : awd.mode == AwdMode.OnDemand ? (awd.primaryFront ? 1 : 0) : awd.frontBias;
            state.rearTorqueShare = 1 - state.frontTorqueShare;
            state.cvtRatio = transmission.cvtMaxRatio; state.effectiveRollingMassKg = wheels.effectiveMass; state.wheelRadiusM = wheels.radius;state.frontWheelRadiusM=wheels.radii[0];state.rearWheelRadiusM=wheels.radii[2];
            state.engineTemperature = this.tune.coldStart ? 0 : 1;
            shiftPoints = new double[spec.gears.Length];
            for (int gear = 1; gear <= shiftPoints.Length; gear++) shiftPoints[gear - 1] = Math.Min(assistedCeiling, DragMath.ShiftPoint(spec, this.tune, gear));
            state.rpm = driver.launch == LaunchMode.Manual || engine.twoStep ? spec.idleRpm : this.tune.launchRpm;
            launchClutch = driver.launch == LaunchMode.Manual && transmission.type == TransmissionType.Manual && !driver.clutchAssist;
            autoLaunchClutch = driver.launch != LaunchMode.Manual || driver.clutchAssist;
            PrepareSimulationRules();
            FlywheelDynamics.Publish(spec, this.tune, state);
        }
        public CarDynamicsCheckpoint CaptureDynamics()
        {
            var captured = new CarDynamicsCheckpoint { state = state.Copy(), components = mechanics.parts.ConvertAll(p => p.Copy()), engagementPenalty = engagementPenalty, launchClutch = launchClutch,
                autoLaunchClutch = autoLaunchClutch, revMatchPending = revMatchPending, engagementPending = engagementPending, stagingPedal = stagingPedal,
                inductionLastThrottle = state.inductionLastThrottle, inductionWasShifting = state.inductionWasShifting };
            captured.exactNumbers = ExactPhysicsNumbers.Encode(captured); return captured;
        }
        public void RestoreDynamics(CarDynamicsCheckpoint checkpoint)
        {
            checkpoint = checkpoint.Copy(); ExactPhysicsNumbers.Decode(checkpoint);
            state = checkpoint.state.Copy(); UpdateMass(); engagementPenalty = checkpoint.engagementPenalty;
            if (checkpoint.components != null) { mechanics.parts.Clear(); mechanics.parts.AddRange(checkpoint.components.ConvertAll(p => p.Copy())); mechanics.RefreshBindings(); }
            launchClutch = checkpoint.launchClutch; autoLaunchClutch = checkpoint.autoLaunchClutch;
            revMatchPending = checkpoint.revMatchPending; engagementPending = checkpoint.engagementPending; stagingPedal = checkpoint.stagingPedal;
            state.inductionLastThrottle = checkpoint.inductionLastThrottle; state.inductionWasShifting = checkpoint.inductionWasShifting;
        }
        public double GearboxInputRpm
        {
            get { if (state.gear == 0) return 0; double ratio = (transmission.type == TransmissionType.CVT ? state.cvtRatio : spec.gears[state.gear - 1]) * tune.finalDrive;
                return DrivenWheelOmega * FlywheelDynamics.RpmPerRadian * ratio; }
        }
        void PrepareSimulationRules()
        {
            UpdateMass(); state.wearRate = driver.WearRate; state.optimalThermals = driver.optimalThermals; state.stallPrevention = driver.stallPrevention;
            if (state.optimalThermals) { state.engineTemperature = 1; state.exhaustHeat = 0; }
            state.conditionSeverity=driver.ConditionSeverity; mechanics.Prepare(state); ServiceDynamics.Prepare(this); state.fuelStarved = state.fuelLitres <= 0; state.engineSupplyAvailable &= !state.fuelStarved; if (state.fuelStarved) state.stalled = true; EnsureWheelContact();
            AxleDriveDynamics.SyncConnected(this);
        }

        public void Stage(double dt, DriverInput input)
        {
            if (state.launched || state.disqualified) return;
            PrepareSimulationRules();
            state.gear = 0; state.stagingBrakesHeld = true; state.brake = 1;
            ClearDisconnectedCoupling();
            state.speed = state.acceleration = state.slip = state.transmittedTorqueNm = state.clutchSlipRpm = state.brakeFrictionPowerW = 0;
            state.frontSlip = state.rearSlip = state.frontDriveForceN = state.rearDriveForceN = 0;
            if (input.throttle > 0) stagingPedal = true;
            bool pedalRevs = driver.launch == LaunchMode.Manual || stagingPedal;
            state.throttle = pedalRevs ? DragMath.Clamp(input.throttle, 0, 1) : 1;
            state.clutch = driver.launch == LaunchMode.Manual ? DragMath.Clamp(input.clutch, 0, 1) : 1;
            EngineBehaviorDynamics.BeginTick(spec, tune, engine, state, state.throttle, true, dt);
            state.transmissionTorqueCut = false;
            InductionDynamics.Step(induction, state, state.throttle, 0, false, state.combustionFraction <= 0, dt);
            if (pedalRevs || engine.twoStep || state.stalled || state.engineFailed)
                state.rpm = ClutchDynamics.FreeRev(spec, tune, state.rpm, state.throttle, dt, ref state.limiter,
                    InductionDynamics.Torque(induction, state, state.rpm, state.throttle) * state.combustionFraction, engine, state);
            else { state.rpm = tune.launchRpm; state.limiter = false; }
            EngineBehaviorDynamics.FinishTick(spec, engine, state, false, dt);
            InductionDynamics.UpdateOutput(induction, state, state.throttle, state.combustionFraction <= 0);
            ConsumeFuel(dt);
            TireDynamics.Tick(this,dt,state.distance);
            mechanics.Tick(this, dt, 0, state.distance); FluidDynamics.Tick(this,dt);
            FlywheelDynamics.Publish(spec, tune, state);
        }

        public void Launch(double timeSinceGreen)
        {
            if (state.launched || state.disqualified) return;
            state.launched = true;
            state.gear = 1; state.stagingBrakesHeld = false; state.brake = 0;
            state.reaction = Math.Max(0, timeSinceGreen);
            state.launchTime = timeSinceGreen;
            state.feedback = "LAUNCH"; state.feedbackRemaining = .8;
        }

        public bool Shift(int direction, DriverInput input)
        {
            if (direction != -1 && direction != 1 || transmission.type == TransmissionType.CVT || state.engineFailed) return false;
            int next = state.gear + direction;
            if (!state.launched || state.finished || state.shiftRemaining > 0 || next < 1 || next > spec.gears.Length) return false;
            if (transmission.type == TransmissionType.Manual && driver.shifting == ShiftMode.ManualClutch && !driver.clutchAssist && input.clutch < .8)
            {
                state.feedback = "HOLD CLUTCH TO SHIFT"; state.feedbackRemaining = 1; return false;
            }
            double target = DrivenWheelOmega * FlywheelDynamics.RpmPerRadian * spec.gears[next - 1] * tune.finalDrive;
            if (direction < 0 && target > engine.limitRpm && transmission.ProtectsDownshift)
            {
                state.feedback = "UNSAFE DOWNSHIFT BLOCKED"; state.feedbackRemaining = 1; return false;
            }
            double ideal = ShiftPoint(state.gear);
            if (direction > 0 && driver.shifting != ShiftMode.Automatic && state.rpm < ideal - 300) state.earlyShifts++;
            if (direction > 0 && driver.shifting != ShiftMode.Automatic && state.rpm > ideal + 300) state.lateShifts++;
            state.feedback = direction < 0 ? (driver.revMatchAssist ? "REV MATCH" : "MATCH REVS WITH THROTTLE") :
                Math.Abs(state.rpm - ideal) < 300 ? "PERFECT SHIFT" : state.rpm < ideal ? "EARLY SHIFT" : "LATE SHIFT";
            state.feedbackRemaining = .9;
            state.gear = next;
            state.shiftRemaining = TransmissionDynamics.ShiftSeconds(transmission, spec, tune);
            revMatchPending = transmission.type == TransmissionType.Manual && driver.shifting == ShiftMode.ManualClutch && driver.revMatchAssist;
            engagementPending = transmission.type == TransmissionType.Manual && driver.shifting == ShiftMode.ManualClutch && !driver.revMatchAssist;
            return true;
        }

        public void Step(double dt, DriverInput input)
        {
            if (double.IsNaN(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException("dt", "Use the 120 Hz fixed step.");
            state.feedbackRemaining = Math.Max(0, state.feedbackRemaining - dt);
            if (!state.launched || state.finished || state.disqualified) return;
            PrepareSimulationRules(); double distanceBefore = state.distance, mismatchBefore = state.rpm - GearboxInputRpm;
            double gas = driver.throttleAssist && !launchClutch ? 1 : DragMath.Clamp(input.throttle, 0, 1);
            bool manualPedal = transmission.type == TransmissionType.Manual && (input.clutch > .01 || driver.shifting == ShiftMode.ManualClutch && !driver.clutchAssist && !autoLaunchClutch || launchClutch);
            if (input.launch && state.stalled && state.engineSupplyAvailable && (transmission.type != TransmissionType.Manual || input.clutch > .8) && !state.engineFailed)
            { state.stalled = false; state.rpm = spec.idleRpm; state.clutch = 1; state.feedback = "RESTARTED"; state.feedbackRemaining = 1; }
            double requestedClutch = manualPedal ? DragMath.Clamp(input.clutch, 0, 1) : 0;
            if (manualPedal)
            {
                double seconds = requestedClutch > state.clutch ? .025 : Math.Max(.008, driver.clutchReleaseSeconds);
                state.clutch += DragMath.Clamp(requestedClutch - state.clutch, -dt / seconds, dt / seconds);
            }
            state.throttle = gas;
            state.brake = DragMath.Clamp(input.brake, 0, 1);
            if (transmission.type != TransmissionType.CVT && driver.shifting == ShiftMode.Automatic && state.speed > 1 && state.clutch < .2 && (state.clutchSlipRpm < 250 || transmission.type == TransmissionType.Automatic) && state.rpm >= Math.Min(tune.shiftRpm, ShiftPoint(state.gear))) Shift(1, input);
            if (input.shiftUp && driver.shifting != ShiftMode.Automatic) Shift(1, input);
            if (input.shiftDown && driver.shifting != ShiftMode.Automatic) Shift(-1, input);
            double ratio = TransmissionDynamics.Ratio(transmission, spec, tune, wheels, state, dt);
            double roadRpm = state.speed / (Math.PI * 2 * wheels.radius) * 60 * ratio;
            double wheelRpm = DrivenWheelOmega * FlywheelDynamics.RpmPerRadian * ratio;
            if (!manualPedal && !autoLaunchClutch && state.speed < 2 && state.rpm < spec.idleRpm * 1.3) autoLaunchClutch = true;
            if (autoLaunchClutch && (roadRpm >= tune.launchRpm && Math.Abs(spec.drive == DriveType.FWD ? state.frontSlip : state.rearSlip) < .08 || state.openCenterFreeBranch && wheelRpm >= tune.launchRpm)) autoLaunchClutch = false;
            bool shifting = state.shiftRemaining > 0;
            // Automatic driving of a manual gearbox includes a lift while its clutch is open.
            // Keeping the pedal floored through an ungoverned V8 handover was making the
            // automatic driver abuse healthy OEM internals. The input adapter stays held;
            // sequential/manual driving still honors the player's pedal and can overrev.
            if (shifting && transmission.type == TransmissionType.Manual && driver.shifting == ShiftMode.Automatic)
                gas = state.throttle = 0;
            if (revMatchPending && state.clutch > .1)
            {
                double matchRpm = Math.Max(spec.idleRpm, wheelRpm);
                matchRpm = Math.Min(matchRpm, engine.limitRpm);
                double matchTorque = FlywheelDynamics.SynchronizingTorque(state.rpm, matchRpm, spec.RotationalInertia(tune), dt, spec.ClutchCapacity);
                state.rpm -= matchTorque / spec.RotationalInertia(tune) * FlywheelDynamics.RpmPerRadian * dt;
            }
            else revMatchPending = false;
            if (engagementPending && state.clutch < .7 && !shifting)
            {
                double mismatch = Math.Abs(state.rpm - Math.Max(spec.idleRpm, wheelRpm));
                if (mismatch > 650)
                {
                    state.roughEngagements++; engagementPenalty = .35;
                    state.feedback = "ROUGH ENGAGEMENT / MATCH REVS";
                }
                else state.feedback = "GOOD REV MATCH";
                state.feedbackRemaining = 1; engagementPending = false;
            }
            bool launchControl = autoLaunchClutch && !manualPedal;
            EngineBehaviorDynamics.BeginTick(spec, tune, engine, state, gas, false, dt);
            bool torqueInterrupt = shifting && transmission.type == TransmissionType.Manual;
            state.transmissionTorqueCut = false;
            InductionDynamics.Step(induction, state, gas, state.drivetrainFailed ? .06 : Math.Max(.06, 1 - state.clutch), false, state.combustionFraction <= 0, dt);
            if (driver.tractionAssist && !shifting && state.clutch < .98) state.combustionFraction *= DragMath.Clamp(1 - Math.Max(0, (spec.drive == DriveType.FWD ? state.frontSlip : spec.drive == DriveType.RWD ? state.rearSlip : Math.Max(state.frontSlip, state.rearSlip)) - .08) * 3, .1, 1);
            double shaftTorque = AdvancePhysicalDrive(dt, gas, manualPedal, launchControl, torqueInterrupt, shifting, ratio);
            EngineBehaviorDynamics.FinishTick(spec, engine, state, manualPedal, dt);
            if (state.stalled || state.engineFailed || state.drivetrainFailed) shaftTorque = state.transmittedTorqueNm = 0;
            InductionDynamics.UpdateOutput(induction, state, gas, false);
            if (engagementPenalty > 0) engagementPenalty = Math.Max(0, engagementPenalty - dt);
            if (shifting) state.shiftRemaining = Math.Max(0, state.shiftRemaining - dt);
            state.bogging = state.rpm < spec.idleRpm * 1.5 && state.speed < 10 && gas > .6 && state.clutch < .5;
            if (launchClutch && state.speed > 2 && state.clutch < .1) launchClutch = false;
            ConsumeFuel(dt);
            TireDynamics.Tick(this,dt,distanceBefore);
            mechanics.Tick(this, dt, mismatchBefore, distanceBefore); FluidDynamics.Tick(this,dt);
            FlywheelDynamics.Publish(spec, tune, state);
        }

        // Race-independent mode uses the same engine, clutch, converter and road model.
        // Neutral disconnects the shaft, retaining flywheel RPM and road momentum.
        public void IndependentStep(double dt, DriverInput input, bool neutral, bool staging)
        {
            if (!Mechanics.MechanicsSchema.Finite(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException("dt");
            if (!neutral)
            {
                if (state.gear == 0)
                {
                    double roadRpm = state.speed / wheels.radius * FlywheelDynamics.RpmPerRadian * spec.gears[0] * tune.finalDrive;
                    if (roadRpm > engine.limitRpm && transmission.ProtectsDownshift || transmission.type == TransmissionType.Manual && driver.shifting == ShiftMode.ManualClutch && !driver.clutchAssist && input.clutch < .8)
                    { state.feedback = "HOLD CLUTCH / FIRST GEAR SPEED LIMIT"; IndependentStep(dt, input, true, false); return; }
                    if (!state.launched) Launch(0);
                    else { state.gear = 1; state.stagingBrakesHeld = false; autoLaunchClutch = state.speed < 2 && driver.launch != LaunchMode.Manual; }
                    input.shiftUp = false; input.launch = false;
                }
                Step(dt, input); return;
            }
            state.gear = 0; state.stagingBrakesHeld = staging; state.shiftRemaining = 0;
            PrepareSimulationRules(); double distanceBefore = state.distance;
            revMatchPending = engagementPending = false;
            state.clutch = 1; state.throttle = DragMath.Clamp(input.throttle, 0, 1); state.brake = staging ? 1 : DragMath.Clamp(input.brake, 0, 1);
            if (input.launch && state.stalled && state.engineSupplyAvailable && !state.engineFailed) { state.stalled = false; state.rpm = spec.idleRpm; }
            EngineBehaviorDynamics.BeginTick(spec, tune, engine, state, state.throttle, staging, dt);
            InductionDynamics.Step(induction, state, state.throttle, .06, false, state.combustionFraction <= 0, dt);
            state.rpm = ClutchDynamics.FreeRev(spec, tune, state.rpm, state.throttle, dt, ref state.limiter,
                InductionDynamics.Torque(induction, state, state.rpm, state.throttle) * state.combustionFraction, engine, state);
            state.clutchSlipRpm = state.transmittedTorqueNm = 0; state.transmissionTorqueCut = false;
            EngineBehaviorDynamics.FinishTick(spec, engine, state, false, dt); InductionDynamics.UpdateOutput(induction, state, state.throttle, false);
            AdvanceUnpoweredRoad(dt, state.brake);
            AxleDriveDynamics.CoastShafts(this, dt);
            ConsumeFuel(dt);
            TireDynamics.Tick(this,dt,distanceBefore);
            mechanics.Tick(this, dt, 0, distanceBefore); FluidDynamics.Tick(this,dt);
            FlywheelDynamics.Publish(spec, tune, state);
        }
        void AdvanceUnpoweredRoad(double dt, double brake)
        {
            ClearDisconnectedCoupling();
            BeginRoadTick(); state.brake = brake; UpdateTorqueSplit(dt, 0); AdvanceContact(dt, dt, 0, 0); FinishRoadTick(dt);
        }
        void ClearDisconnectedCoupling()
        {
            state.clutchFrictionPowerW = state.converterLossPowerW = 0;
            state.converterSlipRpm=0;state.converterLocked=false;
        }
        public double AssistedShutdownBrake(double dt)
        {
            if(!Mechanics.MechanicsSchema.Finite(dt)||dt<=0||dt>.05)throw new ArgumentOutOfRangeException("dt");
            double target=state.brakesFailed?0:Math.Min(RaceSimulation.MaximumShutdownBrake,3.0/spec.BrakeDeceleration);
            if(state.finished&&!state.brakesFailed)
            {
                double remaining=state.finishDistance+RaceSimulation.ShutdownStopMetres-state.distance;
                double capacity=spec.BrakeDeceleration*Math.Max(.01,state.brakeCapacityFactor);
                double passive=Math.Max(0,-state.acceleration-capacity*state.brake);
                double required=state.speed*state.speed/(2*Math.Max(.5,remaining));
                target=Math.Min(RaceSimulation.MaximumShutdownBrake,Math.Max(0,required-passive)/capacity);
                if(remaining<1&&state.speed<.5)target=.15;
            }
            double slip=0;if(state.contact.slip!=null)foreach(double value in state.contact.slip)slip=Math.Max(slip,-value);
            if(state.speed>2&&slip>.08)target*=DragMath.Clamp(1-(slip-.08)*8,0,1);
            double current=DragMath.Clamp(state.brake,0,1),rate=target<current?4:.65;
            return current+DragMath.Clamp(target-current,-rate*dt,rate*dt);
        }
        public void Shutdown(double dt, double brake)
        {
            if (!Mechanics.MechanicsSchema.Finite(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException("dt");
            if (!state.launched) return;
            bool finished = state.finished, throttleAssist = driver.throttleAssist;
            try { state.finished = false; driver.throttleAssist = false; Step(dt, new DriverInput { brake = DragMath.Clamp(brake, 0, 1) }); state.shutdownTime += dt; }
            finally { state.finished = finished; driver.throttleAssist = throttleAssist; }
        }
    }

    public sealed class RaceSimulation
    {
        OpponentWorker opponentWorker;
        readonly Action opponentTick;
        double opponentDt, opponentGreenTime;DriverInput opponentInput;
        public void EnableOpponentWorker() { if (opponentWorker == null) opponentWorker = new OpponentWorker(); }
        public void CloseOpponentWorker() { if (opponentWorker != null) { opponentWorker.Dispose(); opponentWorker = null; } }
        public readonly RivalBehavior opponentBehavior;
        DriverInput RivalInput(double greenTime,double dt)=>opponentBehavior==null?new DriverInput{throttle=1}:opponentBehavior.DriveInput(opponent,greenTime,player.state.distance-opponent.state.distance,dt);
        void AdvanceOpponent() { Advance(opponent, opponentInput, opponentDt, opponentGreenTime); }
        public const double GreenAt = 3;
        public const float ShutdownStopMetres=160;
        public const double MaximumShutdownBrake=.9;
        public readonly CarSimulation player;
        public readonly CarSimulation opponent;
        public readonly double distance;
        public double time;
        public RacePhase phase;
        public bool playerWon;
        public bool timedOut;
        public bool ResultReady => player.state.finished || phase == RacePhase.Finished;
        public bool warmup, warmupNeutral = true;
        public bool WarmupStopped { get { return Math.Abs(player.state.speed)<.1 && Array.TrueForAll(player.state.contact.omega,w=>Math.Abs(w)<.5); } }
        public void BeginWarmup() { if(time!=0||phase!=RacePhase.Countdown)throw new InvalidOperationException("Warm-up must precede countdown.");warmup=true;warmupNeutral=true; }
        public bool BeginCountdown()
        {
            if(!warmup||!WarmupStopped||player.state.engineFailed||player.state.drivetrainFailed)return false;
            var s=player.state;s.warmupLineLock=false;s.launched=s.finished=s.disqualified=false;s.gear=0;s.distance=s.contact.distance=s.elapsed=s.launchTime=0;s.reaction=-1;s.shiftRemaining=0;s.splits.Clear();
            warmup=false;warmupNeutral=true;return true;
        }
        public readonly RaceTelemetry telemetry = new RaceTelemetry();
        static readonly double[] Markers = { 18.288, 201.168, 402.336, 804.672, 1609.344 };

        public RaceSimulation(CarSimulation player, CarSimulation opponent, double distance, RivalBehavior behavior=null)
        {
            if (double.IsNaN(distance) || distance <= 0 || distance > 1609.345) throw new ArgumentOutOfRangeException("distance");
            this.player = player; this.opponent = opponent; this.distance = distance;opponentBehavior=behavior==null?null:UnityEngine.JsonUtility.FromJson<RivalBehavior>(UnityEngine.JsonUtility.ToJson(behavior));
            opponentBehavior?.PlanAgainst(opponent,player);
            opponentTick = AdvanceOpponent;
            phase = RacePhase.Countdown;
        }

        public void RefreshOpponentStrategy(){if(time!=0)throw new InvalidOperationException("Rival strategy is captured before countdown.");opponentBehavior?.PlanAgainst(opponent,player,true);}

        public void Step(DriverInput input, double dt = DragMath.FixedStep)
        {
            if (double.IsNaN(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException("dt");
            if (phase == RacePhase.Finished) return;
            if(warmup) {
                if(input.launch&&BeginCountdown())return;
                if(input.shiftDown&&player.state.gear<=1){warmupNeutral=true;input.shiftDown=false;}
                if(input.shiftUp&&warmupNeutral)warmupNeutral=false;
                bool assist=player.driver.throttleAssist;
                try {player.driver.throttleAssist=false;player.IndependentStep(dt,input,warmupNeutral,false);}
                finally {player.driver.throttleAssist=assist;}
                return;
            }
            double previousTime = time;
            time += dt;
            double greenTime = time - GreenAt;
            player.Stage(dt, input);
            opponent.Stage(dt, new DriverInput());
            if (player.state.stalled && input.shiftUp) { input.launch = true; input.shiftUp = false; }
            // Shift-up selects first (Drive for CVT) from staged neutral. Once driving,
            // that same edge remains an ordinary upshift; pedals are independent.
            bool launchRequested = input.launch || (!player.state.launched && player.state.gear == 0 && input.shiftUp);
            if (launchRequested && previousTime < GreenAt && player.driver.launch != LaunchMode.Automatic)
            {
                player.state.disqualified = true;
                player.state.feedback = "RED LIGHT";
            }
            if (time < GreenAt) return;
            phase = RacePhase.Racing;
            bool playerWasLaunched = player.state.launched, opponentWasLaunched = opponent.state.launched;
            if (player.driver.launch == LaunchMode.Automatic && greenTime >= player.driver.reactionSeconds) player.Launch(greenTime);
            else if (player.driver.launch != LaunchMode.Automatic && launchRequested && previousTime >= GreenAt) player.Launch(greenTime);
            if (greenTime >= opponent.driver.reactionSeconds) opponent.Launch(greenTime);
            // Capture the observable gap/input before either car advances. The worker
            // consumes that same value and never reads concurrently changing player state.
            DriverInput ai = RivalInput(greenTime,dt);
            // Launch is timestamped at this tick's end. Drive begins on the following interval.
            if (opponentWasLaunched && opponentWorker != null) {
                opponentDt = dt; opponentGreenTime = greenTime;opponentInput=ai; opponentWorker.Begin(opponentTick);
                try { if (playerWasLaunched) Advance(player, input, dt, greenTime); }
                finally { opponentWorker.Join(); }
            } else {
                if (playerWasLaunched) Advance(player, input, dt, greenTime);
                if (opponentWasLaunched) Advance(opponent, ai, dt, greenTime);
            }
            telemetry.Record(player, greenTime, dt);
            // A crossed player beam fixes its result; the other lane can keep running.
            if (player.state.finished)
                playerWon = !player.state.disqualified && (!opponent.state.finished ||
                    player.state.reaction + player.state.elapsed < opponent.state.reaction + opponent.state.elapsed);
            if ((player.state.finished || player.state.disqualified || player.state.engineFailed || player.state.drivetrainFailed) && (opponent.state.finished || opponent.state.engineFailed || opponent.state.drivetrainFailed) || greenTime >= 90)
            {
                phase = RacePhase.Finished;
                timedOut = greenTime >= 90 && (!player.state.finished || !opponent.state.finished);
            }
        }

        public void Shutdown(DriverInput input, double dt = DragMath.FixedStep)
        {
            if (phase != RacePhase.Finished) return;
            player.Shutdown(dt, player.driver.shutdownBrakeAssist ? player.AssistedShutdownBrake(dt) : input.brake);
            opponent.Shutdown(dt, opponent.AssistedShutdownBrake(dt));
        }

        void Advance(CarSimulation car, DriverInput input, double dt, double greenTime)
        {
            if (car.state.finished)
            {
                car.Shutdown(dt, car.driver.shutdownBrakeAssist ? car.AssistedShutdownBrake(dt) : input.brake);
                return;
            }
            double before = car.state.distance;
            car.Step(dt, input);
            if (!car.state.launched || car.state.finished || car.state.disqualified) return;
            foreach (double marker in Markers)
                if (marker <= distance + .001 && before < Math.Min(marker, distance) && car.state.distance >= Math.Min(marker, distance) && !car.state.splits.ContainsKey(marker))
                    car.state.splits[marker] = CrossingTime(car, before, Math.Min(marker, distance), dt, greenTime);
            if (car.state.distance >= distance)
            {
                car.state.elapsed = CrossingTime(car, before, distance, dt, greenTime);
                car.state.trapSpeed = car.state.speed * 3.6;
                car.state.finished = true;
                car.state.distance = distance;
                car.state.finishDistance = distance;
                car.state.feedback = "FINISH";
            }
        }
        double CrossingTime(CarSimulation car, double before, double marker, double dt, double greenTime)
        {
            double fraction = DragMath.Clamp((marker - before) / Math.Max(.000001, car.state.distance - before), 0, 1);
            return Math.Max(0, greenTime - dt + fraction * dt - car.state.launchTime);
        }
    }
}
