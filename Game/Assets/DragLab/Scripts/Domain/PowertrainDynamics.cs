using System;

namespace DragLab.Domain
{
    public enum EquipmentSetting { Factory, Off, On }
    public enum LimiterStyle { Factory, Hard, Soft, None }
    public enum CutMethod { Factory, Fuel, Ignition }
    public enum FuelSystem { Injection, Carburetor, Diesel }
    public enum TransmissionType { Manual, DCT, Automatic, CVT }
    public enum ManualGearboxLayout { HPattern, Sequential }
    public enum TransmissionUpgrade { Factory, Manual, DCT, Automatic, CVT }

    [Serializable]
    public sealed class EngineBehaviorSpec
    {
        public int version;
        public FuelSystem fueling;
        public LimiterStyle limiter = LimiterStyle.Hard;
        public CutMethod cut = CutMethod.Fuel, launchCut = CutMethod.Ignition;
        public bool twoStep, launchPreload, antiLag;
        public LimiterStyle launchLimiter = LimiterStyle.Hard;
        public float launchDropRpm = 350, softBandRpm = 350;
        public float launchCutFrequencyHz = 36;
        public float launchRetard = .6f, antiLagStrength = .55f;
        public float antiLagMaxSeconds = 2, exhaustCoolSeconds = 12, antiLagWearPerSecond = .002f;
        public float safeRpmMultiplier = 1.08f, overrevFailureSeconds = 1.6f;
        public float camOverlap = .05f, idleControl = .95f, mixtureVariation = .04f;
        public float acceleratorPump = .95f, hesitationSeconds = .22f;
        public float warmupSeconds = 50, coldRoughness = .35f, mechanicalClatter = .05f;
        public EngineBehaviorSpec Copy() { return (EngineBehaviorSpec)MemberwiseClone(); }
        public string Validate()
        {
            if (!Enum.IsDefined(typeof(FuelSystem), fueling) || !Enum.IsDefined(typeof(LimiterStyle), limiter) || limiter == LimiterStyle.Factory ||
                !Enum.IsDefined(typeof(LimiterStyle), launchLimiter) || launchLimiter == LimiterStyle.Factory || launchLimiter == LimiterStyle.None ||
                !Enum.IsDefined(typeof(CutMethod), cut) || cut == CutMethod.Factory || !Enum.IsDefined(typeof(CutMethod), launchCut) || launchCut == CutMethod.Factory) return "Invalid engine control hardware.";
            if (!Range(launchDropRpm, 50, 1200) || !Range(softBandRpm, 50, 1200) || !Range(launchRetard, 0, 1) || !Range(antiLagStrength, 0, 1)) return "Invalid launch/limiter controls.";
            if (launchCutFrequencyHz != 0 && !Range(launchCutFrequencyHz, 8, 80)) return "Invalid launch ignition-cut cadence (0 = 36 Hz).";
            if (!Range(antiLagMaxSeconds, .1, 8) || !Range(exhaustCoolSeconds, 1, 120) || !Range(antiLagWearPerSecond, 0, .1) || !Range(safeRpmMultiplier, 1.01, 1.4) || !Range(overrevFailureSeconds, .1, 10)) return "Invalid engine protection.";
            if (!Range(camOverlap, 0, 1) || !Range(idleControl, 0, 1) || !Range(mixtureVariation, 0, 1) || !Range(acceleratorPump, 0, 1) || !Range(coldRoughness, 0, 1) || !Range(mechanicalClatter, 0, 1)) return "Invalid fueling/cam settings.";
            if (!Range(hesitationSeconds, .02, 2) || !Range(warmupSeconds, 1, 300)) return "Invalid fueling response.";
            return null;
        }
        internal static bool Range(float x, double lo, double hi) { return !float.IsNaN(x) && !float.IsInfinity(x) && x >= lo && x <= hi; }
    }

    [Serializable]
    public sealed class TransmissionSpec
    {
        public int version;
        public TransmissionType type;
        public bool manualDownshiftInterlock;
        public ManualGearboxLayout manualLayout;
        // Mechanical selector speed, independent of the player's shifting assist.
        public float sequentialShiftScale = .45f;
        public double SequentialShiftScale { get { return sequentialShiftScale == 0 ? .45 : sequentialShiftScale; } }
        public bool ProtectsDownshift { get { return type != TransmissionType.Manual || manualDownshiftInterlock; } }
        public float dctShiftSeconds = .085f, dctTorqueRetention = .65f;
        public float automaticShiftSeconds = .22f, automaticTorqueRetention = .35f;
        public float converterStallRpm = 2500, converterMultiplication = 1.9f, converterLockSpeedMps = 16;
        public float cvtMinRatio = .65f, cvtMaxRatio = 3.8f, cvtRatePerSecond = 3, cvtTargetRpm = 5500, cvtTorqueCapacityNm = 350;
        public TransmissionSpec Copy() { return (TransmissionSpec)MemberwiseClone(); }
        public string Validate()
        {
            if (!Enum.IsDefined(typeof(TransmissionType), type)) return "Invalid transmission hardware.";
            if (!Enum.IsDefined(typeof(ManualGearboxLayout), manualLayout) || sequentialShiftScale != 0 && !EngineBehaviorSpec.Range(sequentialShiftScale, .1, .95)) return "Invalid manual selector hardware.";
            if (!EngineBehaviorSpec.Range(dctShiftSeconds, .02, .4) || !EngineBehaviorSpec.Range(automaticShiftSeconds, .05, 1) || !EngineBehaviorSpec.Range(dctTorqueRetention, 0, .95) || !EngineBehaviorSpec.Range(automaticTorqueRetention, 0, .95)) return "Invalid transmission handover.";
            if (!EngineBehaviorSpec.Range(converterStallRpm, 1000, 7000) || !EngineBehaviorSpec.Range(converterMultiplication, 1, 3) || !EngineBehaviorSpec.Range(converterLockSpeedMps, 2, 50)) return "Invalid torque converter.";
            if (!EngineBehaviorSpec.Range(cvtMinRatio, .3, 2) || !EngineBehaviorSpec.Range(cvtMaxRatio, cvtMinRatio + .2, 6) || !EngineBehaviorSpec.Range(cvtRatePerSecond, .1, 10) || !EngineBehaviorSpec.Range(cvtTargetRpm, 1500, 12000) || !EngineBehaviorSpec.Range(cvtTorqueCapacityNm, 40, 2000)) return "Invalid CVT.";
            return null;
        }
    }

    [Serializable]
    public sealed class WheelSpec
    {
        public float assemblyMassKg = 18, inertiaFactor = .65f;
        public string Validate() { return EngineBehaviorSpec.Range(assemblyMassKg, 5, 100) && EngineBehaviorSpec.Range(inertiaFactor, .3, 1) ? null : "Invalid wheel assembly mass/inertia."; }
    }

    public sealed class WheelProfile
    {
        public readonly double radius, assemblyMass, totalInertia, gripScale, tireMassKg, referenceTireMassKg, rimMassKg;
        public double effectiveMass, vehicleMass;
        public WheelProfile(VehicleSpec spec, Tune tune)
        {
            var data = spec.wheels ?? new WheelSpec();
            radius = spec.wheelRadiusM * tune.WheelDiameter;
            double size=Math.Pow(tune.WheelDiameter,3)*tune.WheelWidth;
            referenceTireMassKg=Math.Min(6,data.assemblyMassKg*.6);
            double rimReference=data.assemblyMassKg-referenceTireMassKg;
            tireMassKg=MassDynamics.Installed(spec,Mechanics.PartCategory.Tires)?referenceTireMassKg*size:0;
            rimMassKg=MassDynamics.Installed(spec,Mechanics.PartCategory.Wheels)?rimReference*size*tune.WheelMass:0;
            assemblyMass=tireMassKg+rimMassKg;
            double rimFactor=DragMath.Clamp((data.assemblyMassKg*data.inertiaFactor-referenceTireMassKg*.9)/rimReference,.1,1);
            totalInertia=4*(tireMassKg*.9+rimMassKg*rimFactor)*radius*radius;
            vehicleMass = MassDynamics.Resolve(spec, tune, this, spec.fuel.capacityLitres).TranslatingKg;
            effectiveMass = vehicleMass + totalInertia / (radius * radius);
            // Width is a modest diminishing-return grip adjustment, independent of compound upgrades.
            gripScale = DragMath.Clamp(1 + .18 * Math.Log(tune.WheelWidth), .93, 1.09);
        }
    }

    public sealed class EngineBehaviorProfile
    {
        public readonly EngineBehaviorSpec data;
        public readonly LimiterStyle limiter, launchLimiter;
        public readonly CutMethod cut, launchCut;
        public readonly bool twoStep, preload, antiLag;
        public readonly bool turboEquipped;
        public readonly double limitRpm, launchRpm;
        public EngineBehaviorProfile(VehicleSpec spec, Tune tune)
        {
            data = (spec.engineBehavior ?? new EngineBehaviorSpec()).Copy();
            turboEquipped = new InductionProfile(spec, tune).turboCount > 0;
            bool upgraded = tune.controlLevel > 0;
            bool adjustableLimiter = tune.partsResolved ? tune.partLimiterAdjustable : upgraded;
            bool adjustableTwoStep = tune.partsResolved ? tune.partTwoStepAdjustable : upgraded;
            twoStep = Resolve(tune.twoStep, data.twoStep, adjustableTwoStep);
            preload = Resolve(tune.launchPreload, data.launchPreload, upgraded);
            antiLag = data.fueling!=FuelSystem.Diesel && Resolve(tune.antiLag, data.antiLag, upgraded);
            limiter = adjustableLimiter && tune.limiter != LimiterStyle.Factory ? tune.limiter : data.limiter;
            cut = adjustableLimiter && tune.limiterCut != CutMethod.Factory ? tune.limiterCut : data.cut;
            launchLimiter = adjustableTwoStep && tune.twoStepStyle != LimiterStyle.Factory ? tune.twoStepStyle : data.launchLimiter;
            launchCut = adjustableTwoStep && tune.twoStepCut != CutMethod.Factory ? tune.twoStepCut : data.launchCut;
            // Modeled carburetors cannot meter a high-frequency injector fuel cut.
            if (data.fueling == FuelSystem.Carburetor) cut = launchCut = CutMethod.Ignition;
            else if(data.fueling==FuelSystem.Diesel){cut=launchCut=CutMethod.Fuel;preload=false;}
            else { if (cut == CutMethod.Factory) cut = CutMethod.Fuel; if (launchCut == CutMethod.Factory) launchCut = CutMethod.Fuel; }
            launchRpm = tune.partsResolved && !adjustableTwoStep ? Math.Min(3500, spec.redlineRpm - 200) : tune.launchRpm;
            limitRpm = adjustableLimiter && tune.revLimitRpm > 0 ? tune.revLimitRpm : spec.redlineRpm;
        }
        static bool Resolve(EquipmentSetting option, bool factory, bool upgraded) { return option == EquipmentSetting.Off ? false : option == EquipmentSetting.On && upgraded ? true : factory; }
    }

    public static class EngineBehaviorDynamics
    {
        public static void BeginTick(VehicleSpec spec, Tune tune, EngineBehaviorProfile profile, CarState s, double gas, bool staging, double dt)
        {
            var d = profile.data; s.engineTime += dt;
            s.engineTemperature = Math.Min(1, s.engineTemperature + dt / d.warmupSeconds);
            double cold = 1 - s.engineTemperature;
            double idle = DragMath.Clamp(1 - (s.rpm - spec.idleRpm) / 1800, 0, 1);
            double carb = d.fueling == FuelSystem.Carburetor ? 1 : .15;
            s.idleRoughness = DragMath.Clamp(d.camOverlap * (1 - d.idleControl * .7) + d.mixtureVariation * carb * (1 - d.idleControl) * .12 + cold * d.coldRoughness * carb, 0, 1) * idle;
            s.idleRoughness=DragMath.Clamp(s.idleRoughness+(1-s.cylinderCombustionFactor)*.8*idle,0,1);
            double opening = Math.Max(0, gas - s.previousGas);
            if (opening > .15)
            {
                s.intakeGulp = Math.Max(s.intakeGulp, opening * carb);
                s.fuelHesitation = Math.Max(s.fuelHesitation, opening * (1 - d.acceleratorPump) * carb * (1 + cold));
                if (d.fueling == FuelSystem.Carburetor && d.mixtureVariation > .1) Pop(s, opening * d.mixtureVariation * .4);
            }
            if (s.previousGas - gas > .25 && s.rpm > spec.idleRpm + 900 && d.fueling == FuelSystem.Carburetor) Pop(s, d.mixtureVariation * .8 + d.camOverlap * .3);
            s.previousGas = gas;
            s.fuelHesitation *= Math.Exp(-dt / d.hesitationSeconds); s.intakeGulp *= Math.Exp(-dt / .3);
            double roughPulse = Math.Sin(s.engineTime * 17) * .65 + Math.Sin(s.engineTime * 29) * .35;
            if(d.fueling==FuelSystem.Diesel)s.idleRoughness=(1-s.engineTemperature)*(1-s.dieselColdFactor)*.35;
            s.fuelingTorqueFactor = DragMath.Clamp(1 - s.fuelHesitation * .7 - s.idleRoughness * .18 * (1 + roughPulse), .2, 1);
            if(d.fueling==FuelSystem.Diesel)s.fuelingTorqueFactor*=s.dieselColdFactor;
            s.launchControlActive = staging && profile.twoStep && gas > .5 && !s.stalled && !s.engineFailed;
            s.activeLimitRpm = s.launchControlActive ? Math.Min(profile.launchRpm, profile.limitRpm) : profile.limitRpm;
            var style = s.launchControlActive ? profile.launchLimiter : profile.limiter;
            s.activeCutMethod = s.launchControlActive ? profile.launchCut : profile.cut;
            s.combustionFraction = LimitFraction(style, s.rpm, s.activeLimitRpm, s.launchControlActive ? d.launchDropRpm : spec.LimiterDrop, d.softBandRpm, ref s.limiter);
            if (s.limiter && !s.previousLimiter && s.activeCutMethod == CutMethod.Ignition) Pop(s, .35 + gas * .4);
            if (s.launchControlActive && profile.launchLimiter == LimiterStyle.Hard && s.limiter && s.activeCutMethod == CutMethod.Ignition)
            {
                s.twoStepPopClock += dt;
                double interval = 1 / (d.launchCutFrequencyHz > 0 ? d.launchCutFrequencyHz : 36);
                if (s.twoStepPopClock >= interval) { s.twoStepPopClock -= interval; Pop(s, .45 + gas * .2); }
            }
            else s.twoStepPopClock = 0;
            s.previousLimiter = s.limiter;
            bool preload = staging && profile.preload && s.launchControlActive;
            bool rolling = !staging && profile.antiLag && (gas < .15 || s.shiftRemaining > 0) && s.rpm > spec.idleRpm + 1000;
            bool requested = (preload || rolling) && profile.turboEquipped;
            s.antiLagTime = requested ? s.antiLagTime + dt : Math.Max(0, s.antiLagTime - dt * 2);
            s.antiLagActive = requested && s.antiLagTime < d.antiLagMaxSeconds && s.exhaustHeat < .95 && !s.engineFailed && !s.stalled;
            s.exhaustEnergy = s.antiLagActive ? (preload ? d.launchRetard * gas : d.antiLagStrength) : 0;
            if (preload && s.activeCutMethod == CutMethod.Fuel) s.exhaustEnergy *= s.combustionFraction;
            // Exhaust-side combustion preserves compressor energy but does not add crankshaft torque.
            s.exhaustHeat = s.optimalThermals ? 0 : DragMath.Clamp(s.exhaustHeat + (s.antiLagActive ? dt * .22 : -dt / d.exhaustCoolSeconds), 0, 1);
            if (s.antiLagActive)
            {
                s.engineDamage = Math.Min(1, s.engineDamage + dt * d.antiLagWearPerSecond * s.wearRate);
                s.popClock += dt;
                if (s.popClock >= .11) { s.popClock -= .11; Pop(s, .45); }
            }
            s.crankCycles+=s.rpm/120*dt;
            s.combustionFraction *= s.fuelingTorqueFactor * (1 - .35 * s.engineDamage);
            if (s.engineFailed || s.stalled) { s.combustionFraction = s.exhaustEnergy = 0; s.antiLagActive = s.launchControlActive = false; }
        }
        public static double LimitFraction(LimiterStyle style, double rpm, double limit, double drop, double band, ref bool cut)
        {
            if (style == LimiterStyle.None) { cut = false; return 1; }
            if (style == LimiterStyle.Soft) { double fraction = DragMath.Clamp((limit - rpm) / band, 0, 1); cut = fraction < .05; return fraction; }
            if (rpm >= limit) cut = true;
            else if (rpm <= limit - drop) cut = false;
            return cut ? 0 : 1;
        }
        public static void FinishTick(VehicleSpec spec, EngineBehaviorProfile profile, CarState s, bool canStall, double dt)
        {
            if (s.rpm > spec.redlineRpm * profile.data.safeRpmMultiplier)
                s.engineDamage = Math.Min(1, s.engineDamage + dt / profile.data.overrevFailureSeconds * Math.Max(.2, (s.rpm / spec.redlineRpm - 1) * 4) * s.wearRate);
            if (s.engineDamage >= 1)
            {
                s.engineFailed = true; s.engineTorqueNm = s.engineHorsepower = s.transmittedTorqueNm = 0;
                s.feedback = "ENGINE FAILURE / OVERREV OR EXHAUST ABUSE"; s.feedbackRemaining = 2;
            }
            if (canStall && !s.stallPrevention && s.rpm < spec.idleRpm * .45 && s.clutch < .3)
            {
                s.stalled = true; s.engineTorqueNm = s.engineHorsepower = s.transmittedTorqueNm = 0;
                s.feedback = "STALLED / CLUTCH IN + ENTER TO RESTART"; s.feedbackRemaining = 2;
            }
        }
        public static void Pop(CarState s, double strength) { if (strength > .03) { s.exhaustPopCount++; s.exhaustPopStrength = DragMath.Clamp(strength, 0, 1); } }
    }

    public static class TransmissionDynamics
    {
        public static TransmissionSpec Configuration(VehicleSpec spec, Tune tune)
        {
            var data = (spec.transmission ?? new TransmissionSpec()).Copy();
            if (tune.transmissionUpgrade != TransmissionUpgrade.Factory) data.type = (TransmissionType)((int)tune.transmissionUpgrade - 1);
            return data;
        }
        public static string DisplayName(TransmissionSpec data)
        { return data.type == TransmissionType.Manual ? data.manualLayout == ManualGearboxLayout.Sequential ? "Sequential manual" : "H-pattern manual" : data.type.ToString(); }
        public static double Ratio(TransmissionSpec data, VehicleSpec spec, Tune tune, WheelProfile wheels, CarState s, double dt)
        {
            if (data.type != TransmissionType.CVT) return spec.gears[s.gear - 1] * tune.finalDrive;
            double targetRpm = Math.Min(tune.shiftRpm, data.cvtTargetRpm);
            double wheelRpm = s.wheelContactInitialized ? s.drivelineWheelRpm : s.speed / wheels.radius * 30 / Math.PI;
            double target = Math.Abs(wheelRpm) < 1 ? data.cvtMaxRatio : targetRpm / (wheelRpm * tune.finalDrive);
            s.cvtRatio += DragMath.Clamp(DragMath.Clamp(target, data.cvtMinRatio, data.cvtMaxRatio) - s.cvtRatio, -dt * data.cvtRatePerSecond, dt * data.cvtRatePerSecond);
            return s.cvtRatio * tune.finalDrive;
        }
        public static double ShiftSeconds(TransmissionSpec data, VehicleSpec spec, Tune tune)
        {
            double manualSeconds = spec.shiftSeconds * (data.type == TransmissionType.Manual && data.manualLayout == ManualGearboxLayout.Sequential ? data.SequentialShiftScale : 1);
            return (data.type == TransmissionType.DCT ? data.dctShiftSeconds : data.type == TransmissionType.Automatic ? data.automaticShiftSeconds : manualSeconds) * tune.ShiftMultiplier;
        }
        public static double Retention(TransmissionSpec data) { return data.type == TransmissionType.DCT ? data.dctTorqueRetention : data.type == TransmissionType.Automatic ? data.automaticTorqueRetention : 0; }
        public static double Converter(VehicleSpec spec, Tune tune, EngineBehaviorProfile engine, InductionProfile induction, TransmissionSpec data, CarState s, double gas, double wheelRpm, bool shifting, double dt, WheelProfile wheels = null, double ratio = 1, double reflectedLoadInertia = double.NaN)
        {
            double torque = s.engineFailed || s.stalled ? 0 : InductionDynamics.Torque(induction, s, s.rpm, gas) * s.combustionFraction;
            double capacity = data.type == TransmissionType.CVT ? data.cvtTorqueCapacityNm * tune.GearCapacityMultiplier : spec.ClutchCapacity * tune.GearCapacityMultiplier;
            capacity *= s.gearCapacityFactor;
            double stall = Math.Min(data.converterStallRpm, engine.limitRpm - 250);
            // Fluid load rises quadratically with pump speed. Coupling approaches 1 at lock-up speed.
            double coupling = DragMath.Clamp(s.speed / data.converterLockSpeedMps, 0, 1);
            bool wasLocked = s.converterLocked;
            s.converterLocked = gas > .15 && !shifting &&
                (wasLocked ? s.speed >= data.converterLockSpeedMps * .85 : coupling >= 1);
            // Fluid load falls as turbine speed catches the pump. A stall-speed
            // load independent of turbine speed would trap RPM at stall forever
            // once lock-up no longer teleports the crank to road RPM.
            double speedDifference = 1 - wheelRpm * wheelRpm / Math.Max(1, s.rpm * s.rpm);
            double pumpLoad = s.engineFailed || s.stalled ? 0 : DragMath.Clamp(
                InductionDynamics.Torque(induction, s, s.rpm, Math.Max(.1, gas)) * Math.Pow(s.rpm / Math.Max(1000, stall), 2) * speedDifference, -capacity, capacity);
            double inertia = spec.RotationalInertia(tune);
            double freeMultiplier = 0;
            if ((s.openCenterFreeBranch || !double.IsNaN(reflectedLoadInertia)) && !s.converterLocked)
            {
                // A lightly loaded free shaft can catch the pump in one fixed tick.
                // Bound fluid impulse by both inertias so explicit turbine integration
                // does not alternate huge forward/back-drive loads and invent traction.
                double turbineRatio = DragMath.Clamp(wheelRpm / Math.Max(1, s.rpm), 0, 1);
                freeMultiplier = 1 + (data.converterMultiplication - 1) * (1 - turbineRatio) * (1 - coupling);
                if (turbineRatio > .001) freeMultiplier = Math.Min(freeMultiplier, 1 / turbineRatio);
                double reflected = AxleDriveDynamics.FreeReflectedInertia(AxleDriveDynamics.Configuration(spec, tune), s, ratio / tune.finalDrive);
                if (!double.IsNaN(reflectedLoadInertia)) reflected = reflectedLoadInertia;
                double synchronousLoad = ((s.rpm - wheelRpm) / (FlywheelDynamics.RpmPerRadian * dt) + torque / inertia) / (1 / inertia + freeMultiplier / reflected);
                pumpLoad = pumpLoad >= 0 ? Math.Min(pumpLoad, Math.Max(0, synchronousLoad)) : Math.Max(pumpLoad, Math.Min(0, synchronousLoad));
            }
            if (s.converterLocked)
            {
                double reflected = wheels == null ? inertia * 20 : wheels.effectiveMass * wheels.radius * wheels.radius / (ratio * ratio * spec.drivetrainEfficiency);
                if (s.openCenterFreeBranch) reflected = AxleDriveDynamics.FreeReflectedInertia(AxleDriveDynamics.Configuration(spec, tune), s, ratio / tune.finalDrive);
                if (!double.IsNaN(reflectedLoadInertia)) reflected = reflectedLoadInertia;
                double reduced = inertia * reflected / (inertia + reflected);
                pumpLoad = DragMath.Clamp(torque * reflected / (inertia + reflected) + (s.rpm - Math.Max(spec.idleRpm, wheelRpm)) * reduced / (FlywheelDynamics.RpmPerRadian * dt), -capacity, capacity);
            }
            s.rpm = DragMath.Clamp(s.rpm + (torque - pumpLoad - (gas < .1 ? 20 : 0)) / inertia * 30 / Math.PI * dt, s.engineFailed || s.stalled ? 0 : spec.idleRpm, spec.redlineRpm * 1.5);
            s.converterSlipRpm = Math.Max(0, s.rpm - wheelRpm);
            double speedRatio = DragMath.Clamp(wheelRpm / Math.Max(1, s.rpm), 0, 1);
            double multiplication = 1 + (data.converterMultiplication - 1) * (1 - speedRatio) * (1 - coupling);
            if (freeMultiplier > 0) multiplication = freeMultiplier;
            double transmitted = s.drivetrainFailed ? 0 : DragMath.Clamp(pumpLoad * multiplication, -capacity, capacity) * (shifting ? Retention(data) : 1);
            s.converterLossPowerW = Math.Max(0, pumpLoad * s.rpm / FlywheelDynamics.RpmPerRadian - transmitted * wheelRpm / FlywheelDynamics.RpmPerRadian);
            s.clutch = 0; s.clutchSlipRpm = s.converterSlipRpm; s.transmittedTorqueNm = transmitted;
            return transmitted;
        }
    }
}
