using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    [Serializable]
    public sealed class DrivingTraceSample
    {
        public long tick;
        public RunSegment segment;
        public double time, rpm, speedKmh, distance, throttle, clutch, brake, engineNm, hp, shaftNm, boost, slipEstimate, flywheelJ, acceleration;
        public double clutchSlipRpm, converterSlipRpm, cvtRatio, turboSpool, engineTemperature, exhaustHeat, engineDamage;
        public double gearboxInputRpm, signedClutchSlipRpm, combustionFraction, activeLimitRpm;
        public bool limiter;
        public double clutchTemperatureC, clutchHealth, gearboxTemperatureC, driveshaftHealth;
        public bool drivetrainFailed;
        public double frontShare, rearShare, frontSlipEstimate, rearSlipEstimate, frontForceN, rearForceN, brakeCapacity;
        public bool frontAvailable, rearAvailable, brakesFailed;
        public double frontShaftRpm, rearShaftRpm, frontShaftNm, rearShaftNm, frontShaftPowerW, rearShaftPowerW;
        public bool openCenterFreeBranch;
        public double translatingMassKg, dryMassKg, driverMassKg, payloadMassKg, fuelMassKg, cgHeightM, cgPositionM, frontLoadN, rearLoadN;
        public double frontLeftSlip, frontRightSlip, rearLeftSlip, rearRightSlip, coolingHeatRejectedW;
        public double fuelLitres, fuelUsedLitres, fuelFlowLps;
        public double oilLitres,coolantLitres,oilTemperatureC,oilPressureBar,coolantPressureBar,oilCondition,oilBurnLps,oilLeakLps,coolantLeakLps,coolantVentLps,valveFloat,combustionEfficiency;
        public double cylinderFactor=1, cylinderFuelFactor=1, individualValveFloat, clutchOilL, clutchOilCondition;
        public double[] cylinderContribution=new double[0];
        public double[] tireHealth=new double[4],tireTemperatureC=new double[4],tireSlipEnergyJ=new double[4];
        public double flWheelRpm, frWheelRpm, rlWheelRpm, rrWheelRpm, brakeWorkJ, contactWorkJ, clutchHeatW;
        public double converterHeatW,engineCoolingFactor,brakeCapacityFactor;
        public double[] partHealth=new double[Enum.GetValues(typeof(PartCategory)).Length],partTemperatureC=new double[Enum.GetValues(typeof(PartCategory)).Length];
        public double bodyEnergyJ,wheelEnergyJ,differentialWorkJ,awdCouplingWorkJ,awdCouplingHeatW,resistanceWorkJ,driveWorkJ,aeroResistanceN,rollingResistanceN,brakeHeatW;
        public double[] wheelDriveNm=new double[4],wheelBrakeNm=new double[4],wheelContactNm=new double[4],wheelForceN=new double[4];
        public int gear;
        public bool shifting, twoStep, stalled, failed;
    }
    public sealed class PhysicalDrivingSession
    {
        public const int MaximumTicks = 120 * 600;
        public readonly CarSimulation car;
        public readonly RunSnapshot snapshot;
        public readonly List<DrivingTraceSample> samples = new List<DrivingTraceSample>();
        long tick;
        public bool Recording { get; private set; }
        public DriverInput LastInput { get; private set; }
        public long Tick { get { return tick; } }
        public long TotalTick { get { return snapshot.startingTick + Tick; } }
        public PhysicalDrivingSession(RunSnapshot start, bool recording = true)
        {
            string error = Validate(start); if (error != null) throw new ArgumentException(error);
            snapshot = Copy(start); snapshot.inputs.Clear(); Recording = recording;
            car = new CarSimulation(snapshot.vehicle.Copy(), snapshot.tune, snapshot.driving, snapshot.components.Count == 0 ? null : snapshot.components);
            if (snapshot.startingFuel != null) car.SetMassInputs(car.tune.driverMassKg, car.tune.payloadMassKg, car.tune.payloadPositionM, car.tune.payloadHeightM, snapshot.startingFuel.litres);
            if (snapshot.startingFuel != null) car.state.fuelUsedLitres = snapshot.startingFuel.cumulativeUsedLitres;
            if(snapshot.startingFluids!=null)car.SetFluids(snapshot.startingFluids);
            else snapshot.startingFluids=car.state.fluids.Copy();
            if (snapshot.hasStartingDynamics) car.RestoreDynamics(snapshot.startingDynamics);
            else { car.state.rpm = car.spec.idleRpm; car.state.gear = 0; car.state.stagingBrakesHeld = true; car.state.brake = 1; }
            FlywheelDynamics.Publish(car.spec, car.tune, car.state); Record(car.state.stagingBrakesHeld ? RunSegment.Staging : car.state.gear == 0 ? RunSegment.Neutral : RunSegment.Driving, 0);
        }
        public PhysicalDrivingSession ContinueRecording(bool recording = true)
        {
            var next = Copy(snapshot); next.tune = car.tune.Copy(); next.inputs.Clear(); next.startingTick = TotalTick; next.hasStartingDynamics = true; next.startingDynamics = car.CaptureDynamics();
            var result = new PhysicalDrivingSession(next, recording); result.LastInput = LastInput; return result;
        }
        public static string Validate(RunSnapshot data)
        {
            if (data != null && data.model != MechanicsSchema.Model) return "Different physics model: record a new scenario for " + MechanicsSchema.Model + ".";
            if (data == null || data.version != MechanicsSchema.TraceVersion || data.model != MechanicsSchema.Model || data.vehicle == null || data.vehicle.Validate() != null || data.tune == null || data.driving == null || data.inputs == null || data.components == null) return "Unsupported/invalid physical snapshot.";
            if (MassDynamics.ValidateInputs(data.vehicle,data.tune) != null) return "Invalid captured mass/placement inputs.";
            if (data.startingFuel != null && data.startingFuel.Validate(data.vehicle.fuel) != null) return "Invalid captured starting fuel.";
            if(data.startingFluids!=null&&data.startingFluids.Validate(data.vehicle.fluids)!=null)return "Invalid captured starting fluids.";
            if(!Enum.IsDefined(typeof(ConsumptionMode),data.driving.fluidDepletion)||!Enum.IsDefined(typeof(ConsumptionMode),data.driving.fluidAging)||!Enum.IsDefined(typeof(ConditionEffectMode),data.driving.conditionEffects)||!Enum.IsDefined(typeof(ServiceAssist),data.driving.serviceAssist))return "Invalid captured service difficulty.";
            if (data.tune.awd != null && data.tune.awd.Validate() != null) return data.tune.awd.Validate();
            if (data.inputs.Count > MaximumTicks) return "Trace exceeds ten-minute recording limit.";
            if (data.startingTick < 0 || data.startingTick > long.MaxValue - MaximumTicks || data.startingTick > 0 && !data.hasStartingDynamics || data.hasStartingDynamics && data.startingDynamics == null) return "Invalid recording continuation.";
            if (data.hasStartingDynamics)
            {
                if (data.startingDynamics.state == null || data.startingDynamics.state.contact == null || data.startingDynamics.state.contact.Validate() != null) return "Invalid checkpoint contact.";
                foreach (var field in typeof(CarState).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) if (field.FieldType == typeof(double) && !MechanicsSchema.Finite((double)field.GetValue(data.startingDynamics.state))) return "Nonfinite starting dynamics.";
                CarDynamicsCheckpoint c; try { c = data.startingDynamics.Copy(); ExactPhysicsNumbers.Decode(c); } catch (Exception) { return "Invalid exact checkpoint."; } var s = c.state;
                if(s.fluids==null||s.fluids.Validate(data.vehicle.fluids)!=null)return "Invalid checkpoint fluids.";
                if (c.components != null) foreach (var part in c.components)
                {
                    string invalid = part == null ? "Missing part." : part.Validate();
                    if (invalid != null) return "Invalid exact component state"
                        + (part == null ? "" : " (" + part.slot + " / " + part.instanceId + ")")
                        + ": " + invalid;
                }
                if (s == null || s.contact == null || s.contact.Validate() != null || !MechanicsSchema.Finite(c.engagementPenalty) || !MechanicsSchema.Finite(c.inductionLastThrottle) || s.gear < 0 || s.gear > data.vehicle.gears.Length || s.speed < 0 || s.speed > 1000 || s.rpm < 0 || s.rpm > data.vehicle.redlineRpm * 1.5 + 1 || s.fuelLitres < 0 || s.fuelLitres > data.vehicle.fuel.capacityLitres || s.fuelUsedLitres < 0 || s.fuelFlowLitresPerSecond < 0 || s.engineDamage < 0 || s.engineDamage > 1 || s.finished || s.disqualified) return "Invalid starting dynamics.";
                foreach (var field in typeof(CarState).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (field.FieldType == typeof(double) && !MechanicsSchema.Finite((double)field.GetValue(s))) return "Nonfinite starting dynamics.";
            }
            if (!Enum.IsDefined(typeof(ShiftMode), data.driving.shifting) || !Enum.IsDefined(typeof(LaunchMode), data.driving.launch)) return "Invalid captured driver mode.";
            if (!Enum.IsDefined(typeof(FuelUsageMode), data.driving.fuelUsage)) return "Invalid captured fuel usage mode.";
            if (!Enum.IsDefined(typeof(WearMode), data.driving.wearMode) || !Enum.IsDefined(typeof(SimulationDifficulty), data.driving.simulationDifficulty) || float.IsNaN(data.driving.wearMultiplier) || float.IsInfinity(data.driving.wearMultiplier) || data.driving.wearMultiplier < 0 || data.driving.wearMultiplier > 1000) return "Invalid captured simulation difficulty or wear multiplier.";
            if (data.tune.partsResolved && new[] { data.tune.partPower, data.tune.partGrip, data.tune.partMass, data.tune.partShift, data.tune.partCapacity, data.tune.partBoost }.Any(v => !MechanicsSchema.Finite(v) || v <= 0 || v > 4)) return "Invalid captured hardware multipliers.";
            for (int i = 0; i < data.inputs.Count; i++)
                if (data.inputs[i] == null || data.inputs[i].tick != i || !ValidPedals(data.inputs[i].input)) return "Replay inputs must be finite, sequential 120 Hz ticks.";
            var identities = new HashSet<string>();
            foreach (var component in data.components) if (component == null || component.Validate() != null || !identities.Add(component.instanceId)) return "Invalid/duplicate captured component condition.";
            return null;
        }
        static bool ValidPedals(DriverInput input) { return MechanicsSchema.Finite(input.throttle) && MechanicsSchema.Finite(input.clutch) && MechanicsSchema.Finite(input.brake) && input.throttle >= 0 && input.throttle <= 1 && input.clutch >= 0 && input.clutch <= 1 && input.brake >= 0 && input.brake <= 1; }
        public void Step(TickInput command)
        {
            if (Recording && Tick >= MaximumTicks || command.tick != Tick || !ValidPedals(command.input)) throw new ArgumentException("Invalid/overflow driving tick.");
            var captured = new TickInput { tick = command.tick, input = command.input, neutral = command.neutral, staging = command.staging };
            car.IndependentStep(DragMath.FixedStep, captured.input, captured.neutral, captured.staging);
            LastInput = captured.input; tick++;
            if (Recording) snapshot.inputs.Add(captured); else samples.Clear();
            Record(captured.staging ? RunSegment.Staging : car.state.gear == 0 ? RunSegment.Neutral : captured.input.throttle > .01 ? RunSegment.Driving : RunSegment.Shutdown, Tick);
        }
        void Record(RunSegment segment, long tick)
        {
            var s = car.state;
            var clutch = car.mechanics.parts.Find(p => p.slot == "Clutch"); var gearbox = car.mechanics.parts.Find(p => p.slot == "Gearbox"); var shaft = car.mechanics.parts.Find(p => p.slot == "Driveshaft");
            samples.Add(new DrivingTraceSample { tick = snapshot.startingTick + tick, segment = segment, time = (snapshot.startingTick + tick) * DragMath.FixedStep, rpm = s.rpm, speedKmh = s.speed * 3.6, distance = s.distance,
                throttle = s.throttle, clutch = s.clutch, brake = s.brake, gear = s.gear, engineNm = s.engineTorqueNm, hp = s.engineHorsepower,
                shaftNm = s.transmittedTorqueNm, boost = s.boostBar, slipEstimate = s.slip, flywheelJ = s.flywheelEnergyJ, acceleration = s.acceleration,
                clutchSlipRpm = s.clutchSlipRpm, converterSlipRpm = s.converterSlipRpm, cvtRatio = s.cvtRatio, turboSpool = s.turboSpool, engineTemperature = s.engineTemperature, exhaustHeat = s.exhaustHeat, engineDamage = s.engineDamage,
                gearboxInputRpm = car.GearboxInputRpm, signedClutchSlipRpm = s.gear == 0 ? 0 : s.rpm - car.GearboxInputRpm, combustionFraction = s.combustionFraction, activeLimitRpm = s.activeLimitRpm, limiter = s.limiter,
                clutchTemperatureC = clutch == null ? 20 : clutch.condition.temperatureC, clutchHealth = clutch == null ? 1 : clutch.condition.health, gearboxTemperatureC = gearbox == null ? 20 : gearbox.condition.temperatureC, driveshaftHealth = shaft == null ? 1 : shaft.condition.health, drivetrainFailed = s.drivetrainFailed,
                shifting = s.shiftRemaining > 0, twoStep = s.launchControlActive, stalled = s.stalled, failed = s.engineFailed });
            var sample = samples[samples.Count - 1]; sample.translatingMassKg = car.mass.TranslatingKg; sample.dryMassKg = car.mass.dryInstalledKg; sample.driverMassKg = car.mass.driverKg; sample.payloadMassKg = car.mass.payloadKg; sample.fuelMassKg = car.mass.fuelKg; sample.cgHeightM = car.mass.centerOfGravityHeightM; sample.cgPositionM = car.mass.centerOfGravityFromFrontM; sample.frontLoadN = s.frontLoadN; sample.rearLoadN = s.rearLoadN; sample.frontShare = s.frontTorqueShare; sample.rearShare = s.rearTorqueShare;
            sample.frontSlipEstimate = s.frontSlip; sample.rearSlipEstimate = s.rearSlip; sample.frontForceN = s.frontDriveForceN; sample.rearForceN = s.rearDriveForceN;
            sample.frontAvailable = s.frontDriveAvailable; sample.rearAvailable = s.rearDriveAvailable; sample.brakeCapacity = s.brakeCapacityFactor; sample.brakesFailed = s.brakesFailed;
            var tires=car.mechanics.parts.Find(p=>p.slot=="Tires");if(tires!=null)for(int i=0;i<4;i++){sample.tireHealth[i]=TireDynamics.RemainingHealth(tires.condition.tires[i]);sample.tireTemperatureC[i]=tires.condition.tires[i].temperatureC;sample.tireSlipEnergyJ[i]=tires.condition.tires[i].slipEnergyJ;}
            var f=s.fluids;sample.oilLitres=f.oilLitres;sample.coolantLitres=f.coolantLitres;sample.oilTemperatureC=f.oilTemperatureC;sample.oilPressureBar=f.oilPressureBar;sample.coolantPressureBar=f.coolantPressureBar;sample.oilCondition=f.oilCondition;sample.oilBurnLps=f.oilBurnLps;sample.oilLeakLps=f.oilLeakLps;sample.coolantLeakLps=f.coolantLeakLps;sample.coolantVentLps=f.coolantVentLps;sample.valveFloat=s.valveFloat;sample.combustionEfficiency=s.conditionCombustionEfficiency;
            sample.cylinderFactor=s.cylinderCombustionFactor;sample.cylinderFuelFactor=s.cylinderFuelFactor;sample.individualValveFloat=s.individualValveFloat;sample.cylinderContribution=(double[])s.cylinderContribution.Clone();sample.clutchOilL=clutch==null?0:clutch.condition.clutchOilLitres;sample.clutchOilCondition=clutch==null?0:clutch.condition.clutchOilCondition;
            sample.fuelLitres = s.fuelLitres; sample.fuelUsedLitres = s.fuelUsedLitres; sample.fuelFlowLps = s.fuelFlowLitresPerSecond;
            sample.flWheelRpm = s.contact.omega[0] * FlywheelDynamics.RpmPerRadian; sample.frWheelRpm = s.contact.omega[1] * FlywheelDynamics.RpmPerRadian; sample.rlWheelRpm = s.contact.omega[2] * FlywheelDynamics.RpmPerRadian; sample.rrWheelRpm = s.contact.omega[3] * FlywheelDynamics.RpmPerRadian; sample.brakeWorkJ = s.contact.brakeWorkJ; sample.contactWorkJ = s.contact.contactWorkJ; sample.clutchHeatW = s.clutchFrictionPowerW;
            sample.converterHeatW=s.converterLossPowerW;sample.engineCoolingFactor=s.engineCoolingFactor;sample.brakeCapacityFactor=s.brakeCapacityFactor;foreach(PartCategory category in Enum.GetValues(typeof(PartCategory))){var component=car.mechanics.parts.Find(p=>p.slot==category.ToString());sample.partHealth[(int)category]=component==null?-1:component.condition.health;sample.partTemperatureC[(int)category]=component==null?-1:component.condition.temperatureC;}
            sample.bodyEnergyJ=.5*car.mass.TranslatingKg*s.speed*s.speed;sample.wheelEnergyJ=Enumerable.Range(0,4).Sum(i=>.5*car.wheels.inertias[i]*s.contact.omega[i]*s.contact.omega[i]);sample.differentialWorkJ=s.contact.differentialWorkJ;sample.awdCouplingWorkJ=s.contact.centerCouplingWorkJ;sample.awdCouplingHeatW=s.awdCouplingLossPowerW;sample.resistanceWorkJ=s.contact.resistanceWorkJ;sample.driveWorkJ=s.contact.driveWorkJ;sample.aeroResistanceN=.5*1.225*car.spec.dragAreaM2*s.speed*s.speed;sample.rollingResistanceN=car.mass.TranslatingKg*DragMath.Gravity*car.spec.rollingResistance;sample.brakeHeatW=s.brakeFrictionPowerW;sample.wheelDriveNm=(double[])s.contact.driveTorqueNm.Clone();sample.wheelBrakeNm=(double[])s.contact.brakeTorqueNm.Clone();sample.wheelContactNm=(double[])s.contact.contactTorqueNm.Clone();sample.wheelForceN=(double[])s.contact.force.Clone();
            sample.frontLeftSlip = s.frontLeftSlip; sample.frontRightSlip = s.frontRightSlip; sample.rearLeftSlip = s.rearLeftSlip; sample.rearRightSlip = s.rearRightSlip; sample.coolingHeatRejectedW = s.coolingHeatRejectedW;
            sample.frontShaftRpm = s.frontShaftRpm; sample.rearShaftRpm = s.rearShaftRpm; sample.frontShaftNm = s.frontShaftTorqueNm; sample.rearShaftNm = s.rearShaftTorqueNm; sample.frontShaftPowerW = s.frontShaftPowerW; sample.rearShaftPowerW = s.rearShaftPowerW; sample.openCenterFreeBranch = s.openCenterFreeBranch;
        }
        public static RunSnapshot Copy(RunSnapshot source)
        {
            return new RunSnapshot { version = source.version, model = source.model, seed = source.seed,
                vehicle = source.vehicle.Copy(), tune = source.tune.Copy(), driving = source.driving.Copy(),
                startingTick = source.startingTick, hasStartingDynamics = source.hasStartingDynamics, startingDynamics = !source.hasStartingDynamics ? null : source.startingDynamics.Copy(),
                installed = source.installed == null ? null : new InstalledBuild { version = source.installed.version, instanceId = source.installed.instanceId, vehicleDefinitionId = source.installed.vehicleDefinitionId, slots = source.installed.slots.Select(s => new SlotBinding { slot = s.slot, partInstanceId = s.partInstanceId }).ToList() },
                startingFuel = source.startingFuel == null ? new FuelState { litres = source.vehicle.fuel.capacityLitres } : new FuelState { litres = source.startingFuel.litres, cumulativeUsedLitres = source.startingFuel.cumulativeUsedLitres, flowLitresPerSecond = source.startingFuel.flowLitresPerSecond },
                startingFluids=source.startingFluids==null?null:source.startingFluids.Copy(),
                components = source.components.Select(p => p.Copy()).ToList(), inputs = source.inputs.Select(t => new TickInput { tick = t.tick, input = t.input, neutral = t.neutral, staging = t.staging }).ToList() };
        }
        public static PhysicalDrivingSession Replay(RunSnapshot capture)
        {
            var result = new PhysicalDrivingSession(capture); foreach (var tick in capture.inputs) result.Step(tick); return result;
        }
        public string Csv()
        {
            var output = new StringBuilder("model,tick,time_s,segment,rpm,gear,speed_kmh,distance_m,throttle,clutch,brake,engine_nm,hp,shaft_nm,boost_bar,slip,flywheel_j,acceleration_m_s2,shifting,two_step,stalled,failed,clutch_slip_rpm,converter_slip_rpm,cvt_ratio,turbo_spool,engine_temperature,exhaust_heat,pass_local_damage,gearbox_input_rpm,signed_clutch_slip_rpm,combustion_fraction,active_limit_rpm,limiter,clutch_temp_c,clutch_health,gearbox_temp_c,driveshaft_health,drivetrain_failed\n");
            foreach (var s in samples) output.AppendFormat(CultureInfo.InvariantCulture,
                "{0},{1},{2:R},{3},{4:R},{5},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R},{14:R},{15:R},{16:R},{17:R},{18},{19},{20},{21},{22:R},{23:R},{24:R},{25:R},{26:R},{27:R},{28:R},{29:R},{30:R},{31:R},{32:R},{33},{34:R},{35:R},{36:R},{37:R},{38}\n",
                snapshot.model, s.tick, s.time, s.segment, s.rpm, s.gear, s.speedKmh, s.distance, s.throttle, s.clutch, s.brake, s.engineNm, s.hp, s.shaftNm, s.boost, s.slipEstimate, s.flywheelJ, s.acceleration, s.shifting, s.twoStep, s.stalled, s.failed, s.clutchSlipRpm, s.converterSlipRpm, s.cvtRatio, s.turboSpool, s.engineTemperature, s.exhaustHeat, s.engineDamage, s.gearboxInputRpm, s.signedClutchSlipRpm, s.combustionFraction, s.activeLimitRpm, s.limiter, s.clutchTemperatureC, s.clutchHealth, s.gearboxTemperatureC, s.driveshaftHealth, s.drivetrainFailed);
            var rows = output.ToString().TrimEnd('\n').Split('\n');
            var enriched = new StringBuilder(rows[0] + ",front_share,rear_share,front_slip,rear_slip,front_force_n,rear_force_n,front_available,rear_available,brake_capacity,brakes_failed,front_shaft_rpm,rear_shaft_rpm,front_shaft_nm,rear_shaft_nm,front_shaft_power_w,rear_shaft_power_w,open_center_free_branch,front_left_slip,front_right_slip,rear_left_slip,rear_right_slip,cooling_heat_rejected_w\n");
            for (int i = 0; i < samples.Count; i++) { var s = samples[i]; enriched.Append(rows[i + 1]); enriched.AppendFormat(CultureInfo.InvariantCulture, ",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6},{7},{8:R},{9},{10:R},{11:R},{12:R},{13:R},{14:R},{15:R},{16},{17:R},{18:R},{19:R},{20:R},{21:R}\n", s.frontShare, s.rearShare, s.frontSlipEstimate, s.rearSlipEstimate, s.frontForceN, s.rearForceN, s.frontAvailable, s.rearAvailable, s.brakeCapacity, s.brakesFailed, s.frontShaftRpm, s.rearShaftRpm, s.frontShaftNm, s.rearShaftNm, s.frontShaftPowerW, s.rearShaftPowerW, s.openCenterFreeBranch, s.frontLeftSlip, s.frontRightSlip, s.rearLeftSlip, s.rearRightSlip, s.coolingHeatRejectedW); }
            var massRows = enriched.ToString().TrimEnd('\n').Split('\n');
            var massCsv = new StringBuilder(massRows[0] + ",mass_translating_kg,mass_dry_kg,driver_kg,payload_kg,fuel_mass_kg,cg_height_m,cg_position_from_front_m,front_load_n,rear_load_n\n");
            for (int i = 0; i < samples.Count; i++) { var s = samples[i]; massCsv.Append(massRows[i + 1]); massCsv.AppendFormat(CultureInfo.InvariantCulture, ",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R}\n", s.translatingMassKg, s.dryMassKg, s.driverMassKg, s.payloadMassKg, s.fuelMassKg, s.cgHeightM, s.cgPositionM, s.frontLoadN, s.rearLoadN); }
            var wheelRows = massCsv.ToString().TrimEnd('\n').Split('\n'); var wheelCsv = new StringBuilder(wheelRows[0] + ",fl_wheel_rpm,fr_wheel_rpm,rl_wheel_rpm,rr_wheel_rpm,brake_work_j,contact_loss_j,clutch_heat_w\n");
            for (int i = 0; i < samples.Count; i++) { var s = samples[i]; wheelCsv.Append(wheelRows[i + 1]); wheelCsv.AppendFormat(CultureInfo.InvariantCulture, ",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R}\n", s.flWheelRpm, s.frWheelRpm, s.rlWheelRpm, s.rrWheelRpm, s.brakeWorkJ, s.contactWorkJ, s.clutchHeatW); }
            var fuelRows=wheelCsv.ToString().TrimEnd('\n').Split('\n');var fuelCsv=new StringBuilder(fuelRows[0]+",fuel_litres,fuel_used_litres,fuel_flow_lps\n");
            for(int i=0;i<samples.Count;i++){var s=samples[i];fuelCsv.Append(fuelRows[i+1]);fuelCsv.AppendFormat(CultureInfo.InvariantCulture,",{0:R},{1:R},{2:R}\n",s.fuelLitres,s.fuelUsedLitres,s.fuelFlowLps);}
            var tireRows=fuelCsv.ToString().TrimEnd('\n').Split('\n');var tireCsv=new StringBuilder(tireRows[0]);foreach(var wheel in new[]{"fl","fr","rl","rr"})tireCsv.Append(","+wheel+"_tire_health,"+wheel+"_tire_temp_c,"+wheel+"_tire_slip_energy_j");tireCsv.Append('\n');
            for(int i=0;i<samples.Count;i++){var sample=samples[i];tireCsv.Append(tireRows[i+1]);for(int wheel=0;wheel<4;wheel++)tireCsv.AppendFormat(CultureInfo.InvariantCulture,",{0:R},{1:R},{2:R}",sample.tireHealth[wheel],sample.tireTemperatureC[wheel],sample.tireSlipEnergyJ[wheel]);tireCsv.Append('\n');}var fluidRows=tireCsv.ToString().TrimEnd('\n').Split('\n');var fluidCsv=new StringBuilder(fluidRows[0]+",oil_l,coolant_l,oil_temp_c,oil_pressure_bar,coolant_pressure_bar,oil_condition,oil_burn_lps,oil_leak_lps,coolant_leak_lps,coolant_vent_lps,valve_float,combustion_efficiency\n");
            for(int i=0;i<samples.Count;i++){var f=samples[i];fluidCsv.Append(fluidRows[i+1]);fluidCsv.AppendFormat(CultureInfo.InvariantCulture,",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R}\n",f.oilLitres,f.coolantLitres,f.oilTemperatureC,f.oilPressureBar,f.coolantPressureBar,f.oilCondition,f.oilBurnLps,f.oilLeakLps,f.coolantLeakLps,f.coolantVentLps,f.valveFloat,f.combustionEfficiency);}var engineRows=fluidCsv.ToString().TrimEnd('\n').Split('\n');var engineCsv=new StringBuilder(engineRows[0]+",cylinder_combustion_factor,cylinder_fuel_factor,individual_valve_float,clutch_oil_l,clutch_oil_condition");int cylinders=car.spec.sound.cylinders;for(int c=0;c<cylinders;c++)engineCsv.Append(",c"+(c+1)+"_contribution");engineCsv.Append('\n');for(int i=0;i<samples.Count;i++){var sample=samples[i];engineCsv.Append(engineRows[i+1]);engineCsv.AppendFormat(CultureInfo.InvariantCulture,",{0:R},{1:R},{2:R},{3:R},{4:R}",sample.cylinderFactor,sample.cylinderFuelFactor,sample.individualValveFloat,sample.clutchOilL,sample.clutchOilCondition);for(int c=0;c<cylinders;c++)engineCsv.Append(","+(sample.cylinderContribution.Length>c?sample.cylinderContribution[c]:1).ToString("R",CultureInfo.InvariantCulture));engineCsv.Append('\n');}return DrivingTraceChannels.AppendCsv(engineCsv.ToString(),samples);
        }
    }
    public enum PhysicalScenario { FullPullShiftCoastBrake, NeutralFlywheel, SeededDriver }
    public static class PhysicalScenarios
    {
        public const int Ticks = 1920;
        // State-driven planning uses the same copied model, then records ordinary
        // pedals/shift edges. Replay never injects RPM, speed, damage or failure.
        public static List<TickInput> MoneyShiftTimeline(RunSnapshot start)
        {
            var probe = new PhysicalDrivingSession(start); var result = new List<TickInput>();
            int holdUntil = 270; bool moneyShift = false;
            for (int tick = 0; tick < 7200; tick++)
            {
                var s = probe.car.state; bool up = false, down = false;
                if (tick > holdUntil && s.shiftRemaining == 0 && !s.engineFailed)
                {
                    if (s.gear < 3 && !moneyShift && s.rpm >= probe.car.spec.redlineRpm * .9) { up = true; holdUntil = tick + 36; }
                    if (s.gear > 1 && (moneyShift || s.speed / probe.car.wheels.radius * FlywheelDynamics.RpmPerRadian * probe.car.spec.gears[0] * probe.car.tune.finalDrive >= probe.car.spec.redlineRpm * 2))
                    { down = true; moneyShift = true; holdUntil = tick + 48; }
                }
                double gas = tick < 120 || moneyShift ? 0 : tick < 240 ? DragMath.Clamp((probe.car.spec.redlineRpm * .6 - s.rpm) / 1800 + .08, 0, 1) : tick < holdUntil ? .2 : 1;
                var input = new TickInput { tick = tick, neutral = tick < 240, staging = tick < 240,
                    input = new DriverInput { throttle = gas,
                        clutch = tick < 240 || up || down || tick < holdUntil ? 1 : 0, shiftUp = tick == 240 || up, shiftDown = down } };
                probe.Step(input); result.Add(input);
                if (s.engineFailed && tick > holdUntil + 240) break;
            }
            return result;
        }
        // Precomputed tick provider: same seed means exactly the same controls, with no performance bonuses.
        public static List<TickInput> Timeline(PhysicalScenario scenario, int seed)
        {
            var random = new Random(seed); int shiftOffset = scenario == PhysicalScenario.SeededDriver ? random.Next(-30, 31) : 0;
            double gas = scenario == PhysicalScenario.SeededDriver ? .8 + random.NextDouble() * .2 : 1;
            var result = new List<TickInput>();
            for (int tick = 0; tick < Ticks; tick++)
            {
                bool neutralOnly = scenario == PhysicalScenario.NeutralFlywheel;
                bool staging = tick < 240 && !neutralOnly;
                bool neutral = staging || tick >= 1320 || neutralOnly;
                bool up = !neutral && (tick == 240 || tick == 600 + shiftOffset || tick == 960 + shiftOffset);
                bool clutch = staging || Math.Abs(tick - (600 + shiftOffset)) <= 12 || Math.Abs(tick - (960 + shiftOffset)) <= 12;
                result.Add(new TickInput { tick = tick, neutral = neutral, staging = staging,
                    input = new DriverInput { throttle = tick < 120 || tick >= (neutralOnly ? 360 : 1200) ? 0 : staging ? .65 : gas, clutch = clutch ? 1 : 0, shiftUp = up, brake = tick >= 1560 ? 1 : 0 } });
            }
            return result;
        }
    }
}
