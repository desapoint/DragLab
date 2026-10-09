using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class CarStatsView
    {
        sealed class QuickStat
        {
            public string label, rangeLabel, unit, format, oldText, newText;
            public double previous, value;
            public int direction;
            public bool Different { get { return oldText != null ? oldText != newText : Format(previous) != Format(value); } }
            public string Format(double number) { return double.IsNaN(number) ? "Unavailable" : number.ToString(format) + " " + unit; }
        }
        GUIStyle quickLabel, quickValue, quickTile;
        List<QuickStat> coreSummary, changedSummary;
        CarInspection summaryBefore, summaryAfter;
        string summarySlot;
        PressureUnit summaryPressure;
        SpeedUnit summarySpeed;
        TorqueUnit summaryTorque;
        internal readonly List<string> SummaryForVerification = new List<string>();
        internal Rect SummaryRect { get; private set; }
        void BuildSummary(string slot)
        {
            if (summaryBefore == before && summaryAfter == after && summarySlot == slot && summaryPressure == units.pressure && summarySpeed == units.speed && summaryTorque == units.torque) return;
            summaryBefore = before; summaryAfter = after; summarySlot = slot; summaryPressure = units.pressure; summarySpeed = units.speed; summaryTorque = units.torque;
            coreSummary = new List<QuickStat>(); changedSummary = new List<QuickStat>();
            Add(coreSummary, "Power", "Peak horsepower", before.peakHp, after.peakHp, "hp", 1);
            Add(coreSummary, "Torque", "Peak engine torque", before.peakTorque, after.peakTorque, "Nm", 1);
            Add(coreSummary, "Vehicle weight", "Vehicle translating mass", before.vehicleMassKg, after.vehicleMassKg, "kg", -1, "F0");
            Add(coreSummary, "¼ mile / standard probe", "Modeled quarter-mile elapsed time", before.completed ? before.elapsed : double.NaN, after.completed ? after.elapsed : double.NaN, "s", -1, "F2");
            // Vehicle effects lead; selected-component ratings also appear when output is unchanged.
            Add(changedSummary, "Driver mass", "Driver", before.mass.driverKg, after.mass.driverKg, "kg", 0);
            Add(changedSummary, "Ballast", "Ballast / payload", before.mass.payloadKg, after.mass.payloadKg, "kg", 0);
            Add(changedSummary, "Fuel mass", "Current fuel mass", before.mass.fuelKg, after.mass.fuelKg, "kg", 0);
            Add(changedSummary, "CG position", "CG from front axle", before.mass.centerOfGravityFromFrontM, after.mass.centerOfGravityFromFrontM, "m", 0, "F2");
            Add(changedSummary, "CG height", "CG height", before.mass.centerOfGravityHeightM, after.mass.centerOfGravityHeightM, "m", 0, "F2");
            Add(changedSummary, "Cooling / heat transfer", "Engine heat transfer", before.coolingTransferPercent, after.coolingTransferPercent, "%", 1);
            Add(changedSummary, "Radiator cooling", "Radiator heat rejection", before.radiatorWattsPerC, after.radiatorWattsPerC, "W/°C", 1, "F0");
            Add(changedSummary, "Clutch capacity", "Clutch torque capacity", before.clutchCapacityNm, after.clutchCapacityNm, "Nm", 1);
            Add(changedSummary, "Braking", "Brake deceleration rating", before.brakeG, after.brakeG, "g", 1, "F2");
            Add(changedSummary, "Lowest part health", "Lowest part health", before.condition * 100, after.condition * 100, "%", 1);
            Add(changedSummary, "Boost (gauge)", "Peak settled boost (gauge)", DisplayUnits.Pressure(before.boostBar, units.pressure), DisplayUnits.Pressure(after.boostBar, units.pressure), DisplayUnits.PressureLabel(units.pressure), 0, "F2");
            Add(changedSummary, "Tire condition grip", "Tire condition grip", before.tireGripFactor, after.tireGripFactor, "×", 1, "F2");
            Add(changedSummary, "Tire rolling life", "Tire rolling life", before.tireRollingLifeKm, after.tireRollingLifeKm, "km", 1, "F0");
            Add(changedSummary, "Tire sliding life", "Tire sliding life", before.tireSlidingLifeMj, after.tireSlidingLifeMj, "MJ per tire", 1, "F1");
            Add(changedSummary, "Tire outer radius", "Tire OUTER radius", before.wheelRadiusIn, after.wheelRadiusIn, "in", 0, "F2");
            Add(changedSummary, "Rotating acceleration load", "Effective acceleration mass", before.effectiveMassKg, after.effectiveMassKg, "kg", -1, "F0");
            Add(changedSummary, "Flywheel inertia", "Flywheel rotational inertia", before.flywheelInertia, after.flywheelInertia, "kg m²", 0, "F3");
            Add(changedSummary, "Shift time", "Shift handover time", before.shiftSeconds, after.shiftSeconds, "s", -1, "F2");
            Add(changedSummary, "Turbo response", "Turbo response constant", before.turboSpoolSeconds, after.turboSpoolSeconds, "s", -1, "F2");
            Add(changedSummary, "Tire pressure", "Tire pressure", DisplayUnits.Pressure(before.source.build.tune.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure), DisplayUnits.Pressure(after.source.build.tune.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure), DisplayUnits.PressureLabel(units.pressure), 0, "F2");
            Add(changedSummary, "Final drive", "Final-drive ratio", before.source.build.tune.finalDrive, after.source.build.tune.finalDrive, ":1", 0, "F2");
            Add(changedSummary, "Measured wheelspin", "Maximum measured tire slip", before.source.build.Drivable ? before.maxSlip * 100 : double.NaN, after.source.build.Drivable ? after.maxSlip * 100 : double.NaN, "%", -1);
            Add(changedSummary, "Launch RPM", "Launch target", before.source.build.tune.launchRpm, after.source.build.tune.launchRpm, "RPM", 0, "F0");
            Add(changedSummary, "Shift RPM", "Shift target", before.source.build.tune.shiftRpm, after.source.build.tune.shiftRpm, "RPM", 0, "F0");
            Add(changedSummary, "Fuel power capacity", "Fuel delivery supported power", before.source.build.spec.fuelPowerCapacityHp, after.source.build.spec.fuelPowerCapacityHp, "hp", 1);
            Add(changedSummary, "Ignition capacity", "Ignition supported power", before.source.build.spec.ignitionPowerCapacityHp, after.source.build.spec.ignitionPowerCapacityHp, "hp", 1);
            Add(changedSummary,"Oil capacity","Oil fill capacity",before.source.build.spec.fluids.oilCapacityLitres,after.source.build.spec.fluids.oilCapacityLitres,"L",0,"F1");
            Add(changedSummary,"Coolant capacity","Coolant fill capacity",before.source.build.spec.fluids.coolantCapacityLitres,after.source.build.spec.fluids.coolantCapacityLitres,"L",0,"F1");
            Add(changedSummary,"Valve control","Spring control rating",before.source.build.spec.valvetrain.springSafeRpm,after.source.build.spec.valvetrain.springSafeRpm,"RPM",1,"F0");
            Add(changedSummary,"Cam duration","Cam duration",before.source.build.spec.valvetrain.durationAt050Degrees,after.source.build.spec.valvetrain.durationAt050Degrees,"crank °",0,"F0");
            AddText("Gearbox", before.transmissionName, after.transmissionName);
            AddText("Induction", before.induction, after.induction);
            AddText("Fuel delivery", before.fueling, after.fueling);
            Add(changedSummary, "Idle control", "Idle control authority", before.source.build.spec.engineBehavior.idleControl*100, after.source.build.spec.engineBehavior.idleControl*100, "%", 1);
            Add(changedSummary, "Fuel metering variation", "Fuel metering variation", before.source.build.spec.engineBehavior.mixtureVariation*100, after.source.build.spec.engineBehavior.mixtureVariation*100, "%", -1);
            Add(changedSummary, "Throttle hesitation decay", "Throttle hesitation time", before.source.build.spec.engineBehavior.hesitationSeconds, after.source.build.spec.engineBehavior.hesitationSeconds, "s", -1, "F2");
            AddText("Front differential", before.source.build.spec.frontDifferential.kind.ToString(), after.source.build.spec.frontDifferential.kind.ToString());
            AddText("Rear differential", before.source.build.spec.rearDifferential.kind.ToString(), after.source.build.spec.rearDifferential.kind.ToString());
            var oldControl = new EngineBehaviorProfile(before.source.build.spec, before.source.build.tune);
            var newControl = new EngineBehaviorProfile(after.source.build.spec, after.source.build.tune);
            AddText("Rev limiter", oldControl.limiter == LimiterStyle.None ? "None" : oldControl.limiter + " / " + oldControl.limitRpm.ToString("F0") + " RPM", newControl.limiter == LimiterStyle.None ? "None" : newControl.limiter + " / " + newControl.limitRpm.ToString("F0") + " RPM");
            AddText("Two-step", oldControl.twoStep ? oldControl.launchLimiter + " / " + oldControl.launchRpm.ToString("F0") + " RPM" : "Off", newControl.twoStep ? newControl.launchLimiter + " / " + newControl.launchRpm.ToString("F0") + " RPM" : "Off");
            AddText("Shift light", before.source.build.tune.partShiftLight ? before.source.build.tune.ShiftLightRpm(before.source.build.spec).ToString("F0") + " RPM" : "None", after.source.build.tune.partShiftLight ? after.source.build.tune.ShiftLightRpm(after.source.build.spec).ToString("F0") + " RPM" : "None");
            if (after.source.build.spec.drive == DriveType.AWD) AddText("AWD center / coupling", AxleDriveDynamics.Configuration(before.source.build.spec, before.source.build.tune).mode.ToString(), AxleDriveDynamics.Configuration(after.source.build.spec, after.source.build.tune).mode.ToString());
            if (slot != null) {
                var a = before.source.components.Find(p => p.slot == slot); var b = after.source.components.Find(p => p.slot == slot);
                var old = a == null ? null : Array.Find(before.source.build.spec.fittedParts, p => p.id == a.definitionId);
                var next = b == null ? null : Array.Find(after.source.build.spec.fittedParts, p => p.id == b.definitionId);
                foreach (var metric in PartSemantics.Stats(next)) {
                    if (changedSummary.Any(s => s.rangeLabel == metric.label && s.unit == metric.unit)) continue;
                    var previous = PartSemantics.Stats(old).Where(s => s.key == metric.key).ToArray();
                    Add(changedSummary, "Part / " + metric.label, metric.label, previous.Length == 0 ? double.NaN : previous[0].value, metric.value, metric.unit, metric.direction, "0.##");
                }
            }
            changedSummary = comparison ? changedSummary.Where(s => s.Different).ToList() : new List<QuickStat>();
            // Direct tuning edits lead; calculated CG/inertia side effects stay in the full detail screen.
            if(slot==null)changedSummary=changedSummary.OrderBy(metric=>{
                if(metric.label=="CG position")return before.source.build.tune.payloadPositionM!=after.source.build.tune.payloadPositionM?0:30;
                if(metric.label=="CG height")return before.source.build.tune.payloadHeightM!=after.source.build.tune.payloadHeightM?0:30;
                return metric.label=="Rotating acceleration load"?20:10;
            }).ToList();
        }
        void Add(List<QuickStat> list, string label, string rangeLabel, double previous, double value, string unit, int direction, string format = "F1")
        { if (unit == "Nm") { previous = DisplayUnits.Torque(previous, units.torque); value = DisplayUnits.Torque(value, units.torque); unit = DisplayUnits.TorqueLabel(units.torque); } if(unit=="bar"){previous=DisplayUnits.Pressure(previous,units.pressure);value=DisplayUnits.Pressure(value,units.pressure);unit=DisplayUnits.PressureLabel(units.pressure);} list.Add(new QuickStat { label = label, rangeLabel = rangeLabel, previous = previous, value = value, unit = unit, direction = direction, format = format }); }
        void AddText(string label, string previous, string next)
        { changedSummary.Add(new QuickStat { label = label, oldText = previous, newText = next, direction = 0 }); }
        string SummaryValue(QuickStat metric)
        {
            string a = metric.oldText ?? metric.Format(metric.previous), b = metric.newText ?? metric.Format(metric.value);
            return comparison && metric.Different ? a + " → " + b : b;
        }
        Color SummaryColor(QuickStat metric)
        {
            if (!comparison || !metric.Different) return new Color(.8f, .87f, .95f);
            if (metric.direction == 0) return new Color(1, .8f, .4f);
            if (double.IsNaN(metric.value)) return new Color(1, .45f, .4f);
            return double.IsNaN(metric.previous) || (metric.value - metric.previous) * metric.direction > 0 ? new Color(.3f, 1, .55f) : new Color(1, .45f, .4f);
        }
        void DrawSummary(float width, string slot)
        {
            BuildSummary(slot);
            if (quickLabel == null) { quickLabel = new GUIStyle(muted) { fontSize = 11 }; quickValue = new GUIStyle(prose) { fontSize = 13, fontStyle = FontStyle.Bold }; quickTile=new GUIStyle(GUI.skin.box){padding=new RectOffset(6,6,2,2),margin=new RectOffset(2,2,2,2)}; }
            SummaryForVerification.Clear();
            GUILayout.BeginVertical();
            GUILayout.Label(comparison ? (healthComparison?"QUICK SUMMARY / current → no wear":"QUICK SUMMARY / current → preview") : "QUICK SUMMARY / saved car");
            DrawSummaryTiles(coreSummary, width);
            if (comparison) {
                if (changedSummary.Count > 0) {
                    GUILayout.Label("OTHER CHANGES / green better · red worse · amber tradeoff", muted);
                    DrawSummaryTiles(changedSummary.Take(4).ToList(), width);
                    if (changedSummary.Count > 4) GUILayout.Label("+ " + (changedSummary.Count - 4) + " more changes in the details below.", muted);
                } else GUILayout.Label("No other summarized values change; check component descriptions for function/sound differences.", muted);
            } else GUILayout.Label("Lowest part health " + after.condition.ToString("P0") + " / " + after.transmissionName + " / " + after.source.build.spec.drive, muted);
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) { var rect = GUILayoutUtility.GetLastRect(); SummaryRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00); }
        }
        void DrawSummaryTiles(List<QuickStat> metrics, float width)
        {
            for (int i = 0; i < metrics.Count; i += 2) {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + 2, metrics.Count); j++) {
                    var metric = metrics[j]; string value = SummaryValue(metric);
                    SummaryForVerification.Add(metric.label + ": " + value);
                    GUILayout.BeginVertical(quickTile, GUILayout.Width(Mathf.Max(100, (width - 24) / 2)));
                    GUILayout.Label(metric.label, quickLabel);
                    var color = GUI.contentColor; GUI.contentColor = SummaryColor(metric); GUILayout.Label(value, quickValue); GUI.contentColor = color;
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
