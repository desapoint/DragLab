using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator InspectionRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-capture-only") >= 0)
            {
                // Visual fixtures set isolated preview state; they never send OS input or test mouse controls.
                game.PreviewPart(); game.ShowParts(PartCategory.HeadIntake);
                game.PreviewPart(game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId, PartCategory.HeadIntake, 3));
                yield return new WaitForSecondsRealtime(.2f); yield return Capture("stats-part-preview");
                game.OpenTuning(); game.DraftValues.wheelDiameter = (float)(14 * DisplayUnits.MetresPerInch / game.SavedInspection.source.build.spec.wheelRadiusM);
                yield return new WaitForSecondsRealtime(.2f); yield return Capture("tire-radius-14in");
                game.DraftValues.wheelDiameter = (float)(18 * DisplayUnits.MetresPerInch / game.SavedInspection.source.build.spec.wheelRadiusM);
                yield return new WaitForSecondsRealtime(.2f); yield return Capture("tire-radius-18in");
                game.DraftValues.wheelDiameter = 1.48f;
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("stats-tuning-draft");
                game.ScrollInspectionTo(440); yield return new WaitForSecondsRealtime(.2f); yield return Capture("stats-mass-tradeoff-bars");
                game.ScrollInspectionTo(0); game.ReturnToGarage(); SharedUnitSettings.Set(SpeedUnit.MilesPerHour, PressureUnit.Psi, TorqueUnit.PoundFeet);
                game.ShowDisplayUnitsForInspection(); yield return new WaitForSecondsRealtime(.2f); yield return Capture("stats-unit-settings");
                game.ReturnToGarage(); yield return new WaitForSecondsRealtime(.2f); yield return Capture("stats-imperial-units");
                Check(errors == 0, "Inspection rendering fixture has no runtime errors.");
                File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "PASS: Inspection rendering fixture / isolated previews and unit preferences / no OS input. Mouse controls were not tested by this fixture.\nRuntime errors: " + errors + "\n");
                yield break;
            }
            var log = new System.Text.StringBuilder("Car stats / previews / draft tuning / actual Windows mouse / isolated save.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            game.Profile.cash = 100000; ProfileStore.Save(game.Profile); yield return new WaitForSecondsRealtime(.25f);
            string savedBytes = File.ReadAllText(ProfileStore.FilePath), installedTune = JsonUtility.ToJson(game.Profile.SelectedCar.tune);
            float savedVisualSize = game.CarViewForVerification.TireSizeScale;
            yield return Capture("stats-saved-car");
            require(game.SavedInspection != null && game.ProposedInspection == null && game.SavedInspection.completed, "Garage automatically shows full saved stats and dyno without a comparison.");
            game.ShowParts(PartCategory.HeadIntake); yield return new WaitForSecondsRealtime(.3f);
            string head = game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId, PartCategory.HeadIntake, 3);
            using (var mouse = new WindowsKeyboardProbe())
            {
                yield return ClickPart(mouse, "preview:" + head);
                yield return Capture("stats-click-diagnostic");
                File.WriteAllText(Path.Combine(output, "preview-diagnostic.txt"), "proposed=" + (game.ProposedInspection == null ? "null" : game.ProposedInspection.peakHp.ToString("R")) + ";saved=" + game.SavedInspection.peakHp.ToString("R") + ";mouse=" + Input.mousePosition + ";events=" + game.AuditMouseEvents + ";diskSame=" + (File.ReadAllText(ProfileStore.FilePath) == savedBytes));
                require(game.ProposedInspection != null && game.ProposedInspection.peakHp > game.SavedInspection.peakHp && File.ReadAllText(ProfileStore.FilePath) == savedBytes, "Actual part Preview shows increased dyno power without buying, installing or saving anything.");
                yield return Capture("stats-part-preview");
                game.OpenTuning(); yield return new WaitForSecondsRealtime(.25f);
                require(!game.DraftDirty, "Opening tuning observes saved values without creating an accidental draft.");
                yield return MoveTuneSlider(mouse, "radius", .85f);
                yield return Capture("stats-slider-check");
                require(game.DraftDirty && game.DraftValues.WheelDiameter > 1.3 && JsonUtility.ToJson(game.Profile.SelectedCar.tune) == installedTune && File.ReadAllText(ProfileStore.FilePath) == savedBytes, "Actual outer-radius slider changes only the preview, not saved tune or disk. [dirty=" + game.DraftDirty + ", diameter=" + game.DraftValues.WheelDiameter + ", savedTuneUnchanged=" + (JsonUtility.ToJson(game.Profile.SelectedCar.tune) == installedTune) + ", diskUnchanged=" + (File.ReadAllText(ProfileStore.FilePath) == savedBytes) + "]");
                require(game.ProposedInspection.wheelRadiusIn > game.SavedInspection.wheelRadiusIn && game.ProposedInspection.warnings.Any(w => w.title.Contains("Large/heavy")), "Live preview exposes larger-tire mass/gearing changes and launch advice.");
                require(game.CarViewForVerification.TireSizeScale > savedVisualSize * 1.3f, "Actual tire slider visibly enlarges wheel layers in the draft preview.");
                yield return Capture("stats-tuning-draft");
                game.ScrollInspectionTo(440); yield return new WaitForSecondsRealtime(.1f); yield return Capture("stats-mass-tradeoff-bars"); game.ScrollInspectionTo(0);
                game.ReturnToGarage(); game.OpenTuning(); yield return new WaitForSecondsRealtime(.2f);
                require(Math.Abs(game.CarViewForVerification.TireSizeScale - savedVisualSize) < 1e-5, "Leaving tuning restores the saved wheel geometry.");
                require(!game.DraftDirty && JsonUtility.ToJson(game.DraftValues) == installedTune, "Leaving and reentering tuning discards the uncommitted slider values.");
                yield return MoveTuneSlider(mouse, "radius", .65f);
                float appliedDiameter = (float)game.DraftValues.WheelDiameter;
                yield return ClickTune(mouse, "apply-top");
                require(!game.DraftDirty && Math.Abs(game.Profile.SelectedCar.tune.WheelDiameter - appliedDiameter) < 1e-5 && ProfileStore.Load().SelectedCar.tune.wheelDiameter == game.Profile.SelectedCar.tune.wheelDiameter, "Actual Apply atomically commits the draft and durable saved values.");
                yield return Capture("stats-tuning-applied");
                game.StartPractice(); require(Math.Abs(game.CarViewForVerification.TireSizeScale - appliedDiameter) < 1e-5, "Applied wheel geometry carries into the actual practice race."); require(Math.Abs(game.Race.player.tune.WheelDiameter - appliedDiameter) < 1e-5, "A real practice race uses the applied wheel size.");
                game.ReturnToGarage(); game.OpenTuning(); yield return new WaitForSecondsRealtime(.2f);
                yield return MoveTuneSlider(mouse, "radius", .05f);
                require(game.DraftDirty, "A subsequent edit creates an independent draft.");
                game.StartPractice(); require(Math.Abs(game.Race.player.tune.WheelDiameter - appliedDiameter) < 1e-5 && game.DraftValues == null, "Starting a race discards unapplied edits and keeps the previously saved size."); game.ReturnToGarage();
                game.ShowParts(PartCategory.HeadIntake); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse, "fit:" + head);
                require(game.Profile.catalog.Find(game.Profile.parts.Find(p => p.instanceId == game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "HeadIntake").partInstanceId).definitionId).stage == 3 && game.ProposedInspection == null && game.SavedInspection.peakHp > 140, "Actual shop Buy + install immediately saves the part and updates the baseline stats.");
                string beforeUnits = JsonUtility.ToJson(game.Profile.SelectedCar.tune);
                game.ShowDisplayUnitsForInspection(); yield return new WaitForSecondsRealtime(.3f);
                yield return Capture("stats-unit-settings-before");
                yield return ClickUnit(mouse, true, 1); yield return ClickUnit(mouse, false, 1);
                yield return ClickTorqueUnit(mouse, 1);
                require(SharedUnitSettings.Current.torque == TorqueUnit.PoundFeet && JsonUtility.ToJson(game.Profile.SelectedCar.tune) == beforeUnits, "Actual torque Settings button selects lb-ft without changing the tune. [torque="+SharedUnitSettings.Current.torque+"; tune unchanged="+(JsonUtility.ToJson(game.Profile.SelectedCar.tune)==beforeUnits)+"]");
                require(SharedUnitSettings.Current.speed == SpeedUnit.MilesPerHour && SharedUnitSettings.Current.pressure == PressureUnit.Psi, "Actual Settings buttons select mph and psi without altering the car.");
                yield return Capture("stats-unit-settings");
                game.ReturnToGarage();
            }
            SharedUnitSettings.Reload(); yield return new WaitForSecondsRealtime(.2f);
            require(SharedUnitSettings.Speed(100 / 3.6).Contains("mph") && SharedUnitSettings.Boost(.7).Contains("psi") && SharedUnitSettings.Boost(.7).Contains("bar"), "Shared units persist and boost displays bar plus psi.");
            require(SharedUnitSettings.Current.torque == TorqueUnit.PoundFeet && game.StatsSummaryForVerification.Any(value => value.StartsWith("Torque:") && value.Contains("lb-ft")), "Torque preference persists after reload and updates the live garage summary.");
            yield return Capture("stats-imperial-units");
            require(errors == 0, "Inspection/tuning/shop UI has no runtime/GUI/DSP errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Inspection UI probe requires Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator ResetTuneSlider(WindowsKeyboardProbe mouse, string key)
        {
            yield return new WaitForEndOfFrame(); Rect rect = game.tuneRects[key];
            Check(rect.center.y >= 125 && rect.center.y < Screen.height - 90, "Reset slider is visible: " + key);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true, 2); yield return new WaitForSecondsRealtime(.06f);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false, 2); yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator FineTuneRadius(WindowsKeyboardProbe mouse, Action<bool, string> require)
        {
            yield return new WaitForEndOfFrame(); Rect rect = game.tuneRects["radius"];
            float before = (float)game.DraftValues.WheelDiameter;
            int x = (int)(rect.x + rect.width * .1f), y = (int)rect.center.y;
            mouse.MouseButtonAt(x, y, true, 1); yield return new WaitForSecondsRealtime(.08f);
            require(Math.Abs(game.DraftValues.WheelDiameter - before) < 1e-5, "Right-click away from the thumb begins relative fine tuning without a value jump.");
            for (int step = 1; step <= 5; step++) { mouse.MoveMouseAt(x + step * 20, y); yield return new WaitForSecondsRealtime(.04f); }
            mouse.MouseButtonAt(x + 100, y, false, 1); yield return new WaitForSecondsRealtime(.2f);
            float expected = .8f * 100 / Mathf.Max(1, rect.width - GUIThumbWidthForProbe) * .1f;
            require(Math.Abs((game.DraftValues.WheelDiameter - before) - expected) < .001, "A 100-pixel right-drag uses one-tenth normal sensitivity and updates the dyno preview.");
            float after = (float)game.DraftValues.WheelDiameter;
            mouse.MoveMouseAt(x + 200, y); yield return new WaitForSecondsRealtime(.15f);
            require(Math.Abs(game.DraftValues.WheelDiameter - after) < 1e-5, "Releasing right-click ends fine tuning; moving the mouse no longer edits.");
        }
        // Unity's default runtime slider thumb is ten pixels wide in this skin.
        const float GUIThumbWidthForProbe = 10;
        IEnumerator ClickTorqueUnit(WindowsKeyboardProbe mouse, int choice)
        {
            yield return new WaitForEndOfFrame(); Rect rect = UnitSettingsView.TorqueRect;
            Check(rect.center.y >= 125 && rect.center.y < Screen.height - 70, "Torque unit buttons are visible.");
            int x = (int)(rect.x + rect.width * (choice + .5f) / 2), y = (int)rect.center.y;
            mouse.MouseButtonAt(x, y, true); yield return new WaitForSecondsRealtime(.06f); mouse.MouseButtonAt(x, y, false); yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator ClickUnit(WindowsKeyboardProbe mouse, bool speed, int choice)
        {
            yield return new WaitForEndOfFrame(); Rect rect = speed ? UnitSettingsView.SpeedRect : UnitSettingsView.PressureRect;
            Check(rect.center.y >= 125 && rect.center.y < Screen.height - 70, "Unit buttons are visible.");
            int x = (int)(rect.x + rect.width * (choice + .5f) / 3), y = (int)rect.center.y;
            mouse.MouseButtonAt(x, y, true); yield return new WaitForSecondsRealtime(.06f); mouse.MouseButtonAt(x, y, false); yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator ClickTune(WindowsKeyboardProbe mouse, string key)
        {
            yield return new WaitForEndOfFrame(); Rect rect = game.tuneRects[key];
            if (rect.center.y < 125 || rect.center.y > Screen.height - 90) throw new InvalidOperationException("Tune control not visible: " + key);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f); mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator MoveTuneSlider(WindowsKeyboardProbe mouse, string key, float fraction)
        {
            yield return new WaitForEndOfFrame(); Rect rect = game.tuneRects[key];
            if (rect.center.y < 125 || rect.center.y > Screen.height - 90) throw new InvalidOperationException("Tune slider not visible: " + key);
            // A Unity slider track click pages the value. Drag its current thumb to test a precise value.
            float current = key == "boost" ? Mathf.InverseLerp(.8f, 1.2f, (float)game.DraftValues.BoostSetting) : Mathf.InverseLerp(.8f, 1.6f, (float)game.DraftValues.WheelDiameter);
            int start = (int)(rect.x + 6 + (rect.width - 12) * current), y = (int)rect.center.y;
            int target = (int)(rect.x + 6 + (rect.width - 12) * fraction);
            mouse.MouseButtonAt(start, y, true); yield return new WaitForSecondsRealtime(key == "boost" ? .2f : .07f);
            for (int step = 1; step <= 5; step++) { mouse.MoveMouseAt((int)Mathf.Lerp(start, target, step / 5f), y); yield return new WaitForSecondsRealtime(key == "boost" ? .12f : .04f); }
            mouse.MouseButtonAt(target, y, false); yield return new WaitForSecondsRealtime(.3f);
        }
#endif
    }
}
