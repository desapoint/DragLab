using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    public sealed class InspectionBuild
    {
        public FuelState startingFuel;
        public FluidState startingFluids;
        public BuildResolution build;
        public List<PartInstance> components;
        public static InspectionBuild Capture(PlayerProfile profile, OwnedCar car, Tune adjustments = null, string definitionId = null, string instanceId = null, PartCategory? remove = null, IEnumerable<PartCartLine> cart = null, bool healthy = false)
        {
            var map = MechanicsOwnership.Definitions(profile, car);
            var components = car.installed.slots.Select(s => profile.parts.Find(p => p.instanceId == s.partInstanceId)).Where(p => p != null).Select(p => p.Copy()).ToList();
            PartInstance existing = instanceId == null ? null : profile.parts.Find(p => p.instanceId == instanceId);
            var definition = profile.catalog.Find(existing == null ? definitionId : existing.definitionId);
            if (definition != null)
            {
                if (!profile.catalog.Fits(profile.catalog.Spec(car.vehicleId), definition)) throw new ArgumentException("Incompatible preview.");
                map[definition.category] = definition.id; components.RemoveAll(p => p.slot == definition.category.ToString());
                var replacement = existing == null ? new PartInstance { instanceId = "preview/" + definition.id, definitionId = definition.id } : existing.Copy();
                replacement.slot = definition.category.ToString(); components.Add(replacement);
            }
            if (remove.HasValue) { map.Remove(remove.Value); components.RemoveAll(p => p.slot == remove.Value.ToString()); }
            if(cart!=null) foreach(var line in cart.Where(l=>l.install)) {
                var d=profile.catalog.Find(line.definitionId);
                if(d==null||!profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),d))throw new ArgumentException("Incompatible cart preview.");
                map[d.category]=d.id;components.RemoveAll(p=>p.slot==d.category.ToString());
                components.Add(new PartInstance {instanceId="cart-preview/"+d.id,definitionId=d.id,slot=d.category.ToString()});
            }
            if(healthy) foreach(var item in components) {
                var c=item.condition;c.health=1;c.failed=false;c.failureReason="";c.deposits=c.leakSeverity=c.adjustmentError=c.stressExposure=c.serviceWearRemainder=0;c.clutchOilCondition=1;
                foreach(var e in c.elements){e.health=1;e.fault=ElementFault.None;e.severity=0;e.wearRemainder=0;}
                foreach(var t in c.tires){t.health=1;t.failed=false;t.consumedLife=0;}
            }
            var resolved = profile.catalog.Resolve(car.vehicleId, map, adjustments ?? car.tune);
            if(string.IsNullOrEmpty(car.engineAssemblyId))resolved.errors.Add("No engine attached: stored engines retain their own hardware and liquids.");
            foreach (var p in components) if ((PartSemantics.Wears(profile.catalog.Find(p.definitionId).category) || profile.catalog.Find(p.definitionId).category == PartCategory.Controls && resolved.spec.engineBehavior.fueling == FuelSystem.Injection) && (p.condition.failed || p.condition.health <= 0)) (MechanicalCatalog.DisablingEngineFailure(profile.catalog.Find(p.definitionId).category) || profile.catalog.Find(p.definitionId).category == PartCategory.Controls && resolved.spec.engineBehavior.fueling == FuelSystem.Injection ? resolved.errors : resolved.warnings).Add("Failed " + p.slot + ": " + p.condition.failureReason);
            return new InspectionBuild { build = resolved, components = components, startingFluids=car.fluids.Copy(), startingFuel = new FuelState { litres = car.fuel.litres } };
        }
    }
    public struct InspectionCurvePoint { public double rpm, hp, torque; }
    public sealed class SetupWarning
    {
        public string title, explanation, suggestion;
        public bool critical;
        public SetupWarning(string title, string explanation, string suggestion, bool critical = false) { this.title = title; this.explanation = explanation; this.suggestion = suggestion; this.critical = critical; }
    }
    public sealed class CarInspection
    {
        public InspectionBuild source;
        public readonly List<InspectionCurvePoint> curve = new List<InspectionCurvePoint>();
        public readonly List<SetupWarning> warnings = new List<SetupWarning>();
        public double peakHp, hpRpm, peakTorque, torqueRpm, boostBar, wheelRadiusIn, wheelDiameterIn, wheelMassKg, vehicleMassKg, effectiveMassKg, wheelInertia, flywheelMassKg, flywheelInertia, clutchCapacityNm, shiftSeconds, brakeG, condition, turboSpoolSeconds;
        public double coolingTransferPercent, radiatorWattsPerC, tireRollingLifeKm, tireSlidingLifeMj, tireGripFactor, lowestTireHealth;
        public MassBreakdown mass;
        public double elapsed, trapMps, maxSlip, maxClutchSlip, slippingSeconds;
        public bool completed, probeFailed, probeComplete;
        public TransmissionType transmission;
        public string induction, fueling, transmissionName;
        public double[] gearRedlineMps, rpmAfterUpshift;
        public static CarSimulation PrepareCar(InspectionBuild source)
        {
            var tune=source.build.tune;
            var car=new CarSimulation(source.build.spec,tune,new DriverSettings { tractionAssist=false, wearMode=WearMode.Full },source.components);
            if(source.startingFuel!=null)car.SetMassInputs(tune.driverMassKg,tune.payloadMassKg,tune.payloadPositionM,tune.payloadHeightM,source.startingFuel.litres);
            if(source.startingFluids!=null)car.SetFluids(source.startingFluids);
            return car;
        }
        public static CarInspection Calculate(InspectionBuild source) => CalculatePrepared(source,PrepareCar(source));
        // Preparation loads authored catalog data on the main thread. This method is pure domain math.
        public static CarInspection CalculatePrepared(InspectionBuild source,CarSimulation car,bool runProbe=true)
        {
            var result = new CarInspection { source = source, probeComplete=runProbe }; var b = source.build; var spec = b.spec; var tune = b.tune; var wheels = car.wheels;
            result.mass = car.mass;
            var radiator = car.mechanics.parts.Find(p => p.slot == PartCategory.Cooling.ToString());
            result.coolingTransferPercent = car.state.engineCoolingFactor * 100;
            result.radiatorWattsPerC = radiator == null || radiator.condition.failed ? 0 : car.mechanics.Definition(radiator).coolingWattsPerC * (.4 + .6 * radiator.condition.health);
            var tirePack=car.mechanics.parts.Find(p=>p.slot=="Tires");if(tirePack!=null){var tireDefinition=car.mechanics.Definition(tirePack);result.tireRollingLifeKm=tireDefinition.tireLifeDistanceM/1000;result.tireSlidingLifeMj=tireDefinition.wearEnergyJ/1e6;result.tireGripFactor=tireDefinition.grip*tirePack.condition.tires.Min(t=>TireDynamics.Grip(tireDefinition,t));result.lowestTireHealth=tirePack.condition.tires.Min(t=>t.health);}
            result.transmission = car.transmission.type; result.transmissionName = TransmissionDynamics.DisplayName(car.transmission); result.induction = car.induction.type.ToString(); result.fueling = car.engine.data.fueling.ToString();
            result.wheelRadiusIn = wheels.radius / DisplayUnits.MetresPerInch; result.wheelDiameterIn = result.wheelRadiusIn * 2;
            result.wheelMassKg = wheels.assemblyMass; result.vehicleMassKg = wheels.vehicleMass; result.effectiveMassKg = wheels.effectiveMass; result.wheelInertia = wheels.totalInertia;
            result.flywheelMassKg = MassDynamics.Installed(spec,PartCategory.Flywheel) ? spec.flywheel.massKg * tune.FlywheelMass : 0; result.flywheelInertia = FlywheelDynamics.Inertia(spec, tune);
            result.clutchCapacityNm = spec.ClutchCapacity * tune.CapacityMultiplier * car.state.clutchCapacityFactor;
            result.shiftSeconds = TransmissionDynamics.ShiftSeconds(car.transmission, spec, tune); result.brakeG = spec.BrakeDeceleration * car.state.brakeCapacityFactor / DragMath.Gravity;
            result.condition = car.mechanics.parts.Where(p => PartSemantics.Wears(car.mechanics.Definition(p).category)).Select(p => (double)p.condition.health).DefaultIfEmpty(1).Min();
            result.turboSpoolSeconds = car.induction.data.turboSpoolSeconds * Math.Pow(car.induction.size, 1.7) * (car.induction.turboCount == 2 ? car.induction.data.twinTurboSpoolFactor : 1);
            for (double rpm = spec.idleRpm; rpm <= spec.redlineRpm + .01; rpm = Math.Min(spec.redlineRpm, rpm + 100))
            {
                car.state.rpm=rpm;car.mechanics.Prepare(car.state);ServiceDynamics.Prepare(car);
                var steady = InductionDynamics.SteadyOutput(car.induction, rpm);
                double torque = Math.Max(0,steady.engineTorqueNm * car.state.mechanicalPowerFactor-car.state.extraEngineFrictionNm);
                if (car.state.deliveryPowerCapacityHp > 0) torque = Math.Min(torque, car.state.deliveryPowerCapacityHp * 7127 / rpm);
                if (car.state.engineFailed || !car.state.engineSupplyAvailable) torque = 0;
                double hp = torque * rpm / 7127;
                result.curve.Add(new InspectionCurvePoint { rpm = rpm, hp = hp, torque = torque });
                if (torque > result.peakTorque) { result.peakTorque = torque; result.torqueRpm = rpm; }
                if (hp > result.peakHp) { result.peakHp = hp; result.hpRpm = rpm; }
                result.boostBar = Math.Max(result.boostBar, steady.boostBar);
                if (rpm >= spec.redlineRpm) break;
            }
            result.gearRedlineMps = spec.gears.Select(ratio => spec.redlineRpm / FlywheelDynamics.RpmPerRadian * wheels.radius / (ratio * tune.finalDrive)).ToArray();
            result.rpmAfterUpshift = Enumerable.Range(1, spec.gears.Length - 1).Select(i => car.ShiftPoint(i) * spec.gears[i] / spec.gears[i - 1]).ToArray();
            if (result.transmission == TransmissionType.CVT) result.gearRedlineMps = new[] { spec.redlineRpm / FlywheelDynamics.RpmPerRadian * wheels.radius / (car.transmission.cvtMaxRatio * tune.finalDrive), spec.redlineRpm / FlywheelDynamics.RpmPerRadian * wheels.radius / (car.transmission.cvtMinRatio * tune.finalDrive) };
            car.state.rpm=spec.idleRpm;car.mechanics.Prepare(car.state);ServiceDynamics.Prepare(car);
            if (b.Drivable && runProbe)
            {
                for (int tick = 0; tick < 360; tick++) car.Stage(DragMath.FixedStep, new DriverInput());
                car.Launch(0); double stable = 0;
                for (int tick = 0; tick < 3600; tick++)
                {
                    car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 }); result.elapsed = (tick + 1) * DragMath.FixedStep;
                    if (car.state.shiftRemaining > 0) stable = 0; else stable += DragMath.FixedStep;
                    if (car.state.speed > 5 && stable > .5)
                    {
                        result.maxSlip = Math.Max(result.maxSlip, car.state.slip); result.maxClutchSlip = Math.Max(result.maxClutchSlip, car.state.clutchSlipRpm);
                        if (car.state.clutchSlipRpm > 500) result.slippingSeconds += DragMath.FixedStep;
                    }
                    if (car.state.engineFailed || car.state.drivetrainFailed) { result.probeFailed = true; break; }
                    if (car.state.distance >= 402.336) { result.completed = true; break; }
                }
                result.trapMps = car.state.speed;
            }
            result.Diagnose(car);
            return result;
        }
        void Diagnose(CarSimulation car)
        {
            var b = source.build; var tune = b.tune;
            var f=source.startingFluids??FluidState.Full(b.spec.fluids);var fs=b.spec.fluids;
            if(f.oilLitres<fs.oilMinimumLitres)warnings.Add(new SetupWarning("Oil quantity below safe pickup minimum","Only "+f.oilLitres.ToString("F2")+" L remains; pressure can fall under acceleration.","Top up oil; inspect ring, seal, pan and turbo losses. Top-up does not repair the cause.",true));
            if(f.coolantLitres<fs.coolantCapacityLitres*.65)warnings.Add(new SetupWarning("Coolant quantity limits circulation","Reduced fill lowers thermal capacity and coolant circulation.","Top up coolant; inspect radiator and hoses for leaks before sustained load.",true));
            if(f.oilCondition<.5)warnings.Add(new SetupWarning("Oil needs service","Aged oil reduces lubrication protection.","Change oil and filter; topping up alone retains the existing oil condition."));
            var springs=source.components.Find(p=>p.slot=="ValveSprings");double effectiveSpringRating=b.spec.valvetrain.springSafeRpm*(1-.4*(1-(springs==null?0:springs.condition.failed?0:springs.condition.health)));
            if(b.spec.valvetrain.profileDemandRpm>effectiveSpringRating)warnings.Add(new SetupWarning("Cam needs stronger valve springs","Cam demand "+b.spec.valvetrain.profileDemandRpm.ToString("F0")+" RPM exceeds spring control rating "+effectiveSpringRating.ToString("F0")+" RPM after wear. Valve float reduces output and can damage valves.","Fit matching valve springs or lower RPM before racing."));
            foreach(var item in source.components)if(EngineElements.NeedsService(item.condition))warnings.Add(new SetupWarning("Individual "+item.slot+" fault","One or more individual components are worn/failed; inspect cylinder and valve details. Other cylinders may continue running.","Use targeted replacement or a suitable engine rebuild; inspect compression, ignition, injection and spring control."));
            foreach(var item in source.components)if(item.condition.leakSeverity>=.01)warnings.Add(new SetupWarning(item.slot+" leak","Leak severity "+item.condition.leakSeverity.ToString("P0")+"; loss persists during simulated cooldown.","Use the applicable reseal repair, or replace damaged hardware; refilling alone does not seal it."));
            if (!b.Drivable) warnings.Add(new SetupWarning("Build cannot race", string.Join("; ", b.errors), "Fit missing compatible parts or replace the failed components.", true));
            foreach (var warning in b.warnings) warnings.Add(new SetupWarning("Component needs attention", warning, "Inspect its individual effect below and replace the damaged or missing part."));
            if (car.state.brakesFailed) warnings.Add(new SetupWarning("Braking unavailable", "The failed/missing brake system provides no friction braking. Engine operation and drive are independent; the car can still accelerate and coast.", "Replace the brakes to restore stopping capacity.", true));
            if (car.state.drivetrainFailed) warnings.Add(new SetupWarning("No drive path to the road", "A common transmission link, driven axle, or open-center branch is disconnected. The engine can run and rev while the car coasts.", "Replace the failed drive component; an AWD fallback requires suitable center/coupling hardware.", true));
            else if (car.state.openCenterFreeBranch) warnings.Add(new SetupWarning("Open center spins a disconnected shaft", "The free branch consumes power in shaft acceleration and drag; healthy-axle torque is limited by that low resistance. The engine can run, but useful propulsion is severely reduced.", "Replace the broken branch, or use installed hardware that can lock or isolate it.", true));
            else if (car.spec.drive == DriveType.AWD && (!car.state.frontDriveAvailable || !car.state.rearDriveAvailable)) warnings.Add(new SetupWarning("AWD running on its surviving axle", "The current center/coupling configuration retains drive, with reduced available grip. Engine internals have not failed.", "Replace the failed axle/shaft/differential to restore both drive paths."));
            if (peakTorque > clutchCapacityNm && (transmission == TransmissionType.Manual || transmission == TransmissionType.DCT) || slippingSeconds > .5 && (transmission == TransmissionType.Manual || transmission == TransmissionType.DCT))
                warnings.Add(new SetupWarning("Clutch may slip instead of accelerating", "Engine peak " + peakTorque.ToString("F0") + " Nm versus current clutch capacity " + clutchCapacityNm.ToString("F0") + " Nm. Sustained slip in the unassisted probe: " + slippingSeconds.ToString("F1") + " s.", "Fit a stronger/healthier clutch, allow it to cool, reduce boost/power or engage it more gently."));
            if (maxSlip > .15) warnings.Add(new SetupWarning("Excessive wheelspin is likely", "Unassisted full-throttle probe reached " + (maxSlip * 100).ToString("F0") + "% measured wheel slip from independent wheel rotation and tire contact.", "Lower launch RPM/boost, use gentler throttle, improve tire grip or enable traction control."));
            if (tune.WheelDiameter > 1.25 || effectiveMassKg / Math.Max(1, car.spec.massKg) > 1.25)
                warnings.Add(new SetupWarning("Large/heavy tires can make launches sluggish", "Taller tires reduce force at the road and increase rotating load. Effective acceleration mass is " + effectiveMassKg.ToString("F0") + " kg.", "Try a smaller outer radius/lighter wheel assembly or a higher final-drive ratio. In a manual, release the clutch gradually to avoid bogging/stalling."));
            foreach (var p in source.components)
            {
                var d = car.mechanics.Definition(p); if (d == null) continue;
                bool engine = d.category == PartCategory.Pistons || d.category == PartCategory.Rods || d.category == PartCategory.Crankshaft;
                bool input = d.category == PartCategory.Gearbox;
                bool output = d.category == PartCategory.Driveshaft || d.category == PartCategory.Differential || MechanicalCatalog.DriveBranch(d.category);
                double demand = peakTorque * (output ? car.spec.gears[0] * car.spec.drivetrainEfficiency : 1);
                if (d.category == PartCategory.FrontAxle || d.category == PartCategory.RearAxle) demand *= tune.finalDrive;
                if (car.spec.drive == DriveType.AWD && d.category != PartCategory.TransferCase && MechanicalCatalog.DriveBranch(d.category)) demand *= d.category == PartCategory.FrontAxle || d.category == PartCategory.FrontDriveshaft || d.category == PartCategory.FrontDifferential ? car.awd.frontBias : 1 - car.awd.frontBias;
                double strength = MechanicalStress.TorqueStrength(d, p.condition);
                if ((engine || input || output) && demand > strength)
                    warnings.Add(new SetupWarning(d.category + " torque headroom is low", "Estimated peak demand " + demand.ToString("F0") + " Nm exceeds the current condition-adjusted rating (" + strength.ToString("F0") + " Nm).", "Reduce boost/power or replace this part with a compatible stronger/healthier version."));
            }
            if(lowestTireHealth<.5)warnings.Add(new SetupWarning("Tire condition limits traction", "Lowest tire life "+lowestTireHealth.ToString("P1")+". A failed tire retains only a small residual grip.","Install a compatible healthy tire pack. Keep the removed pack's actual wear when reusing or selling it."));
            if (radiatorWattsPerC == 0) warnings.Add(new SetupWarning("No active cooling", "Engine can start, but only residual air cooling remains.", "Fit a compatible radiator/pump before sustained load."));
            if (condition < .5) warnings.Add(new SetupWarning("A fitted component is heavily worn", "Lowest fitted-part health is " + (condition * 100).ToString("F0") + "%. Replacement does not repair the removed original.", "Inspect the component list and replace or swap the worn part before hard driving."));
            if (car.engine.limiter == LimiterStyle.None) warnings.Add(new SetupWarning("No combustion rev limiter", "Holding gas can overrev and damage the engine; even a limiter cannot prevent a mechanical money shift.", "Select a safe main limiter, upgrade appropriate internals, and avoid unsafe downshifts."));
            if (probeFailed) warnings.Add(new SetupWarning("The assisted full-throttle probe broke the car", car.state.feedback, "Inspect torque/RPM ratings and worn parts; reduce boost and aggressive controls or replace weak hardware.", true));
            else if (probeComplete && b.Drivable && !completed) warnings.Add(new SetupWarning("Probe did not finish within 30 seconds", "The copied car could not complete a quarter mile under the standard automatic driver.", "Check clutch capacity, tall/heavy wheels, grip and final-drive gearing."));
        }
    }
}
