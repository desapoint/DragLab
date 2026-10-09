using System;
using System.IO;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator TuningGesturesRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("Tuning slider gestures / actual Windows mouse / isolated save.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            ProfileStore.Save(game.Profile);
            string bytes = File.ReadAllText(ProfileStore.FilePath), saved = JsonUtility.ToJson(game.Profile.SelectedCar.tune);
            using (var mouse = new WindowsKeyboardProbe())
            {
                game.OpenTuning(); yield return new WaitForSecondsRealtime(.3f);
                require(!game.DraftDirty, "Opening tuning leaves the saved car untouched.");
                yield return MoveTuneSlider(mouse, "radius", .65f);
                require(game.DraftValues.WheelDiameter > 1.25 && game.DraftValues.WheelDiameter < 1.4, "Normal left-drag still adjusts the outer tire radius.");
                yield return FineTuneRadius(mouse, require);
                require(game.DraftDirty && JsonUtility.ToJson(game.Profile.SelectedCar.tune) == saved && File.ReadAllText(ProfileStore.FilePath) == bytes, "Fine tuning edits only the live draft; installed tune and disk remain unchanged.");
                yield return ResetTuneSlider(mouse, "radius");
                require(Math.Abs(game.DraftValues.WheelDiameter - 1) < 1e-5, "Middle-click restores the stock tire radius.");
                yield return MoveTuneSlider(mouse, "radius", .65f);
                float applied = (float)game.DraftValues.WheelDiameter;
                yield return ClickTune(mouse, "apply-top");
                require(!game.DraftDirty && Math.Abs(ProfileStore.Load().SelectedCar.tune.WheelDiameter - applied) < 1e-5, "Apply commits the adjusted tune durably.");
                yield return ResetTuneSlider(mouse, "radius");
                require(game.DraftDirty && Math.Abs(game.DraftValues.WheelDiameter - 1) < 1e-5 && Math.Abs(game.Profile.SelectedCar.tune.WheelDiameter - applied) < 1e-5, "Reset targets the stock default after saving another value, and still requires Apply.");
                yield return ClickTune(mouse, "discard-top");
                require(!game.DraftDirty && Math.Abs(game.DraftValues.WheelDiameter - applied) < 1e-5, "Revert discards a middle-click reset and restores the saved value.");
                foreach (PressureUnit unit in new[] { PressureUnit.Bar, PressureUnit.Psi })
                {
                    SharedUnitSettings.Set(SpeedUnit.KilometresPerHour, unit);
                    game.DraftValues.tirePressurePsi = 24; yield return new WaitForSecondsRealtime(.15f);
                    game.ScrollTuningTo("pressure"); yield return new WaitForSecondsRealtime(.15f);
                    yield return ResetTuneSlider(mouse, "pressure");
                    require(Math.Abs(game.DraftValues.tirePressurePsi - 30) < .001, "Pressure reset restores 30 psi with " + unit + " display units.");
                }
                yield return Capture("tuning-gestures-pressure-reset");
                game.ReturnToGarage(); game.OpenTuning(); yield return new WaitForSecondsRealtime(.15f);
                require(!game.DraftDirty && Math.Abs(game.DraftValues.WheelDiameter - applied) < 1e-5, "Leaving discards unapplied reset changes.");
                game.StartPractice();
                require(Math.Abs(game.Race.player.tune.WheelDiameter - applied) < 1e-5, "Practice uses the applied tune."); game.ReturnToGarage();
                require(errors == 0, "Gesture UI has no runtime/GUI/DSP errors.");
            }
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Mouse gesture probe requires Windows development player.");
#endif
        }
    }
}
