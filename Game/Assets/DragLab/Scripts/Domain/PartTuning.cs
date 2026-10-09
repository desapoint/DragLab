using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    [Flags]
    public enum PartTuningOptions { None = 0, Boost = 1, Pulley = 2, Valve = 4, FinalDrive = 8, Limiter = 16, TwoStep = 32, ShiftLight = 64, CamTiming = 128 }

    // Capabilities belong to definitions, not price, quality or upgrade rank.
    public static class PartTuning
    {
        public static bool Allows(PartDefinition part, PartTuningOptions option) { return (part.tuning & option) != 0; }
        public static string Summary(PartDefinition part)
        {
            var labels = new List<string>();
            if(part.camTimingAdjustable)labels.Add("cam timing +/-6 crank degrees");
            if (Allows(part, PartTuningOptions.Boost)) labels.Add("boost target " + part.boostMin.ToString("F2") + "–" + part.boostMax.ToString("F2") + "×");
            if (Allows(part, PartTuningOptions.Pulley)) labels.Add("pulley drive " + part.pulleyMin.ToString("F2") + "–" + part.pulleyMax.ToString("F2") + "×");
            if (Allows(part, PartTuningOptions.Valve)) labels.Add("pressure-release valve setup");
            if (Allows(part, PartTuningOptions.FinalDrive)) labels.Add("interchangeable final-drive gearsets " + part.finalDriveMin.ToString("F2") + "–" + part.finalDriveMax.ToString("F2"));
            if (Allows(part, PartTuningOptions.Limiter)) labels.Add("rev limiter threshold / cut");
            if (Allows(part, PartTuningOptions.TwoStep)) labels.Add("two-step threshold / cut");
            if (Allows(part, PartTuningOptions.ShiftLight)) labels.Add("shift-light threshold");
            return labels.Count == 0 ? "Fixed hardware: no tuning unlocked by this part." : "Adjustable: " + string.Join(", ", labels) + ". Other hardware properties stay fixed.";
        }
        public static void Project(PartDefinition p, Tune t, VehicleSpec stock)
        {
            if (p.category == PartCategory.Induction) {
                t.partTurboSize = p.turboSize;
                t.partFixedBoost = p.fixedBoost; t.partBoostAdjustable = Allows(p, PartTuningOptions.Boost);
                t.partBoostMin = p.boostMin; t.partBoostMax = p.boostMax;
                t.partFixedPulley = p.fixedPulley; t.partPulleyAdjustable = Allows(p, PartTuningOptions.Pulley);
                t.partPulleyMin = p.pulleyMin; t.partPulleyMax = p.pulleyMax;
                t.partValveAdjustable = Allows(p, PartTuningOptions.Valve); t.partFixedValve = p.valve;
            }
            if (p.category == PartCategory.Gearbox) {
                t.partFinalDriveAdjustable = Allows(p, PartTuningOptions.FinalDrive);
                t.partFixedFinalDrive = p.fixedFinalDrive > 0 ? p.fixedFinalDrive : stock.finalDrive;
                t.partFinalDriveMin = p.finalDriveMin; t.partFinalDriveMax = p.finalDriveMax;
            }
        }
        public static void Enforce(Tune t)
        {
            if (!t.partsResolved || !t.partTuningConfigured) return; // Raw engineering/sound bench remains unrestricted.
            t.turboSize = t.partTurboSize;
            t.boostSetting = t.partBoostAdjustable ? Clamp(t.BoostSetting, t.partBoostMin, t.partBoostMax) : t.partFixedBoost;
            t.superchargerPulley = t.partPulleyAdjustable ? Clamp(t.SuperchargerPulley, t.partPulleyMin, t.partPulleyMax) : t.partFixedPulley;
            t.finalDrive = t.partFinalDriveAdjustable ? Clamp(t.finalDrive, t.partFinalDriveMin, t.partFinalDriveMax) : t.partFixedFinalDrive;
            if (!t.partValveAdjustable) t.blowOffValve = t.partFixedValve;
        }
        static float Clamp(double v, float min, float max) { return (float)DragMath.Clamp(v, min, max); }
    }
}
