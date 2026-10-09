using System;
using UnityEngine;
namespace DragLab.Runtime
{
    public sealed partial class PhysicalDrivingBench
    {
        internal readonly System.Collections.Generic.Dictionary<string,Rect> massRects = new System.Collections.Generic.Dictionary<string,Rect>();
        internal void MassPanelForVerification(float scroll) { showDiagnostics = true; diagnosticsScroll = new Vector2(0,scroll); }
        bool MassSlider(string label, ref float value, float min, float max, string format, float initial) { GUILayout.Label(label + " " + value.ToString(format)); bool changed = TuningSlider.Draw(ref value, min, max, initial); if (Event.current.type == EventType.Repaint) massRects[label] = RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect()); return changed; }
        void MassControls()
        {
            var car = Session.car; var m = car.mass;
            GUILayout.Label("MASS / installed + service fluids " + m.dryInstalledKg.ToString("F1") + " kg / driver " + m.driverKg.ToString("F1") + " / payload " + m.payloadKg.ToString("F1") + " / fuel " + m.fuelKg.ToString("F1") + " / total " + m.TranslatingKg.ToString("F1"));
            GUILayout.Label("CG " + m.centerOfGravityFromFrontM.ToString("F2") + " m from front / height " + m.centerOfGravityHeightM.ToString("F2") + " m / static axle loads " + m.frontNormalLoadN.ToString("F0") + " : " + m.rearNormalLoadN.ToString("F0") + " N");
            FuelControls(); FluidControls();
            float driver = car.tune.driverMassKg, payload = car.tune.payloadMassKg, position = car.tune.payloadPositionM, height = car.tune.payloadHeightM, fuel = (float)car.state.fuelLitres;
            bool changed = false;
            changed |= MassSlider("Test driver (kg)", ref driver, 0, 200, "F0", 75);
            changed |= MassSlider("Test ballast (kg)", ref payload, 0, 1000, "F0", 0);
            changed |= MassSlider("Ballast position (m)", ref position, -.5f, car.spec.wheelbaseM + .5f, "F2", 1.5f);
            changed |= MassSlider("Ballast height (m)", ref height, 0, 2, "F2", .45f);
            changed |= MassSlider("Test fuel mass input (litres)", ref fuel, 0, car.spec.fuel.capacityLitres, "F1", car.spec.fuel.capacityLitres);
            if (changed) { Archive(); car.SetMassInputs(driver, payload, position, height, fuel); config.startingFuel = car.FuelSnapshot(); config.tune = car.tune.Copy(); Session = Session.ContinueRecording(Recording); notice = "Mass changed; movement and pedal source retained. Recorded segment continued from a physical checkpoint."; }
        }
    }
}
