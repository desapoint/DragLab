using System;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        VisualElement OwnedCarDyno(OwnedCar car)
        {
            var panel = new VisualElement { name = "ownedCarDyno", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("car-dyno");
            if (string.IsNullOrEmpty(car.engineAssemblyId)) {
                var missing = new Label("No engine attached"); missing.AddToClassList("car-warning"); panel.Add(missing); return panel;
            }
            try {
                var source = InspectionBuild.Capture(game.Profile,car);
                var stats = CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);
                var healthySource = InspectionBuild.Capture(game.Profile,car,healthy:true);
                var healthy = CarInspection.CalculatePrepared(healthySource,CarInspection.PrepareCar(healthySource),false);
                TorqueUnit units = SharedUnitSettings.Current.torque;
                panel.Add(new NativeDynoPanel(new[] {
                    new NativeDynoSeries("Current","Current",stats,NativeDynoStyle.Primary),
                    new NativeDynoSeries("NoWear","No wear",healthy,NativeDynoStyle.Reference)
                },units,"ownedDynoPlot"));
                panel.tooltip = "Estimated engine output at full load and steady boost. Separate HP and torque scales; applied hardware, tune and condition. Dashed curves remove part wear only for comparison; stored fluids and missing hardware stay. No race or recording.";
            } catch (Exception) { var unavailable = new Label("Dyno unavailable"); unavailable.AddToClassList("car-warning"); panel.Add(unavailable); }
            return panel;
        }
    }
}
