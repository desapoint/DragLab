using System.Collections.Generic;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        readonly HashSet<string> ownedStatsShown=new HashSet<string>();
        readonly Dictionary<string,bool> ownedStatsCollapsed=new Dictionary<string,bool>();
        readonly List<RacingSurface> ownedStatsSurfaces=new List<RacingSurface>();
        void BuildOwnedStats(VisualElement top,OwnedCar car)
        {
            var inspection=top.Q<NativeDynoPlot>("ownedDynoPlot")?.Series[0].Inspection;
            NativeStatsPanel panel=null;
            var button=new Button {name="ownedStatsToggle",text="Stats",tooltip=inspection==null?"Engine stats unavailable":"Show engine stats"};
            button.AddToClassList("action-button");button.AddToClassList("car-stats-button");button.Add(new FilledMenuIcon("Data"));
            top.Q(className:"car-summary").Add(button);DecorateOwnedStats(button,RacingSurface.Kind.SecondaryAction);
            button.SetEnabled(inspection!=null);
            if(inspection==null)return;
            panel=new NativeStatsPanel(inspection,SharedUnitSettings.Current,new[] {NativeStatCatalog.Engine},ownedStatsCollapsed,
                definition=>OpenDialog(definition.Label,definition.Help+(definition.Number!=null?"\nBar endpoints are display ranges, not safe operating limits.":""),new[] {new DialogAction("Close",CloseDialog)}),DecorateOwnedStats) {name="ownedCarStats"};
            ownedDetailContent.Add(panel);
            void UpdateVisibility(){bool visible=ownedStatsShown.Contains(car.instanceId);panel.EnableInClassList("hidden",!visible);button.EnableInClassList("selected",visible);button.tooltip=visible?"Hide engine stats":"Show engine stats";}
            button.clicked+=()=>{if(!ownedStatsShown.Add(car.instanceId))ownedStatsShown.Remove(car.instanceId);UpdateVisibility();};UpdateVisibility();
        }
        void DecorateOwnedStats(VisualElement element,RacingSurface.Kind kind)
        { AddSurface(element,kind);ownedStatsSurfaces.Add(element.Q<RacingSurface>()); }
    }
}
