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
        VisualElement partsPage,partsGrid,partsDetail;
        ScrollView partsScroll,partsDetailScroll;
        TextField partsSearch;
        Label partsCount,partsEmpty,partsNotice,partsReference;
        Button partsFilter,partsSort,partsCart;
        NativePartSection partsSection;
        bool partsPlanned;
        string partsStateKey,partsDetailId,partsOrder;
        PlayerProfile partsProfile;
        Vector2 partsReturnScroll;
        NativePartBrowser partsBrowser;
        readonly Dictionary<string,NativePartQuery> partQueries=new Dictionary<string,NativePartQuery>();
        readonly List<RacingSurface> partGridSurfaces=new List<RacingSurface>(),partDetailSurfaces=new List<RacingSurface>();
        readonly Dictionary<string,Dictionary<string,bool>> partDetailCollapsed=new Dictionary<string,Dictionary<string,bool>>();
        readonly Dictionary<string,bool> partDetailDifferences=new Dictionary<string,bool>();
        readonly Dictionary<string,Label[]> partCardLabels=new Dictionary<string,Label[]>();
        NativePartQuery PartsQuery {get{string key=game.PartsPage+"/"+partsSection;if(!partQueries.TryGetValue(key,out var q)){q=new NativePartQuery();partQueries[key]=q;}return q;}}
        internal NativePartBrowser PartBrowser=>partsBrowser;
        internal NativePartQuery PartQuery=>PartsQuery;
        internal bool PartsPlanned=>partsPlanned;
        internal int PartBrowserBuildCount{get;private set;}
        void InitializeParts()
        {
            partsPage=new VisualElement{name="partsPage"};partsPage.AddToClassList("parts-page");partsPage.AddToClassList("hidden");root.Q(className:"workspace").Add(partsPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");partsPage.Add(heading);var name=new Label{name="partsCarName",enableRichText=false};name.AddToClassList("car-detail-title");heading.Add(name);
            partsCart=PartButton(heading,"partsCart","Cart",OpenNativeCart);partsCart.Insert(1,new FilledMenuIcon("Business"));
            var sources=new VisualElement();sources.AddToClassList("owned-query-choices");sources.AddToClassList("choice-group");partsPage.Add(sources);
            sources.AddToClassList("parts-sources");
            PartChoices(sources,"partsSource",new[]{"Installed","Inventory","Shop"},()=>game.PartsPage,v=>{game.SelectPartsPage(v);partsDetailId=null;partsStateKey=null;});
            var tabs=new VisualElement{name="partsTabs"};tabs.AddToClassList("settings-tabs");tabs.AddToClassList("parts-tabs");partsPage.Add(tabs);
            for(int i=0;i<NativePartBrowser.SectionNames.Length;i++){int index=i;var b=new Button(()=>{partsSection=(NativePartSection)index;partsDetailId=null;RenderPartGrid();}){name="partsTab"+i,text=NativePartBrowser.SectionNames[i],focusable=false,tabIndex=-1};b.AddToClassList("category-button");tabs.Add(b);AddSurface(b,i==0?RacingSurface.Kind.FirstTab:RacingSurface.Kind.Tab);}
            var toolbar=new VisualElement{name="partsToolbar"};toolbar.AddToClassList("owned-car-toolbar");partsPage.Add(toolbar);
            partsSearch=new TextField{name="partsSearch",maxLength=128};partsSearch.AddToClassList("setting-text");partsSearch.AddToClassList("owned-car-search");toolbar.Add(partsSearch);partsSearch.RegisterValueChangedCallback(e=>{PartsQuery.Search=e.newValue;ApplyPartQuery();});
            var hint=new Label("Search parts"){pickingMode=PickingMode.Ignore};hint.AddToClassList("owned-search-hint");partsSearch.Add(hint);partsSearch.RegisterCallback<FocusInEvent>(_=>hint.AddToClassList("hidden"));partsSearch.RegisterCallback<FocusOutEvent>(_=>hint.EnableInClassList("hidden",partsSearch.value!=""));partsSearch.RegisterValueChangedCallback(e=>hint.EnableInClassList("hidden",e.newValue!=""));
            partsFilter=PartButton(toolbar,"partsFilters","Filters",()=>OpenPartFilters());partsFilter.Insert(1,new FilledMenuIcon("Tuning"));partsSort=PartButton(toolbar,"partsSort","Name",OpenPartSort);
            var clear=PartButton(toolbar,"partsClear","×",()=>{partQueries[game.PartsPage+"/"+partsSection]=new NativePartQuery();partsSearch.SetValueWithoutNotify("");ApplyPartQuery();});clear.AddToClassList("owned-query-clear");
            partsCount=new Label{name="partsCount"};partsCount.AddToClassList("owned-car-count");toolbar.Add(partsCount);
            partsReference=new Label{name="partsReference"};partsReference.AddToClassList("tuning-readout");partsPage.Add(partsReference);
            partsScroll=new ScrollView{name="partsScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};partsScroll.AddToClassList("owned-cars-scroll");partsPage.Add(partsScroll);
            partsGrid=new VisualElement{name="partsGrid"};partsGrid.AddToClassList("owned-cars-grid");partsScroll.Add(partsGrid);
            partsEmpty=new Label{name="partsEmpty"};partsEmpty.AddToClassList("car-empty");partsScroll.Add(partsEmpty);
            partsDetail=new VisualElement{name="partsDetail"};partsDetail.AddToClassList("car-details-page");partsDetail.AddToClassList("hidden");partsPage.Add(partsDetail);
            partsDetailScroll=new ScrollView{name="partsDetailScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};partsDetailScroll.AddToClassList("car-details-scroll");partsDetail.Add(partsDetailScroll);
            partsNotice=new Label{name="partsNotice",enableRichText=false};partsNotice.AddToClassList("car-critical");partsPage.Add(partsNotice);
        }
        Button PartButton(VisualElement parent,string id,string label,Action action,bool primary=false,bool detail=false)
        {var b=new Button(action){name=id,text=label,focusable=false,tabIndex=-1};b.AddToClassList("action-button");if(primary)b.AddToClassList("primary-action");parent.Add(b);AddSurface(b,primary?RacingSurface.Kind.Action:RacingSurface.Kind.SecondaryAction);if(detail)partDetailSurfaces.Add(b.Q<RacingSurface>());return b;}
        void PartChoices(VisualElement parent,string id,string[] options,Func<string> get,Action<string> set)
        {
            var buttons=new List<Button>();void Update(){for(int i=0;i<buttons.Count;i++)buttons[i].EnableInClassList("selected",get()==options[i]);}
            for(int i=0;i<options.Length;i++){string value=options[i];var b=new Button(()=>{set(value);Update();}){name=id+":"+value,text=value,focusable=false,tabIndex=-1};b.AddToClassList("choice-button");b.AddToClassList(i==0?"choice-first":i==options.Length-1?"choice-last":"choice-middle");parent.Add(b);AddSurface(b,i==0?RacingSurface.Kind.ChoiceFirst:i==options.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);buttons.Add(b);}Update();
        }
        void RefreshParts(string route)
        {
            partsPage.EnableInClassList("hidden",route!="Parts");if(route!="Parts")return;
            var p=game.Profile;game.PrepareNativeCart();var car=p.SelectedCar;
            root.Q<Label>("partsCarName").text=car==null?"No car":VehicleCollection.DisplayName(p.catalog,car);
            partsCart.Q<Label>().text="Cart · "+game.CartCount;partsCart.SetEnabled(car!=null);
            string key=p.revision+"/"+car?.instanceId+"/"+game.PartsPage+"/"+partsPlanned+"/"+JsonUtility.ToJson(SharedUnitSettings.Current)+"/"+JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            if(partsProfile!=p||partsStateKey!=key){partsProfile=p;partsStateKey=key;partsBrowser=new NativePartBrowser(p,game.CartLines,partsPlanned,game.PartsPage,game.NativeUsedPrice,game.NativeSalePrice);PartBrowserBuildCount++;RenderPartGrid();if(partsDetailId!=null)OpenPartDetail(partsDetailId,false);}
            int count=0;var clock=System.Diagnostics.Stopwatch.StartNew();foreach(var e in partsBrowser.Entries.Where(e=>!e.Ready&&NativePartBrowser.Section(e.Definition.category)==partsSection)){partsBrowser.Evaluate(e);UpdatePartCard(e);count++;if(count>=8||clock.ElapsedMilliseconds>=3)break;}
            if(count>0)ApplyPartQuery();
            partsReference.text=(partsPlanned?"Planned build":"Current build")+(partsBrowser.PlanError==null?"":" · "+partsBrowser.PlanError);
            for(int i=0;i<5;i++)root.Q<Button>("partsTab"+i).EnableInClassList("selected",i==(int)partsSection);
            foreach(var b in root.Query<Button>().ToList().Where(b=>b.name.StartsWith("partsSource:")))b.EnableInClassList("selected",b.name=="partsSource:"+game.PartsPage);
        }
        void RenderPartGrid()
        {
            if(partsBrowser==null)return;foreach(var s in partGridSurfaces)surfaces.Remove(s);partGridSurfaces.Clear();partsGrid.Clear();partCardLabels.Clear();partsOrder=null;partsSearch.SetValueWithoutNotify(PartsQuery.Search);
            foreach(var e in partsBrowser.Entries.Where(e=>NativePartBrowser.Section(e.Definition.category)==partsSection)){
                string id=e.Id;var card=new Button(()=>OpenPartDetail(id)){name="partCard:"+id,focusable=false,tabIndex=-1};card.AddToClassList("owned-car-card");card.AddToClassList("part-card");card.AddToClassList("clickable-panel");card.EnableInClassList("hidden",!PartsQuery.Matches(e,game.Profile.cash));partsGrid.Add(card);AddSurface(card,RacingSurface.Kind.Panel);partGridSurfaces.Add(card.Q<RacingSurface>());
                var title=new Label(e.Definition.name){enableRichText=false,pickingMode=PickingMode.Ignore};title.AddToClassList("car-card-title");card.Add(title);var labels=new Label[6];for(int i=0;i<labels.Length;i++){labels[i]=new Label{enableRichText=false,pickingMode=PickingMode.Ignore};labels[i].AddToClassList(i==0?"car-condition":"part-card-fact");card.Add(labels[i]);}partCardLabels[id]=labels;UpdatePartCard(e);
            }
            ShowPartsBrowsing(partsDetailId==null);partsScroll.EnableInClassList("hidden",partsDetailId!=null);partsDetail.EnableInClassList("hidden",partsDetailId==null);ApplyPartQuery();ExcludeTabTraversal();
        }
        string PartDelta(NativePartEntry e,int metric)
        {if(!e.Change[metric].HasValue)return e.Ready?"—":"…";double value=e.Change[metric].Value;if(metric==1||metric==4)value=DisplayUnits.Torque(value,SharedUnitSettings.Current.torque);return (value>0?"+":"")+value.ToString(metric==3?"0.#":"0.##")+" "+(metric==0?"HP":metric==1||metric==4?SharedUnitSettings.TorqueLabel:metric==2?"kg":metric==3?"pp":metric==5?"RPM":"W/°C");}
        void UpdatePartCard(NativePartEntry e)
        {
            if(!partCardLabels.TryGetValue(e.Id,out var labels))return;
            labels[0].text=e.Compatibility+(e.Compatibility=="Compatible"&&e.Reason!=""?" · warnings":"");labels[1].text="HP "+PartDelta(e,0)+"  ·  Torque "+PartDelta(e,1);labels[2].text="Weight "+PartDelta(e,2)+"  ·  Condition "+PartDelta(e,3);
            double health=e.Unit?.element.health??e.Part?.condition.health??1;bool failed=e.Unit!=null?e.Unit.element.health<=0:e.Part?.condition.failed??false;
            labels[3].text=e.Source+" · "+PartSemantics.Friendly(e.Definition.category.ToString())+(PartSemantics.Wears(e.Definition.category)?" · "+(failed?"FAILED":health.ToString("P0")):"");labels[3].EnableInClassList("car-critical",failed);labels[4].text=e.Price.HasValue?DisplayUnits.Money(e.Price.Value):"—";labels[5].text=e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable?"Adjustable":"Fixed";
        }
        void ApplyPartQuery()
        {
            if(partsBrowser==null)return;var list=partsBrowser.Entries.Where(e=>NativePartBrowser.Section(e.Definition.category)==partsSection).ToList();list.Sort(PartsQuery.Compare);int matches=0;
            string order=string.Join("/",list.Select(e=>e.Id));bool reorder=partsOrder!=order;partsOrder=order;
            var cards=partsGrid.Children().ToDictionary(card=>card.name);
            foreach(var e in list){if(!cards.TryGetValue("partCard:"+e.Id,out var card))continue;bool match=PartsQuery.Matches(e,game.Profile.cash);card.EnableInClassList("hidden",!match);if(match)matches++;if(reorder)card.BringToFront();}
            partsCount.text=matches+" / "+list.Count;partsEmpty.text=game.Profile.SelectedCar==null?"Select a car in Garage.":list.Any(e=>!e.Ready)?"Calculating part changes…":"No matching parts";partsEmpty.EnableInClassList("hidden",matches>0);partsSort.Q<Label>().text=PartsQuery.Sort+(PartsQuery.Descending?" ↓":" ↑");
        }
        void ClearPartDetails(){foreach(var s in partDetailSurfaces)surfaces.Remove(s);partDetailSurfaces.Clear();foreach(var child in partsDetail.Children().Where(c=>c!=partsDetailScroll&&c!=partPurchaseControls).ToArray())child.RemoveFromHierarchy();partsDetailScroll.Clear();}
        void DecoratePartDetail(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);partDetailSurfaces.Add(element.Q<RacingSurface>());}
        string PartStatText(PartStat stat)=>stat.label+": "+(stat.unit=="Nm"?SharedUnitSettings.Torque(stat.value,"0.##"):stat.unit=="bar"?SharedUnitSettings.Pressure(stat.value):stat.value.ToString("0.##")+" "+stat.unit);
        string PartDescription(PartDefinition definition){string text=definition.description??"";int supplier=text.IndexOf(" Supplier: ",StringComparison.Ordinal);return SharedUnitSettings.TorqueText(supplier<0?text:text.Substring(0,supplier));}
        void ShowPartsBrowsing(bool show){root.Q("partsToolbar").EnableInClassList("hidden",!show);root.Q("partsTabs").EnableInClassList("hidden",!show);partsPage.Q(className:"parts-sources").EnableInClassList("hidden",!show);partsReference.EnableInClassList("hidden",!show);}
        void BackToParts(){partsDetailId=null;ShowPartsBrowsing(true);partsScroll.RemoveFromClassList("hidden");partsDetail.AddToClassList("hidden");partsScroll.scrollOffset=partsReturnScroll;ApplyPartQuery();}
        void ChoosePartPosition(NativePartEntry entry)
        {
            var positions=partsBrowser.Positions(entry);string selected=entry.Position;
            var labels=positions.Select(p=>partsBrowser.PositionLabel(entry,p)).ToArray();
            OpenDialog("Replacement position",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=>{entry.Position=selected;entry.Ready=false;entry.Proposed=null;entry.Change=new double?[7];partsBrowser.Evaluate(entry);UpdatePartCard(entry);CloseDialog();OpenPartDetail(entry.Id,false);})},populate:content=>
                QueryChoices(content,"partUnitPosition","Position",labels,()=>partsBrowser.PositionLabel(entry,selected),v=>selected=positions[Array.IndexOf(labels,v)],2));
        }
        void PartDetailFact(VisualElement parent,string label,string value,string state=null)
        {
            var row=new VisualElement();row.AddToClassList("car-fact");parent.Add(row);
            var caption=new Label(label){enableRichText=false};caption.AddToClassList("car-fact-label");row.Add(caption);
            var text=new Label(value){enableRichText=false};text.AddToClassList("car-fact-value");if(state!=null)text.AddToClassList(state);row.Add(text);
        }
        void OpenPartDetail(string id,bool remember=true)
        {
            var e=partsBrowser.Entries.Find(p=>p.Id==id);if(e==null){partsDetailId=null;RenderPartGrid();return;}
            partsBrowser.Evaluate(e);var offer=e;e=PurchasePreview(offer);if(remember)partsReturnScroll=partsScroll.scrollOffset;
            var offset=remember?Vector2.zero:partsDetailScroll.scrollOffset;
            partsDetailId=id;ShowPartsBrowsing(false);ClearPartDetails();partsScroll.AddToClassList("hidden");partsDetail.RemoveFromClassList("hidden");
            var heading=new VisualElement{name="partDetailHeading"};heading.AddToClassList("part-detail-heading");partsDetail.Insert(0,heading);
            var back=PartButton(heading,"partBack","",BackToParts,detail:true);back.AddToClassList("car-back-button");back.Add(new FilledMenuIcon("Back"));back.tooltip="Back to parts";
            var title=new Label(e.Definition.name){name="partDetailTitle",enableRichText=false};title.AddToClassList("car-detail-title");title.AddToClassList("part-detail-title");heading.Add(title);
            var info=PartButton(heading,"partInfo","i",()=>OpenDialog(e.Definition.name,SharedUnitSettings.TorqueText(e.Definition.description+"\n"+PartTuning.Summary(e.Definition)+"\n"+e.Reason+"\n"+string.Join("\n",PartSemantics.Stats(e.Definition).Select(PartStatText))),new[]{new DialogAction("Close",CloseDialog)}),detail:true);info.AddToClassList("part-info-button");info.tooltip="All part specifications";
            BuildPurchaseControls(offer);
            var top=new VisualElement{name="partDetailTop"};top.AddToClassList("part-detail-top");partsDetailScroll.Add(top);
            var facts=new VisualElement{name="partDetailFacts"};facts.AddToClassList("part-detail-facts");top.Add(facts);top.EnableInClassList("part-preview-unavailable",e.Proposed==null);DecoratePartDetail(facts,RacingSurface.Kind.Panel);
            PartDetailFact(facts,"Compatibility",e.Compatibility,e.Compatibility=="Compatible"?"car-ready":"car-warning");
            PartDetailFact(facts,"Brand / quality",e.Definition.brand+" · "+e.Definition.quality);
            PartDetailFact(facts,"Type",PartSemantics.Friendly(e.Definition.category.ToString())+" · "+e.Source);
            string set=e.Unit==null?InventoryUnits.SetLabel(game.Profile,game.Profile.SelectedCar,e.Definition):"Individual "+e.Unit.element.role;
            PartDetailFact(facts,"Includes",set.Replace("1 units","1 unit"));
            if(PartSemantics.Wears(e.Definition.category)){
                double health=e.Unit?.element.health??e.Part?.condition.health??1;bool failed=e.Unit!=null?e.Unit.element.Failed:e.Part?.condition.failed??false;
                PartDetailFact(facts,"Part condition",failed?"Failed · "+health.ToString("P0"):health.ToString("P0"),failed||health<=.25?"car-critical":health<=.5?"car-warning":"car-ready");
            }
            PartDetailFact(facts,"Adjustment",e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable?"Adjustable":"Fixed");
            var description=new Label(PartDescription(e.Definition)){name="partDescription",enableRichText=false};description.AddToClassList("part-description");facts.Add(description);description.EnableInClassList("hidden",string.IsNullOrWhiteSpace(description.text));
            var specifications=new VisualElement{name="partKeySpecs"};specifications.AddToClassList("part-key-specs");partsDetailScroll.Add(specifications);
            foreach(var stat in PartSemantics.Stats(e.Definition).Take(4))PartDetailFact(specifications,stat.label,PartStatText(stat).Substring(stat.label.Length+2));
            if(!string.IsNullOrWhiteSpace(e.Reason)){
                var reasons=e.Reason.Split('\n').Where(r=>!string.IsNullOrWhiteSpace(r)).Distinct().ToArray();
                if(e.Proposed==null){var reason=new Label(SharedUnitSettings.TorqueText(e.Reason)){name="partAvailability",enableRichText=false};reason.AddToClassList("part-description");reason.AddToClassList("car-warning");facts.Add(reason);}
                else {var warning=PartButton(facts,"partWarnings","Warnings · "+reasons.Length,()=>OpenDialog("Warnings",SharedUnitSettings.TorqueText(e.Reason),new[]{new DialogAction("Close",CloseDialog)}),detail:true);warning.AddToClassList("part-warning-button");warning.AddToClassList(e.Proposed.warnings.Any(w=>w.critical)?"car-critical":"car-warning");}
            }
            bool fitting=e.Compatibility=="Compatible"||e.Compatibility=="Requires supporting changes";
            bool actualPosition=e.Unit==null||InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,e.Definition.id,e.Unit.element.role).Contains(e.Position);
            if(e.Source!="New"&&e.Unit!=null&&e.Position!=null){PartDetailFact(facts,"Position",partsBrowser.PositionLabel(e,e.Position));PartButton(facts,"partPosition","Change position",()=>ChoosePartPosition(e),detail:true);if(!actualPosition){var notice=new Label("Apply the matching complete set before replacing this unit.");notice.AddToClassList("part-description");facts.Add(notice);}}
            if(e.Source=="New"&&e.Unit!=null&&e.Position!=null)PartDetailFact(facts,"Preview",string.Join(" · ",PurchaseDraft(offer).Positions.Count>0?PurchaseDraft(offer).Positions.Select(p=>InventoryUnits.PositionLabel(game.Profile,game.Profile.SelectedCar,e.Definition.id,p)):new[]{partsBrowser.PositionLabel(e,e.Position)}));
            var actions=new VisualElement{name="partDetailActions"};actions.AddToClassList("car-details-actions");actions.AddToClassList("part-detail-actions");partsDetail.Add(actions);
            var price=new Label(e.Price.HasValue?DisplayUnits.Money(e.Price.Value):""){name="partDetailPrice",enableRichText=false};price.AddToClassList("part-detail-price");actions.Add(price);
            int queued=game.CartLines.Where(l=>e.Source=="New"?l.definitionId==e.Definition.id&&l.action==(e.Unit==null?CartAction.NewSet:CartAction.NewUnits)&&l.role==e.Unit?.element.role:l.assetId==e.AssetId).Sum(l=>l.quantity);
            if(queued>0||e.Source=="New"){var queue=new Label(queued>0?"Queued × "+queued:""){name="partQueued"};queue.AddToClassList("part-queued");actions.Add(queue);}
            if(game.PartsPage!="Installed"){
                if(e.Source=="New"||e.Source=="Used")PartButton(actions,"partAdd","Add to cart",()=>StagePurchase(offer,false),detail:true).SetEnabled(fitting&&game.Profile.reputation>=e.Definition.reputationRequired);
                var fit=PartButton(actions,"partPlanInstall",e.Unit!=null&&e.Source=="Owned"?"Install unit":e.Source=="New"||e.Source=="Used"?"Add & install":"Plan install",()=>{
                    if(e.Unit!=null&&e.Source=="Owned"){long revision=game.Profile.revision;OpenDialog("Install unit?",partsBrowser.PositionLabel(e,e.Position),new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Install",()=>{if(game.InstallNativeUnit(e.Unit.element.id,e.Position,revision)){CloseDialog();partsStateKey=null;}else root.Q<ScrollView>("modalContent").Add(new Label(game.NativeNotice));},true)});}
                    else StagePurchase(offer,true);
                },true,true);fit.tooltip=e.Unit!=null&&e.Source=="Owned"?"Replace the selected unit after confirmation.":"Adds an installation plan to the cart; apply it at checkout.";fit.SetEnabled(fitting&&actualPosition&&!game.NativeBlockPreparationPending(e.Definition)&&(e.Unit==null||e.Position!=null)&&game.Profile.reputation>=e.Definition.reputationRequired);
                if(game.NativeBlockPreparationPending(e.Definition)){var notice=new Label("Prepare a complete engine installation before installing this block.");notice.AddToClassList("part-description");facts.Add(notice);}
            }else PartButton(actions,"partPlanRemove","Plan removal",()=>{game.StageInventory(new PartCartLine{action=CartAction.Move,assetId=e.AssetId,definitionId=e.Definition.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage});partsStateKey=null;},detail:true);
            if(e.Source=="Owned"&&(e.Unit!=null||e.Part?.location==PartLocation.Inventory))PartButton(actions,"partPlanSale","Add sale",()=>{game.StageInventory(new PartCartLine{action=e.Unit==null?CartAction.SellPart:CartAction.SellUnit,assetId=e.AssetId,definitionId=e.Definition.id});partsStateKey=null;},detail:true);
            if(e.Proposed!=null&&partsBrowser.Reference!=null){
                NativeStatsPanel quick=null;
                quick=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,new[]{NativeStatCatalog.TuningSummary()},new Dictionary<string,bool>(),d=>OpenDialog(d.Label,d.InfoExplanation(quick.Current,quick.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed){name="partQuickStats"};quick.AddToClassList("part-quick-stats");quick.Q<Button>("statsDifferencesOnly").name="partQuickDifferences";
                var dyno=new VisualElement{name="partDetailDyno"};dyno.AddToClassList("part-detail-dyno");top.Add(dyno);DecoratePartDetail(dyno,RacingSurface.Kind.Panel);
                dyno.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Reference",partsPlanned?"Planned build":"Current build",partsBrowser.Reference,NativeDynoStyle.Primary),new NativeDynoSeries("WithPart","With part",e.Proposed,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","Current no wear",OwnedHealthy(game.Profile.SelectedCar),NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"partDyno"));partsDetailScroll.Insert(1,quick);
                if(!partDetailCollapsed.TryGetValue(id,out var collapsed)){collapsed=new Dictionary<string,bool>();partDetailCollapsed[id]=collapsed;}
                var groups=NativeStatCatalog.Groups(partsBrowser.Reference,e.Proposed).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children});
                NativeStatsPanel panel=null;
                panel=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,groups,collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed,partDetailDifferences.TryGetValue(id,out bool only)&&only){name="partStats"};panel.Q<Button>("statsDifferencesOnly").name="partDifferencesOnly";panel.DifferencesOnlyChanged=value=>partDetailDifferences[id]=value;partsDetailScroll.Add(panel);
            }
            if(offer.Source=="New")RefreshPurchasePrice(offer);
            ExcludeTabTraversal();partsDetailScroll.schedule.Execute(()=>{if(partsDetailId==id)partsDetailScroll.scrollOffset=offset;});
        }
    }
}
