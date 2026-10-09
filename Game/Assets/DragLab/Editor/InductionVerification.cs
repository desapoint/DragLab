using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using DragLab.Domain;
using DragLab.Data;

namespace DragLab.Editor
{
    public static class InductionVerification
    {
        static void Check(bool ok, string message, ref int count) { if (!ok) throw new InvalidOperationException("INDUCTION CHECK FAILED: " + message); count++; }
        static Tune Kit(EngineUpgradeInduction kit) { return new Tune { engineLevel = 1, inductionUpgrade = kit, tireLevel = 2, gearboxLevel = 2 }; }
        static CarState Pull(VehicleSpec spec, Tune tune, double rpm, double seconds)
        {
            var profile = new InductionProfile(spec, tune); var state = new CarState { rpm = rpm, throttle = 1, launched = true };
            for (int i = 0; i < seconds * 120; i++) InductionDynamics.Step(profile, state, 1, 1, false, false, DragMath.FixedStep);
            return state;
        }
        public static int Run(string root)
        {
            int count = 0; var spec = new VehicleSpec();
            var turbo = Kit(EngineUpgradeInduction.Turbo); var sc = Kit(EngineUpgradeInduction.Supercharger);
            var report = new StringBuilder("Fixed 120 Hz induction response checks; fictional calibrations.\n");
            var early = Pull(spec, turbo, 4500, .1); var late = Pull(spec, turbo, 4500, 3);
            Check(early.boostBar < .05 && late.boostBar > .6, "Turbo takes time to spool under load.", ref count);
            Check(late.engineTorqueNm > early.engineTorqueNm * 1.35, "Turbo lag changes delivered engine torque.", ref count);
            Check(Math.Abs(late.engineHorsepower - late.engineTorqueNm * 4500 / 7127) < .00001, "Live horsepower is derived from live torque.", ref count);
            Check(Math.Abs(late.engineTorqueNm - DragMath.Torque(spec, turbo, 4500)) < 4, "Settled live WOT agrees with dyno envelope.", ref count);
            var scEarly = Pull(spec, sc, 4500, .1);
            Check(scEarly.boostBar > .4 && scEarly.engineTorqueNm > early.engineTorqueNm * 1.2, "Mechanically driven blower responds before turbo spool.", ref count);
            Check(scEarly.superchargerDriveTorqueNm > 5, "Supercharger deducts compressor work and drive friction.", ref count);
            var freeDrive = JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(spec)); freeDrive.induction.superchargerFrictionNm = 0; freeDrive.induction.superchargerEfficiency = .9f;
            Check(Pull(freeDrive, sc, 4500, 1).engineTorqueNm > Pull(spec, sc, 4500, 1).engineTorqueNm, "More efficient blower drive changes net torque.", ref count);
            var profile = new InductionProfile(spec, turbo); var state = Pull(spec, turbo, 4500, 3);
            double charged = state.boostBar, chargedSpool = state.turboSpool;
            for (int i = 0; i < 12; i++) InductionDynamics.Step(profile, state, 0, 1, false, false, DragMath.FixedStep);
            Check(state.boostBar < charged * .2 && state.turboSpool > 0 && state.turboSpool < chargedSpool && state.boostReleaseCount == 1,
                "Lift vents manifold pressure while rotor coasts, releasing once.", ref count);
            for (int i = 0; i < 120; i++) InductionDynamics.Step(profile, state, 0, 1, false, false, DragMath.FixedStep);
            Check(state.boostReleaseCount == 1 && state.engineTorqueNm == 0, "Held lift does not repeat release or retain combustion torque.", ref count);
            var shifted = Pull(spec, turbo, 4500, 3);
            for (int i = 0; i < 30; i++) InductionDynamics.Step(profile, shifted, 1, 1, true, false, DragMath.FixedStep);
            Check(shifted.boostReleaseCount == 1 && shifted.boostBar < .1 && shifted.engineTorqueNm == 0, "Shift cuts torque and boost with gas still held.", ref count);
            double afterShift = shifted.boostBar;
            for (int i = 0; i < 30; i++) InductionDynamics.Step(profile, shifted, 1, 1, false, false, DragMath.FixedStep);
            Check(shifted.boostBar > afterShift + .1 && shifted.boostBar < charged, "Pressure recovers progressively after shift.", ref count);
            var neutral = new CarState { rpm = 6000 };
            for (int i = 0; i < 360; i++) InductionDynamics.Step(profile, neutral, 1, 0, false, false, DragMath.FixedStep);
            Check(neutral.boostBar < .08, "Unloaded neutral cannot make full turbo boost.", ref count);
            var cut = Pull(spec, turbo, 4500, 3);
            for (int i = 0; i < 120; i++) InductionDynamics.Step(profile, cut, 1, 1, false, true, DragMath.FixedStep);
            Check(cut.boostBar < .01 && cut.engineTorqueNm == 0, "Limiter removes exhaust energy and combustion torque.", ref count);
            var small = Kit(EngineUpgradeInduction.Turbo); small.turboSize = .7f;
            var big = Kit(EngineUpgradeInduction.Turbo); big.turboSize = 1.6f;
            Check(Pull(spec, small, 3000, .5).boostBar > Pull(spec, big, 3000, .5).boostBar + .1, "Larger turbo has a later threshold and slower response.", ref count);
            var twin = Kit(EngineUpgradeInduction.TwinTurbo);
            Check(Pull(spec, twin, 4500, .5).boostBar > Pull(spec, turbo, 4500, .5).boostBar, "Matched smaller twin preset improves spool response.", ref count);
            Check(Math.Abs(Pull(spec, twin, 4500, 5).boostBar - Pull(spec, turbo, 4500, 5).boostBar) < .02, "Parallel twins do not double pressure.", ref count);
            var limited = JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(spec)); limited.induction.turboUnitFlowKgS = .07f;
            Check(DragMath.Torque(limited, twin, 6000) > DragMath.Torque(limited, turbo, 6000) * 1.2, "Twins add usable compressor flow capacity.", ref count);
            var twinSc = Kit(EngineUpgradeInduction.TwinSupercharger);
            Check(Pull(spec, twinSc, 4500, 3).superchargerDriveTorqueNm > Pull(spec, sc, 4500, 3).superchargerDriveTorqueNm + 3,
                "Second parallel blower adds drive friction at equal pressure.", ref count);
            limited.induction.superchargerUnitFlowKgS = .07f;
            Check(DragMath.Torque(limited, twinSc, 6000) > DragMath.Torque(limited, sc, 6000) * 1.1, "Twin blowers add flow rather than automatic double boost.", ref count);
            var mixed = Kit(EngineUpgradeInduction.Twincharged);
            var mixedLow = Pull(spec, mixed, 1300, .5); var mixedEarly = Pull(spec, mixed, 4500, .1); var mixedLate = Pull(spec, mixed, 4500, 4);
            Check(mixedLow.engineTorqueNm > Pull(spec, turbo, 1300, .5).engineTorqueNm * 1.2, "Twincharge blower fills low-RPM turbo gap.", ref count);
            Check(mixedEarly.superchargerBoostBar > .3 && mixedLate.superchargerBoostBar < .03 && mixedLate.superchargerBypass > .95,
                "Twincharge bypass transitions from blower to developed turbo boost.", ref count);
            Check(mixedEarly.boostBar >= mixedEarly.superchargerBoostBar && mixedLate.boostBar <= spec.induction.maxBoostBar,
                "Mixed pressure is bounded and includes active compressor stages.", ref count);
            var mixedMid = Pull(spec, mixed, 2400, 5);
            Check(Math.Abs(mixedMid.engineTorqueNm - DragMath.Torque(spec, mixed, 2400)) < 2,
                "Twincharge midrange bypass agrees between live engine and dyno.", ref count);
            var safe = JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(spec)); safe.induction.maxBoostBar = .45f;
            var capped = Pull(safe, mixed, 4500, 3);
            Check(capped.boostBar <= .451 && capped.turboBoostBar <= .451 && capped.superchargerBoostBar <= .451,
                "Combined compressors obey the authored pressure limit.", ref count);
            var hot = JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(spec)); hot.induction.intercoolerEffectiveness = 0;
            var hotPull = Pull(hot, turbo, 4500, 3);
            Check(hotPull.chargeTemperatureK > late.chargeTemperatureK + 20 && hotPull.engineTorqueNm < late.engineTorqueNm,
                "Charge heating/intercooler efficiency change torque.", ref count);
            var lower = Kit(EngineUpgradeInduction.Turbo); lower.boostSetting = .5f;
            Check(DragMath.Torque(spec, lower, 5000) < DragMath.Torque(spec, turbo, 5000), "Wastegate boost tuning changes dyno curve.", ref count);
            var fastPulley = Kit(EngineUpgradeInduction.Supercharger); fastPulley.superchargerPulley = 1.2f;
            var pulleyPull = Pull(spec, fastPulley, 4500, 2);
            Check(pulleyPull.boostBar > Pull(spec, sc, 4500, 2).boostBar && pulleyPull.superchargerDriveTorqueNm > scEarly.superchargerDriveTorqueNm,
                "Pulley tuning trades greater boost against drive work.", ref count);
            foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                foreach (var point in car.spec.torque)
                    Check(Math.Abs(DragMath.Torque(car.spec, car.CreateStockTune(), point.rpm) - point.torqueNm) < .01,
                        "Factory steady curve has no double-counted boost: " + car.spec.id + " / " + point.rpm, ref count);
            var actual = new CarSimulation(spec, turbo, new DriverSettings { throttleAssist = false }); actual.Launch(0);
            var coupe = Array.Find(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"), v => v.spec.id == "sport-coupe");
            var duel = new RaceSimulation(new CarSimulation(coupe.spec, coupe.CreateStockTune(), new DriverSettings()), new CarSimulation(coupe.spec, coupe.CreateStockTune(), new DriverSettings()), 402.336);
            while (duel.phase != RacePhase.Finished) duel.Step(new DriverInput());
            Check(Math.Abs(duel.player.state.elapsed - Benchmark.QuarterMile(coupe.spec, coupe.CreateStockTune()).elapsed) < .000001 &&
                Math.Abs(duel.player.state.elapsed - duel.opponent.state.elapsed) < .000001,
                "Turbo benchmark/player/AI share neutral staging history and finish timing.", ref count);
            // A real lift opens the valve; disconnecting a manual clutch with
            // held gas no longer pretends to shut the throttle/combustion off.
            for (int i = 0; i < 1500; i++) actual.Step(DragMath.FixedStep, new DriverInput { throttle = i >= 600 && i < 624 ? 0 : 1 });
            Check(actual.state.distance > 150 && actual.state.boostBar > .1 && actual.state.boostReleaseCount > 0 && actual.state.transmittedTorqueNm > 0,
                "Induction affects actual driving/shifts and valve release on a real throttle lift.", ref count);
            var telemetry = new RaceTelemetry(); telemetry.Record(actual, 13);
            Check(telemetry.Csv().Contains("engine_hp") && telemetry.samples[0].boostBar == actual.state.boostBar,
                "Actual pressure and engine output reach race telemetry.", ref count);
            var roundtrip = JsonUtility.FromJson<Tune>(JsonUtility.ToJson(new Tune { engineLevel = 2, inductionUpgrade = EngineUpgradeInduction.Twincharged, turboSize = 1.4f, boostSetting = .8f, superchargerPulley = 1.1f }));
            Check(roundtrip.inductionUpgrade == EngineUpgradeInduction.Twincharged && roundtrip.turboSize == 1.4f && roundtrip.boostSetting == .8f && roundtrip.superchargerPulley == 1.1f,
                "Topologies and physical adjustments survive save roundtrip.", ref count);
            var legacy = JsonUtility.FromJson<Tune>("{\"engineLevel\":1,\"inductionUpgrade\":2}"); legacy.ClampTo(spec);
            Check(legacy.SuperchargerPulley == 1 && legacy.BoostSetting == 1 && legacy.TurboSize == 1 && legacy.inductionUpgrade == EngineUpgradeInduction.Supercharger,
                "Previous single-induction IDs and absent settings remain compatible.", ref count);
            var snapshot = new InductionProfile(spec, turbo); spec.induction.turboSpoolSeconds = 3; turbo.turboSize = 1.6f;
            Check(snapshot.data.turboSpoolSeconds == .65f && snapshot.size == 1, "Simulation captures compressor parameters and tune.", ref count);
            spec.induction.turboSpoolSeconds = float.NaN;
            Check(spec.Validate() != null, "Invalid mechanical induction specs rejected.", ref count);
            var example = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            foreach (var car in example) if (car.spec.id == "starter-hatch") File.WriteAllText(Path.Combine(root, "reports/induction-comparison.csv"), ComparisonCsv(car.spec));
            report.AppendLine("Single turbo at 4500 RPM: 0.1 s = " + early.boostBar.ToString("F3") + " bar / " + early.engineTorqueNm.ToString("F1") + " Nm; 3 s = " + late.boostBar.ToString("F3") + " bar / " + late.engineTorqueNm.ToString("F1") + " Nm.");
            report.AppendLine("Single supercharger at 0.1 s: " + scEarly.boostBar.ToString("F3") + " bar / drive " + scEarly.superchargerDriveTorqueNm.ToString("F1") + " Nm.");
            report.AppendLine("PASS: " + count + " induction checks. No compressor-map calibration or manufacturer performance claims.");
            File.WriteAllText(Path.Combine(root, "reports/induction-verification.txt"), report.ToString()); return count;
        }
        public static string ComparisonCsv(VehicleSpec spec)
        {
            var csv = new StringBuilder("vehicle,kit,rpm,steady_torque_nm,steady_hp,boost_after_0_1s_bar,boost_after_0_5s_bar,boost_after_3s_bar,torque_after_0_1s_nm,torque_after_3s_nm,drive_after_3s_nm,quarter_s,trap_kmh\n");
            foreach (EngineUpgradeInduction mode in Enum.GetValues(typeof(EngineUpgradeInduction)))
            {
                var tune = Kit(mode); var pass = Benchmark.QuarterMile(spec, tune);
                for (int rpm = 1500; rpm <= spec.redlineRpm; rpm += 500)
                {
                    var early = Pull(spec, tune, rpm, .1); var mid = Pull(spec, tune, rpm, .5); var late = Pull(spec, tune, rpm, 3);
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F3},{4:F3},{5:F4},{6:F4},{7:F4},{8:F3},{9:F3},{10:F3},{11:F4},{12:F2}\n",
                        spec.id, mode, rpm, DragMath.Torque(spec, tune, rpm), DragMath.Horsepower(spec, tune, rpm), early.boostBar, mid.boostBar, late.boostBar,
                        early.engineTorqueNm, late.engineTorqueNm, late.superchargerDriveTorqueNm, pass.elapsed, pass.trapKmh);
                }
            }
            return csv.ToString();
        }
    }
}
