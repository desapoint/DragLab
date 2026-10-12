using System;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        VisualElement OwnedCarDyno(OwnedCar car,CarInspection comparedTo=null)
        {
            var panel = new VisualElement { name = "ownedCarDyno", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("car-dyno");
            if (string.IsNullOrEmpty(car.engineAssemblyId)) {
                var missing = new Label("No engine attached"); missing.AddToClassList("car-warning"); panel.Add(missing); return panel;
            }
            try {
                var cached=InspectOwned(car);if(cached==null)throw new InvalidOperationException("Inspection unavailable");
                var stats=cached.Stats;
                if(comparedTo!=null){panel.Add(ComparisonDyno(comparedTo,stats,"ownedDynoPlot"));return panel;}
                var healthy=OwnedHealthy(car);
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
