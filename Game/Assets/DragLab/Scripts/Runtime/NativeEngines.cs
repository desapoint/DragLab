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
        bool engineMarketOpen;bool enginePageOpen;string engineDetailId,engineViewKey,engineReferenceId,engineSearch="";PlayerProfile engineViewProfile;
        VisualElement enginePage,engineContent,engineActions;ScrollView engineScroll;Label engineTitle;Button engineRename;
        readonly List<RacingSurface> engineSurfaces=new List<RacingSurface>();readonly Dictionary<string,bool> engineCollapsed=new Dictionary<string,bool>();
        internal int EngineViewBuildCount{get;private set;}
        void EngineSurface(VisualElement owner,RacingSurface.Kind kind){AddSurface(owner,kind);engineSurfaces.Add(owner.Q<RacingSurface>());}
        Button EngineButton(VisualElement parent,string id,string caption,Action action,bool primary=false)
        {var b=new Button(action){name=id,text=caption,focusable=false,tabIndex=-1};b.AddToClassList("action-button");if(primary)b.AddToClassList("primary-action");parent.Add(b);EngineSurface(b,primary?RacingSurface.Kind.Action:RacingSurface.Kind.SecondaryAction);return b;}
        void InitializeNativeEngines()
        {
            enginePage=new VisualElement{name="enginePage"};enginePage.AddToClassList("car-details-page");enginePage.AddToClassList("hidden");root.Q(className:"workspace").Add(enginePage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");enginePage.Add(heading);
            var back=new Button(()=>{if(engineDetailId!=null){engineDetailId=null;engineViewKey=null;}else enginePageOpen=false;Refresh();}){name="engineBack",focusable=false,tabIndex=-1};back.AddToClassList("action-button");back.AddToClassList("car-back-button");heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);back.Add(new FilledMenuIcon("Back"));
            engineTitle=new Label("Engines"){name="engineTitle",enableRichText=false};engineTitle.AddToClassList("car-detail-title");heading.Add(engineTitle);
            engineRename=new Button(OpenEngineRename){name="engineRename",focusable=false,tabIndex=-1,tooltip="Rename engine"};engineRename.AddToClassList("action-button");engineRename.AddToClassList("car-rename-button");heading.Add(engineRename);AddSurface(engineRename,RacingSurface.Kind.SecondaryAction);engineRename.Add(new FilledMenuIcon("Pencil"));
            var cartButton=new Button(()=>{enginePageOpen=false;OpenNativeCart();Refresh();}){name="engineCart",text="Cart",focusable=false,tabIndex=-1};cartButton.AddToClassList("action-button");heading.Add(cartButton);AddSurface(cartButton,RacingSurface.Kind.SecondaryAction);
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");enginePage.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            engineScroll=new ScrollView{name="engineScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};engineScroll.AddToClassList("car-details-scroll");frame.Add(engineScroll);engineContent=new VisualElement{name="engineContent"};engineContent.AddToClassList("car-details-content");engineScroll.Add(engineContent);
            engineActions=new VisualElement{name="engineActions"};engineActions.AddToClassList("car-details-actions");enginePage.Add(engineActions);
        }
        void RefreshEngines(string route)
        {
            if(route!="Parts"&&route!="Settings")enginePageOpen=false;
            enginePage.EnableInClassList("hidden",route!="Parts"||!enginePageOpen);if(route!="Parts"||!enginePageOpen)return;
            string key=game.Profile.revision+"/"+engineMarketOpen+"/"+engineDetailId+"/"+engineReferenceId+"/"+JsonUtility.ToJson(SharedUnitSettings.Current)+"/"+JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            if(engineViewProfile==game.Profile&&engineViewKey==key)return;engineViewProfile=game.Profile;engineViewKey=key;EngineViewBuildCount++;
            foreach(var s in engineSurfaces)surfaces.Remove(s);engineSurfaces.Clear();var offset=engineScroll.scrollOffset;engineContent.Clear();engineActions.Clear();
            var engine=game.Profile.engines.Find(e=>e.instanceId==engineDetailId);if(engineDetailId!=null&&engine==null)engineDetailId=null;
            engineRename.EnableInClassList("hidden",engine==null);engineTitle.text=engine==null?"Engines":AssemblyOperations.DisplayName(game.Profile,engine);
            if(engine==null)BuildEngineGrid();else BuildEngineDetail(engine);ExcludeTabTraversal();engineScroll.schedule.Execute(()=>engineScroll.scrollOffset=offset);
        }
        void BuildEngineGrid()
        {
            EngineButton(engineContent,"engineMarketToggle",engineMarketOpen?"Owned engines":"Engine shop",()=>{engineMarketOpen=!engineMarketOpen;engineViewKey=null;engineScroll.scrollOffset=Vector2.zero;Refresh();});
            if(engineMarketOpen){BuildEngineMarket();return;}
            var search=new TextField{name="engineSearch",value=engineSearch,maxLength=128};search.AddToClassList("setting-text");search.AddToClassList("owned-car-search");engineContent.Add(search);search.tooltip="Search engine names / models";
            var grid=new VisualElement{name="engineGrid"};grid.AddToClassList("owned-cars-grid");engineContent.Add(grid);
            foreach(var engine in game.Profile.engines.OrderBy(e=>AssemblyOperations.DisplayName(game.Profile,e),StringComparer.OrdinalIgnoreCase)){
                string id=engine.instanceId;var card=new Button(()=>{engineDetailId=id;engineReferenceId=null;engineViewKey=null;engineScroll.scrollOffset=Vector2.zero;Refresh();}){name="engineCard:"+id,focusable=false,tabIndex=-1};card.AddToClassList("owned-car-card");card.AddToClassList("clickable-panel");grid.Add(card);EngineSurface(card,RacingSurface.Kind.Panel);
                var title=new Label(AssemblyOperations.DisplayName(game.Profile,engine)){enableRichText=false};title.AddToClassList("car-card-title");card.Add(title);card.Add(new Label(game.Profile.catalog.Spec(engine.vehicleDefinitionId).displayName+" · #"+id.Substring(Math.Max(0,id.Length-6))){enableRichText=false});
                card.Add(new Label(string.IsNullOrEmpty(engine.parentVehicleId)?InventoryLocation(SharedInventory.StorageOf(engine.storageId)):InventoryLocation(engine.parentVehicleId)){enableRichText=false});
                var parts=EngineWorkshop.Parts(game.Profile,engine);card.Add(new Label(parts.Length+" parts · "+(parts.Length==0?"—":parts.Average(i=>i.condition.health).ToString("P0"))+" condition"));
                int faults=parts.Count(i=>i.condition.failed||EngineElements.NeedsService(i.condition));var status=new Label(EngineWorkshop.Missing(game.Profile,engine).Length+" missing · "+faults+" need service");status.AddToClassList(faults>0?"car-warning":"car-identity");card.Add(status);
                card.Query<VisualElement>().ForEach(e=>{if(e!=card)e.pickingMode=PickingMode.Ignore;});
            }
            void Filter(){foreach(var e in game.Profile.engines){var name=AssemblyOperations.DisplayName(game.Profile,e)+" "+game.Profile.catalog.Spec(e.vehicleDefinitionId).displayName;grid.Q("engineCard:"+e.instanceId).EnableInClassList("hidden",!name.ToLowerInvariant().Contains(engineSearch.Trim().ToLowerInvariant()));}}
            search.RegisterValueChangedCallback(e=>{engineSearch=e.newValue;Filter();});Filter();if(game.Profile.engines.Count==0)engineContent.Add(new Label("No owned engines"));
        }
        void BuildEngineMarket()
        {
            var p=game.Profile;
            engineContent.Add(new Label("Stock refreshes with the dealership · every 3 completed races"));
            var search=new TextField{name="engineMarketSearch",value=engineSearch,maxLength=128};search.AddToClassList("setting-text");engineContent.Add(search);
            var grid=new VisualElement{name="engineMarketGrid"};grid.AddToClassList("owned-cars-grid");engineContent.Add(grid);
            foreach(var offer in p.inventory.market.engines.Where(o=>!o.used||o.quantity>0)){
                string id=offer.id;var spec=p.catalog.Spec(offer.model);int price=PartsMarket.EnginePrice(offer);
                var card=new Button(()=>OpenEngineMarketPurchase(id)){name="engineOffer:"+id,focusable=false,tabIndex=-1};card.AddToClassList("owned-car-card");card.AddToClassList("clickable-panel");grid.Add(card);EngineSurface(card,RacingSurface.Kind.Panel);
                card.Add(new Label(spec.displayName+" · OEM engine"){enableRichText=false});
                card.Add(new Label((offer.used?"Used":"New")+" · "+offer.parts.Average(i=>i.condition.health).ToString("P0")+" condition · "+offer.parts.Count+" parts"));
                card.Add(new Label(offer.quantity>0?offer.quantity+" in stock":"Express delivery · +10%"));card.Add(new Label(DisplayUnits.Money(price)));
                card.tooltip="Buy into storage; swap or prepare later. Chassis compatibility is checked at installation.";
                card.Query<VisualElement>().ForEach(e=>{if(e!=card)e.pickingMode=PickingMode.Ignore;});
            }
            void Filter(){foreach(var o in p.inventory.market.engines){var card=grid.Q("engineOffer:"+o.id);if(card!=null)card.EnableInClassList("hidden",!(p.catalog.Spec(o.model).displayName+" "+(o.used?"used":"new")).ToLowerInvariant().Contains(engineSearch.Trim().ToLowerInvariant()));}}
            search.RegisterValueChangedCallback(e=>{engineSearch=e.newValue;Filter();});Filter();if(grid.childCount==0)engineContent.Add(new Label("No engines in stock"));
        }
        void OpenEngineMarketPurchase(string id)
        {
            var offer=game.Profile.inventory.market.engines.Find(o=>o.id==id);if(offer==null)return;
            long revision=game.Profile.revision;int price=PartsMarket.EnginePrice(offer);string snapshot=JsonUtility.ToJson(offer);
            var error=new Label{name="enginePurchaseError",enableRichText=false};error.AddToClassList("car-critical");bool valid=true;
            try{PartsMarket.BuyEngine(EngineWorkshop.Copy(game.Profile),id,price);}catch(Exception e){valid=false;error.text=e.Message;}
            OpenDialog("Buy engine",DisplayUnits.Money(price)+" · Balance "+DisplayUnits.Money(game.Profile.cash)+(offer.quantity==0?" · Express delivery included (+10%)":""),new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Buy",()=>{
                if(game.CartCount>0){error.text="Checkout or clear the cart first.";return;}
                if(!game.CommitEngineAction(revision,p=>{var current=p.inventory.market.engines.Find(o=>o.id==id);if(current==null||!SnapshotEquality.Serialized(snapshot,current))throw new InvalidOperationException("Engine offer changed. Review again.");PartsMarket.BuyEngine(p,id,price);})){error.text=game.NativeNotice;return;}
                CloseDialog();engineViewKey=null;partsStateKey=null;Refresh();
            },true)},populate:content=>{
                content.Add(new Label(game.Profile.catalog.Spec(offer.model).displayName+" · stock configuration · store in garage"){enableRichText=false});
                content.Add(new Label("Oil "+offer.engine.fluids.oilLitres.ToString("0.##")+" L · coolant "+offer.engine.fluids.coolantLitres.ToString("0.##")+" L"));
                foreach(var section in offer.parts.GroupBy(i=>NativePartBrowser.Section(game.Profile.catalog.Find(i.definitionId).category))){content.Add(new Label(NativePartBrowser.SectionNames[(int)section.Key]));foreach(var item in section)content.Add(new Label(game.Profile.catalog.Find(item.definitionId).name+" · "+item.condition.health.ToString("P0")){enableRichText=false});}content.Add(error);
            });root.Q<Button>("dialogBuy").SetEnabled(valid&&game.CartCount==0);ExcludeTabTraversal();
        }
        void OpenEngineRename()
        {
            var engine=game.Profile.engines.Find(e=>e.instanceId==engineDetailId);if(engine==null)return;string id=engine.instanceId;long revision=game.Profile.revision;
            var input=new TextField{name="engineName",value=engine.customName??"",maxLength=VehicleCollection.NameLimit};input.AddToClassList("setting-text");var error=new Label{name="engineNameError",enableRichText=false};error.AddToClassList("car-critical");
            OpenDialog("Rename engine",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Save",()=>{if(!game.RenameNativeEngine(id,revision,input.value)){error.text=game.NativeNotice;return;}CloseDialog();engineViewKey=null;Refresh();},true)},populate:content=>{content.Add(input);content.Add(new Label("Blank: "+game.Profile.catalog.Spec(engine.vehicleDefinitionId).displayName+" engine"));content.Add(error);});input.Focus();ExcludeTabTraversal();
        }
        OwnedCar EngineReference(EngineAssemblyInstance engine)
        {
            var p=game.Profile;return p.cars.Find(c=>c.instanceId==(engine.parentVehicleId??engineReferenceId))??p.cars.Find(c=>c.instanceId==engineReferenceId&&AssemblyOperations.Fits(engine,c))??p.cars.Find(c=>c.instanceId==p.selectedOwnedVehicleId&&AssemblyOperations.Fits(engine,c))??p.cars.FirstOrDefault(c=>AssemblyOperations.Fits(engine,c));
        }
        void EngineComparison(VisualElement parent,CarInspection current,CarInspection proposed,string id,Action<VisualElement,RacingSurface.Kind> decorate,bool detailed=true,CarInspection healthy=null,Action<NativeStatDefinition,NativeStatsPanel> info=null,string proposedLabel="With engine")
        {
            if(proposed==null){parent.Add(new Label("Dyno unavailable · choose a compatible chassis"));return;}
            var series=new List<NativeDynoSeries>();if(current!=null)series.Add(new NativeDynoSeries("Current","Current car",current,NativeDynoStyle.Primary));series.Add(new NativeDynoSeries("Engine",proposedLabel,proposed,NativeDynoStyle.Alternative));if(healthy!=null)series.Add(new NativeDynoSeries("NoWear","No wear",healthy,NativeDynoStyle.Reference));parent.Add(new NativeDynoPanel(series,SharedUnitSettings.Current.torque,id+"Dyno"));
            NativeStatsPanel quick=null;quick=new NativeStatsPanel(current??proposed,SharedUnitSettings.Current,new[]{NativeStatCatalog.TuningSummary()},new Dictionary<string,bool>(),d=>{if(info!=null)info(d,quick);else OpenDialog(d.Label,d.InfoExplanation(quick.Current,quick.Proposed),new[]{new DialogAction("Close",CloseDialog)});},decorate,current==null?null:proposed){name=id+"Summary"};parent.Add(quick);
            if(detailed){NativeStatsPanel full=null;var groups=NativeStatCatalog.Groups(current??proposed,proposed).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children});full=new NativeStatsPanel(current??proposed,SharedUnitSettings.Current,groups,engineCollapsed,d=>OpenDialog(d.Label,d.InfoExplanation(full.Current,full.Proposed),new[]{new DialogAction("Close",CloseDialog)}),decorate,current==null?null:proposed){name=id+"Stats"};parent.Add(full);}
        }
        void BuildEngineDetail(EngineAssemblyInstance engine)
        {
            var p=game.Profile;bool stored=string.IsNullOrEmpty(engine.parentVehicleId);string id=engine.instanceId;var reference=EngineReference(engine);CarInspection current=null,proposed=null,healthy=null;string error=null;
            if(reference!=null)try{current=InspectOwned(reference)?.Stats;var copy=EngineWorkshop.Preview(p,id,reference.instanceId,game.CartLines);var car=copy.cars.Find(c=>c.instanceId==reference.instanceId);proposed=NativePartBrowser.Inspect(InspectionBuild.Capture(copy,car));healthy=NativePartBrowser.Inspect(InspectionBuild.Capture(copy,car,healthy:true));}catch(Exception e){error=e.Message;}
            engineContent.Add(new Label(stored?InventoryLocation(SharedInventory.StorageOf(engine.storageId)):InventoryLocation(engine.parentVehicleId)){name="engineLocation",enableRichText=false});
            var actions=engineActions;
            if(stored){EngineButton(actions,"enginePrepare","Prepare parts",()=>OpenEnginePartPicker(id));EngineButton(actions,"engineSwap","Swap / install",()=>OpenEngineSwap(id),true).SetEnabled(p.cars.Any(c=>AssemblyOperations.Fits(engine,c)));}
            else EngineButton(actions,"engineDetach","Detach",()=>ConfirmEngineAction("Detach engine",id,q=>AssemblyOperations.Detach(q,engine.parentVehicleId),"The car cannot run until an engine is installed. Parts, wear and fluids stay with this engine."));
            if(reference!=null)engineContent.Add(new Label("Preview in "+VehicleCollection.DisplayName(p.catalog,reference)){enableRichText=false});
            if(error!=null){var unavailable=new Label(error){enableRichText=false};unavailable.AddToClassList("car-critical");engineContent.Add(unavailable);}else EngineComparison(engineContent,current,proposed,"engine",EngineSurface,healthy:healthy);
            if(proposed!=null)WarningButton(actions,"engineWarnings",proposed.warnings.ToArray(),()=>OpenCombinedWarnings(current?.warnings.ToArray()??Array.Empty<SetupWarning>(),proposed.warnings.ToArray(),current!=null,carId:p.cars.FirstOrDefault(c=>c.engineAssemblyId==id)?.instanceId,repairLinks:!stored,prepareEngine:stored?(Action<PartCategory>)(category=>{CloseDialog();OpenEnginePartPicker(id,category);}):null),EngineSurface);
            var missing=EngineWorkshop.Missing(p,engine);if(missing.Length>0){engineContent.Add(new Label("Missing: "+string.Join(" · ",missing.Select(def=>PartSemantics.Friendly(p.catalog.Find(def).category.ToString())))));if(stored){int price=EngineWorkshop.MissingPrice(p,engine);EngineButton(engineContent,"engineBuyMissing","Buy missing OEM · "+DisplayUnits.Money(price),()=>ConfirmEngineAction("Buy missing OEM parts",id,q=>{if(EngineWorkshop.MissingPrice(q,q.engines.Find(e=>e.instanceId==id))!=price)throw new InvalidOperationException("Required parts or prices changed.");foreach(string def in missing)EngineWorkshop.BuyPart(q,id,def,PartsMarket.Quote(q,q.catalog.Find(def),EngineWorkshop.Count(q,q.engines.Find(e=>e.instanceId==id),q.catalog.Find(def))).cost);},DisplayUnits.Money(price)+" · Existing parts and fluids are retained."),true);}}
            foreach(var section in EngineWorkshop.Parts(p,engine).GroupBy(part=>NativePartBrowser.Section(p.catalog.Find(part.definitionId).category))){
                var body=new VisualElement();var toggle=EngineButton(engineContent,"engineGroup:"+section.Key,NativePartBrowser.SectionNames[(int)section.Key]+" · "+section.Count(),()=>body.EnableInClassList("hidden",!body.ClassListContains("hidden")));body.AddToClassList("hidden");engineContent.Add(body);
                foreach(var part in section){string partId=part.instanceId;var row=new VisualElement();row.AddToClassList("warning-item");body.Add(row);row.Add(new Label(p.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0")+(part.condition.failed?" · FAILED":"")){enableRichText=false});
                    if(part.condition.elements.Length>0)EngineButton(row,"engineMembers:"+partId,"Individual components · "+part.condition.elements.Length,()=>OpenEngineMembers(id,partId)).SetEnabled(stored);
                    if(stored)EngineButton(row,"engineRemove:"+partId,"Remove",()=>ConfirmEngineAction("Remove part",id,q=>AssemblyOperations.RemovePart(q,id,q.catalog.Find(part.definitionId).category),"The exact part moves to storage. The engine may become incomplete."));
                }
            }
        }
        void ConfirmEngineAction(string title,string id,Action<PlayerProfile> action,string message)
        {
            long revision=game.Profile.revision;var error=new Label{name="engineActionError",enableRichText=false};error.AddToClassList("car-critical");
            bool valid=false;
            OpenDialog(title,message,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=>{if(game.CartCount>0){error.text="Checkout or clear the cart before this direct workshop action.";return;}if(!game.CommitEngineAction(revision,action)){error.text=game.NativeNotice;return;}CloseDialog();engineViewKey=null;partsStateKey=null;Refresh();},true)},populate:content=>{
                try{if(game.CartCount>0)throw new InvalidOperationException("Checkout or clear the cart before this direct workshop action.");var copy=EngineWorkshop.Copy(game.Profile);action(copy);MechanicsOwnership.Ensure(copy,copy.catalog);valid=true;
                    var engine=copy.engines.Find(e=>e.instanceId==id);var reference=EngineReference(game.Profile.engines.Find(e=>e.instanceId==id));
                    if(reference!=null&&title!="Detach engine"){var preview=EngineWorkshop.Preview(copy,id,reference.instanceId);var car=preview.cars.Find(c=>c.instanceId==reference.instanceId);var inspection=NativePartBrowser.Inspect(InspectionBuild.Capture(preview,car));EngineComparison(content,InspectOwned(reference)?.Stats,inspection,"engineAction",(element,kind)=>{AddSurface(element,kind);dialogSurfaces.Add(element.Q<RacingSurface>());},false,NativePartBrowser.Inspect(InspectionBuild.Capture(preview,car,healthy:true)));
                        foreach(var warning in inspection.warnings){var text=new Label(warning.title+" · "+warning.explanation+"\n"+warning.suggestion){enableRichText=false};text.AddToClassList(warning.critical?"car-critical":"car-warning");content.Add(text);}
                    }
                }catch(Exception e){error.text=e.Message;}
                content.Add(error);
            });root.Q<Button>("dialogConfirm").SetEnabled(valid);ExcludeTabTraversal();
        }
        void OpenEngineSwap(string id)
        {
            long revision=game.Profile.revision;var engine=game.Profile.engines.Find(e=>e.instanceId==id);string carId=EngineReference(engine)?.instanceId;PartCartQuote quote=null;string quoteError=null;
            try{if(game.CartCount>0)quote=game.QuoteNativeCart();}catch(Exception e){quoteError=e.Message;}
            var error=new Label{name="engineSwapError",enableRichText=false};error.AddToClassList("car-critical");VisualElement preview=null;Action update=null;
            OpenDialog("Swap engine",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=>{try{if(quoteError!=null)throw new InvalidOperationException(quoteError);if(quote==null&&game.CartCount>0)throw new InvalidOperationException("Cart changed. Reopen swap preview.");if(!game.CommitEngineSwap(revision,id,carId,quote)){error.text=game.NativeNotice;return;}CloseDialog();engineViewKey=null;partsStateKey=null;Refresh();}catch(Exception e){error.text=e.Message;}},true)},populate:content=>{
                foreach(var car in game.Profile.cars){string target=car.instanceId;var b=new Button(()=>{carId=target;update();}){name="engineSwapTarget:"+target,text=VehicleCollection.DisplayName(game.Profile.catalog,car)+" · #"+car.instanceId.Substring(Math.Max(0,car.instanceId.Length-6))+(car.instanceId==game.Profile.selectedOwnedVehicleId?" · CURRENT":""),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");content.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());b.SetEnabled(AssemblyOperations.Fits(engine,car));if(!b.enabledSelf)b.tooltip="Engine mounts do not fit this chassis.";}
                preview=new VisualElement{name="engineSwapPreview"};content.Add(preview);content.Add(error);
                update=()=>{preview.Clear();error.text=quoteError??"";bool valid=false;foreach(var b in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("engineSwapTarget:")))b.EnableInClassList("selected",b.name=="engineSwapTarget:"+carId);
                    if(carId!=null&&quoteError==null)try{var car=game.Profile.cars.Find(c=>c.instanceId==carId);var copy=EngineWorkshop.Preview(game.Profile,id,carId,game.CartLines);var next=copy.cars.Find(c=>c.instanceId==carId);var build=MechanicsOwnership.Resolve(copy,next,copy.catalog);preview.Add(new Label((string.IsNullOrEmpty(car.engineAssemblyId)?"Empty chassis":"Store "+AssemblyOperations.DisplayName(game.Profile,game.Profile.engines.Find(e=>e.instanceId==car.engineAssemblyId)))+" · "+DisplayUnits.Money(quote?.total??0)+" cart total"){enableRichText=false});
                        if(!build.Drivable){var blocked=new Label("Replacement cannot run: "+string.Join(" · ",build.errors)){enableRichText=false};blocked.AddToClassList("car-critical");preview.Add(blocked);}EngineComparison(preview,InspectOwned(car)?.Stats,NativePartBrowser.Inspect(InspectionBuild.Capture(copy,next)),"engineSwap",(element,kind)=>{AddSurface(element,kind);dialogSurfaces.Add(element.Q<RacingSurface>());},false,NativePartBrowser.Inspect(InspectionBuild.Capture(copy,next,healthy:true)));valid=true;
                    }catch(Exception e){error.text=e.Message;}root.Q<Button>("dialogConfirm").SetEnabled(valid);
                };
            });update();ExcludeTabTraversal();
        }
        void OpenEnginePartPicker(string id,PartCategory? initialCategory=null)
        {
            var engine=game.Profile.engines.Find(e=>e.instanceId==id);PartCategory category=initialCategory??PartCategory.Pistons;VisualElement choices=null;Action update=null;
            OpenDialog("Prepare engine parts",null,new[]{new DialogAction("Close",CloseDialog)},populate:content=>{
                QueryChoices(content,"enginePartCategory","Category",MechanicalCatalog.Slots(game.Profile.catalog.Spec(engine.vehicleDefinitionId)).Where(MechanicalCatalog.EngineSlot).Select(c=>c.ToString()).ToArray(),()=>category.ToString(),value=>{category=(PartCategory)Enum.Parse(typeof(PartCategory),value);update();},3);
                foreach(var b in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("enginePartCategory:")))b.Q<Label>().text=PartSemantics.Friendly(b.name.Substring("enginePartCategory:".Length));
                choices=new VisualElement();content.Add(choices);update=()=>{choices.Clear();
                    foreach(var part in game.Profile.parts.Where(i=>i.location==PartLocation.Inventory&&game.Profile.catalog.Find(i.definitionId).category==category&&game.Profile.catalog.Fits(game.Profile.catalog.Spec(engine.vehicleDefinitionId),game.Profile.catalog.Find(i.definitionId)))){string partId=part.instanceId;var b=new Button(()=>ConfirmEngineAction("Install owned part",id,q=>AssemblyOperations.InstallPart(q,id,partId),game.Profile.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0")+" · Removed part stays in storage")){name="engineFit:"+partId,text="Owned · "+game.Profile.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0"),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");choices.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                    foreach(var definition in game.Profile.catalog.parts.Where(d=>d.category==category&&game.Profile.catalog.Fits(game.Profile.catalog.Spec(engine.vehicleDefinitionId),d)).OrderBy(d=>d.price)){string def=definition.id;int price=PartsMarket.Quote(game.Profile,definition,EngineWorkshop.Count(game.Profile,engine,definition)).cost;int count=EngineWorkshop.Count(game.Profile,engine,definition);var b=new Button(()=>ConfirmEngineAction("Buy & install complete set",id,q=>EngineWorkshop.BuyPart(q,id,def,price),definition.name+" · "+count+" units · "+DisplayUnits.Money(price)+" · Removed part stays in storage")){name="engineBuy:"+def,text=definition.name+" · "+count+" units · "+DisplayUnits.Money(price),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");b.SetEnabled(game.Profile.cash>=price&&game.Profile.reputation>=definition.reputationRequired);choices.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                };
            });update();ExcludeTabTraversal();
        }
        void OpenEngineMembers(string engineId,string partId)
        {
            var part=game.Profile.parts.Find(i=>i.instanceId==partId);OpenDialog("Individual components",null,new[]{new DialogAction("Close",CloseDialog)},populate:content=>{
                foreach(var element in part.condition.elements){string member=element.id;var row=new VisualElement();row.AddToClassList("warning-item");content.Add(row);row.Add(new Label(InventoryUnits.PositionLabel(game.Profile,null,part.definitionId,member)+" · "+element.health.ToString("P0")+" · "+PartSemantics.Friendly(element.fault.ToString())+" · "+game.Profile.catalog.Find(part.definitionId).name){enableRichText=false});
                    int price=PartsMarket.Quote(game.Profile,game.Profile.catalog.Find(part.definitionId),part.condition.elements.Length,true).cost;var buy=new Button(()=>ConfirmEngineAction("Replace individual component",engineId,q=>{var pack=q.parts.Find(i=>i.instanceId==partId);if(PartsMarket.Quote(q,q.catalog.Find(pack.definitionId),pack.condition.elements.Length,true).cost!=price)throw new InvalidOperationException("Price changed.");EngineElementService.ReplaceStored(q,engineId,partId,member);},InventoryUnits.PositionLabel(game.Profile,null,part.definitionId,member)+" · "+DisplayUnits.Money(price)+" · Retain removed component")){name="engineMemberBuy:"+member,text="Replace · "+DisplayUnits.Money(price),focusable=false,tabIndex=-1};buy.AddToClassList("action-button");row.Add(buy);AddSurface(buy,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(buy.Q<RacingSurface>());
                    foreach(var spare in game.Profile.looseEngineElements.Where(u=>u.definitionId==part.definitionId&&u.element.role==element.role&&!u.element.Failed)){string spareId=spare.element.id;var reuse=new Button(()=>ConfirmEngineAction("Reuse individual component",engineId,q=>EngineElementService.ReplaceStored(q,engineId,partId,member,spareId),spare.element.health.ToString("P0")+" · Retain wear and removed component")){name="engineMemberReuse:"+member+":"+spareId,text="Reuse spare · "+spare.element.health.ToString("P0"),focusable=false,tabIndex=-1};reuse.AddToClassList("action-button");row.Add(reuse);AddSurface(reuse,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(reuse.Q<RacingSurface>());}
                }
            });ExcludeTabTraversal();
        }
    }
}
