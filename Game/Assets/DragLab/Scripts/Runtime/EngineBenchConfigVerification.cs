using System;
using System.Collections;
using System.IO;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Runtime
{
    public sealed partial class EngineBenchRuntimeVerification
    {
        IEnumerator ConfigWhileDrivingRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            scene.SelectVehicle(Array.FindIndex(scene.vehicles, v => v.spec.id == "starter-hatch"));
            scene.verificationKeyboard = true;
            using (var input = new WindowsKeyboardProbe())
            {
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.3f);
                Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "Held OS W owns driving before hardware edits.");
                scene.ShowTestTab(2); yield return new WaitForSecondsRealtime(.2f);
                Rect rect = scene.VerificationFieldControl("induction.factory");
                yield return ConfigClick(input, GridCell(rect, (int)InductionType.TwinTurbo, Enum.GetNames(typeof(InductionType)).Length, 3));
                Check(scene.config.vehicle.induction.factory == InductionType.TwinTurbo && scene.Bench.induction.type == InductionType.TwinTurbo, "Actual turbo-type mouse selection applies during keyboard driving.");
                Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1 && scene.Bench.state.rpm > 0, "Delayed hardware apply preserves keyboard ownership, held gas and rotating engine.");
                yield return new WaitForSecondsRealtime(.4f);
                Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "Further repaints cannot switch driving back to slider values.");
                scene.ShowTestTab(4); yield return new WaitForSecondsRealtime(.15f);
                rect = scene.VerificationFieldControl("transmission.type");
                yield return ConfigClick(input, GridCell(rect, (int)TransmissionType.DCT, Enum.GetNames(typeof(TransmissionType)).Length, 3));
                Check(scene.Bench.transmission.type == TransmissionType.DCT && scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "Actual gearbox hardware selection leaves keyboard pedals active.");
                rect = scene.VerificationFieldControl("transmission.type");
                yield return ConfigClick(input, GridCell(rect, (int)TransmissionType.Manual, Enum.GetNames(typeof(TransmissionType)).Length, 3));
                rect = scene.VerificationFieldControl("transmission.manualLayout");
                yield return ConfigClick(input, GridCell(rect, (int)ManualGearboxLayout.Sequential, 2, 3));
                Check(scene.Bench.transmission.type == TransmissionType.Manual && scene.Bench.transmission.manualLayout == ManualGearboxLayout.Sequential && scene.Bench.state.throttle == 1 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Actual sequential-manual hardware control applies while keyboard gas remains held.");
                Check(TransmissionDynamics.ShiftSeconds(scene.Bench.transmission, scene.Bench.spec, scene.Bench.tune) < scene.Bench.spec.shiftSeconds * scene.Bench.tune.ShiftMultiplier, "Sound bench uses the shared faster sequential manual shift duration.");
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "sequential-manual-hardware.png"));
                scene.ShowTestTab(5); yield return new WaitForSecondsRealtime(.15f);
                rect = scene.VerificationMixSlider("engineGain"); float oldGain = scene.Bench.spec.sound.engineGain;
                yield return ConfigClick(input, new Rect(rect.x + rect.width * .2f - 2, rect.center.y - 2, 4, 4));
                Check(Math.Abs(scene.Bench.spec.sound.engineGain - oldGain) > .05 && scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "Actual Mix slider applies without changing pedal ownership or cancelling W.");
                rect = scene.VerificationFieldControl("sound.engineGain:text");
                yield return ConfigClick(input, rect);
                Check(scene.IsEditingText && scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "A hardware numeric field can receive focus while keyboard gas stays held.");
                input.Key(0x57, false); yield return new WaitForSecondsRealtime(.2f);
                Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 0, "Releasing W during hardware editing stops gas without restoring old slider throttle.");
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.15f);
                Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1 && !scene.IsEditingText, "W from hardware numeric focus resumes driving without a lost press.");
                yield return new WaitForEndOfFrame();
                rect = scene.verificationVehicleRect;
                int pickup = Array.FindIndex(scene.vehicles, v => v.spec.id == "utility-pickup");
                yield return ConfigClick(input, GridCell(rect, pickup, scene.vehicles.Length, 2));
                Check(scene.Bench.spec.id == "utility-pickup" && scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1, "Actual engine/car selection retains keyboard driving and held gas on the fresh test engine.");
                scene.ShowTestTab(0); yield return new WaitForSecondsRealtime(.15f);
                float assigned = scene.config.controls.throttle;
                var slider = scene.VerificationFieldControl("controls.engineEnabled");
                // The independent Drive options are below the pedal fields: scroll
                // their panel to reach the same actual mouse controls.
                scene.ScrollTestToBottom(); yield return new WaitForSecondsRealtime(.2f);
                slider = scene.VerificationFieldControl("controls.engineEnabled");
                yield return ConfigClick(input, new Rect(slider.x + 4, slider.center.y - 2, 4, 4));
                Check(!scene.config.controls.engineEnabled && scene.Bench.state.rpm == 0 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Engine on/off remains an editable independent Drive option in keyboard mode.");
                yield return ConfigClick(input, new Rect(slider.x + 4, slider.center.y - 2, 4, 4));
                Check(scene.config.controls.engineEnabled && scene.Bench.state.rpm > 0 && scene.Bench.state.throttle == 1 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Re-enabling the test engine resumes held keyboard gas without a mode switch.");
                scene.ScrollTestToTop(); yield return new WaitForSecondsRealtime(.2f);
                rect = scene.VerificationDriveSlider("throttle");
                yield return ConfigClick(input, new Rect(rect.x + rect.width * .3f - 2, rect.center.y - 2, 4, 4));
                Check(scene.config.controls.throttle == assigned && scene.Bench.state.throttle == 1 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Keyboard-controlled pedal slider remains feedback-only and cannot override held gas.");
                scene.ShowTestTab(2); yield return new WaitForSecondsRealtime(.2f);
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "keyboard-unlocked-induction.png"));
                input.Key(0x57, false); yield return new WaitForSecondsRealtime(.2f);
                Check(scene.Bench.state.throttle == 0 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Final gas release keeps keyboard mode with zero throttle after every hardware edit.");
            }
#else
            yield return null; throw new InvalidOperationException("Live configuration probe requires a Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        static Rect GridCell(Rect grid, int index, int count, int columns)
        {
            int rows = (count + columns - 1) / columns;
            float w = grid.width / columns, h = grid.height / rows;
            return new Rect(grid.x + index % columns * w, grid.y + index / columns * h, w, h);
        }
        IEnumerator ConfigClick(WindowsKeyboardProbe input, Rect rect)
        {
            Check(rect.width > 0 && rect.center.y > 0 && rect.center.y < Screen.height, "Configuration control is visible for OS mouse input.");
            input.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f);
            input.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.35f);
        }
#endif
    }
}
