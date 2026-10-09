using System;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class CarStatsView : IDisposable
    {
        readonly BenchPlotLines lines = new BenchPlotLines();
        Vector2 scroll;
        GUIStyle prose, muted, rightValue;
        bool showWarnings;
        CarInspection before, after;
        bool comparison;
        UnitPreferences units;
        internal void SetScrollForInspection(float y) { scroll.y = y; }
        public void Draw(Rect area, CarInspection installed, CarInspection preview, string title, string partSlot = null)
        {
            before = installed; after = preview ?? installed; comparison = preview != null; units = SharedUnitSettings.Current;
            if (prose == null) { prose = new GUIStyle(GUI.skin.label) { wordWrap = true }; muted = new GUIStyle(prose) { normal = { textColor = new Color(.65f, .72f, .8f) }, fontSize = 11 }; rightValue = new GUIStyle(prose) { alignment = TextAnchor.UpperRight }; }
            Color original = GUI.color; GUI.color = new Color(.025f, .045f, .065f, 1); GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = original;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(title, prose);
            GUILayout.Label(comparison ? "Saved build: grey / preview: green. Preview never changes the race car." : "Saved installed build / no comparison", muted);
            DrawSummary(area.width, partSlot);
            if (after.warnings.Count == 0) GUILayout.Label("No setup issues found by the limited probe.", muted);
            else
            {
                var first = after.warnings.Find(w => w.critical) ?? after.warnings[0];
                var tint = GUI.contentColor; GUI.contentColor = first.critical ? new Color(1, .4f, .35f) : new Color(1, .8f, .35f);
                showWarnings = GUILayout.Toggle(showWarnings, after.warnings.Count + " setup warning(s): " + first.title + " / show advice", GUI.skin.button);
                GUI.contentColor = tint;
            }
            var chart = GUILayoutUtility.GetRect(100, area.height < 500 ? 104 : 126, GUILayout.ExpandWidth(true)); DrawDyno(chart);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("DETAILED STATS / scroll for explanations and component ratings");
            GUILayout.Label("Bars: grey = current; green = improvement; red = drawback; amber = tradeoff. Ranges are display scales, not safe operating limits.", muted);
            if (showWarnings) foreach (var warning in after.warnings)
            {
                var color = GUI.contentColor; GUI.contentColor = warning.critical ? new Color(1, .4f, .35f) : new Color(1, .8f, .35f);
                GUILayout.Label((warning.critical ? "STOP: " : "CAUTION: ") + warning.title, prose); GUI.contentColor = color;
                GUILayout.Label(SharedUnitSettings.TorqueText(warning.explanation), prose); GUILayout.Label("Try: " + warning.suggestion, muted); GUILayout.Space(4);
            }
            GUILayout.Label("POWER / settled full-throttle engine dyno");
            Row("Peak horsepower", before.peakHp, after.peakHp, "hp", 1, "Power describes how quickly the engine can do work at high speed.");
            GUILayout.Label("Preview peak power at " + after.hpRpm.ToString("F0") + " RPM / peak torque at " + after.torqueRpm.ToString("F0") + " RPM", muted);
            Row("Peak engine torque", before.peakTorque, after.peakTorque, "Nm", 1, "Twisting force before gearing. Extra torque can exceed clutch or tire capacity.");
            Row("Peak settled boost (gauge)", DisplayUnits.Pressure(before.boostBar, units.pressure), DisplayUnits.Pressure(after.boostBar, units.pressure), DisplayUnits.PressureLabel(units.pressure), 0, "Pressure above ambient, not absolute pressure. More boost brings more power and more mechanical load.");
            GUILayout.Label("Boost reference: " + SharedUnitSettings.Boost(after.boostBar), muted);
            GUILayout.Label(after.source.build.spec.sound.cylinders + " cylinders / " + after.source.build.spec.DisplacementLitres.ToString("F1") + " L / " + after.fueling + " / " + after.induction + " / " + after.transmissionName, prose);
            var layout = after.source.build.spec.drive; var awd = AxleDriveDynamics.Configuration(after.source.build.spec, after.source.build.tune);
            GUILayout.Label("Drive: " + layout + (layout == DriveType.AWD ? " / " + awd.mode + (awd.mode == AwdMode.FixedSplit || awd.mode == AwdMode.OpenCenter || awd.mode == AwdMode.ActiveBias ? " / configured front:rear " + awd.frontBias.ToString("P0") + ":" + (1 - awd.frontBias).ToString("P0") : "") : ""), prose);
            if (layout == DriveType.AWD && awd.mode == AwdMode.OnDemand) GUILayout.Label((awd.primaryFront ? "Front" : "Rear") + " primary / secondary up to " + awd.maximumSecondary.ToString("P0") + " / coupling response " + awd.responseSeconds.ToString("F2") + " s", muted);
            if (new InductionProfile(after.source.build.spec, after.source.build.tune).turboCount > 0) Row("Turbo response constant", before.turboSpoolSeconds, after.turboSpoolSeconds, "s", -1, "Smaller is quicker response. Actual boost also depends on RPM, load and launch controls.");
            Row("Fuel delivery supported power", before.source.build.spec.fuelPowerCapacityHp, after.source.build.spec.fuelPowerCapacityHp, "hp", 1, "Delivery headroom. Does not add output unless the old fuel system was limiting it.");
            Row("Ignition supported power", before.source.build.spec.ignitionPowerCapacityHp, after.source.build.spec.ignitionPowerCapacityHp, "hp", 1, "Ignition capacity headroom under load; no unrelated torque-strength or RPM rating.");
            GUILayout.Label("THERMALS / installed cooling");
            Row("Engine heat transfer", before.coolingTransferPercent, after.coolingTransferPercent, "%", 1, "Condition-adjusted conductance relative to factory. More heat reaches the radiator, rather than remaining in the engine.");
            Row("Radiator heat rejection", before.radiatorWattsPerC, after.radiatorWattsPerC, "W/°C", 1, "Cooling capacity before road airflow. A larger radiator can sustain more continuous load.");
            GUILayout.Label("Axle differentials: front " + after.source.build.spec.frontDifferential.kind + " / rear " + after.source.build.spec.rearDifferential.kind + ". Open/LSD/locked differ when left/right grip differs.", muted);
            GUILayout.Label("MASS / tires and engine momentum");
            Row("Vehicle translating mass", before.vehicleMassKg, after.vehicleMassKg, "kg", -1, "Actual translating mass: installed build, current oil/coolant, driver, ballast and current fuel.");
            Row("Installed build + oil/coolant", before.mass.dryInstalledKg, after.mass.dryInstalledKg, "kg", -1, "Installed hardware and current oil/coolant, without driver, ballast or fuel; wheel and flywheel replacement masses count once.");
            Row("Driver", before.mass.driverKg, after.mass.driverKg, "kg", 0, "Driver weight carried in the cabin.");
            Row("Ballast / payload", before.mass.payloadKg, after.mass.payloadKg, "kg", 0, "Extra weight; placement changes axle grip as well as total acceleration load.");
            Row("Current fuel mass", before.mass.fuelKg, after.mass.fuelKg, "kg", 0, "Tank quantity times fuel density. Fuel burns during physical idle, staging and driving, reducing weight. Refill is free in Slice 2.");
            Row("CG from front axle", before.mass.centerOfGravityFromFrontM, after.mass.centerOfGravityFromFrontM, "m", 0, "Moving weight toward the rear shifts static grip toward the rear axle.");
            Row("CG height", before.mass.centerOfGravityHeightM, after.mass.centerOfGravityHeightM, "m", 0, "Higher weight transfers more load rearward during acceleration and forward under braking.");
            Row("Static front axle load", before.mass.frontNormalLoadN, after.mass.frontNormalLoadN, "N", 0, "Gravitational load before acceleration/braking transfer.");
            Row("Static rear axle load", before.mass.rearNormalLoadN, after.mass.rearNormalLoadN, "N", 0, "Together the static axle loads equal the total weight.");
            Row("Effective acceleration mass", before.effectiveMassKg, after.effectiveMassKg, "kg", -1, "Vehicle mass plus equivalent rotating wheel load. Inspection approximation for locked rolling wheels; the road solver uses independent wheel inertia.");
            Row("Tire condition grip", before.tireGripFactor, after.tireGripFactor, "×", 1, "Compound friction adjusted for the weakest tire's current life and temperature.");
            Row("Tire rolling life", before.tireRollingLifeKm, after.tireRollingLifeKm, "km", 1, "Fictional fresh-tire reference life; load, pressure and sliding work shorten it.");
            Row("Tire sliding life", before.tireSlidingLifeMj, after.tireSlidingLifeMj, "MJ per tire", 1, "Sliding-work reserve at reference size. More grip can come with shorter life.");
            Row("Tire OUTER radius", before.wheelRadiusIn, after.wheelRadiusIn, "in", 0, "Axle center to outside rubber. Larger tires raise speed per RPM but reduce launch leverage and add mass.");
            GUILayout.Label("Outer tire diameter: " + after.wheelDiameterIn.ToString("F2") + " in. Rim diameter is not authored yet.", muted);
            Row("Wheel/tire assembly mass", before.wheelMassKg, after.wheelMassKg, "kg each", -1, "Includes the modeled rotating assembly; lighter wheels take less energy to accelerate.");
            Row("All-wheel rotational inertia", before.wheelInertia, after.wheelInertia, "kg m²", 0, "Resistance to changing wheel rotation. Larger/heavier assemblies can slow acceleration.");
            Row("Flywheel mass", before.flywheelMassKg, after.flywheelMassKg, "kg", 0, "A heavier flywheel stores more energy and drops revs more slowly; a light one reacts faster.");
            Row("Flywheel rotational inertia", before.flywheelInertia, after.flywheelInertia, "kg m²", 0, "Geometry matters as well as mass. More inertia smooths engagement but slows RPM response.");
            Row("Tire pressure", DisplayUnits.Pressure(before.source.build.tune.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure), DisplayUnits.Pressure(after.source.build.tune.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure), DisplayUnits.PressureLabel(units.pressure), 0, "Grip depends on pressure; the current model favors about 26 psi / 1.79 bar. Higher is not automatically better.");
            Row("Clutch torque capacity", before.clutchCapacityNm, after.clutchCapacityNm, "Nm", 1, "Current condition and temperature reduce capacity. For converter/CVT hardware this is not a friction-clutch diagnostic.");
            Row("Shift handover time", before.shiftSeconds, after.shiftSeconds, "s", -1, "Shorter handovers can reduce interruption. Actual behavior depends on gearbox type.");
            Row("Brake deceleration rating", before.brakeG, after.brakeG, "g", 1, "Stopping capability before the current tire-grip cap; better brakes cannot bypass tire grip.");
            Row("Lowest part health", before.condition * 100, after.condition * 100, "%", 1, "The weakest-condition installed part, not average car health.");
            GUILayout.Label("GEARING / theoretical speeds at engine redline");
            Row("Final-drive ratio", before.source.build.tune.finalDrive, after.source.build.tune.finalDrive, ":1", 0, "A higher number gives more wheel torque but less speed per engine revolution.");
            Row("Launch target", before.source.build.tune.launchRpm, after.source.build.tune.launchRpm, "RPM", 0, "Too high can spin tires; too low can bog. It is a target, not a guaranteed held RPM.");
            Row("Shift target", before.source.build.tune.shiftRpm, after.source.build.tune.shiftRpm, "RPM", 0, "Shift near the useful power range; automatic control also respects its calculated shift points and limiter.");
            for (int i = 0; i < after.gearRedlineMps.Length; i++)
                Row(after.transmission == TransmissionType.CVT ? (i == 0 ? "CVT shortest ratio speed" : "CVT tallest ratio speed") : "Gear " + (i + 1) + " redline speed", before.transmission == after.transmission && before.gearRedlineMps.Length > i ? DisplayUnits.Speed(before.gearRedlineMps[i], units.speed) : double.NaN, DisplayUnits.Speed(after.gearRedlineMps[i], units.speed), DisplayUnits.SpeedLabel(units.speed), 0, "Higher speed per revolution trades launch leverage for reach; this is not a guaranteed top speed.");
            if (after.transmission != TransmissionType.CVT) for (int i = 0; i < after.rpmAfterUpshift.Length; i++) Row("Upshift " + (i + 1) + " → " + (i + 2) + " engagement", before.transmission == after.transmission && before.rpmAfterUpshift.Length > i ? before.rpmAfterUpshift[i] : double.NaN, after.rpmAfterUpshift[i], "RPM", 0, "Approximate engine speed after the shift; aim to land inside the useful power range.");
            GUILayout.Label("These speeds are gearing limits, not drag-limited top speed.", muted);
            GUILayout.Label("STANDARD PROBE / automatic driver, traction control OFF");
            GUILayout.Label("Copied hardware and wear, full throttle, quarter mile, up to 30 s. No player fuel, cash or part condition is changed.", muted);
            if (after.completed) Row("Modeled quarter-mile elapsed time", before.completed ? before.elapsed : double.NaN, after.elapsed, "s", -1, "For comparing builds under the same driver; it does not predict your exact launch technique.");
            else GUILayout.Label("Quarter-mile estimate unavailable: failed, incomplete or did not finish.", prose);
            Row("Probe final speed", DisplayUnits.Speed(before.trapMps, units.speed), DisplayUnits.Speed(after.trapMps, units.speed), DisplayUnits.SpeedLabel(units.speed), 1, "Final probe speed; only a completed probe reaches quarter-mile trap distance.");
            Row("Maximum measured tire slip", before.source.build.Drivable ? before.maxSlip * 100 : double.NaN, after.source.build.Drivable ? after.maxSlip * 100 : double.NaN, "%", -1, "Excludes very low speed. Measured from actual wheel surface speed versus ground speed. Unavailable when required hardware prevents driving.");
            var bf=before.source.startingFluids??FluidState.Full(before.source.build.spec.fluids);var af=after.source.startingFluids??FluidState.Full(after.source.build.spec.fluids);
            Row("Oil quantity",bf.oilLitres,af.oilLitres,"L",0,"Quantity persists across hardware swaps. Top-up to installed capacity; excess after a smaller pan can aerate.");
            Row("Oil fill capacity",before.source.build.spec.fluids.oilCapacityLitres,after.source.build.spec.fluids.oilCapacityLitres,"L",0,"Larger pans add fluid mass when filled, without refilling automatically.");
            Row("Coolant quantity",bf.coolantLitres,af.coolantLitres,"L",0,"Fill supports circulation and thermal capacity. Leaks and boiling reduce quantity.");
            Row("Oil condition",bf.oilCondition*100,af.oilCondition*100,"%",1,"Aging degrades lubrication; oil and filter service restore the fluid, not damaged bearings.");
            Row("Cam duration at 0.050 inch",before.source.build.spec.valvetrain.durationAt050Degrees,after.source.build.spec.valvetrain.durationAt050Degrees,"crank °",0,"Larger cams shift torque upward and can increase low-speed fuel demand. Match springs and use compatible timing hardware.");
            if (partSlot != null) ComponentComparison(partSlot);
            GUILayout.Label("INSTALLED COMPONENT DETAILS");
            foreach (var item in after.source.components)
            {
                var definition = Array.Find(after.source.build.spec.fittedParts, p => p.id == item.definitionId); if (definition == null) continue;
                bool wears = PartSemantics.Wears(definition.category);
                GUILayout.Label(definition.category + " / " + definition.quality + (wears ? " / health " + item.condition.health.ToString("P0") + " / " + item.condition.temperatureC.ToString("F0") + " °C" : " / no mechanical wear"), prose);
                GUILayout.Label(definition.name + " / " + SharedUnitSettings.TorqueText(definition.description), muted);
                foreach (var value in PartSemantics.Stats(definition)) GUILayout.Label(value.label + ": " + (value.unit == "Nm" ? SharedUnitSettings.Torque(value.value, "0.##") : value.unit == "bar" ? SharedUnitSettings.Pressure(value.value) : value.value.ToString("0.##") + " " + value.unit), muted);
            }
            GUILayout.Label("Modeled, fictional specifications. Settled dyno excludes transient lag and combustion cuts. Shared contact, fuel, wear and fluids are active; operating-cost balance remains Slice 3.", muted);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void ComponentComparison(string slot)
        {
            var a = before.source.components.Find(p => p.slot == slot); var b = after.source.components.Find(p => p.slot == slot);
            var old = a == null ? null : Array.Find(before.source.build.spec.fittedParts, p => p.id == a.definitionId);
            var next = b == null ? null : Array.Find(after.source.build.spec.fittedParts, p => p.id == b.definitionId);
            GUILayout.Label("SELECTED COMPONENT / " + slot);
            if (next == null) { GUILayout.Label("Removing this component leaves an empty slot. Review its removal effect and build warnings.", prose); return; }
            GUILayout.Label(next.name + " / " + next.quality, prose);
            GUILayout.Label(SharedUnitSettings.TorqueText(next.description), prose);
            foreach (var metric in PartSemantics.Stats(next)) {
                double previous = double.NaN;
                if (old != null) foreach (var prior in PartSemantics.Stats(old)) if (prior.key == metric.key) previous = prior.value;
                Row(metric.label, previous, metric.value, metric.unit, metric.direction, metric.explanation);
            }
            if (!PartSemantics.Wears(next.category)) GUILayout.Label("No modeled mechanical wear / no periodic replacement requirement.", muted);

        }
        void Row(string label, double previous, double value, string unit, int beneficialDirection, string explanation)
        {
            if (unit == "Nm") { previous = DisplayUnits.Torque(previous, units.torque); value = DisplayUnits.Torque(value, units.torque); unit = DisplayUnits.TorqueLabel(units.torque); }
            if (unit == "bar") {previous=DisplayUnits.Pressure(previous,units.pressure);value=DisplayUnits.Pressure(value,units.pressure);unit=DisplayUnits.PressureLabel(units.pressure);}
            string format = Math.Abs(value) < 10 ? "F2" : "F1";
            bool hasPrevious = comparison && !double.IsNaN(previous);
            double delta = hasPrevious ? value - previous : 0;
            bool different = hasPrevious && Math.Abs(delta) > .00001;
            Color changeColor = !different ? new Color(.7f, .75f, .8f) : beneficialDirection == 0 ? new Color(1, .8f, .4f) : delta * beneficialDirection > 0 ? new Color(.3f, 1, .55f) : new Color(1, .45f, .4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, prose, GUILayout.ExpandWidth(true));
            var old = GUI.contentColor; GUI.contentColor = changeColor;
            GUILayout.Label((hasPrevious ? previous.ToString(format) + " → " : "") + value.ToString(format) + " " + unit, rightValue, GUILayout.Width(215));
            GUI.contentColor = old; GUILayout.EndHorizontal();
            double min, max; BarRange(label, unit, previous, value, out min, out max);
            Rect row = GUILayoutUtility.GetRect(100, 35, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Rect bar = new Rect(row.x, row.y + 2, row.width, 10);
                float current = (float)Math.Max(0, Math.Min(1, ((double.IsNaN(previous) ? value : previous) - min) / (max - min)));
                float proposed = (float)Math.Max(0, Math.Min(1, (value - min) / (max - min)));
                Fill(bar, new Color(.12f, .18f, .23f));
                Fill(new Rect(bar.x, bar.y, bar.width * (hasPrevious ? current : proposed), bar.height), new Color(.4f, .5f, .6f));
                if (different) Fill(new Rect(bar.x + bar.width * Math.Min(current, proposed), bar.y, bar.width * Math.Abs(current - proposed), bar.height), changeColor);
                if (hasPrevious) Fill(new Rect(bar.x + bar.width * current - 1, bar.y - 2, 2, bar.height + 4), new Color(.85f, .9f, .95f));
                Fill(new Rect(bar.x + bar.width * proposed - 1, bar.y - 2, 2, bar.height + 4), hasPrevious ? changeColor : new Color(.8f, .87f, .95f));
                GUI.Label(new Rect(row.x, row.y + 14, 130, 19), "Min " + min.ToString("0.##") + " " + unit, muted);
                GUI.Label(new Rect(row.xMax - 130, row.y + 14, 130, 19), "Max " + max.ToString("0.##") + " " + unit, rightValue);
                if (hasPrevious)
                {
                    GUI.contentColor = changeColor;
                    string result = !different ? "unchanged" : beneficialDirection == 0 ? "tradeoff" : delta * beneficialDirection > 0 ? "better" : "worse";
                    GUI.Label(new Rect(row.x + 130, row.y + 14, row.width - 260, 19), delta.ToString("+0.##;-0.##;0") + " " + unit + " · " + result, muted);
                    GUI.contentColor = old;
                }
            }
            GUILayout.Label(explanation, muted); GUILayout.Space(4);
        }
        void BarRange(string label, string unit, double previous, double value, out double min, out double max)
        {
            min = 0;
            switch (label)
            {
                case "Peak horsepower": max = 500; break;
                case "Peak engine torque": case "Clutch torque capacity": max = DisplayUnits.Torque(1000, units.torque); break;
                case "Peak settled boost (gauge)": max = DisplayUnits.Pressure(3, units.pressure); break;
                case "Turbo response constant": max = 2; break;
                case "Vehicle translating mass": case "Effective acceleration mass": max = 3500; break;
                case "Tire OUTER radius": min = 8; max = 24; break;
                case "Wheel/tire assembly mass": max = 50; break;
                case "All-wheel rotational inertia": max = 20; break;
                case "Flywheel mass": max = 20; break;
                case "Flywheel rotational inertia": max = .5; break;
                case "Tire pressure": max = DisplayUnits.Pressure(60 / DisplayUnits.PsiPerBar, units.pressure); break;
                case "Shift handover time": max = 1; break;
                case "Brake deceleration rating": max = 2; break;
                case "Lowest part health": case "Maximum measured tire slip": max = 100; break;
                case "Final-drive ratio": min = 2; max = 6; break;
                case "Launch target": case "Shift target": case "Component RPM rating": max = 10000; break;
                case "Modeled quarter-mile elapsed time": max = 30; break;
                case "Probe final speed": max = DisplayUnits.Speed(100, units.speed); break;
                case "Component torque-strength rating": max = DisplayUnits.Torque(5000, units.torque); break;
                case "Component heat limit": max = 300; break;
                case "Component friction-wear energy": max = 20; break;
                default: max = unit == "RPM" ? 10000 : unit == DisplayUnits.SpeedLabel(units.speed) ? DisplayUnits.Speed(100, units.speed) : 100; break;
            }
            // Keep the same reference range on both sides; unusually large builds expand rather than clip.
            double greatest = Math.Max(value, double.IsNaN(previous) ? value : previous);
            if (greatest > max) { double magnitude = Math.Pow(10, Math.Floor(Math.Log10(greatest))); max = Math.Ceiling(greatest / magnitude) * magnitude; }
            if (value < min || (!double.IsNaN(previous) && previous < min)) min = 0;
        }
        static void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        void DrawDyno(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            double maxRpm = Math.Max(before.source.build.spec.redlineRpm, after.source.build.spec.redlineRpm), maxHp = Math.Max(1, Math.Max(before.peakHp, after.peakHp)), maxNm = Math.Max(1, Math.Max(before.peakTorque, after.peakTorque));
            var plot = new Rect(rect.x + 35, rect.y + 25, rect.width - 75, rect.height - 50);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), "DYNO / HP thick, engine " + SharedUnitSettings.TorqueLabel + " thin / independent scales");
            GUI.Label(new Rect(rect.x, plot.y - 3, 40, 20), maxHp.ToString("F0")); GUI.Label(new Rect(plot.xMax + 4, plot.y - 3, 42, 20), DisplayUnits.Torque(maxNm, units.torque).ToString("F0"));
            GUI.Label(new Rect(plot.x, plot.yMax + 3, plot.width, 20), "0 RPM                         " + maxRpm.ToString("F0") + " RPM");
            // This chart is inside a GUILayout area; GL uses absolute screen coordinates.
            Vector2 offset = GUIUtility.GUIToScreenPoint(Vector2.zero) / GUI.matrix.m00;
            plot.position += offset;
            lines.Begin();
            for (int i = 0; i <= 4; i++) { float f = i / 4f; lines.Line(new Vector2(plot.x, plot.y + f * plot.height), new Vector2(plot.xMax, plot.y + f * plot.height), new Color(.3f, .4f, .5f, .4f), 1); }
            if (comparison) Curve(before, plot, maxRpm, maxHp, maxNm, new Color(.6f, .65f, .75f, .85f));
            Curve(after, plot, maxRpm, maxHp, maxNm, new Color(.25f, 1, .55f)); lines.End();
        }
        void Curve(CarInspection stats, Rect plot, double rpm, double hp, double nm, Color color)
        {
            lines.Series(stats.curve.Count, plot.width, i => new Vector2(plot.x + (float)(stats.curve[i].rpm / rpm) * plot.width, plot.yMax - (float)(stats.curve[i].hp / hp) * plot.height), color, 3);
            color = Color.Lerp(color, new Color(1, .7f, .2f, color.a), .5f);
            lines.Series(stats.curve.Count, plot.width, i => new Vector2(plot.x + (float)(stats.curve[i].rpm / rpm) * plot.width, plot.yMax - (float)(stats.curve[i].torque / nm) * plot.height), color, 1.5f);
        }
        public void Dispose() { lines.Dispose(); }
    }
}
