using System;
using System.Linq;
using System.Collections.Generic;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // One shared, provisional stress calibration for race and bench. Conditions belong
    // to copied part instances; only the race owner decides when to persist them.
    public sealed class MechanicalStress
    {
        public readonly List<PartInstance> parts;
        readonly MechanicalCatalog catalog;
        readonly Dictionary<string, PartDefinition> definitions;
        readonly VehicleSpec spec;
        readonly AwdSpec awd;
        readonly bool frictionClutch;
        readonly Dictionary<string, PartInstance> bySlot;
        public MechanicalStress(VehicleSpec spec, Tune tune, IEnumerable<PartInstance> installed)
        {
            this.spec = spec; awd = AxleDriveDynamics.Configuration(spec, tune);
            var type = tune.transmissionUpgrade == TransmissionUpgrade.Factory ? spec.transmission.type : (TransmissionType)((int)tune.transmissionUpgrade - 1);
            frictionClutch = type == TransmissionType.Manual || type == TransmissionType.DCT;
            catalog = new MechanicalCatalog(new[] { spec });
            definitions = catalog.parts.ToDictionary(p => p.id);
            foreach (var p in spec.fittedParts) definitions[p.id] = p.Copy();
            parts = installed == null ? (spec.hasInstalledParts ? spec.fittedParts.ToDictionary(p => p.category, p => p.id) : catalog.Recipe(spec.id, tune)).Select(p => new PartInstance { instanceId = "simulation/" + p.Key, definitionId = p.Value, location = PartLocation.Vehicle, ownerId = "simulation", slot = p.Key.ToString() }).ToList() : installed.Select(p => p.Copy()).ToList();
            foreach(var p in parts){var d=Definition(p);EngineElements.Ensure(spec,d,p);ClutchHardware.Ensure(d,p.condition);}
            bySlot = parts.ToDictionary(p => p.slot);
        }
        public void RefreshBindings() { bySlot.Clear(); foreach (var p in parts) bySlot.Add(p.slot, p); }
        public PartInstance Part(PartCategory category) { PartInstance part; return bySlot.TryGetValue(category.ToString(),out part)?part:null; }
        public PartDefinition Definition(PartInstance part) { PartDefinition result; return definitions.TryGetValue(part.definitionId, out result) ? result : null; }
        public static double TorqueStrength(PartDefinition definition, ComponentCondition condition) { return definition.torqueLimitNm * (.35 + .65 * condition.health); }
        public void Prepare(CarState s)
        {
            s.clutchCapacityFactor = 1; s.mechanicalPowerFactor = 1;s.clutchViscousDragNm=0;s.clutchOilFactor=1;var clutchPart=Part(PartCategory.Clutch);if(clutchPart!=null&&frictionClutch){var clutchDefinition=Definition(clutchPart);ClutchHardware.Ensure(clutchDefinition,clutchPart.condition);s.clutchOilFactor=ClutchHardware.OilFactor(clutchDefinition,clutchPart.condition);if(clutchDefinition.clutchEnvironment==ClutchEnvironment.Wet&&s.gear==0)s.clutchViscousDragNm=Math.Min(5,s.rpm*.0008)*s.clutchOilFactor;}
            s.frontDriveAvailable = spec.drive != DriveType.RWD && Healthy(PartCategory.FrontAxle) && (spec.drive != DriveType.AWD || Healthy(PartCategory.FrontDriveshaft) && Healthy(PartCategory.FrontDifferential));
            s.rearDriveAvailable = spec.drive != DriveType.FWD && Healthy(PartCategory.RearAxle) && (spec.drive != DriveType.AWD || Healthy(PartCategory.RearDriveshaft) && Healthy(PartCategory.RearDifferential));
            bool transferFailed = spec.drive == DriveType.AWD && !Healthy(PartCategory.TransferCase);
            if (transferFailed && awd.mode == AwdMode.OnDemand) { if (awd.primaryFront) s.rearDriveAvailable = false; else s.frontDriveAvailable = false; transferFailed = false; }
            bool commonFailed = !Healthy(PartCategory.Gearbox) || !Healthy(PartCategory.Driveshaft) || !Healthy(PartCategory.Differential) || frictionClutch && !Healthy(PartCategory.Clutch) || transferFailed;
            s.openCenterFreeBranch = !commonFailed && spec.drive == DriveType.AWD && (awd.mode == AwdMode.OpenCenter || awd.mode == AwdMode.LimitedSlipCenter) && !awd.isolateFailedAxle && (!s.frontDriveAvailable || !s.rearDriveAvailable);
            s.drivetrainFailed = commonFailed || !s.openCenterFreeBranch && !AxleDriveDynamics.HasDrivePath(spec, awd, s.frontDriveAvailable, s.rearDriveAvailable);
            PartInstance gearbox; s.gearCapacityFactor = bySlot.TryGetValue(PartCategory.Gearbox.ToString(), out gearbox) ? (gearbox.condition.failed ? 0 : .4 + .6 * ConditionHealth(gearbox.condition,s)) : 0;
            var brakes = parts.Find(p => p.slot == PartCategory.Brakes.ToString());
            s.brakesFailed = !Healthy(PartCategory.Brakes);
            s.brakeCapacityFactor = s.brakesFailed ? 0 : Capacity(brakes,s);
            var cooling = parts.Find(p => p.slot == PartCategory.Cooling.ToString());
            s.engineCoolingFactor = cooling == null || cooling.condition.failed ? .15 : Definition(cooling).engineHeatTransferScale * (.4 + .6 * ConditionHealth(cooling.condition,s));
            s.frontDifferentialHealth = DifferentialHealth(s,spec.drive == DriveType.FWD ? PartCategory.Differential : PartCategory.FrontDifferential);
            s.rearDifferentialHealth = DifferentialHealth(s,spec.drive == DriveType.RWD ? PartCategory.Differential : PartCategory.RearDifferential);
            if (!Healthy(PartCategory.Pistons) || !Healthy(PartCategory.Rods) || !Healthy(PartCategory.Crankshaft)) { s.engineFailed = true; s.engineDamage = 1; }
            PartInstance controller; bySlot.TryGetValue(PartCategory.Controls.ToString(), out controller);
            s.engineSupplyAvailable = Healthy(PartCategory.FuelSystem) && Healthy(PartCategory.Ignition) && (spec.engineBehavior.fueling == FuelSystem.Carburetor || Healthy(PartCategory.Controls) && FuelingHardware.ControllerSupports(controller == null ? null : Definition(controller), spec));
            if (!s.engineSupplyAvailable) s.stalled = true;
            s.deliveryPowerCapacityHp = 0;
            foreach (var category in new[] { PartCategory.FuelSystem, PartCategory.Ignition }) {
                PartInstance supply; if (!bySlot.TryGetValue(category.ToString(), out supply)) continue;
                var d = Definition(supply); if (d.maxPowerHp <= 0) continue;
                double capacity = d.maxPowerHp * (.4 + .6 * ConditionHealth(supply.condition,s));
                s.deliveryPowerCapacityHp = s.deliveryPowerCapacityHp == 0 ? capacity : Math.Min(s.deliveryPowerCapacityHp, capacity);
            }
            foreach (var part in parts)
            {
                var d = Definition(part); if (d == null) continue; var c = part.condition;
                if(d.category==PartCategory.Tires){TireDynamics.Ensure(c);continue;}
                if (s.optimalThermals) c.temperatureC = OptimalTemperature(d.category);
                if (d.category == PartCategory.Clutch && frictionClutch)
                    s.clutchCapacityFactor = c.failed ? 0 : ClutchHardware.Capacity(d,c)*Math.Max(.05, .4 + .6 * ConditionHealth(c,s)) * DragMath.Clamp(1 - Math.Max(0, c.temperatureC - d.thermalLimitC * .7) / d.thermalLimitC, .15, 1);
                if (d.category == PartCategory.HeadIntake || d.category == PartCategory.Induction || d.category == PartCategory.Exhaust)
                    s.mechanicalPowerFactor = Math.Min(s.mechanicalPowerFactor, c.failed ? .3 : .5 + .5 * ConditionHealth(c,s));
                if (d.category == PartCategory.Pistons || d.category == PartCategory.Rods || d.category == PartCategory.Crankshaft)
                {
                    if (c.failed) { s.engineFailed = true; s.engineDamage = 1; }
                    s.mechanicalPowerFactor = Math.Min(s.mechanicalPowerFactor, ConditionHealth(c,s) >= .9 ? 1 : .7 + ConditionHealth(c,s) / 3);
                }
            }
            if (s.drivetrainFailed) s.clutchCapacityFactor = 0;
        }
        static double ConditionHealth(ComponentCondition c,CarState s){return c.failed?0:1-(1-c.health)*s.conditionSeverity;}
        double DifferentialHealth(CarState s,PartCategory category) { PartInstance p; return bySlot.TryGetValue(category.ToString(), out p) ? (p.condition.failed ? 0 : ConditionHealth(p.condition,s)) : 1; }
        bool Healthy(PartCategory category) { PartInstance p; return bySlot.TryGetValue(category.ToString(), out p) && !p.condition.failed && p.condition.health > 0; }
        double Capacity(PartInstance p,CarState s) { var d = Definition(p); return (.4 + .6 * ConditionHealth(p.condition,s)) * DragMath.Clamp(1 - Math.Max(0, p.condition.temperatureC - d.thermalLimitC * .7) / d.thermalLimitC, .15, 1); }
        public void Tick(CarSimulation car, double dt, double mismatchBefore, double distanceBefore)
        {
            var s = car.state; double crankPower = Math.Abs(s.engineHorsepower) * 745.7;
            double ratio = s.gear == 0 ? 0 : (car.transmission.type == TransmissionType.CVT ? s.cvtRatio : car.spec.gears[s.gear - 1]);
            double shaftTorque = Math.Abs(s.transmittedTorqueNm) * ratio * car.spec.drivetrainEfficiency;
            double drivePower = Math.Abs(s.transmittedTorqueNm * car.GearboxInputRpm / FlywheelDynamics.RpmPerRadian);
            double slipPower = s.wheelContactInitialized ? (car.transmission.type == TransmissionType.Manual || car.transmission.type == TransmissionType.DCT ? s.clutchFrictionPowerW : s.converterLossPowerW) : Math.Abs(s.transmittedTorqueNm) * Math.Abs(mismatchBefore) / FlywheelDynamics.RpmPerRadian;
            bool frictionClutch = car.transmission.type == TransmissionType.Manual || car.transmission.type == TransmissionType.DCT;
            PartInstance coolant; bySlot.TryGetValue(PartCategory.Cooling.ToString(), out coolant);
            double transferredHeat = 0, coolantTemperature = coolant == null || coolant.condition.failed ? 20 : coolant.condition.temperatureC;
            bool existingCoreFailure = parts.Any(p => PartSemantics.EngineInternal(Definition(p).category) && (p.condition.failed || p.condition.health <= 0));
            s.coolingHeatRejectedW = 0;
            foreach (var part in parts)
            {
                var d = Definition(part); if (d == null || !PartSemantics.Wears(d.category) || d.category==PartCategory.Tires || ServiceHardware.IsCategory(d.category)) continue; var c = part.condition;
                double heat = 0, torque = 0, overspeed = 0; bool enginePart = false;
                switch (d.category)
                {
                    case PartCategory.Clutch: heat = frictionClutch ? slipPower : 0; torque = 0; break;
                    case PartCategory.Gearbox: heat = drivePower * .025 + (frictionClutch ? 0 : slipPower); torque = Math.Abs(s.transmittedTorqueNm); break;
                    case PartCategory.Driveshaft: case PartCategory.Differential: heat = drivePower * .003; torque = shaftTorque; break;
                    case PartCategory.TransferCase: heat = drivePower * .003; torque = shaftTorque; break;
                    case PartCategory.FrontDriveshaft: case PartCategory.FrontDifferential: case PartCategory.FrontAxle:
                        torque = s.frontDriveAvailable ? shaftTorque * s.frontTorqueShare * (d.category == PartCategory.FrontAxle ? car.tune.finalDrive : 1) : 0; heat = drivePower * .003 * s.frontTorqueShare; break;
                    case PartCategory.RearDriveshaft: case PartCategory.RearDifferential: case PartCategory.RearAxle:
                        torque = s.rearDriveAvailable ? shaftTorque * s.rearTorqueShare * (d.category == PartCategory.RearAxle ? car.tune.finalDrive : 1) : 0; heat = drivePower * .003 * s.rearTorqueShare; break;
                    case PartCategory.Pistons: case PartCategory.Rods: case PartCategory.Crankshaft:
                        enginePart = true; heat = crankPower * .025; torque = Math.Abs(s.engineTorqueNm);
                        overspeed = Math.Max(0, s.rpm / Math.Max(1, d.rpmLimit) - 1); break;
                    case PartCategory.Cooling: continue;
                    case PartCategory.HeadIntake: heat = crankPower * .002; break;
                    case PartCategory.FuelSystem: case PartCategory.Ignition: heat = crankPower * .001; break;
                    case PartCategory.Induction: heat = crankPower * s.boostBar * .006; break;
                    case PartCategory.Exhaust: heat = crankPower * .004 + (s.antiLagActive ? 25000 : 0); break;
                    case PartCategory.Brakes: heat = s.brakeFrictionPowerW; break;
                }
                double cooling = d.coolingWattsPerC * (1 + s.speed * .035);
                if(d.category==PartCategory.Clutch&&frictionClutch&&d.clutchEnvironment==ClutchEnvironment.Wet){cooling*=ClutchHardware.OilFactor(d,c);c.clutchOilLitres=Math.Max(0,c.clutchOilLitres-c.leakSeverity*.002*dt*car.driver.FluidDepletionRate);c.clutchOilCondition=DragMath.Clamp(c.clutchOilCondition-heat*dt/1e9*car.driver.FluidAgingRate,0,1);}
                if (enginePart) cooling *= s.engineCoolingFactor;
                double rejected = cooling * (c.temperatureC - (enginePart ? coolantTemperature : 20));
                if (enginePart && coolant != null && !coolant.condition.failed) transferredHeat += rejected;
                if (!s.optimalThermals) c.temperatureC = DragMath.Clamp(c.temperatureC + (heat - rejected) / d.heatCapacityJPerC * dt, -50, 2000);
                c.runningSeconds += dt; c.distanceM += Math.Max(0, s.distance - distanceBefore);
                if (d.category == PartCategory.Clutch && frictionClutch) c.slipEnergyJ += heat * dt;
                if (c.failed) continue;
                double strength = TorqueStrength(d, c);
                double overload = torque <= 0 ? 0 : Math.Max(0, torque / Math.Max(1, strength) - 1);
                double thermal = Math.Max(0, c.temperatureC / d.thermalLimitC - 1);
                double loss = heat * dt / d.wearEnergyJ + dt * (overload * overload + overspeed * overspeed * 8 + thermal * thermal) / d.overloadSeconds;
                c.stressExposure += dt * (overload + overspeed + thermal);
                EngineElements.Wear(part,loss*car.driver.WearRate);
                c.health = (float)Math.Max(0, c.health - loss * car.driver.WearRate);
                if (enginePart && s.engineFailed && !existingCoreFailure && car.driver.WearRate > 0) { c.health = 0; Fail(c, "Engine overspeed / thermal damage"); }
                else if (c.health <= 0) Fail(c, overspeed > 0 ? "Mechanical overspeed" : overload > 0 ? "Torque overload" : thermal > 0 ? "Thermal overload" : "Friction wear");
            }
            if (coolant != null) {
                var d = Definition(coolant); var c = coolant.condition;
                double radiator = c.failed ? 0 : d.coolingWattsPerC * (.4 + .6 * c.health) * (1 + s.speed * .035) * (c.temperatureC - 20);
                s.coolingHeatRejectedW = Math.Max(0, radiator);
                double fill=DragMath.Clamp(s.fluids.coolantLitres/Math.Max(1,car.spec.fluids.coolantCapacityLitres),.1,1);
                radiator*=fill; s.coolingHeatRejectedW=Math.Max(0,radiator);
                if (!s.optimalThermals) c.temperatureC = DragMath.Clamp(c.temperatureC + (transferredHeat + crankPower * .08 - radiator) / (d.heatCapacityJPerC*fill) * dt, -50, 2000);
                c.runningSeconds += dt; c.distanceM += Math.Max(0, s.distance - distanceBefore);
                double thermal = Math.Max(0, c.temperatureC / d.thermalLimitC - 1);
                if (!c.failed) { c.health = (float)Math.Max(0, c.health - dt * thermal * thermal / d.overloadSeconds * car.driver.WearRate); if (c.health <= 0) Fail(c, "Cooling thermal overload"); }
            }
            Prepare(s);
            if (s.engineFailed) s.engineTorqueNm = s.engineHorsepower = 0;
            if (s.drivetrainFailed || s.engineFailed) { s.transmittedTorqueNm = 0; s.feedback = s.engineFailed ? "ENGINE FAILED" : "DRIVELINE FAILED / NO WHEEL TORQUE"; s.feedbackRemaining = 2; }
            else if (s.brakesFailed) { s.feedback = "BRAKES FAILED / ENGINE AND DRIVE STILL AVAILABLE"; s.feedbackRemaining = 2; }
            else if (s.openCenterFreeBranch) { s.feedback = "FREE CENTER OUTPUT / DISCONNECTED SHAFT SPINNING / LOW ROAD TORQUE"; s.feedbackRemaining = 2; }
            else if (spec.drive == DriveType.AWD && (!s.frontDriveAvailable || !s.rearDriveAvailable)) { s.feedback = "AWD DEGRADED / " + (s.frontDriveAvailable ? "FRONT" : "REAR") + " DRIVE AVAILABLE"; s.feedbackRemaining = 2; }
        }
        public static double OptimalTemperature(PartCategory category) { return category==PartCategory.Gearbox?70:category==PartCategory.Clutch?60:category==PartCategory.Brakes?80:PartSemantics.EngineInternal(category)||category==PartCategory.Cooling?90:20; }
        static void Fail(ComponentCondition c, string reason) { c.failed = true; c.failureReason = reason; }
        public void ResetWear(CarState s)
        {
            foreach (var p in parts){var old=p.condition;p.condition = new ComponentCondition{clutchFluidVersion=old.clutchFluidVersion,clutchOilLitres=old.clutchOilLitres,clutchOilCondition=old.clutchOilCondition};}
            s.engineFailed = s.drivetrainFailed = s.stalled = false; s.engineDamage = 0;s.cylinderCombustionFactor=s.cylinderFuelFactor=1;s.individualValveFloat=0;if(s.cylinderContribution!=null)for(int i=0;i<s.cylinderContribution.Length;i++){s.cylinderContribution[i]=1;s.cylinderFuelDelivery[i]=1;}
            Prepare(s);TireDynamics.ResetContactReference(s,parts);
        }
    }
}
