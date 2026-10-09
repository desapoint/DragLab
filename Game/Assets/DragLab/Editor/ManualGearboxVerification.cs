using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class ManualGearboxVerification
    {
        public static void RunStandalone()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Debug.Log("Manual gearbox checks: " + Run(root));
        }
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var catalog = MechanicsTransactions.Catalog();
            var legacy = JsonUtility.FromJson<TransmissionSpec>("{\"type\":0,\"version\":0}");
            check(legacy.manualLayout == ManualGearboxLayout.HPattern, "Old transmission JSON defaults to H-pattern, with enum ordinals unchanged.");
            var malformed = new TransmissionSpec { manualLayout = (ManualGearboxLayout)99 }; check(malformed.Validate() != null, "Unknown manual selector is rejected.");
            malformed.manualLayout = ManualGearboxLayout.Sequential; malformed.sequentialShiftScale = 1.2f; check(malformed.Validate() != null, "Sequential multiplier outside the supported faster-selection range is rejected.");
            foreach (var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                var map = catalog.Recipe(v.spec.id); map[PartCategory.Gearbox] = v.spec.id + "/Gearbox/Manual";
                var slow = catalog.Resolve(v.spec.id, map); map[PartCategory.Gearbox] = v.spec.id + "/Gearbox/SequentialManual";
                var fast = catalog.Resolve(v.spec.id, map); string label = v.spec.id;
                var normal = TransmissionDynamics.Configuration(slow.spec, slow.tune); var sequential = TransmissionDynamics.Configuration(fast.spec, fast.tune);
                double normalSeconds = TransmissionDynamics.ShiftSeconds(normal, slow.spec, slow.tune), fastSeconds = TransmissionDynamics.ShiftSeconds(sequential, fast.spec, fast.tune);
                check(slow.Drivable && fast.Drivable && sequential.type == TransmissionType.Manual && sequential.manualLayout == ManualGearboxLayout.Sequential, label + ": installed sequential hardware remains a manual gearbox.");
                check(Math.Abs(fastSeconds / normalSeconds - .45) < 1e-6, label + ": equivalent gearboxes select in " + normalSeconds.ToString("F3") + " s H-pattern versus " + fastSeconds.ToString("F3") + " s sequential.");
                check(slow.spec.gears.SequenceEqual(fast.spec.gears) && slow.tune.PowerMultiplier == fast.tune.PowerMultiplier && normal.ProtectsDownshift == sequential.ProtectsDownshift, label + ": selector does not invent different ratios, power or ECU interlocks.");
                check(TransmissionDynamics.Retention(sequential) == 0 && sequential.Validate() == null, label + ": manual torque interruption has no DCT overlap or converter retention.");
                var altered = sequential.Copy(); altered.sequentialShiftScale = .25f;
                check(Math.Abs(TransmissionDynamics.ShiftSeconds(altered, fast.spec, fast.tune) / fastSeconds - .25 / .45) < 1e-6, label + ": authored selector speed changes the shared shift duration.");
                var acoustic = new EngineTestBench(fast.spec, fast.tune); acoustic.state.rpm = fast.spec.redlineRpm - 500; acoustic.SetGear(1); acoustic.SetGear(2);
                check(Math.Abs(acoustic.state.shiftRemaining - fastSeconds) < 1e-8, label + ": sound bench uses the same selector duration.");
                foreach (ShiftMode mode in Enum.GetValues(typeof(ShiftMode))) foreach (ManualGearboxLayout layout in Enum.GetValues(typeof(ManualGearboxLayout))) {
                    var b = layout == ManualGearboxLayout.Sequential ? fast : slow;
                    var driver = new DriverSettings { shifting = mode, throttleAssist = false, tractionAssist = false, launch = LaunchMode.Assisted, wearMode = WearMode.None };
                    var car = new CarSimulation(b.spec, b.tune, driver); car.Launch(0); RoadFixture.Seed(car, 12, 1, true);
                    double duration = TransmissionDynamics.ShiftSeconds(car.transmission, car.spec, car.tune);
                    var held = new DriverInput { throttle = 1, clutch = mode == ShiftMode.ManualClutch ? 1 : 0 };
                    check(car.Shift(1, held), label + " / " + mode + " / " + layout + ": ordinary adjacent upshift accepted.");
                    int ticks = 0; bool rotating = true, heldGas = true, disconnected = true;
                    while (car.state.shiftRemaining > 0 && ticks < 120) {
                        car.Step(DragMath.FixedStep, held); ticks++;
                        rotating &= car.state.rpm > 0 && car.state.flywheelEnergyJ > 0;
                        heldGas &= mode == ShiftMode.Automatic || car.state.throttle == 1;
                        var fitted=car.mechanics.Part(PartCategory.Clutch);bool wet=fitted!=null&&car.mechanics.Definition(fitted).clutchEnvironment==ClutchEnvironment.Wet;
                        disconnected &= Math.Abs(car.state.transmittedTorqueNm) <= (wet?5.000001:0);
                    }
                    check(ticks == (int)Math.Ceiling(duration / DragMath.FixedStep - 1e-8) && car.state.gear == 2, label + " / " + mode + " / " + layout + ": physical handover lasts exactly its authored fixed ticks.");
                    check(rotating && heldGas && disconnected, label + " / " + mode + " / " + layout + ": flywheel keeps rotating, player gas remains held and manual shaft carries only bounded wet-oil residual drag (dry = zero).");
                }
                var strict = new CarSimulation(fast.spec, fast.tune, new DriverSettings { shifting = ShiftMode.ManualClutch, launch = LaunchMode.Assisted, clutchAssist = false, tractionAssist = false, wearMode = WearMode.Full });
                strict.Launch(0); RoadFixture.Seed(strict, 12, 1, true);
                check(!strict.Shift(1, new DriverInput { throttle = 1 }) && strict.state.gear == 1, label + ": full-manual sequential shift still needs the single clutch.");
                strict.state.gear = 2;
                double moneySpeed = strict.engine.limitRpm * 1.3 / FlywheelDynamics.RpmPerRadian * strict.wheels.radius / (strict.spec.gears[0] * strict.tune.finalDrive);
                RoadFixture.Seed(strict, moneySpeed, 2, true);
                check(strict.Shift(-1, new DriverInput { clutch = 1 }), label + ": unprotected sequential manual permits a dangerous downshift.");
                for (int i = 0; i < 240; i++) strict.Step(DragMath.FixedStep, new DriverInput());
                check(strict.state.engineDamage > 0 || strict.state.engineFailed, label + ": money-shift synchronization can damage the engine.");
                var protectedSpec = fast.spec.Copy(); protectedSpec.transmission.manualDownshiftInterlock = true;
                var guarded = new CarSimulation(protectedSpec, fast.tune, new DriverSettings { shifting = ShiftMode.Sequential }); guarded.Launch(0); RoadFixture.Seed(guarded, moneySpeed, 2, true);
                check(!guarded.Shift(-1, new DriverInput()), label + ": explicit installed interlock blocks the same downshift.");
                var snap = new RunSnapshot { vehicle = fast.spec.Copy(), tune = fast.tune.Copy(), driving = new DriverSettings { shifting = ShiftMode.Sequential, throttleAssist = false, tractionAssist = false, wearMode = WearMode.Partial } };
                var first = new PhysicalDrivingSession(snap); for (int i = 0; i < 480; i++) first.Step(new TickInput { tick = first.Tick, input = new DriverInput { throttle = .7, shiftUp = i == 200 || i == 350 }, neutral = i < 50, staging = i < 50 });
                var captured = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(first.snapshot)); var replay = new PhysicalDrivingSession(captured);
                foreach (var input in captured.inputs) replay.Step(input);
                check(first.Csv() == replay.Csv() && replay.car.transmission.manualLayout == ManualGearboxLayout.Sequential, label + ": captured sequential hardware and inputs replay exactly through shared physics.");
                var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); profile.cars.Clear(); profile.cars.Add(new OwnedCar { vehicleId = v.spec.id, tune = v.CreateStockTune() }); profile.selectedVehicleId = v.spec.id; MechanicsOwnership.Ensure(profile, catalog);
                var owned = profile.SelectedCar; string oldBox = owned.installed.slots.Find(s => s.slot == "Gearbox").partInstanceId;
                var item = new PartInstance { instanceId = "test/sequential/" + v.spec.id, definitionId = v.spec.id + "/Gearbox/SequentialManual" }; profile.parts.Add(item); MechanicsOwnership.Replace(profile, owned, item, catalog);
                var saved = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile)); MechanicsOwnership.Ensure(saved, catalog);
                check(MechanicsOwnership.Resolve(saved, saved.SelectedCar, catalog).spec.transmission.manualLayout == ManualGearboxLayout.Sequential && saved.parts.Find(p => p.instanceId == oldBox).location == PartLocation.Inventory, label + ": installed identity persists and original gearbox remains available for reuse.");
                MechanicsOwnership.Replace(saved, saved.SelectedCar, saved.parts.Find(p => p.instanceId == oldBox), catalog);
                check(MechanicsOwnership.Resolve(saved, saved.SelectedCar, catalog).spec.transmission.manualLayout == v.spec.transmission.manualLayout, label + ": reinstalling OEM removes the sequential selector benefit.");
                foreach (TransmissionType type in new[] { TransmissionType.DCT, TransmissionType.Automatic, TransmissionType.CVT }) {
                    var other = sequential.Copy(); other.type = type; double a = TransmissionDynamics.ShiftSeconds(other, fast.spec, fast.tune); other.manualLayout = ManualGearboxLayout.HPattern;
                    check(a == TransmissionDynamics.ShiftSeconds(other, fast.spec, fast.tune), label + ": manual layout does not alter " + type + " handovers.");
                }
            }
            string folder = Path.Combine(root, "reports/manual-gearboxes"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\nModel: " + MechanicsSchema.Model + "\n"); return count;
        }
    }
}
