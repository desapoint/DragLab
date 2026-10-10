using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        readonly Dictionary<string,Task<CarInspection>> ownedDrivingEstimates=new Dictionary<string,Task<CarInspection>>();
        void BuildOwnedStats(VisualElement top,OwnedCar car)
        {
            var inspection=top.Q<NativeDynoPlot>("ownedDynoPlot")?.Series[0].Inspection;
            NativeStatsPanel panel=null;
            var button=new Button {name="ownedStatsToggle",text="Stats",tooltip=inspection==null?"Stats unavailable":"Show car stats"};
            button.AddToClassList("action-button");button.AddToClassList("car-stats-button");button.Add(new FilledMenuIcon("Data"));
            top.Q(className:"car-summary").Add(button);DecorateOwnedStats(button,RacingSurface.Kind.SecondaryAction);
            button.SetEnabled(inspection!=null);
            if(inspection==null)return;
            var units=SharedUnitSettings.Current;
            void Info(NativeStatDefinition definition)=>OpenDialog(definition.Label,definition.Explanation(panel.Current)+(definition.Number!=null?"\nBar endpoints are display ranges, not safe operating limits.":""),new[] {new DialogAction("Close",CloseDialog)});
            panel=new NativeStatsPanel(inspection,units,NativeStatCatalog.Groups(inspection),ownedStatsCollapsed,Info,DecorateOwnedStats) {name="ownedCarStats"};
            ownedDetailContent.Add(panel);
            bool estimateStarted=false;
            void StartEstimate()
            {
                if(estimateStarted)return;estimateStarted=true;
                string key=game.Profile.revision+"/"+car.instanceId;
                if(!ownedDrivingEstimates.TryGetValue(key,out var task)) {
                    // Capture and resource-backed preparation stay on the main thread; only domain math runs in the worker.
                    var source=InspectionBuild.Capture(game.Profile,car);var simulated=CarInspection.PrepareCar(source);
                    task=Task.Run(()=>CarInspection.CalculatePrepared(source,simulated));ownedDrivingEstimates[key]=task;
                    if(ownedDrivingEstimates.Count>16)foreach(string old in ownedDrivingEstimates.Where(p=>p.Key!=key&&p.Value.IsCompleted).Select(p=>p.Key).ToArray())ownedDrivingEstimates.Remove(old);
                }
                panel.Current.DrivingPending=!task.IsCompleted;
                panel.SetDrivingInspection(inspection,units,Info);
                IVisualElementScheduledItem polling=null;
                void Complete() {
                    if(!task.IsCompleted)return;
                    polling?.Pause();panel.Current.DrivingPending=false;
                    try {panel.SetDrivingInspection(task.GetAwaiter().GetResult(),units,Info);}
                    catch(Exception error){panel.Current.DrivingError=error.Message;panel.SetDrivingInspection(inspection,units,Info);}
                }
                if(task.IsCompleted)Complete();else polling=panel.schedule.Execute(Complete).Every(100);
            }
            panel.GroupOpened=id=>{if(id=="Performance")StartEstimate();};
            if(!panel.Q("statsBody:Performance").ClassListContains("hidden"))StartEstimate();
            void UpdateVisibility(){bool visible=ownedStatsShown.Contains(car.instanceId);panel.EnableInClassList("hidden",!visible);button.EnableInClassList("selected",visible);button.tooltip=visible?"Hide car stats":"Show car stats";}
            button.clicked+=()=>{if(!ownedStatsShown.Add(car.instanceId))ownedStatsShown.Remove(car.instanceId);UpdateVisibility();};UpdateVisibility();
        }
        void DecorateOwnedStats(VisualElement element,RacingSurface.Kind kind)
        { AddSurface(element,kind);ownedStatsSurfaces.Add(element.Q<RacingSurface>()); }
    }
}
