using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed class NativeRaceEntry
    {
        internal string Mode,Event,Opponent,Profile,EventData;internal float Distance;internal long Revision;
    }
    public sealed partial class PrototypeGame
    {
        internal PassRecord NativePreviousPass=>finishedPass!=null&&liveRunOwner==Build?.instanceId?previousPass:null;
        internal float NativeRaceDistance {get=>freeDistance;set=>freeDistance=value;}
        internal int NativeRaceOpponent {get=>freeOpponent;set=>freeOpponent=value;}
        internal NativeRaceEntry CaptureNativeEntry(string mode,string eventId)
        {
            if(screen!="Race setup"||pendingAction!=null)throw new InvalidOperationException("Race selection is no longer open.");
            var evt=mode=="Career"?career.FirstOrDefault(e=>e.stableId==eventId):null;
            if(!new[]{"Career","Free race","Practice"}.Contains(mode)||mode=="Career"&&evt==null)throw new InvalidOperationException("Select an available event.");
            string error=CareerProgression.EntryError(profile,Build,evt);if(error!=null)throw new InvalidOperationException(error);
            return new NativeRaceEntry{Mode=mode,Event=evt?.stableId,Opponent=vehicles[freeOpponent].spec.id,Distance=mode=="Practice"?402.336f:evt!=null?evt.distanceM:freeDistance,Revision=profile.revision,Profile=JsonUtility.ToJson(profile),EventData=evt==null?null:JsonUtility.ToJson(evt)};
        }
        internal bool StartNativeEntry(NativeRaceEntry entry)
        {
            try{
                if(entry==null||screen!="Race setup"||pendingAction!=null||profile.revision!=entry.Revision||!SnapshotEquality.Profile(entry.Profile,profile,profile.catalog))throw new InvalidOperationException("Car or entry changed. Review again.");
                var evt=entry.Mode=="Career"?career.FirstOrDefault(e=>e.stableId==entry.Event):null;
                if(entry.Mode=="Career"&&(evt==null||JsonUtility.ToJson(evt)!=entry.EventData))throw new InvalidOperationException("Event terms changed. Review again.");
                if(!new[]{"Career","Free race","Practice"}.Contains(entry.Mode)||!new[]{201.168f,402.336f,804.672f}.Any(d=>Math.Abs(d-entry.Distance)<.01)&&evt==null)throw new InvalidOperationException("Invalid entry options.");
                int opponent=Array.FindIndex(vehicles,v=>v.spec.id==entry.Opponent);if(opponent<0)throw new InvalidOperationException("Opponent no longer exists.");
                if(entry.Mode=="Free race"){freeOpponent=opponent;freeDistance=entry.Distance;}StartRace(evt,entry.Mode=="Practice");return screen=="Race";
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
    internal sealed partial class NativeMenuShell
    {
        VisualElement raceSelectionPage,raceSelectionContent;ScrollView raceSelectionScroll;
        Button raceSelectionStage,raceSelectionCar;Label raceSelectionStatus;
        string raceSelectionMode="Career",raceSelectionEvent,raceSelectionKey,raceSearch="",raceTier="All",raceActionKey,raceActionError;
        readonly List<RacingSurface> raceSelectionSurfaces=new List<RacingSurface>();
        void RaceSurface(VisualElement e,RacingSurface.Kind kind){AddSurface(e,kind);raceSelectionSurfaces.Add(e.Q<RacingSurface>());}
        void InitializeRaceSelection()
        {
            raceSelectionPage=new VisualElement{name="raceSelectionPage"};raceSelectionPage.AddToClassList("car-details-page");raceSelectionPage.AddToClassList("hidden");root.Q(className:"workspace").Add(raceSelectionPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");raceSelectionPage.Add(heading);
            raceSelectionCar=new Button(OpenRaceCarPicker){name="raceCarSelect",text="Choose car",tooltip="Choose owned car"};raceSelectionCar.AddToClassList("action-button");raceSelectionCar.style.marginLeft=0;heading.Add(raceSelectionCar);AddSurface(raceSelectionCar,RacingSurface.Kind.SecondaryAction);
            var tabs=new VisualElement();tabs.AddToClassList("choice-group");tabs.style.flexDirection=FlexDirection.Row;tabs.style.height=32;tabs.style.flexShrink=0;tabs.style.marginBottom=12;raceSelectionPage.Add(tabs);
            PartChoices(tabs,"raceMode",new[]{"Career","Free race","Practice","Records"},()=>raceSelectionMode,v=>{raceSelectionMode=v;raceSelectionKey=null;RefreshRaceSelection("Race setup");});
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");raceSelectionPage.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            raceSelectionScroll=new ScrollView{name="raceSelectionScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};raceSelectionScroll.AddToClassList("car-details-scroll");frame.Add(raceSelectionScroll);
            raceSelectionContent=new VisualElement{name="raceSelectionContent"};raceSelectionContent.AddToClassList("car-details-content");raceSelectionScroll.Add(raceSelectionContent);
            var actions=new VisualElement();actions.AddToClassList("car-details-actions");raceSelectionPage.Add(actions);raceSelectionStatus=new Label{name="raceEntryStatus",enableRichText=false};raceSelectionStatus.AddToClassList("settings-status");raceSelectionStatus.style.whiteSpace=WhiteSpace.Normal;actions.Add(raceSelectionStatus);
            raceSelectionStage=new Button(OpenRaceEntry){name="raceStage",text="Stage"};raceSelectionStage.AddToClassList("action-button");raceSelectionStage.AddToClassList("primary-action");actions.Add(raceSelectionStage);AddSurface(raceSelectionStage,RacingSurface.Kind.Action);
        }
        void OpenRaceCarPicker()
        {
            long revision=game.Profile.revision;
            OpenDialog("Choose car",null,new[]{new DialogAction("Cancel",CloseDialog)},populate:content=>{
                foreach(var car in game.Profile.cars){string id=car.instanceId;var button=new Button(()=>{if(!game.SelectNativeCar(id,revision)){content.Add(new Label(game.NativeNotice));return;}CloseDialog();raceSelectionKey=null;Refresh();}){name="raceChooseCar:"+id,text=VehicleCollection.DisplayName(game.Profile.catalog,car),tooltip=car.instanceId};button.AddToClassList("action-button");content.Add(button);AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());}
                if(game.Profile.cars.Count==0)content.Add(new Label("No owned cars. Buy a car in Garage."));
            });
        }
        void RefreshRaceSelection(string route)
        {
            raceSelectionPage.EnableInClassList("hidden",route!="Race setup");if(route!="Race setup")return;
            var p=game.Profile;var car=p.SelectedCar;raceSelectionCar.Q<Label>().text=car==null?"Choose car":VehicleCollection.DisplayName(p.catalog,car);
            string key=p.revision+"/"+car?.instanceId+"/"+raceSelectionMode+"/"+game.NativeRaceDistance+"/"+game.NativeRaceOpponent+"/"+p.difficulty+"/"+JsonUtility.ToJson(SharedUnitSettings.Current);
            if(raceSelectionKey!=key){raceSelectionKey=key;foreach(var surface in raceSelectionSurfaces)surfaces.Remove(surface);raceSelectionSurfaces.Clear();raceSelectionContent.Clear();
                if(raceSelectionMode=="Career")BuildRaceEvents();
                else if(raceSelectionMode=="Records")BuildRaceRecords();
                else{
                    raceSelectionContent.Add(new Label(raceSelectionMode=="Practice"?"¼ mile · no payout":"No entry fee · Win "+DisplayUnits.Money(120)+" · Completed loss "+DisplayUnits.Money(40)));
                    if(raceSelectionMode=="Free race"){
                        var distances=new VisualElement();distances.AddToClassList("choice-group");distances.style.flexDirection=FlexDirection.Row;distances.style.height=32;raceSelectionContent.Add(distances);
                        string[] names={"⅛ mile","¼ mile","½ mile"};float[] values={201.168f,402.336f,804.672f};PartChoices(distances,"raceDistance",names,()=>names[game.NativeRaceDistance<300?0:game.NativeRaceDistance<600?1:2],v=>{game.NativeRaceDistance=values[Array.IndexOf(names,v)];raceSelectionKey=null;});
                        var grid=new VisualElement();grid.AddToClassList("owned-cars-grid");raceSelectionContent.Add(grid);
                        foreach(var vehicle in game.vehicles){int index=Array.IndexOf(game.vehicles,vehicle);var button=new Button(()=>{game.NativeRaceOpponent=index;raceSelectionKey=null;}){name="raceOpponent:"+vehicle.spec.id,text=vehicle.spec.displayName};button.AddToClassList("owned-car-card");button.style.minHeight=76;button.AddToClassList("clickable-panel");button.EnableInClassList("selected",index==game.NativeRaceOpponent);grid.Add(button);RaceSurface(button,RacingSurface.Kind.Panel);}
                    }
                    raceSelectionContent.Add(new Label("Opponent · "+new[]{"Easy","Normal","Hard"}[p.difficulty]+" · Shifting · "+p.driving.shifting));
                }
                foreach(var surface in raceSelectionContent.Query<RacingSurface>().ToList())if(!raceSelectionSurfaces.Contains(surface))raceSelectionSurfaces.Add(surface);ExcludeTabTraversal();
            }
            var selected=game.career.FirstOrDefault(e=>e.stableId==raceSelectionEvent);
            string actionKey=key+"/"+raceSelectionEvent;if(raceActionKey!=actionKey){raceActionKey=actionKey;raceActionError=raceSelectionMode=="Career"&&selected==null?"Select an event.":CareerProgression.EntryError(p,car,raceSelectionMode=="Career"?selected:null);}
            string error=raceActionError;
            raceSelectionStage.EnableInClassList("hidden",raceSelectionMode=="Records");raceSelectionStage.SetEnabled(error==null&&raceSelectionMode!="Records");raceSelectionStatus.text=raceSelectionMode=="Records"?"":error??(raceSelectionMode=="Career"?selected.displayName+" · Entry "+DisplayUnits.Money(selected.entryFee):raceSelectionMode=="Practice"?"No payout":"Free entry");
        }
        void BuildRaceEvents()
        {
            var search=new TextField{name="raceSearch",value=raceSearch,tooltip="Search events / rivals",maxLength=128};search.AddToClassList("setting-text");raceSelectionContent.Add(search);
            var hint=new Label("Search events / rivals"){pickingMode=PickingMode.Ignore};hint.AddToClassList("owned-search-hint");search.Add(hint);hint.EnableInClassList("hidden",raceSearch!="");search.RegisterCallback<FocusInEvent>(_=>hint.AddToClassList("hidden"));search.RegisterCallback<FocusOutEvent>(_=>hint.EnableInClassList("hidden",search.value!=""));
            var choices=new VisualElement();choices.AddToClassList("choice-group");choices.style.flexDirection=FlexDirection.Row;choices.style.height=32;raceSelectionContent.Add(choices);
            var grid=new VisualElement{name="raceEventGrid"};grid.AddToClassList("owned-cars-grid");raceSelectionContent.Add(grid);
            void Filter(){foreach(var evt in game.career){var card=grid.Q("raceEvent:"+evt.stableId);card.EnableInClassList("hidden",raceTier!="All"&&evt.tier!=raceTier||!string.IsNullOrWhiteSpace(raceSearch)&&(evt.displayName+" "+evt.rivalName+" "+evt.opponent.spec.displayName).IndexOf(raceSearch,StringComparison.OrdinalIgnoreCase)<0);}if(raceSelectionEvent!=null&&grid.Q("raceEvent:"+raceSelectionEvent)?.ClassListContains("hidden")==true){raceSelectionEvent=null;grid.Query<Button>().ForEach(b=>b.RemoveFromClassList("selected"));}}
            PartChoices(choices,"raceTier",new[]{"All","F","E","D","C","B","A","S"},()=>raceTier,v=>{raceTier=v;Filter();});search.RegisterValueChangedCallback(e=>{raceSearch=e.newValue;hint.EnableInClassList("hidden",e.newValue!="");Filter();});
            foreach(var evt in game.career){string id=evt.stableId;var card=new Button(()=>{raceSelectionEvent=id;foreach(var button in grid.Query<Button>().ToList())button.EnableInClassList("selected",button.name=="raceEvent:"+id);}){name="raceEvent:"+id};card.AddToClassList("owned-car-card");card.style.minHeight=152;card.AddToClassList("clickable-panel");card.EnableInClassList("selected",id==raceSelectionEvent);grid.Add(card);RaceSurface(card,RacingSurface.Kind.Panel);
                var title=new Label(evt.tier+" · "+evt.eventKind+" · "+evt.displayName){enableRichText=false};title.AddToClassList("car-card-title");card.Add(title);
                card.Add(new Label(evt.opponent.spec.displayName+" · "+evt.rivalName){enableRichText=false});card.Add(new Label("Entry "+DisplayUnits.Money(evt.entryFee)+" · Win "+DisplayUnits.Money(evt.cashWin)+" · Loss "+DisplayUnits.Money(evt.cashLoss)));
                card.Add(new Label(game.Profile.careerWins.Contains(id)?"First win claimed":"First win +"+DisplayUnits.Money(evt.firstWinBonus)+" · REP +"+evt.reputationWin));
                string error=CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,evt);var status=new Label(error??"Eligible"){name="raceEligibility:"+id,enableRichText=false};status.style.whiteSpace=WhiteSpace.Normal;status.AddToClassList(error==null?"car-ready":"car-warning");card.Add(status);card.Query<Label>().ForEach(l=>l.pickingMode=PickingMode.Ignore);
            }
            Filter();
        }
        void BuildRaceRecords()
        {
            var car=game.Profile.SelectedCar;if(car==null){raceSelectionContent.Add(new Label("Select an owned car."));return;}
            CarFact(raceSelectionContent,"¼ mile best",car.bestQuarterSeconds>0?car.bestQuarterSeconds.ToString("F3")+" s":"—");CarFact(raceSelectionContent,"60 ft best",car.bestSixtyFootSeconds>0?car.bestSixtyFootSeconds.ToString("F3")+" s":"—");CarFact(raceSelectionContent,"⅛ mile best",car.bestEighthSeconds>0?car.bestEighthSeconds.ToString("F3")+" s":"—");CarFact(raceSelectionContent,"Best trap",car.bestTrapKmh>0?SharedUnitSettings.Speed(car.bestTrapKmh/3.6):"—");
            if(car.lastPass!=null){CarFact(raceSelectionContent,"Last pass",car.lastPass.elapsedSeconds.ToString("F3")+" s");CarFact(raceSelectionContent,"Reaction",car.lastPass.reactionSeconds.ToString("F3")+" s");CarFact(raceSelectionContent,"Trap",SharedUnitSettings.Speed(car.lastPass.trapKmh/3.6));if(game.NativePreviousPass!=null)raceSelectionContent.Add(new Label(car.lastPass.Compare(game.NativePreviousPass,SharedUnitSettings.Current)));}
            foreach(var run in game.Profile.careerRuns.Where(r=>r.complete&&r.carId==car.instanceId).Reverse().Take(20))raceSelectionContent.Add(new Label((game.career.FirstOrDefault(e=>e.stableId==run.eventId)?.displayName??(run.practice?"Practice":"Free race"))+" · "+run.outcome){enableRichText=false});
        }
        void OpenRaceEntry()
        {
            NativeRaceEntry entry;try{entry=game.CaptureNativeEntry(raceSelectionMode,raceSelectionEvent);}catch(Exception error){raceSelectionStatus.text=error.Message;return;}
            var evt=game.career.FirstOrDefault(e=>e.stableId==entry.Event);var errorLabel=new Label{name="raceEntryError"};errorLabel.AddToClassList("car-critical");
            OpenDialog("Race entry",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Stage",()=>{if(!game.StartNativeEntry(entry)){errorLabel.text=game.NativeNotice;return;}CloseDialog();},true)},populate:content=>{
                content.Add(new Label(VehicleCollection.DisplayName(game.Profile.catalog,game.Profile.SelectedCar)+" · "+entry.Mode){enableRichText=false});
                content.Add(new Label(evt?.displayName??(entry.Mode=="Practice"?"¼ mile · no payout":game.vehicles[game.NativeRaceOpponent].spec.displayName)){enableRichText=false});
                content.Add(new Label("Entry "+DisplayUnits.Money(evt?.entryFee??0)+" · Distance "+entry.Distance.ToString("F1")+" m"));
                if(entry.Mode!="Practice")content.Add(new Label("Win "+DisplayUnits.Money(evt?.cashWin??120)+" · Completed loss "+DisplayUnits.Money(evt?.cashLoss??40)));
                if(evt!=null&&!game.Profile.careerWins.Contains(evt.stableId))content.Add(new Label("First win +"+DisplayUnits.Money(evt.firstWinBonus)+" · REP +"+evt.reputationWin));content.Add(errorLabel);
            });
        }
    }
}
