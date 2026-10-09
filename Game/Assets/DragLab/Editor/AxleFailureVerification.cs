using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using DriveType = DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    public static class AxleFailureVerification
    {
        public static void RunStandalone() { Debug.Log("Axle/failure checks: " + Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../..")))); }
        static CarSimulation Car(DriveType drive = DriveType.AWD, AwdMode mode = AwdMode.FixedSplit, bool isolate = false)
        {
            return new CarSimulation(new VehicleSpec { drive = drive }, new Tune { awdConfigured = true, awd = new AwdSpec { mode = mode, isolateFailedAxle = isolate } }, new DriverSettings { throttleAssist = false, tractionAssist = false, wearMode = WearMode.Full });
        }
        static void Fail(CarSimulation car, PartCategory category)
        {
            var p = car.mechanics.parts.Find(v => v.slot == category.ToString()); p.condition.health = 0; p.condition.failed = true; p.condition.failureReason = "Verification failure"; car.mechanics.Prepare(car.state);
        }
        static void Pull(CarSimulation car, int ticks = 600)
        {
            for (int i = 0; i < 120; i++) car.Stage(DragMath.FixedStep, new DriverInput());
            car.Launch(0); for (int i = 0; i < ticks; i++) car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
        }
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder("Shared component failure effects / axle demand model / isolated fixtures.\n");
            Action<bool, string> check = (okay, label) => { log.AppendLine((okay ? "PASS: " : "FAIL: ") + label); Directory.CreateDirectory(Path.Combine(root, "reports/axle-failures")); File.WriteAllText(Path.Combine(root, "reports/axle-failures/verification.txt"), log.ToString()); if (!okay) throw new InvalidOperationException(label); count++; };
            var noBrakes = Car(); Fail(noBrakes, PartCategory.Brakes); Pull(noBrakes);
            check(noBrakes.state.speed > 10 && noBrakes.state.engineHorsepower > 0 && !noBrakes.state.engineFailed && !noBrakes.state.drivetrainFailed && noBrakes.state.engineDamage == 0, "Failed brakes retain engine combustion and propulsion during a shared full-throttle pull.");
            var fresh = Car(); var idleBrakes = fresh.mechanics.parts.Find(p => p.slot == "Brakes"); Pull(fresh);
            check(idleBrakes.condition.health == 1 && fresh.state.brakeFrictionPowerW == 0, "Unused healthy brakes accumulate no friction wear or heat during staging/acceleration.");
            RoadFixture.Seed(fresh, 30); RoadFixture.Seed(noBrakes, 30);
            for (int i = 0; i < 240; i++) { fresh.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, false); noBrakes.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, false); }
            check(fresh.state.speed < noBrakes.state.speed - 10 && noBrakes.state.brakeFrictionPowerW == 0 && noBrakes.state.brakeCapacityFactor == 0, "Brake failure removes actual friction deceleration; rolling/aero coast remain without phantom braking heat.");
            var worn = Car(); worn.mechanics.parts.Find(p => p.slot == "Brakes").condition.health = .5f; worn.mechanics.Prepare(worn.state);
            check(worn.state.brakeCapacityFactor > 0 && worn.state.brakeCapacityFactor < 1 && !worn.state.engineFailed, "Worn brakes progressively lose capacity rather than breaking engine internals.");
            var hot = Car(); hot.mechanics.parts.Find(p => p.slot == "Brakes").condition.temperatureC = 500; hot.mechanics.Prepare(hot.state);
            check(hot.state.brakeCapacityFactor < 1 && !hot.state.engineFailed, "Hot brakes fade without an engine failure.");
            foreach (var category in new[] { PartCategory.Driveshaft, PartCategory.Differential, PartCategory.Gearbox, PartCategory.Clutch, PartCategory.TransferCase })
            {
                var broken = Car(); Fail(broken, category); broken.Launch(0); RoadFixture.Seed(broken, 25);
                double peakHp = 0; for (int i = 0; i < 180; i++) { broken.Step(DragMath.FixedStep, new DriverInput { throttle = .5 }); peakHp = Math.Max(peakHp, broken.state.engineHorsepower); }
                check(broken.state.drivetrainFailed && !broken.state.engineFailed && peakHp > 0 && broken.state.rpm > broken.spec.idleRpm && broken.state.transmittedTorqueNm == 0 && broken.state.frontDriveForceN == 0 && broken.state.rearDriveForceN == 0 && broken.state.speed > 20, "Common " + category + " failure disconnects drive, keeps engine running and preserves road momentum. RPM " + broken.state.rpm.ToString("F0") + ", peak HP " + peakHp.ToString("F0") + ", engine failed " + broken.state.engineFailed);
                for (int i = 0; i < 600; i++) broken.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, false);
                check(broken.state.speed == 0 && !broken.state.brakesFailed, "Healthy brakes stop a car with failed " + category + ".");
            }
            foreach (DriveType layout in new[] { DriveType.FWD, DriveType.RWD })
            {
                var broken = Car(layout); Fail(broken, layout == DriveType.FWD ? PartCategory.FrontAxle : PartCategory.RearAxle); Pull(broken, 180);
                check(broken.state.drivetrainFailed && !broken.state.engineFailed && broken.state.speed == 0 && broken.state.rpm > broken.spec.idleRpm, layout + " driven axle failure removes wheel drive, preserving engine free-revving.");
            }
            foreach (var category in new[] { PartCategory.FrontAxle, PartCategory.FrontDriveshaft, PartCategory.FrontDifferential, PartCategory.RearAxle, PartCategory.RearDriveshaft, PartCategory.RearDifferential })
            {
                bool front = category == PartCategory.FrontAxle || category == PartCategory.FrontDriveshaft || category == PartCategory.FrontDifferential;
                var broken = Car(isolate: true); Fail(broken, category); Pull(broken);
                check(!broken.state.drivetrainFailed && !broken.state.engineFailed && broken.state.speed > 10 && (front ? broken.state.frontDriveForceN == 0 && broken.state.rearDriveForceN > 0 : broken.state.rearDriveForceN == 0 && broken.state.frontDriveForceN > 0), "Isolating AWD retains healthy branch after " + category + " failure.");
                check(front ? broken.state.frontTorqueShare == 0 : broken.state.frontTorqueShare == 1, "Disconnected " + category + " receives no allocated torque after isolation.");
            }
            var open = Car(mode: AwdMode.OpenCenter); Fail(open, PartCategory.FrontAxle); Pull(open, 180);
            check(open.state.openCenterFreeBranch && !open.state.engineFailed && open.state.speed < 1 && open.state.frontShaftRpm > 1000, "Unisolated open center spins disconnected shaft, with negligible useful propulsion.");
            foreach (bool frontBroken in new[] { true, false }) foreach (TransmissionType type in Enum.GetValues(typeof(TransmissionType)))
            {
                var spec = new VehicleSpec { drive = DriveType.AWD, transmission = new TransmissionSpec { type = type }, awd = new AwdSpec { mode = AwdMode.OpenCenter } };
                var free = new CarSimulation(spec, new Tune(), new DriverSettings { throttleAssist = false }); Fail(free, frontBroken ? PartCategory.FrontDriveshaft : PartCategory.RearDriveshaft);
                for (int tick = 0; tick < 120; tick++) free.Stage(DragMath.FixedStep, new DriverInput()); free.Launch(0);
                double freePower = 0, healthyPower = 0;
                for (int tick = 0; tick < 600; tick++) { free.Step(DragMath.FixedStep, new DriverInput { throttle = 1 }); freePower += Math.Max(0, frontBroken ? free.state.frontShaftPowerW : free.state.rearShaftPowerW) * DragMath.FixedStep; healthyPower += Math.Max(0, frontBroken ? free.state.rearShaftPowerW : free.state.frontShaftPowerW) * DragMath.FixedStep; }
                double freeRpm = frontBroken ? free.state.frontShaftRpm : free.state.rearShaftRpm;
                check(freeRpm > 1000 && !free.state.engineFailed && free.state.speed < 2 && freePower > 0 && freePower > Math.Max(1, healthyPower) * 5, type + " open center routes most output work into free " + (frontBroken ? "front" : "rear") + " shaft: " + freePower.ToString("F0") + " J versus healthy " + healthyPower.ToString("F0") + " J / road " + free.state.speed.ToString("F2") + " m/s.");
                check(Math.Abs(free.state.frontShaftTorqueNm - free.state.rearShaftTorqueNm) < 1e-9, "50/50 open center preserves equal shaft torque while power differs with shaft speed: " + type);
                for (int tick = 0; tick < 240; tick++) free.IndependentStep(DragMath.FixedStep, new DriverInput(), true, false);
                check((frontBroken ? free.state.frontShaftRpm : free.state.rearShaftRpm) < freeRpm && !free.state.engineFailed, "Free shaft retains inertia and decays under friction in neutral: " + type);
            }
            var locked = Car(mode: AwdMode.LockedCenter); Fail(locked, PartCategory.RearAxle); Pull(locked);
            check(!locked.state.drivetrainFailed && locked.state.speed > 10 && locked.state.frontTorqueShare == 1, "Locked center retains surviving front drive without optional controller isolation.");
            var fixedSplit = Car(); Fail(fixedSplit, PartCategory.FrontAxle); Pull(fixedSplit);
            check(!fixedSplit.state.drivetrainFailed && fixedSplit.state.speed > 5 && fixedSplit.state.frontTorqueShare == .5, "Fixed independent coupling preserves only the assigned healthy share when isolation is absent.");
            foreach (bool frontPrimary in new[] { true, false })
            {
                var demand = Car(mode: AwdMode.OnDemand); demand.awd.primaryFront = frontPrimary;
                demand.state.frontSlip = demand.state.rearSlip = .5;
                AxleDriveDynamics.RoadForce(demand, 12000, 4000, 4000, DragMath.FixedStep);
                check(demand.state.awdSecondaryShare > 0 && demand.state.awdSecondaryShare < .05, "On-demand " + (frontPrimary ? "front" : "rear") + " primary engages gradually after slip rather than instant full coupling.");
                for (int i = 0; i < 120; i++) AxleDriveDynamics.RoadForce(demand, 20000, 3000, 3000, DragMath.FixedStep);
                check(demand.state.awdSecondaryShare > .35 && demand.state.awdSecondaryShare <= .5, "On-demand secondary engagement respects its maximum share and response time.");
                var spec = new VehicleSpec { drive = DriveType.AWD, awd = new AwdSpec { mode = AwdMode.OnDemand, primaryFront = frontPrimary } };
                var uncoupled = new CarSimulation(spec, new Tune(), new DriverSettings { throttleAssist = false }); Fail(uncoupled, PartCategory.TransferCase); Pull(uncoupled);
                check(!uncoupled.state.drivetrainFailed && uncoupled.state.speed > 10 && (frontPrimary ? !uncoupled.state.rearDriveAvailable : !uncoupled.state.frontDriveAvailable), "Failed on-demand coupling leaves the direct primary drive functional.");
                var primaryBroken = new CarSimulation(spec, new Tune(), new DriverSettings { throttleAssist = false }); Fail(primaryBroken, frontPrimary ? PartCategory.FrontAxle : PartCategory.RearAxle);
                check(primaryBroken.state.drivetrainFailed && !primaryBroken.state.engineFailed, "Primary-branch break requires isolation-capable hardware for on-demand fallback.");
            }
            var fifty = Car(); double force = AxleDriveDynamics.RoadForce(fifty, 6000, 10000, 10000, DragMath.FixedStep);
            check(force == 6000 && fifty.state.frontDriveForceN == 3000 && fifty.state.rearDriveForceN == 3000, "50/50 divides demand equally and conserves total force below grip limits.");
            var biased = Car(); biased.awd.frontBias = .25f; force = AxleDriveDynamics.RoadForce(biased, 6000, 10000, 10000, DragMath.FixedStep);
            check(force == 6000 && biased.state.frontDriveForceN == 1500 && biased.state.rearDriveForceN == 4500, "25/75 actually changes axle force while conserving demand.");
            force = AxleDriveDynamics.RoadForce(biased, 20000, 1000, 9000, DragMath.FixedStep);
            check(force <= 10000 && biased.state.frontDriveForceN <= 1000 && biased.state.rearDriveForceN <= 9000 && biased.state.frontSlip > 0 && biased.state.rearSlip > 0, "Each axle is grip-limited separately, including independent demand slip and weight-transfer capacity.");
            var active = Car(mode: AwdMode.ActiveBias); active.state.frontSlip = 1;
            for (int i = 0; i < 240; i++) { active.state.frontSlip = 1; active.state.rearSlip = 0; AxleDriveDynamics.RoadForce(active, 0, 5000, 5000, DragMath.FixedStep); }
            check(active.state.frontTorqueShare >= active.awd.minimumFront && active.state.frontTorqueShare < .25, "Active bias transfers requested share away from slipping front axle within authored bounds.");
            force = AxleDriveDynamics.RoadForce(fifty, -2000, 10000, 10000, DragMath.FixedStep);
            check(force == -2000, "Engine braking stays signed through axle distribution; it cannot turn into propulsion.");
            var lockedGrip = Car(mode: AwdMode.LockedCenter); AxleDriveDynamics.RoadForce(lockedGrip, 20000, 1000, 9000, DragMath.FixedStep);
            check(lockedGrip.state.frontSlip == lockedGrip.state.rearSlip && lockedGrip.state.frontTorqueShare == .1, "Locked center keeps axle slip estimates synchronized and distributes resisting torque against unequal grip.");
            foreach (var type in new[] { TransmissionType.Automatic, TransmissionType.CVT })
            {
                var spec = new VehicleSpec { transmission = new TransmissionSpec { type = type } }; var converter = new CarSimulation(spec, new Tune(), new DriverSettings()); Fail(converter, PartCategory.Clutch); Pull(converter);
                check(!converter.state.drivetrainFailed && converter.state.speed > 5, type + " propulsion does not depend on the separate manual friction clutch.");
            }
            foreach (var category in new[] { PartCategory.FuelSystem, PartCategory.Ignition })
            {
                var car = Car(); Fail(car, category); car.IndependentStep(DragMath.FixedStep, new DriverInput(), true, false);
                check(car.state.stalled && !car.state.engineFailed && car.state.engineDamage == 0 && car.state.engineHorsepower == 0, category + " failure stops combustion without destroying rotating engine internals.");
            }
            var cooling = Car(); Fail(cooling, PartCategory.Cooling);
            check(!cooling.state.engineFailed && cooling.state.engineCoolingFactor < 1, "Cooling failure reduces heat rejection instead of instantly destroying the engine.");
            var catalog = MechanicsTransactions.Catalog(); var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(profile, catalog);
            var pickupSpec = catalog.Spec("utility-pickup"); var pickupTune = new Tune { awdConfigured = true, awd = new AwdSpec { mode = AwdMode.OpenCenter } };
            var ungoverned = new CarSimulation(pickupSpec, pickupTune, new DriverSettings { throttleAssist = false }); Fail(ungoverned, PartCategory.FrontDriveshaft); Pull(ungoverned);
            check(ungoverned.engine.limiter == LimiterStyle.None && ungoverned.state.engineDamage > 0, "Ungoverned stock pickup can overrev from sustained gas against an open-center free shaft; this is RPM abuse, not a blanket axle-failure engine flag.");
            var protectedRecipe = catalog.Recipe(pickupSpec.id); protectedRecipe[PartCategory.Controls] = catalog.Id(pickupSpec.id, PartCategory.Controls, 1); protectedRecipe[PartCategory.RevLimiter] = catalog.Id(pickupSpec.id, PartCategory.RevLimiter, 1);
            pickupTune.limiter = LimiterStyle.Hard; pickupTune.revLimitRpm = pickupSpec.redlineRpm - 200;
            var protectedBuild = catalog.Resolve(pickupSpec.id, protectedRecipe, pickupTune); var protectedPickup = new CarSimulation(protectedBuild.spec, protectedBuild.tune, new DriverSettings { throttleAssist = false }); Fail(protectedPickup, PartCategory.FrontDriveshaft); Pull(protectedPickup, 1200);
            check(protectedPickup.engine.limiter == LimiterStyle.Hard && !protectedPickup.state.engineFailed && protectedPickup.state.engineDamage == 0 && protectedPickup.state.frontShaftRpm > 1000 && protectedPickup.state.speed < 2, "An installed limiter protects the actual pickup during sustained free-shaft operation without restoring open-center road propulsion.");
            var owned = profile.SelectedCar; var brakePart = profile.parts.Find(p => p.slot == "Brakes" && p.ownerId == owned.instanceId); brakePart.condition.failed = true; brakePart.condition.health = 0; brakePart.condition.failureReason = "Fixture brake failure";
            check(MechanicsOwnership.Resolve(profile, owned, catalog).Drivable, "Garage permits an otherwise working engine with failed brakes; failures no longer share a global build gate.");
            var inspected = CarInspection.Calculate(InspectionBuild.Capture(profile, owned));
            check(inspected.brakeG == 0 && inspected.warnings.Any(w => w.title == "Braking unavailable") && inspected.completed, "Inspection reports zero stopping capacity with a warning while the propulsion probe still completes.");
            var critical = profile.parts.Find(p => p.slot == "Crankshaft" && p.ownerId == owned.engineAssemblyId); critical.condition.failed = true; critical.condition.health = 0; critical.condition.failureReason = "Fixture crank failure";
            check(!MechanicsOwnership.Resolve(profile, owned, catalog).Drivable, "Failed crankshaft still blocks a running-engine launch.");
            var pickup = new OwnedCar { vehicleId = "utility-pickup" }; profile.cars.Add(pickup); MechanicsOwnership.Ensure(profile, catalog);
            var existing = profile.parts.Where(p => p.ownerId == pickup.instanceId || p.ownerId == pickup.engineAssemblyId).ToArray();
            var oldIds = existing.Where(p => !MechanicalCatalog.DriveBranch((PartCategory)Enum.Parse(typeof(PartCategory), p.slot))).Select(p => p.instanceId).ToArray();
            foreach (var p in existing.Where(p => MechanicalCatalog.DriveBranch((PartCategory)Enum.Parse(typeof(PartCategory), p.slot)))) { pickup.installed.slots.RemoveAll(s => s.partInstanceId == p.instanceId); profile.parts.Remove(p); }
            profile.drivelineVersion = 1; MechanicsOwnership.Ensure(profile, catalog);
            check(oldIds.All(id => profile.parts.Any(p => p.instanceId == id)) && pickup.installed.slots.Count(s => MechanicalCatalog.DriveBranch((PartCategory)Enum.Parse(typeof(PartCategory), s.slot))) == 7, "Legacy pickup migration adds AWD branch parts once while preserving existing identities and damage.");
            MechanicsOwnership.Remove(profile, pickup, "FrontAxle"); int partCount = profile.parts.Count; MechanicsOwnership.Ensure(profile, catalog);
            check(profile.parts.Count == partCount && !pickup.installed.slots.Any(p => p.slot == "FrontAxle"), "Later intentional removal stays removed; migration never respawns a missing axle.");
            var draft = new TuneDraft(pickup, profile.revision); draft.values.awdConfigured = true; draft.values.awd = new AwdSpec { frontBias = .3f }; draft.ApplyTo(profile);
            draft.values.awd.frontBias = .9f;
            check(pickup.tune.awd.frontBias == .3f, "Applied AWD settings are copied, not linked to discarded tune drafts.");
            pickup.SavePreset(0); pickup.tune.awd.frontBias = .8f; pickup.LoadPreset(0);
            check(pickup.tune.awd.frontBias == .3f, "Tune presets retain isolated AWD calibration.");
            var invalid = new TuneDraft(pickup, profile.revision); invalid.values.awd.frontBias = float.NaN; bool rejected = false; try { invalid.ApplyTo(profile); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Invalid nested AWD settings are rejected before tune publication.");
            var replayCar = Car(mode: AwdMode.OnDemand, isolate: true); Fail(replayCar, PartCategory.FrontAxle);
            var start = new RunSnapshot { vehicle = replayCar.spec.Copy(), tune = replayCar.tune.Copy(), driving = replayCar.driver.Copy(), components = replayCar.mechanics.parts.Select(p => p.Copy()).ToList() };
            var session = new PhysicalDrivingSession(start); foreach (var input in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, 19)) session.Step(input);
            var repeated = PhysicalDrivingSession.Replay(session.snapshot);
            check(JsonUtility.ToJson(session.car.CaptureDynamics()) == JsonUtility.ToJson(repeated.car.CaptureDynamics()) && session.Csv() == repeated.Csv(), "On-demand surviving-axle run replays exact dynamics and diagnostic traces, including coupling state.");
            check(session.Csv().Contains("front_slip") && session.Csv().Contains("fl_wheel_rpm") && session.samples.Any(s => !s.frontAvailable && s.rearForceN > 0), "Physical CSV exposes branch failures, torque shares, forces, measured wheel slip and brake capacity.");
            var continuation = session.ContinueRecording(); continuation.Step(new TickInput { tick = 0, neutral = false, input = new DriverInput { throttle = .5 } });
            check(continuation.Csv() == PhysicalDrivingSession.Replay(continuation.snapshot).Csv(), "Recording continuation preserves partial failure and axle/coupler state exactly.");
            var old = PhysicalDrivingSession.Copy(start); old.model = "draglab-shared-stress-v3";
            check(PhysicalDrivingSession.Validate(old).Contains("Different physics model"), "Prior physics recordings explicitly require rerecording rather than silently changing replay behavior.");
            var freeStart = PhysicalDrivingSession.Copy(start); freeStart.tune.awd = new AwdSpec { mode = AwdMode.OpenCenter }; freeStart.tune.awdConfigured = true;
            var freeSession = new PhysicalDrivingSession(freeStart);
            for (int tick = 0; tick < 600; tick++) freeSession.Step(new TickInput { tick = tick, staging = tick < 120, neutral = tick < 120, input = new DriverInput { throttle = tick < 120 ? 0 : 1 } });
            var freeReplay = PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(freeSession.snapshot)));
            check(freeReplay.Csv() == freeSession.Csv() && freeSession.car.state.frontShaftRpm > 1000, "Free-shaft speed, torque, power and damaged branch replay exactly through JSON serialization.");
            var freeContinuation = freeSession.ContinueRecording(); freeContinuation.Step(new TickInput { tick = 0, input = new DriverInput { throttle = 1 } });
            check(freeContinuation.Csv() == PhysicalDrivingSession.Replay(freeContinuation.snapshot).Csv(), "Mid-spin checkpoint preserves free-shaft inertia state and restored component bindings.");
            var direct = new CarSimulation(freeStart.vehicle, freeStart.tune, freeStart.driving, freeStart.components); direct.RestoreDynamics(new PhysicalDrivingSession(freeStart).car.CaptureDynamics());
            foreach (var tick in freeSession.snapshot.inputs) direct.IndependentStep(DragMath.FixedStep, tick.input, tick.neutral, tick.staging);
            check(JsonUtility.ToJson(direct.CaptureDynamics()) == JsonUtility.ToJson(freeSession.car.CaptureDynamics()), "Physical bench recording does not alter free-shaft dynamics compared with direct shared race-model integration.");
            File.WriteAllText(Path.Combine(root, "reports/axle-failures/free-shaft-trace.csv"), freeSession.Csv());
            var weakSpec = new VehicleSpec { drive = DriveType.AWD }; var localCatalog = new MechanicalCatalog(new[] { weakSpec }); var build = localCatalog.Resolve(weakSpec.id, localCatalog.Recipe(weakSpec.id));
            var weakAxle = build.spec.fittedParts.First(p => p.category == PartCategory.RearAxle); weakAxle.torqueLimitNm = 1;
            build.tune.awdConfigured = true; build.tune.awd = new AwdSpec { isolateFailedAxle = true }; var overloaded = new CarSimulation(build.spec, build.tune, new DriverSettings());
            overloaded.state.gear = 1; overloaded.state.transmittedTorqueNm = 300; overloaded.state.engineTorqueNm = 300; overloaded.mechanics.Tick(overloaded, DragMath.FixedStep, 0, 0);
            check(overloaded.mechanics.parts.Find(p => p.slot == "RearAxle").condition.failed && !overloaded.state.drivetrainFailed && !overloaded.state.engineFailed && overloaded.state.frontDriveAvailable, "Actual torque overload can break an axle without destroying engine or surviving AWD branch.");
            log.AppendLine("Checks: " + count); File.WriteAllText(Path.Combine(root, "reports/axle-failures/verification.txt"), log.ToString()); File.WriteAllText(Path.Combine(root, "reports/axle-failures/trace.csv"), session.Csv());
            return count;
        }
    }
}
