using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        VisualElement tuningPage,tuningControls,tuningPreviewContent;
        Label tuningName,tuningState,tuningEmpty,tuningError;
        Button tuningApply,tuningRevert,tuningSaveSetup;
        Button[] tuningTabs;
        int tuningCategory;
        string tuningKey;
        PlayerProfile tuningProfile;
        NativeStatsPanel tuningStats;
        readonly Dictionary<string,bool> tuningCollapsed=new Dictionary<string,bool>();
        readonly List<RacingSurface> tuningPreviewSurfaces=new List<RacingSurface>();
        bool tuningDifferencesOnly;
        internal int TuningPreviewCaptureCount {get;private set;}

        void InitializeTuning()
        {
            tuningPage=new VisualElement{name="tuningPage"};tuningPage.AddToClassList("tuning-page");tuningPage.AddToClassList("hidden");root.Q(className:"workspace").Add(tuningPage);
            var columns=new VisualElement();columns.AddToClassList("tuning-columns");tuningPage.Add(columns);
            var left=new VisualElement{name="tuningEditor"};left.AddToClassList("tuning-editor");columns.Add(left);
            tuningName=new Label{ name="tuningCarName",enableRichText=false};tuningName.AddToClassList("car-detail-title");left.Add(tuningName);
            var tabs=new VisualElement{name="tuningTabs"};tabs.AddToClassList("settings-tabs");tabs.AddToClassList("tuning-tabs");left.Add(tabs);
            var labels=new[]{"Engine","Induction","Drivetrain","Wheels"};tuningTabs=new Button[labels.Length];
            for(int i=0;i<labels.Length;i++){
                int index=i;var tab=new Button(()=>{tuningCategory=index;ShowTuningCategory();}){name="tuningTab"+labels[i],text=labels[i],focusable=false,tabIndex=-1};tab.AddToClassList("category-button");tabs.Add(tab);AddSurface(tab,i==0?RacingSurface.Kind.FirstTab:RacingSurface.Kind.Tab);tuningTabs[i]=tab;
            }
            tuningControls=new VisualElement{name="tuningControls"};tuningControls.AddToClassList("tuning-control-frame");left.Add(tuningControls);AddSurface(tuningControls,RacingSurface.Kind.Panel);
            foreach(string label in labels){var area=new ScrollView{name="tuningArea"+label,horizontalScrollerVisibility=ScrollerVisibility.Hidden};area.AddToClassList("tuning-control-area");tuningControls.Add(area);}
            tuningEmpty=new Label{name="tuningUnavailable",enableRichText=false};tuningEmpty.AddToClassList("car-empty");tuningControls.Add(tuningEmpty);
            var right=new VisualElement{name="tuningPreview"};right.AddToClassList("tuning-preview");columns.Add(right);AddSurface(right,RacingSurface.Kind.Panel);
            var scroll=new ScrollView{name="tuningPreviewScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("tuning-preview-scroll");right.Add(scroll);
            tuningPreviewContent=new VisualElement{name="tuningPreviewContent"};scroll.Add(tuningPreviewContent);
            var actions=new VisualElement{name="tuningActions"};actions.AddToClassList("car-details-actions");tuningPage.Add(actions);
            tuningState=new Label{name="tuningDraftState"};tuningState.AddToClassList("settings-status");actions.Add(tuningState);
            tuningError=new Label{name="tuningError",enableRichText=false};tuningError.AddToClassList("car-critical");tuningError.AddToClassList("car-selection-error");actions.Add(tuningError);
            tuningSaveSetup=new Button(OpenSaveSetup){name="tuningSaveSetup",text="Save setup",focusable=false,tabIndex=-1};tuningSaveSetup.AddToClassList("action-button");actions.Add(tuningSaveSetup);AddSurface(tuningSaveSetup,RacingSurface.Kind.SecondaryAction);tuningSaveSetup.Insert(1,new FilledMenuIcon("Save"));
            var loadSetup=new Button(OpenSetupLibrary){name="tuningLoadSetup",text="Load setup",focusable=false,tabIndex=-1};loadSetup.AddToClassList("action-button");actions.Add(loadSetup);AddSurface(loadSetup,RacingSurface.Kind.SecondaryAction);loadSetup.Insert(1,new FilledMenuIcon("Business"));
            tuningRevert=new Button(()=>{game.BeginTuneDraft();tuningError.text="";RefreshTuning("Tuning");}){name="tuningRevert",text="Revert",focusable=false,tabIndex=-1};tuningRevert.AddToClassList("action-button");actions.Add(tuningRevert);AddSurface(tuningRevert,RacingSurface.Kind.SecondaryAction);
            tuningApply=new Button(()=>{tuningError.text=game.ApplyTuneDraftNow()?"":game.NativeNotice;RefreshTuning("Tuning");}){name="tuningApply",text="Apply",focusable=false,tabIndex=-1};tuningApply.AddToClassList("action-button");tuningApply.AddToClassList("primary-action");actions.Add(tuningApply);AddSurface(tuningApply,RacingSurface.Kind.Action);
            ShowTuningCategory();
            InitializeSetupLibrary();
        }
        void ShowTuningCategory()
        {
            for(int i=0;i<tuningTabs.Length;i++){tuningTabs[i].EnableInClassList("selected",i==tuningCategory);tuningControls.Q("tuningArea"+new[]{"Engine","Induction","Drivetrain","Wheels"}[i]).EnableInClassList("hidden",i!=tuningCategory);}
        }
        void DecorateTuningPreview(VisualElement element,RacingSurface.Kind kind)
        {AddSurface(element,kind);tuningPreviewSurfaces.Add(element.Q<RacingSurface>());}
        void ClearTuningPreview()
        {foreach(var surface in tuningPreviewSurfaces)surfaces.Remove(surface);tuningPreviewSurfaces.Clear();tuningPreviewContent.Clear();tuningStats=null;}
        void RefreshTuning(string route)
        {
            RefreshSetupLibrary(route);tuningPage.EnableInClassList("hidden",route!="Tuning"||setupLibraryOpen);if(route!="Tuning"||setupLibraryOpen)return;
            var profile=game.Profile;var car=profile.SelectedCar;game.EnsureTuneDraft();
            bool available=car!=null&&!string.IsNullOrEmpty(car.engineAssemblyId),dirty=available&&game.DraftDirty;
            RefreshTuningInputs(available);
            tuningName.text=car==null?"No car":VehicleCollection.DisplayName(profile.catalog,car);
            foreach(var tab in tuningTabs)tab.SetEnabled(available);
            tuningControls.EnableInClassList("tuning-unavailable",!available);
            tuningApply.SetEnabled(dirty&&car.engineRebuild==null);tuningRevert.SetEnabled(dirty);
            tuningSaveSetup.SetEnabled(car!=null&&!game.DraftDirty&&car.engineRebuild==null&&string.IsNullOrEmpty(car.garageJobId)&&!profile.runCheckpoints.Any(r=>!r.settled));
            tuningSaveSetup.tooltip=game.DraftDirty?"Apply or revert tuning before saving a setup.":"Save the applied configuration as a named blueprint.";
            tuningState.text=dirty?"Unapplied changes":"";tuningEmpty.text=car==null?"Select a car in Garage.":"Attach an engine to preview tuning.";tuningEmpty.EnableInClassList("hidden",available);
            string key=profile.revision+"/"+car?.instanceId+"/"+car?.engineAssemblyId+"/"+JsonUtility.ToJson(game.DraftValues)+"/"+JsonUtility.ToJson(SharedUnitSettings.Current);
            if(tuningProfile==profile&&tuningKey==key)return;
            if(tuningRanges.Any(r=>r.Dragging)&&Time.unscaledTime-tuningLastPreview<.1f)return;
            tuningLastPreview=Time.unscaledTime;
            tuningProfile=profile;tuningKey=key;ClearTuningPreview();
            if(!available)return;
            try{
                var current=InspectOwned(car)?.Stats;if(current==null)throw new InvalidOperationException("Inspection unavailable.");
                var proposed=current;
                if(dirty){var source=InspectionBuild.Capture(profile,car,game.DraftValues);proposed=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);TuningPreviewCaptureCount++;}
                RefreshTuningWheelPreview(proposed.source.build);
                tuningPreviewContent.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",current,NativeDynoStyle.Primary),new NativeDynoSeries("Proposed","Proposed",proposed,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","No wear",OwnedHealthy(car),NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"tuningDynoPlot"));
                var groups=new List<NativeStatGroup>{NativeStatCatalog.TuningSummary()};
                foreach(var group in NativeStatCatalog.Groups(current,proposed))groups.Add(new NativeStatGroup(group.Id,group.Title,group.Definitions.ToArray()){InitiallyCollapsed=true,Children=group.Children});
                void Info(NativeStatDefinition definition)=>OpenDialog(definition.Label,definition.InfoExplanation(tuningStats.Current,tuningStats.Proposed),new[]{new DialogAction("Close",CloseDialog)});
                tuningStats=new NativeStatsPanel(current,SharedUnitSettings.Current,groups,tuningCollapsed,Info,DecorateTuningPreview,proposed,tuningDifferencesOnly){name="tuningStats"};
                tuningStats.Q("statsBody:TuneSummary").AddToClassList("tuning-summary");
                tuningStats.DifferencesOnlyChanged=value=>tuningDifferencesOnly=value;tuningPreviewContent.Add(tuningStats);ExcludeTabTraversal();
            }catch(Exception error){var message=new Label(error.Message){name="tuningPreviewError",enableRichText=false};message.AddToClassList("car-critical");tuningPreviewContent.Add(message);tuningApply.SetEnabled(false);}
        }
    }
}
