using System;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool ApplyNativeSettings(DriverSettings driver, int ai, UnitPreferences units, float master, float opponent, out string error, bool? reverseTouchShifting=null)
        {
            error = null;
            if (screen != "Settings" || pendingAction != null) { error = "Settings are no longer open."; return false; }
            if(driver.guaranteedFittingParts<0||driver.guaranteedFittingParts>20){error="Market guarantee must be 0–20 offers.";return false;}
            if (ai < 0 || ai > 2 || float.IsNaN(driver.clutchReleaseSeconds) || float.IsInfinity(driver.clutchReleaseSeconds) || driver.clutchReleaseSeconds < .02f || driver.clutchReleaseSeconds > .6f)
            { error = "Clutch release must be 0.02–0.60 seconds."; return false; }
            foreach (var field in typeof(DriverSettings).GetFields())
                if (field.FieldType.IsEnum && !Enum.IsDefined(field.FieldType, field.GetValue(driver))) { error = "Invalid " + field.Name; return false; }
            foreach (var field in typeof(UnitPreferences).GetFields())
                if (field.FieldType.IsEnum && !Enum.IsDefined(field.FieldType, field.GetValue(units))) { error = "Invalid " + field.Name; return false; }
            var candidate = profile.CopyWithPreferences(driver,ai);
            candidate.catalog = profile.catalog; candidate.driving = driver.Copy(); candidate.difficulty = ai;
            if (!PreferenceCommit.TryApply(candidate, units, master, opponent, out error, reverseTouchShifting)) return false;
            profile = candidate; settings = profile.driving; difficulty = ai; saveDelay = 0;drivingPreferencesDirty=false;
            // A running race owns its captured driver rules; menu edits do not
            // reconstruct its cars, damage, resources or entry contract.
            return true;
        }
        internal long BeginNativeReset() { resetGameRevision = profile.revision; resetGameConfirm = true; return resetGameRevision; }
        internal void CancelNativeReset() { resetGameConfirm = false; }
        internal string NativeNotice => notice;
    }
}
