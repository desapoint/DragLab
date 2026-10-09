using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using DragLab.Domain;
using DragLab.Data;

namespace DragLab.Editor
{
    public static class PowertrainVerification
    {
        static void Check(bool ok, string message, ref int count) { if (!ok) throw new InvalidOperationException("POWERTRAIN CHECK FAILED: " + message); count++; }
        static CarSimulation Stage(VehicleSpec spec, Tune tune, double seconds = 3)
        {
            var car = new CarSimulation(spec, tune, new DriverSettings { launch = LaunchMode.Manual, throttleAssist = false });
            for (int i = 0; i < seconds * 120; i++) car.Stage(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 });
            return car;
        }
        static VehicleSpec Clone(VehicleSpec spec) { return JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(spec)); }
        public static int Run(string root)
        {
            int count = 0; var report = new StringBuilder("Fixed 120 Hz launch, limiter, wheel inertia, transmission and fueling checks. Fictional calibration.\n");
            var csv = new StringBuilder("mode,rpm_mean,rpm_range,cut_transitions,boost_bar,pop_count,damage\n");
            var spec = new VehicleSpec();
            var turbo = new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Turbo, controlLevel = 1, twoStep = EquipmentSetting.On, launchPreload = EquipmentSetting.On };
            spec.engineBehavior.antiLagMaxSeconds = 6;
            var unloaded = Stage(spec, new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Turbo });
            var twoStepOnly = turbo.Copy(); twoStepOnly.launchPreload = EquipmentSetting.Off;
            var plain = Stage(spec, twoStepOnly); var charged = Stage(spec, turbo);
            Check(charged.state.boostBar > plain.state.boostBar + .1, "Launch retard/preload builds boost beyond a two-step alone.", ref count);
            Check(charged.state.distance == 0 && charged.state.gear == 0 && charged.state.transmittedTorqueNm == 0 && charged.state.brake == 1, "Boost preload does not creep or engage staging wheels.", ref count);
            Check(plain.state.rpm < 3500 && unloaded.state.rpm > 6000, "Two-step uses launch RPM separately from the main limiter.", ref count);
            report.AppendLine("Launch boost: two-step alone " + plain.state.boostBar.ToString("F3") + " bar; ignition-retard preload " + charged.state.boostBar.ToString("F3") + " bar.");
            double heldBoost = charged.state.boostBar;
            charged.Launch(.18); charged.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Check(!charged.state.launchControlActive && charged.state.boostBar > heldBoost * .7, "Launch releases two-step without resetting compressor charge.", ref count);
            foreach (var style in new[] { LimiterStyle.Hard, LimiterStyle.Soft })
                foreach (var cut in new[] { CutMethod.Fuel, CutMethod.Ignition })
                {
                    var tune = twoStepOnly.Copy(); tune.twoStepStyle = style; tune.twoStepCut = cut;
                    var car = new CarSimulation(spec, tune, new DriverSettings { launch = LaunchMode.Manual });
                    double sum = 0, lo = double.MaxValue, hi = 0; int transitions = 0; bool prior = false;
                    for (int i = 0; i < 480; i++)
                    {
                        car.Stage(DragMath.FixedStep, new DriverInput { throttle = 1 });
                        if (i >= 240) { sum += car.state.rpm; lo = Math.Min(lo, car.state.rpm); hi = Math.Max(hi, car.state.rpm); if (car.state.limiter != prior) transitions++; }
                        prior = car.state.limiter;
                    }
                    Check(hi <= tune.launchRpm + 120 && lo > tune.launchRpm - 700, "Two-step holds its authored window: " + style + "/" + cut, ref count);
                    if (style == LimiterStyle.Hard) Check(transitions > 2 && hi - lo > 200, "Hard two-step bounces with repeated cuts.", ref count);
                    else Check(transitions < 3 && hi - lo < 60, "Soft two-step smoothly reduces combustion near its target.", ref count);
                    if (cut == CutMethod.Ignition && style == LimiterStyle.Hard) Check(car.state.exhaustPopCount > transitions * 3, "Ignition two-step has rapid exhaust events distinct from slow idle/limiter bounce.", ref count);
                    if (cut == CutMethod.Fuel) Check(car.state.exhaustPopCount == 0, "Fuel cut on modern injection does not invent ignition-cut pops.", ref count);
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0}-{1},{2:F2},{3:F2},{4},{5:F4},{6},{7:F5}\n", style, cut, sum / 240, hi - lo, transitions, car.state.boostBar, car.state.exhaustPopCount, car.state.engineDamage);
                    EngineSoundAudition.WriteWav(Path.Combine(root, "reports/audio-previews/two-step-" + style.ToString().ToLowerInvariant() + "-" + cut.ToString().ToLowerInvariant() + ".wav"), LaunchAudition(spec, tune));
                }
            var fuelPreload = turbo.Copy(); fuelPreload.twoStepCut = CutMethod.Fuel;
            var fuelCharged = Stage(spec, fuelPreload);
            Check(fuelCharged.state.boostBar < Stage(spec, turbo).state.boostBar, "Fuel cuts reduce exhaust preload energy relative to ignition retard.", ref count);
            var off = turbo.Copy(); off.twoStep = EquipmentSetting.Off;
            Check(!Stage(spec, off).state.antiLagActive, "Launch preload requires active two-step.", ref count);
            var unowned = turbo.Copy(); unowned.controlLevel = 0;
            Check(!Stage(spec, unowned).state.launchControlActive, "Unpurchased control hardware cannot enable aftermarket launch functions.", ref count);
            var factory = Clone(spec); factory.engineBehavior.twoStep = factory.engineBehavior.launchPreload = true;
            Check(Stage(factory, new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Turbo }).state.boostBar > .1, "Factory launch equipment works without purchase.", ref count);
            var noTurbo = Stage(spec, new Tune { controlLevel = 1, twoStep = EquipmentSetting.On, launchPreload = EquipmentSetting.On });
            Check(!noTurbo.state.antiLagActive && noTurbo.state.boostBar == 0, "NA two-step cannot invent compressor boost or anti-lag load.", ref count);
            var shortSpec = Clone(spec); shortSpec.engineBehavior.antiLagMaxSeconds = .3f;
            var timed = Stage(shortSpec, turbo);
            Check(!timed.state.antiLagActive && timed.state.antiLagTime >= shortSpec.engineBehavior.antiLagMaxSeconds && timed.state.engineDamage > 0, "Anti-lag duration lockout applies while held.", ref count);
            var anti = turbo.Copy(); anti.antiLag = EquipmentSetting.On;
            var withAls = Stage(spec, anti); var withoutAls = Stage(spec, turbo);
            withAls.Launch(.18); withoutAls.Launch(.18);
            withAls.state.rpm = withoutAls.state.rpm = 5000;
            withAls.state.turboSpool = withoutAls.state.turboSpool = 1;
            for (int i = 0; i < 60; i++)
            {
                withAls.Step(DragMath.FixedStep, new DriverInput()); withoutAls.Step(DragMath.FixedStep, new DriverInput());
            }
            Check(withAls.state.turboSpool > withoutAls.state.turboSpool && withAls.state.engineTorqueNm == 0, "Lift anti-lag preserves rotor energy without free crankshaft power.", ref count);
            Check(withAls.state.exhaustHeat > withoutAls.state.exhaustHeat && withAls.state.engineDamage > 0, "Exhaust energy has heat and wear costs.", ref count);
            var none = new Tune { controlLevel = 1, limiter = LimiterStyle.None };
            var broken = Stage(spec, none, 8);
            Check(broken.state.engineFailed && broken.state.engineDamage >= 1 && broken.state.engineTorqueNm == 0, "Unrestricted free revving overrevs and fails an engine.", ref count);
            Check(!Stage(spec, new Tune(), 8).state.engineFailed, "Ordinary protected limiter survives a held pedal.", ref count);
            var rpmTune = new Tune { controlLevel = 1, limiter = LimiterStyle.Hard, revLimitRpm = 4500 };
            Check(Stage(spec, rpmTune).state.rpm < 4700, "Main limiter setpoint changes independently of redline.", ref count);
            foreach (var style in new[] { LimiterStyle.Hard, LimiterStyle.Soft })
            {
                var protectedTune = new Tune { controlLevel = 1, limiter = style, revLimitRpm = 3000, launchRpm = 5000 };
                var protectedCar = new CarSimulation(spec, protectedTune, new DriverSettings());
                protectedCar.Stage(DragMath.FixedStep, new DriverInput());
                Check(protectedCar.state.rpm < 3000 && protectedTune.launchRpm == 5000 && protectedCar.ShiftPoint(1) < 3000,
                    "Protected assisted targets respect lowered limits without mutating garage settings: " + style, ref count);
                protectedCar.Launch(.18);
                for (int i = 0; i < 2400; i++) protectedCar.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                Check(protectedCar.state.gear > 1 && protectedCar.state.distance > 100 && !protectedCar.state.stalled,
                    "Automatic shifting progresses below a lowered main limiter: " + style, ref count);
            }
            var standard = new WheelProfile(spec, new Tune()); var oversize = new WheelProfile(spec, new Tune { wheelDiameter = 1.6f, wheelWidth = 1.6f, wheelMass = 3 });
            Check(oversize.radius > standard.radius && oversize.assemblyMass > standard.assemblyMass * 10 && oversize.effectiveMass > standard.effectiveMass, "Oversized wheels increase radius, static mass and rotational load.", ref count);
            Check(standard.effectiveMass > standard.vehicleMass && standard.totalInertia > 0, "Stock wheels also contribute rolling inertia.", ref count);
            Check(DragMath.GearSpeedKmh(spec, new Tune { wheelDiameter = 1.5f }, 1) > DragMath.GearSpeedKmh(spec, new Tune(), 1) * 1.49, "Wheel diameter changes effective gearing.", ref count);
            var normalTime = Benchmark.QuarterMile(spec, new Tune()).elapsed;
            var heavyTime = Benchmark.QuarterMile(spec, new Tune { wheelDiameter = 1.5f, wheelMass = 2 }).elapsed;
            Check(heavyTime > normalTime + .5, "Larger heavy wheels are not automatically beneficial in real passes.", ref count);
            report.AppendLine("Quarter-mile wheel comparison: stock " + normalTime.ToString("F3") + " s; large/heavy " + heavyTime.ToString("F3") + " s.");
            var manual = new CarSimulation(spec, new Tune { wheelDiameter = 1.6f, wheelMass = 3, wheelWidth = 1.6f }, new DriverSettings { shifting = ShiftMode.ManualClutch, launch = LaunchMode.Manual, throttleAssist = false, clutchReleaseSeconds = .008f });
            manual.Launch(.18);
            for (int i = 0; i < 120; i++) manual.Step(DragMath.FixedStep, new DriverInput());
            Check(manual.state.stalled && manual.state.engineTorqueNm == 0, "Low-rev clutch dump against high rolling inertia stalls.", ref count);
            manual.Step(DragMath.FixedStep, new DriverInput { launch = true, clutch = 1 });
            Check(!manual.state.stalled && manual.state.rpm > spec.idleRpm * .85 && manual.state.clutch > .98, "Clutch-in restart recovers a stall.", ref count);
            var assisted = new CarSimulation(spec, manual.tune, new DriverSettings()); assisted.Launch(.18);
            for (int i = 0; i < 300; i++) assisted.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Check(!assisted.state.stalled && assisted.state.distance > 0, "Assisted clutch can launch a heavy wheel build without forced stalling.", ref count);
            foreach (var hardware in new[] { TransmissionUpgrade.Manual, TransmissionUpgrade.DCT, TransmissionUpgrade.Automatic, TransmissionUpgrade.CVT })
            {
                var tune = new Tune { gearboxLevel = 1, transmissionUpgrade = hardware };
                var car = new CarSimulation(spec, tune, new DriverSettings()); car.Launch(.18);
                bool lockedDuringPull = false;
                for (int i = 0; i < 1200; i++) { car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 }); lockedDuringPull |= car.state.converterLocked; }
                Check(car.state.distance > 100 && !car.state.engineFailed && !car.state.stalled, "Transmission accelerates: " + hardware, ref count);
                var pass = Benchmark.QuarterMile(spec, tune);
                Check(pass.elapsed < 45, "Transmission completes quarter mile: " + hardware, ref count);
                if (hardware == TransmissionUpgrade.CVT)
                    Check(car.state.gear == 1 && !car.Shift(1, new DriverInput()) && car.state.cvtRatio < car.transmission.cvtMaxRatio, "CVT continuously varies ratio without fake discrete shifts.", ref count);
                if (hardware == TransmissionUpgrade.Automatic || hardware == TransmissionUpgrade.CVT)
                    Check(lockedDuringPull && car.state.clutch == 0, "Fluid converter locks during the pull without manual pedal (may release during a shift): " + hardware, ref count);
                report.AppendLine(hardware + " quarter " + pass.elapsed.ToString("F3") + " s / ratio " + car.state.cvtRatio.ToString("F3"));
            }
            var dct = new CarSimulation(spec, new Tune { gearboxLevel = 1, transmissionUpgrade = TransmissionUpgrade.DCT }, new DriverSettings { shifting = ShiftMode.ManualClutch });
            dct.Launch(.18); dct.state.rpm = 5000; RoadFixture.Seed(dct, 10);
            Check(dct.Shift(1, new DriverInput()) && dct.state.shiftRemaining < .1, "DCT accepts a shift without a clutch pedal and uses quick handover.", ref count);
            dct.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Check(dct.state.transmittedTorqueNm > 0 && !dct.state.transmissionTorqueCut, "DCT retains torque through clutch handover.", ref count);
            var mt = new CarSimulation(spec, new Tune(), new DriverSettings { shifting = ShiftMode.ManualClutch }); mt.Launch(.18);
            Check(!mt.Shift(1, new DriverInput()), "Manual hardware still requires a clutch pedal in manual-clutch mode.", ref count);
            var carb = Clone(spec); carb.engineBehavior.fueling = FuelSystem.Carburetor; carb.engineBehavior.camOverlap = .8f;
            carb.engineBehavior.idleControl = .1f; carb.engineBehavior.acceleratorPump = .35f; carb.engineBehavior.mixtureVariation = .5f; carb.engineBehavior.mechanicalClatter = .4f;
            var cold = new CarSimulation(carb, new Tune { coldStart = true }, new DriverSettings { launch = LaunchMode.Manual });
            var warm = new CarSimulation(carb, new Tune(), new DriverSettings { launch = LaunchMode.Manual });
            for (int i = 0; i < 120; i++) { cold.Stage(DragMath.FixedStep, new DriverInput()); warm.Stage(DragMath.FixedStep, new DriverInput()); }
            Check(cold.state.idleRoughness > warm.state.idleRoughness && cold.state.engineTemperature < .1, "Cold carb engine has greater idle roughness.", ref count);
            double coldRough = cold.state.idleRoughness;
            for (int i = 0; i < 6500; i++) cold.Stage(DragMath.FixedStep, new DriverInput());
            Check(cold.state.engineTemperature == 1 && cold.state.idleRoughness < coldRough, "Warmup gradually removes cold roughness.", ref count);
            cold.Stage(DragMath.FixedStep, new DriverInput { throttle = 1 });
            var injection = Stage(spec, new Tune(), .05);
            Check(cold.state.fuelHesitation > injection.state.fuelHesitation * 5 && cold.state.intakeGulp > injection.state.intakeGulp, "Poor carb accelerator-pump calibration causes transient bog and intake gulp.", ref count);
            double firstHesitation = cold.state.fuelHesitation;
            for (int i = 0; i < 120; i++) cold.Stage(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Check(cold.state.fuelHesitation < firstHesitation * .1, "Fueling hesitation recovers with steady throttle.", ref count);
            cold.state.rpm = 4000; cold.Stage(DragMath.FixedStep, new DriverInput());
            Check(cold.state.exhaustPopCount > 0, "Carb transition/overrun generates bounded physical pop events.", ref count);
            var sound = new EngineSoundController(carb, new Tune()); sound.Update(cold.state, true, .7, .01);
            cold.state.exhaustPopCount++; var frame = sound.Update(cold.state, true, .7, .01);
            Check(frame.popId > 0 && frame.intakeGulp >= 0 && frame.combustionFraction < 1, "Audio observes actual fueling and pop state.", ref count);
            sound.Update(cold.state, false, .7, .01); cold.state.exhaustPopCount++; var resume = sound.Update(cold.state, true, .7, .01);
            Check(resume.popId == frame.popId, "Pause rebases exhaust events without stale pops.", ref count);
            var synth = new EngineSoundSynth(new EngineSoundProfile(carb, new Tune())); var pcm = new float[44100];
            synth.Fill(pcm, 1, 44100, frame); double peak = 0;
            foreach (float value in pcm) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Nonfinite carb audio."); peak = Math.Max(peak, Math.Abs(value)); }
            Check(peak > .01 && peak < .95, "Carb resonances/pops/clatter remain audible and bounded.", ref count);
            synth.Fill(pcm, 1, 44100, new EngineSoundFrame(0, 0, 0, 0, 0, active: false));
            Check(Array.TrueForAll(pcm, value => value == 0), "Pause/mute clears carb pop tails.", ref count);
            EngineSoundAudition.WriteWav(Path.Combine(root, "reports/audio-previews/carb-warm.wav"), FuelingAudition(carb, false));
            EngineSoundAudition.WriteWav(Path.Combine(root, "reports/audio-previews/carb-cold.wav"), FuelingAudition(carb, true));
            EngineSoundAudition.WriteWav(Path.Combine(root, "reports/audio-previews/injection-idle.wav"), FuelingAudition(spec, false));
            var saved = JsonUtility.FromJson<Tune>(JsonUtility.ToJson(new Tune { controlLevel = 1, twoStep = EquipmentSetting.On, antiLag = EquipmentSetting.On, limiter = LimiterStyle.None, wheelMass = 2, gearboxLevel = 1, transmissionUpgrade = TransmissionUpgrade.CVT, coldStart = true }));
            saved.ClampTo(spec);
            Check(saved.controlLevel == 1 && saved.antiLag == EquipmentSetting.On && saved.limiter == LimiterStyle.None && saved.WheelMass == 2 && saved.transmissionUpgrade == TransmissionUpgrade.CVT && saved.coldStart, "New hardware/tune data round-trips through save JSON.", ref count);
            var legacy = JsonUtility.FromJson<Tune>("{\"engineLevel\":1}"); legacy.ClampTo(spec);
            Check(legacy.WheelDiameter == 1 && legacy.WheelMass == 1 && legacy.controlLevel == 0 && legacy.limiter == LimiterStyle.Factory, "Missing legacy fields preserve stock behavior.", ref count);
            var owned = new OwnedCar { tune = saved }; owned.SavePreset(0); owned.tune.controlLevel = 0; owned.tune.wheelMass = 3; owned.LoadPreset(0);
            Check(owned.tune.controlLevel == 0 && owned.tune.WheelMass == 3, "Tune slots never grant controls or restore wheel hardware.", ref count);
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks);
            Check(profile.Upgrade("controls") && profile.SelectedCar.tune.controlLevel == 1 && !profile.Upgrade("controls"), "Control package is purchased once with cash.", ref count);
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                Check(definition.spec.engineBehavior.version == 1 && definition.spec.Validate() == null, "Vehicle control data prepared: " + definition.spec.id, ref count);
            report.AppendLine("PASS: " + count + " powertrain checks. Damage is pass-local; wheel contact slip and component families remain simplified.");
            File.WriteAllText(Path.Combine(root, "reports/powertrain-verification.txt"), report.ToString());
            File.WriteAllText(Path.Combine(root, "reports/launch-limiter-comparison.csv"), csv.ToString());
            return count;
        }

        static float[] LaunchAudition(VehicleSpec spec, Tune tune)
        {
            var car = new CarSimulation(spec, tune, new DriverSettings { launch = LaunchMode.Manual });
            return Audition(car, false, true);
        }
        static float[] FuelingAudition(VehicleSpec spec, bool cold)
        {
            var car = new CarSimulation(spec, new Tune { coldStart = cold }, new DriverSettings { launch = LaunchMode.Manual });
            return Audition(car, true, false);
        }
        public static float[] NeutralBlips(VehicleSpec spec)
        {
            var car = new CarSimulation(spec, new Tune(), new DriverSettings { launch = LaunchMode.Manual });
            return Audition(car, true, true);
        }
        public static float[] V8TwoStep(VehicleSpec spec)
        {
            var tune = new Tune { controlLevel = 1, twoStep = EquipmentSetting.On, twoStepCut = CutMethod.Ignition, launchRpm = 3500 };
            var car = new CarSimulation(spec, tune, new DriverSettings { launch = LaunchMode.Manual });
            const int rate = 44100; var data = new float[rate * 8]; var block = new float[735];
            var controller = new EngineSoundController(car.spec, car.tune); var synth = new EngineSoundSynth(new EngineSoundProfile(car.spec, car.tune));
            for (int frame = 0; frame < 480; frame++)
            {
                double gas = frame >= 60 && frame < 420 ? 1 : 0;
                for (int tick = 0; tick < 2; tick++) car.Stage(DragMath.FixedStep, new DriverInput { throttle = gas, clutch = 1 });
                synth.Fill(block, 1, rate, controller.Update(car.state, true, .8, 1.0 / 60)); Array.Copy(block, 0, data, frame * 735, 735);
            }
            return data;
        }
        static float[] Audition(CarSimulation car, bool fueling, bool neutralBlips)
        {
            const int rate = 44100; var data = new float[rate * 8]; var block = new float[735];
            var controller = new EngineSoundController(car.spec, car.tune); var synth = new EngineSoundSynth(new EngineSoundProfile(car.spec, car.tune));
            for (int frame = 0; frame < 480; frame++)
            {
                double t = frame / 60.0;
                double gas = neutralBlips ? (t >= 3 && t < 3.28 || t >= 5 && t < 5.18 ? .9 : 0) : fueling ? (t >= 3 && t < 4 || t >= 5 && t < 5.5 ? .65 : 0) : 1;
                for (int tick = 0; tick < 2; tick++) car.Stage(DragMath.FixedStep, new DriverInput { throttle = gas, clutch = 1 });
                synth.Fill(block, 1, rate, controller.Update(car.state, true, .8, 1.0 / 60)); Array.Copy(block, 0, data, frame * 735, 735);
            }
            return data;
        }
    }
}
