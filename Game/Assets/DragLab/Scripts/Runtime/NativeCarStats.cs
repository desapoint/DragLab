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
        bool comparisonDifferencesOnly;
        readonly HashSet<string> ownedStatsShown=new HashSet<string>();
        readonly Dictionary<string,bool> ownedStatsCollapsed=new Dictionary<string,bool>();
        readonly List<RacingSurface> ownedStatsSurfaces=new List<RacingSurface>();
        void BuildOwnedStats(VisualElement top,OwnedCar car,CarInspection comparedTo=null)
        {
            var inspection=InspectOwned(car)?.Stats;
            BuildInspectionStats(top,ownedDetailContent,inspection,comparedTo,car.instanceId,"ownedStatsToggle","ownedCarStats",ownedStatsShown,ownedStatsCollapsed,DecorateOwnedStats,()=>OwnedDriving(car),()=>OwnedDriving(game.Profile.SelectedCar));
        }
        void BuildInspectionStats(VisualElement top,VisualElement content,CarInspection inspection,CarInspection comparedTo,string id,string toggleName,string panelName,
            HashSet<string> shown,IDictionary<string,bool> collapsed,Action<VisualElement,RacingSurface.Kind> decorate,Func<Task<CarInspection>> viewedDriving,Func<Task<CarInspection>> selectedDriving=null)
        {
            NativeStatsPanel panel=null;
            var button=new Button {name=toggleName,text="Stats",tooltip=inspection==null?"Stats unavailable":"Show car stats",focusable=false,tabIndex=-1};
            button.AddToClassList("action-button");button.AddToClassList("car-stats-button");button.Add(new FilledMenuIcon("Data"));
            top.Q(className:"car-summary").Add(button);decorate(button,RacingSurface.Kind.SecondaryAction);
            button.SetEnabled(inspection!=null);
            if(inspection==null)return;
            var units=SharedUnitSettings.Current;
            void Info(NativeStatDefinition definition)=>OpenDialog(definition.Label,definition.InfoExplanation(panel.Current,panel.Proposed)+(panel.Proposed==null&&definition.Number!=null?"\nBar endpoints are display ranges, not safe operating limits.":""),new[] {new DialogAction("Close",CloseDialog)});
            panel=new NativeStatsPanel(comparedTo??inspection,units,NativeStatCatalog.Groups(comparedTo??inspection,comparedTo==null?null:inspection),collapsed,Info,decorate,comparedTo==null?null:inspection,comparisonDifferencesOnly) {name=panelName};
            content.Add(panel);
            bool estimateStarted=false;
            void StartEstimate()
            {
                if(estimateStarted)return;estimateStarted=true;
                var task=comparedTo==null?viewedDriving():selectedDriving();var next=comparedTo==null?null:viewedDriving();
                bool completed=false,nextCompleted=next==null;
                panel.Current.DrivingPending=true;if(panel.Proposed!=null)panel.Proposed.DrivingPending=true;
                panel.SetDrivingInspection(panel.Current.Driving,units,Info,panel.Proposed?.Driving);
                IVisualElementScheduledItem polling=null;
                void Complete() {
                    bool changed=false;
                    void Resolve(Task<CarInspection> pending,NativeStatsSnapshot snapshot,ref bool done) {
                        if(done||!pending.IsCompleted)return;done=true;changed=true;snapshot.DrivingPending=false;
                        try{snapshot.DrivingInspection=pending.GetAwaiter().GetResult();}catch(Exception error){snapshot.DrivingError=error.Message;}
                    }
                    Resolve(task,panel.Current,ref completed);if(next!=null)Resolve(next,panel.Proposed,ref nextCompleted);
                    if(changed)panel.SetDrivingInspection(panel.Current.Driving,units,Info,panel.Proposed?.Driving);
                    if(completed&&nextCompleted)polling?.Pause();
                }
                Complete();if(!completed||!nextCompleted)polling=panel.schedule.Execute(Complete).Every(100);
            }
            panel.DifferencesOnlyChanged=value=>{comparisonDifferencesOnly=value;if(value)StartEstimate();};
            if(panel.DifferencesOnly)StartEstimate();
            panel.GroupOpened=id=>{if(id=="Performance")StartEstimate();};
            if(!panel.Q("statsBody:Performance").ClassListContains("hidden"))StartEstimate();
            void UpdateVisibility(){bool visible=shown.Contains(id);panel.EnableInClassList("hidden",!visible);button.EnableInClassList("selected",visible);button.tooltip=visible?"Hide car stats":"Show car stats";}
            button.clicked+=()=>{if(!shown.Add(id))shown.Remove(id);UpdateVisibility();};UpdateVisibility();
        }
        void DecorateOwnedStats(VisualElement element,RacingSurface.Kind kind)
        { AddSurface(element,kind);ownedStatsSurfaces.Add(element.Q<RacingSurface>()); }
    }
}
