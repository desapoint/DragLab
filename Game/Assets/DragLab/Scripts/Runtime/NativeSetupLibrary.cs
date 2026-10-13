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
        bool setupLibraryOpen;string setupSelectedId,setupLibraryKey;
        PlayerProfile setupLibraryProfile;
        VisualElement setupPage,setupGrid,setupPreviewContent;ScrollView setupListScroll,setupPreviewScroll;
        TextField setupSearch;Label setupCount,setupEmpty;
        readonly NativeSetupQuery setupQuery=new NativeSetupQuery();
        readonly Dictionary<string,Button> setupCards=new Dictionary<string,Button>();
        readonly Dictionary<string,bool> setupCollapsed=new Dictionary<string,bool>();
        readonly List<RacingSurface> setupSurfaces=new List<RacingSurface>();
        NativeSetupPreview setupPreview;bool setupDifferencesOnly;
        internal int SetupPreviewCaptureCount{get;private set;}
        internal bool SetupLibraryOpen=>setupLibraryOpen;
        void SetupSurface(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);setupSurfaces.Add(element.Q<RacingSurface>());}
        void InitializeSetupLibrary()
        {
            setupPage=new VisualElement{name="setupLibraryPage"};setupPage.AddToClassList("car-details-page");setupPage.AddToClassList("hidden");root.Q(className:"workspace").Add(setupPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");setupPage.Add(heading);
            var back=new Button(BackFromSetupLibrary){name="setupLibraryBack",focusable=false,tabIndex=-1};back.AddToClassList("action-button");back.AddToClassList("car-back-button");heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);back.Add(new FilledMenuIcon("Back"));
            var title=new Label("Saved setups");title.AddToClassList("car-detail-title");heading.Add(title);
            var toolbar=new VisualElement();toolbar.AddToClassList("owned-car-toolbar");setupPage.Add(toolbar);
            setupSearch=new TextField{name="setupLibrarySearch",maxLength=128};setupSearch.AddToClassList("setting-text");setupSearch.AddToClassList("owned-car-search");setupSearch.tooltip="Search setup / car names";toolbar.Add(setupSearch);
            var hint=new Label("Search setups"){pickingMode=PickingMode.Ignore};hint.AddToClassList("owned-search-hint");setupSearch.Add(hint);setupSearch.RegisterCallback<FocusInEvent>(_=>hint.AddToClassList("hidden"));setupSearch.RegisterCallback<FocusOutEvent>(_=>hint.EnableInClassList("hidden",setupSearch.value!=""));setupSearch.RegisterValueChangedCallback(e=>{setupQuery.Search=e.newValue;hint.EnableInClassList("hidden",e.newValue!="");FilterSetupCards();});
            setupCount=new Label{name="setupLibraryCount"};setupCount.AddToClassList("owned-car-count");toolbar.Add(setupCount);
            var choices=new VisualElement();choices.AddToClassList("choice-group");choices.AddToClassList("setup-scope-choices");setupPage.Add(choices);
            PartChoices(choices,"setupLibraryScope",new[]{"All","Tune only","Engine + controls","Whole car"},()=>setupQuery.Scope,v=>{setupQuery.Scope=v;FilterSetupCards();});
            var columns=new VisualElement{name="setupLibraryColumns"};columns.AddToClassList("tuning-columns");setupPage.Add(columns);
            var left=new VisualElement();left.AddToClassList("setup-library-list");columns.Add(left);AddSurface(left,RacingSurface.Kind.Panel);
            setupListScroll=new ScrollView{name="setupLibraryList",horizontalScrollerVisibility=ScrollerVisibility.Hidden};setupListScroll.AddToClassList("tuning-preview-scroll");left.Add(setupListScroll);
            setupGrid=new VisualElement{name="setupLibraryGrid"};setupListScroll.Add(setupGrid);setupEmpty=new Label{name="setupLibraryEmpty"};setupEmpty.AddToClassList("car-empty");setupListScroll.Add(setupEmpty);
            var right=new VisualElement();right.AddToClassList("tuning-preview");columns.Add(right);AddSurface(right,RacingSurface.Kind.Panel);
            setupPreviewScroll=new ScrollView{name="setupLibraryPreview",horizontalScrollerVisibility=ScrollerVisibility.Hidden};setupPreviewScroll.AddToClassList("tuning-preview-scroll");right.Add(setupPreviewScroll);setupPreviewContent=new VisualElement();setupPreviewScroll.Add(setupPreviewContent);
        }
        void OpenSetupLibrary(){setupLibraryOpen=true;setupLibraryKey=null;RefreshTuning("Tuning");}
        void BackFromSetupLibrary(){setupLibraryOpen=false;setupPage.AddToClassList("hidden");RefreshTuning("Tuning");}
        void RefreshSetupLibrary(string route)
        {
            if(route!="Tuning"&&route!="Settings")setupLibraryOpen=false;
            setupPage.EnableInClassList("hidden",route!="Tuning"||!setupLibraryOpen);if(route!="Tuning"||!setupLibraryOpen)return;
            var p=game.Profile;if(!p.savedSetups.Any(s=>s.id==setupSelectedId))setupSelectedId=null;string key=p.revision+"/"+p.SelectedCar?.instanceId+"/"+setupSelectedId+"/"+JsonUtility.ToJson(SharedUnitSettings.Current);
            if(setupLibraryProfile==p&&setupLibraryKey==key)return;setupLibraryProfile=p;setupLibraryKey=key;
            foreach(var surface in setupSurfaces)surfaces.Remove(surface);setupSurfaces.Clear();setupGrid.Clear();setupCards.Clear();
            foreach(var saved in p.savedSetups.OrderBy(s=>s.name,StringComparer.OrdinalIgnoreCase).ThenBy(s=>s.id,StringComparer.Ordinal)){
                string id=saved.id;var card=new Button(()=>{setupSelectedId=id;setupLibraryKey=null;setupPreviewScroll.scrollOffset=Vector2.zero;RefreshSetupLibrary("Tuning");}){name="setupCard:"+id,focusable=false,tabIndex=-1,tooltip=id};card.AddToClassList("owned-car-card");card.AddToClassList("setup-card");card.AddToClassList("clickable-panel");card.EnableInClassList("selected",id==setupSelectedId);setupGrid.Add(card);SetupSurface(card,RacingSurface.Kind.Panel);setupCards[id]=card;
                var name=new Label(saved.name){enableRichText=false};name.AddToClassList("car-card-title");card.Add(name);card.Add(new Label(NativeSetupQuery.ScopeName(saved.scope)){enableRichText=false});card.Add(new Label(p.catalog.Spec(saved.vehicleDefinitionId).displayName){enableRichText=false});card.Add(new Label(saved.parts.Count+" hardware slots"){enableRichText=false});
                card.Query<VisualElement>().ForEach(e=>{if(e!=card)e.pickingMode=PickingMode.Ignore;});
            }
            FilterSetupCards();BuildSetupPreview();ExcludeTabTraversal();
        }
        void FilterSetupCards()
        {
            int count=0;foreach(var setup in game.Profile.savedSetups){if(!setupCards.TryGetValue(setup.id,out var card))continue;bool match=setupQuery.Matches(setup,game.Profile.catalog);card.EnableInClassList("hidden",!match);if(match)count++;}
            setupCount.text=count+" / "+game.Profile.savedSetups.Count;setupEmpty.text=game.Profile.savedSetups.Count==0?"No saved setups":"No matching setups";setupEmpty.EnableInClassList("hidden",count>0);
        }
        void BuildSetupPreview()
        {
            setupPreviewContent.Clear();setupPreview=null;
            var p=game.Profile;var saved=p.savedSetups.Find(s=>s.id==setupSelectedId);
            if(saved==null){setupPreviewContent.Add(new Label("Select a setup"));return;}
            var title=new Label(saved.name){enableRichText=false};title.AddToClassList("car-detail-title");setupPreviewContent.Add(title);
            try{
                setupPreview=NativeSetupPreview.Capture(p,p.SelectedCar,saved,partPreviewCache);SetupPreviewCaptureCount++;
                var plan=setupPreview.Plan;
                if(plan!=null&&plan.resolution!=null){var summary=new Label(saved.scope==SetupScope.TuneOnly?"Installed hardware retained":plan.lines.Count(l=>l.installed)+" fitted · "+plan.lines.Count(l=>!l.installed&&!l.purchase)+" reuse"+(plan.Missing>0?" · "+plan.Missing+" missing · "+DisplayUnits.Money(plan.cost)+" estimate":"")){name="setupPlanSummary",enableRichText=false};summary.AddToClassList("tuning-readout");setupPreviewContent.Add(summary);}
                if(setupPreview.Unavailable!=null){var unavailable=new Label(setupPreview.Unavailable){name="setupPreviewUnavailable",enableRichText=false};unavailable.AddToClassList("car-warning");setupPreviewContent.Add(unavailable);}
                if(plan!=null)foreach(string error in plan.errors.Distinct()){if(error==setupPreview.Unavailable)continue;var message=new Label(error){enableRichText=false};message.AddToClassList("car-warning");setupPreviewContent.Add(message);}
                var current=setupPreview.Current;var proposed=setupPreview.Proposed;
                if(current!=null&&proposed!=null){
                    setupPreviewContent.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",current,NativeDynoStyle.Primary),new NativeDynoSeries("Saved","Saved setup",proposed,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","Current no wear",OwnedHealthy(p.SelectedCar),NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"setupLibraryDyno"));
                    var warnings=new Button(()=>OpenCombinedWarnings(current.warnings.ToArray(),proposed.warnings.ToArray(),true,"Setup warnings")){name="setupLibraryWarnings",text="Warnings",focusable=false,tabIndex=-1};warnings.AddToClassList("action-button");warnings.AddToClassList("warning-header-button");setupPreviewContent.Add(warnings);SetupSurface(warnings,RacingSurface.Kind.SecondaryAction);warnings.Insert(1,new FilledMenuIcon("Warning"));SetWarningButton(warnings,proposed.warnings.ToArray());warnings.Q<Label>().text="Warnings · "+NativeWarningOverview.Combine(current.warnings,proposed.warnings,true).Length;
                    var groups=new List<NativeStatGroup>{NativeStatCatalog.TuningSummary()};groups.AddRange(NativeStatCatalog.Groups(current,proposed).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children}));
                    NativeStatsPanel panel=null;panel=new NativeStatsPanel(current,SharedUnitSettings.Current,groups,setupCollapsed,d=>OpenDialog(d.Label,d.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)}),SetupSurface,proposed,setupDifferencesOnly){name="setupLibraryStats"};panel.Q("statsBody:TuneSummary").AddToClassList("tuning-summary");panel.DifferencesOnlyChanged=v=>setupDifferencesOnly=v;panel.GroupOpened=_=>ExcludeTabTraversal();setupPreviewContent.Add(panel);
                }
                if(plan!=null)foreach(var group in plan.lines.Where(l=>!l.installed).GroupBy(l=>l.purchase?"Missing parts":"Reuse inventory")){
                    var section=new Label(group.Key){enableRichText=false};section.AddToClassList("setup-parts-heading");setupPreviewContent.Add(section);
                    foreach(var line in group){var d=p.catalog.Find(line.definitionId);var item=p.parts.Find(i=>i.instanceId==line.itemId);var label=new Label(d.name+" · "+(line.purchase?DisplayUnits.Money(line.cost):item.condition.health.ToString("P0")+" condition")){name="setupPlanPart:"+line.category,enableRichText=false};label.AddToClassList("part-description");setupPreviewContent.Add(label);}
                }
                if(plan!=null)foreach(var line in plan.tires){var d=p.catalog.Find(line.definitionId);var unit=p.looseEngineElements.Find(u=>u.element.id==line.itemId);var label=new Label(InventoryUnits.PositionLabel(p,p.SelectedCar,line.definitionId,(line.wheel?"wheel:":"tire:")+line.corner)+" · "+d.name+" · "+(line.purchase?"Missing · "+DisplayUnits.Money(line.cost):"Reuse · "+(line.wheel?unit.element.health:unit.tire.health).ToString("P0"))){name="setupPlan"+(line.wheel?"Wheel:":"Tire:")+line.corner,enableRichText=false};label.AddToClassList("part-description");setupPreviewContent.Add(label);}
            }catch(Exception error){var label=new Label(error.Message){name="setupPreviewError",enableRichText=false};label.AddToClassList("car-critical");setupPreviewContent.Add(label);}
        }
    }
}
