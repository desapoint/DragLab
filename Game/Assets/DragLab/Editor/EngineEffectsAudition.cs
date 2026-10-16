using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Editor
{
    // Offline exporter only. Captures the same CarSimulation/event controller used by races.
    public static class EngineEffectsAudition
    {
        [Serializable] sealed class Scalar { public string name, value; }
        [Serializable] sealed class Profile { public Scalar[] scalars; public ExhaustSetup exhaustSystem; }
        [Serializable] sealed class Entry
        {
            public string id, engine, engineKey, layout, fuel, fuelKind, induction, exhaust = "Factory", valve;
            public string group = "Effects", effect, kind, timeline, note, rawFolder = "effects-raw", type = "effect";
            public int cylinders, segmentSeconds = 12, limiterTicks, limitedTicks, twoStepTicks, antiLagTicks, pops, releases, shiftingTicks;
            public float turboSize = 1, pulley = 1, boostScale = 1;
            public double maxRpm;
            public bool factoryReference, engineFailed;
        }
        [Serializable] sealed class Index { public List<Entry> cases = new List<Entry>(); }
        static readonly Encoding Utf8 = new UTF8Encoding(false);
        static readonly FieldInfo[] FrameFields = typeof(EngineSoundFrame).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name).ToArray();
        static Profile Capture(EngineSoundProfile profile)
        {
            return new Profile { exhaustSystem = profile.exhaustSystem.Copy(), scalars = typeof(EngineSoundProfile)
                .GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.Name != "exhaustSystem")
                .Select(f => new Scalar { name = f.Name, value = f.FieldType.IsEnum ? Convert.ToInt32(f.GetValue(profile)).ToString(CultureInfo.InvariantCulture)
                    : f.FieldType == typeof(double) ? ((double)f.GetValue(profile)).ToString("R", CultureInfo.InvariantCulture)
                    : Convert.ToString(f.GetValue(profile), CultureInfo.InvariantCulture) }).ToArray() };
        }
        static void WriteFrame(BinaryWriter writer, EngineSoundFrame frame)
        {
            foreach (var f in FrameFields)
            {
                object value = f.GetValue(frame);
                if (f.FieldType == typeof(double)) writer.Write((double)value);
                else if (f.FieldType == typeof(bool)) writer.Write((bool)value);
                else if (f.FieldType == typeof(int) || f.FieldType.IsEnum) writer.Write(Convert.ToInt32(value));
                else if (f.FieldType == typeof(double[])) { var values = (double[])value; writer.Write(values == null ? 0 : values.Length); if (values != null) foreach (double v in values) writer.Write(v); }
                else throw new InvalidOperationException("Unsupported captured audio field: " + f.Name);
            }
        }
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string directory = Path.Combine(root, "reports/audio-matrix-20261015/effects-capture"); Directory.CreateDirectory(directory);
            var index = new Index();
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(d => d.name))
            {
                bool carb = definition.spec.engineBehavior.fueling == FuelSystem.Carburetor;
                bool diesel = definition.spec.engineBehavior.fueling == FuelSystem.Diesel;
                Export(definition, carb ? "Backfire / overrun" : "Throttle lift", "lift", CutMethod.Factory, index, directory);
                foreach (var cut in diesel ? new[] { CutMethod.Fuel } : carb ? new[] { CutMethod.Ignition } : new[] { CutMethod.Fuel, CutMethod.Ignition })
                {
                    Export(definition, "Hard rev limiter", "hard", cut, index, directory);
                    Export(definition, "Soft rev limiter", "soft", cut, index, directory);
                    Export(definition, "Two-step", "two-step", cut, index, directory);
                }
                Export(definition, "Shift handover", "shifts", CutMethod.Factory, index, directory);
                Export(definition, "Cold idle", "cold", CutMethod.Factory, index, directory);
                Export(definition, "Pause / resume", "pause", CutMethod.Factory, index, directory);
                if (!diesel && new InductionProfile(definition.spec, definition.CreateStockTune()).turboCount > 0)
                {
                    Export(definition, "Anti-lag on lift", "anti-lag", CutMethod.Ignition, index, directory);
                    Export(definition, "Two-step turbo preload", "preload", CutMethod.Ignition, index, directory);
                }
            }
            File.WriteAllText(Path.Combine(directory, "index.json"), JsonUtility.ToJson(index, true), Utf8);
            var log = new StringBuilder("Actual 120 Hz CarSimulation / 60 Hz immutable audio frame capture; no player saves.\n");
            foreach (var row in index.cases) log.AppendLine(row.id + " / " + row.engine + " / " + row.effect + " / " + row.note + " / pops " + row.pops + " / limited ticks " + row.limitedTicks + " / anti-lag ticks " + row.antiLagTicks);
            File.WriteAllText(Path.Combine(root, "reports/audio-matrix-20261015/effects-capture-verification.txt"), log.AppendLine("PASS: " + index.cases.Count + " physically driven event captures.").ToString(), Utf8);
            Debug.Log("ENGINE EFFECT CAPTURE PASS: " + index.cases.Count);
        }
        static void Export(VehicleDefinition definition, string effect, string kind, CutMethod cut, Index index, string folder)
        {
            var tune = definition.CreateStockTune(); tune.partsResolved = false; tune.controlLevel = 1;
            tune.twoStep = EquipmentSetting.Off; tune.launchPreload = tune.antiLag = EquipmentSetting.Off;
            tune.revLimitRpm = Math.Min(5000, definition.spec.redlineRpm - 600); tune.limiter = kind == "soft" ? LimiterStyle.Soft : LimiterStyle.Hard;
            tune.limiterCut = cut; tune.twoStepCut = cut; tune.twoStepStyle = LimiterStyle.Hard;
            tune.launchRpm = Math.Min(3500, definition.spec.redlineRpm - 1000); tune.coldStart = kind == "cold";
            if (kind == "two-step" || kind == "preload") tune.twoStep = EquipmentSetting.On;
            if (kind == "preload") tune.launchPreload = EquipmentSetting.On;
            if (kind == "anti-lag") tune.antiLag = EquipmentSetting.On;
            var driver = new DriverSettings { shifting = ShiftMode.Sequential, launch = LaunchMode.Manual, throttleAssist = false,
                clutchAssist = true, wearMode = WearMode.None, fuelUsage = FuelUsageMode.None, optimalThermals = kind != "cold",
                fluidDepletion = ConsumptionMode.None, fluidAging = ConsumptionMode.None, tractionAssist = false };
            var car = new CarSimulation(definition.spec, tune, driver); car.state.rpm = car.spec.idleRpm;
            var controller = new EngineSoundController(car.spec, car.tune);
            var profile = new EngineSoundProfile(car.spec, car.tune);
            var row = new Entry { id = "E" + (index.cases.Count + 1).ToString("0000"), engine = definition.spec.displayName, engineKey = definition.name,
                cylinders = profile.cylinders, layout = profile.layout.ToString(), kind = kind, effect = effect,
                fuel = profile.fueling == FuelSystem.Carburetor ? "Carburetor" : profile.fueling == FuelSystem.Diesel ? "Diesel common rail" : "Factory injection",
                fuelKind = profile.fueling == FuelSystem.Diesel ? "Diesel" : "Petrol",
                induction = new[] { "Naturally aspirated", "Single turbo", "Supercharger", "Twin turbo", "Twin supercharger", "Twincharged" }[(int)profile.induction],
                valve = profile.turboCount == 0 || profile.fueling == FuelSystem.Diesel ? "Not applicable" : profile.valve.ToString(),
                timeline = kind == "anti-lag" ? "Idle 0–1.5 s; loaded acceleration 1.5–6 s; lift/anti-lag 6–8 s; throttle 8–9 s; lift."
                    : kind == "lift" ? "Three short throttle blips at 1.5/5/7.7 s, with actual lift/overrun between them."
                    : kind == "pause" ? "Idle 0–1.5 s; rev; paused/silent 3.5–5.5 s; resume; lift at 9 s."
                    : kind == "shifts" ? "Idle 0–1.5 s; accelerate; shifts requested at 4/6/8 s; lift at 9 s."
                    : kind == "cold" ? "12 seconds of actual cold-idle behavior; no starter-motor layer."
                    : "Idle 0–1.5 s; throttle/event 1.5–9 s; lift and coast 9–12 s." };
            row.note = "Isolated healthy car + programmable-controls fixture; " + (kind == "two-step" || kind == "preload" ? car.engine.launchCut : car.engine.cut) + " cut. No player resources used.";
            File.WriteAllText(Path.Combine(folder, row.id + "-profile.json"), JsonUtility.ToJson(Capture(profile), true), Utf8);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(folder, row.id + ".frames")), Utf8))
            {
                writer.Write(720); writer.Write(FrameFields.Length);
                foreach (var f in FrameFields) { writer.Write(f.Name); writer.Write(f.FieldType == typeof(double) ? "double" : f.FieldType == typeof(bool) ? "bool" : f.FieldType == typeof(double[]) ? "array" : "int"); }
                for (int publication = 0; publication < 720; publication++)
                {
                    double t = publication / 60.0; bool paused = kind == "pause" && t >= 3.5 && t < 5.5;
                    for (int sub = 0; sub < 2 && !paused; sub++)
                    {
                        double time = t + sub / 120.0; bool drive = kind == "shifts" || kind == "anti-lag";
                        double throttle = time >= 1.5 && time < 9 ? 1 : 0;
                        if (kind == "lift") throttle = time >= 1.5 && time < 2.2 || time >= 5 && time < 5.7 || time >= 7.7 && time < 8.3 ? 1 : 0;
                        if (kind == "cold" || kind == "anti-lag" && time >= 6 && time < 8) throttle = 0;
                        bool shift = kind == "shifts" && sub == 0 && (publication == 240 || publication == 360 || publication == 480);
                        car.IndependentStep(DragMath.FixedStep, new DriverInput { throttle = throttle, shiftUp = shift }, !drive || time < 1.5, kind == "two-step" || kind == "preload");
                        var s = car.state; row.maxRpm = Math.Max(row.maxRpm, s.rpm);
                        if (s.limiter) row.limiterTicks++;
                        if (s.throttle > .5 && s.combustionFraction < .95 && s.rpm > car.engine.launchRpm * .7) row.limitedTicks++;
                        if (s.launchControlActive) row.twoStepTicks++;
                        if (s.antiLagActive) row.antiLagTicks++;
                        if (s.shiftRemaining > 0) row.shiftingTicks++;
                    }
                    WriteFrame(writer, controller.Update(car.state, !paused, 1, 1.0 / 60));
                }
            }
            row.pops = car.state.exhaustPopCount; row.releases = car.state.boostReleaseCount; row.engineFailed = car.state.engineFailed;
            if (row.engineFailed || ((kind == "hard" || kind == "soft" || kind == "two-step") && row.limitedTicks == 0)
                || (kind == "two-step" && row.twoStepTicks == 0) || ((kind == "anti-lag" || kind == "preload") && row.antiLagTicks == 0)
                || (effect == "Backfire / overrun" && row.pops == 0))
                throw new InvalidOperationException("Effect fixture did not produce its labelled behavior: " + row.engine + " / " + effect + " / " + row.note);
            index.cases.Add(row);
        }
    }
}
