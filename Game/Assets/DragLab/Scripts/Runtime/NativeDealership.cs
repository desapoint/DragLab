using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        sealed class DealerModel
        {
            public VehicleOffer Offer;
            public bool Known;
            public BuildResolution Exterior;
            public CarInspection Stats, Driving, Healthy;
            public OwnedCarEntry Entry;
            public int Price;
            public string Blocker;
        }
        bool dealerMode,lastFirstPurchase;
        string dealerStockKey,dealerInventoryKey;
        string InventoryKey=>game.Profile.vehicleStock.seed+"/"+game.Profile.vehicleStock.generation+"/"+game.Profile.vehicleStock.purchases+"/"+game.Profile.firstPurchasePending;
        string StockKey=>game.Profile.vehicleStock.seed+"/"+game.Profile.vehicleStock.generation+"/"+game.Profile.vehicleStock.purchases+"/"+game.Profile.firstPurchasePending+"/"+game.Profile.vehicleStock.knowledgeRevision;
        void StartDealership(){bool pending=game.Profile.firstPurchasePending;if(pending&&!lastFirstPurchase){dealerMode=true;dealerDetailId=null;ownedDetailId=null;}lastFirstPurchase=pending;}
        string dealerDetailId;
        Vector2 dealerReturnScroll;
        VisualElement garagePages,dealerGrid,dealerDetails,dealerContent;
        ScrollView dealerScroll;
        Label dealerNoMatches,dealerError;
        Button ownedPageButton,dealerPageButton,dealerBuy,dealerInspect;
        readonly Dictionary<string,OwnedCarEntry> dealerEntries=new Dictionary<string,OwnedCarEntry>();
        readonly Dictionary<string,DealerModel> dealerModels=new Dictionary<string,DealerModel>();
        ConcurrentQueue<KeyValuePair<string,CarInspection>> dealerRatingUpdates;
        readonly List<RacingSurface> dealerSurfaces=new List<RacingSurface>();
        readonly List<RacingSurface> dealerStatsSurfaces=new List<RacingSurface>();
        readonly HashSet<string> dealerStatsShown=new HashSet<string>();
        readonly Dictionary<string,bool> dealerStatsCollapsed=new Dictionary<string,bool>();
        MechanicalCatalog dealerCatalog;
        PlayerProfile dealerProfile;
        long dealerRevision=-1;
        TorqueUnit dealerTorque;
        SpeedUnit dealerSpeed;
        Task<Dictionary<string,CarInspection>> dealerRatings;
        CancellationTokenSource dealerCancellation;
        internal bool DealerPageOpen=>dealerMode&&game.ScreenName=="Garage"&&Visible;
        internal bool DealerDetailsOpen=>DealerPageOpen&&dealerDetailId!=null;
        internal void InvalidateDealer()=>dealerRevision=-1;
        internal int DealerProbeCount {get;private set;}
        void InitializeDealership()
        {
            var workspace=root.Q(className:"workspace");
            garagePages=new VisualElement {name="garagePages"};garagePages.AddToClassList("choice-group");garagePages.AddToClassList("garage-pages");workspace.Insert(0,garagePages);
            Button Page(string id,string text,bool dealer,bool first) {
                var button=new Button(()=>SwitchGaragePage(dealer)) {name=id,text=text,tabIndex=-1,focusable=false};
                button.AddToClassList("choice-button");button.AddToClassList(first?"choice-first":"choice-last");garagePages.Add(button);
                AddSurface(button,first?RacingSurface.Kind.ChoiceFirst:RacingSurface.Kind.ChoiceLast);return button;
            }
            ownedPageButton=Page("garageOwned","Owned cars",false,true);dealerPageButton=Page("garageDealer","Dealership",true,false);
            dealerScroll=new ScrollView {name="dealerCars",horizontalScrollerVisibility=ScrollerVisibility.Hidden};dealerScroll.AddToClassList("owned-cars-scroll");dealerScroll.AddToClassList("hidden");workspace.Add(dealerScroll);
            dealerGrid=new VisualElement {name="dealerCarGrid"};dealerGrid.AddToClassList("owned-cars-grid");dealerScroll.Add(dealerGrid);
            dealerNoMatches=new Label("No matching cars");dealerNoMatches.AddToClassList("car-empty");dealerNoMatches.AddToClassList("hidden");dealerScroll.Add(dealerNoMatches);
            dealerDetails=new VisualElement {name="dealerDetails"};dealerDetails.AddToClassList("car-details-page");dealerDetails.AddToClassList("hidden");workspace.Add(dealerDetails);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");dealerDetails.Add(heading);
            var back=new Button(BackToDealer) {name="dealerBack",tooltip="Back to dealership",tabIndex=-1,focusable=false};back.AddToClassList("action-button");back.AddToClassList("car-back-button");back.Add(new FilledMenuIcon("Back"));heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);
            var title=new Label {name="dealerDetailTitle",enableRichText=false};title.AddToClassList("car-detail-title");heading.Add(title);
            AddEngineListening(heading,"dealerHearEngine",()=>dealerModels[dealerDetailId].Stats.source,AddSurface,()=>dealerDetailId==null||!dealerModels.TryGetValue(dealerDetailId,out var heard)||!heard.Known);
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");dealerDetails.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            var scroll=new ScrollView {name="dealerDetailScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("car-details-scroll");frame.Add(scroll);
            dealerContent=new VisualElement {name="dealerContent"};dealerContent.AddToClassList("car-details-content");scroll.Add(dealerContent);
            var actions=new VisualElement();actions.AddToClassList("car-details-actions");dealerDetails.Add(actions);
            dealerError=new Label {name="dealerPurchaseError"};dealerError.AddToClassList("car-critical");dealerError.AddToClassList("car-selection-error");actions.Add(dealerError);
            dealerBuy=new Button(OpenDealerPurchase) {name="dealerBuy",text="Buy",tabIndex=-1,focusable=false};dealerBuy.AddToClassList("action-button");dealerBuy.AddToClassList("primary-action");actions.Add(dealerBuy);AddSurface(dealerBuy,RacingSurface.Kind.Action);
            dealerInspect=new Button(OpenDealerInspection){name="dealerInspect",text="Inspect",tabIndex=-1,focusable=false};dealerInspect.AddToClassList("action-button");dealerInspect.Add(new FilledMenuIcon("Build"));actions.Insert(actions.childCount-1,dealerInspect);AddSurface(dealerInspect,RacingSurface.Kind.SecondaryAction);
            dealerCompare=CarCompareButton(actions,"dealerCompareCar",ToggleDealerComparison);
        }
        void SwitchGaragePage(bool dealer)
        {
            if(ModalOpen||game.ScreenName!="Garage"||dealerMode==dealer)return;
            ownedSearch.Blur();dealerMode=dealer;ownedSearch.SetValueWithoutNotify(ownedQuery.Search);
            ownedSearch.Q("ownedCarSearchHint").EnableInClassList("hidden",!string.IsNullOrEmpty(ownedQuery.Search));
            Refresh();ApplyOwnedQuery();ExcludeTabTraversal();
        }
        void RefreshDealership(string route)
        {
            garagePages.EnableInClassList("hidden",route!="Garage"||disposalCarId!=null);ownedPageButton.EnableInClassList("selected",!dealerMode);dealerPageButton.EnableInClassList("selected",dealerMode);
            dealerScroll.EnableInClassList("hidden",route!="Garage"||!dealerMode||dealerDetailId!=null);
            dealerDetails.EnableInClassList("hidden",route!="Garage"||!dealerMode||dealerDetailId==null);
            if(route!="Garage"||!dealerMode)return;
            var profile=game.Profile;var units=SharedUnitSettings.Current.torque;
            if(dealerCatalog!=profile.catalog||dealerStockKey!=StockKey)PrepareDealerModels();
            if(dealerProfile!=profile||dealerRevision!=profile.revision||dealerTorque!=units||dealerSpeed!=SharedUnitSettings.Current.speed)BuildDealerGrid();
            bool changed=false;
            while(dealerRatingUpdates!=null&&dealerRatingUpdates.TryDequeue(out var pair))if(dealerModels.TryGetValue(pair.Key,out var model)) {
                changed=true;
                model.Driving=pair.Value;model.Entry.RatingPending=false;
                model.Entry.Rating=pair.Value!=null&&pair.Value.completed?Benchmark.Rating(pair.Value.elapsed,pair.Value.trapMps*3.6):null;
                var label=dealerGrid.Q<Label>("dealerClass:"+pair.Key);if(label!=null)label.text=DealerRating(model);
            }
            if(dealerRatings!=null&&dealerRatings.IsCompleted){try{dealerRatings.GetAwaiter().GetResult();}catch(Exception){foreach(var model in dealerModels.Values.Where(m=>m.Entry.RatingPending)){model.Entry.RatingPending=false;model.Entry.Rating=null;}changed=true;}dealerRatings=null;}
            if(!changed)return;
            ApplyOwnedQuery();
            if(dealerDetailId!=null&&dealerModels.TryGetValue(dealerDetailId,out var viewed)) {
                var panel=dealerContent.Q<NativeStatsPanel>("dealerCarStats");
                if(comparingDealerId==dealerDetailId&&InspectOwned(game.Profile.SelectedCar)?.Stats is CarInspection selected)ComparisonSummary(dealerContent.Q(className:"car-summary"),selected,viewed.Stats,viewed.Entry,viewed.Offer.reportedCondition);
                else {var rating=dealerContent.Q<Label>("dealerClass:"+dealerDetailId);if(rating!=null)rating.text=DealerRating(viewed);}
                var estimate=dealerContent.Q<Label>("dealerQuarterEstimate");if(estimate!=null)estimate.text=DealerEstimate(viewed);
            }
        }
        void PrepareDealerModels()
        {
            while(dealerRatingUpdates!=null&&dealerRatingUpdates.TryDequeue(out var update))if(dealerModels.TryGetValue(update.Key,out var completed)){completed.Driving=update.Value;completed.Entry.RatingPending=false;completed.Entry.Rating=update.Value!=null&&update.Value.completed?Benchmark.Rating(update.Value.elapsed,update.Value.trapMps*3.6):null;}
            var previous=dealerInventoryKey==InventoryKey&&dealerCatalog==game.Profile.catalog?new Dictionary<string,DealerModel>(dealerModels):null;dealerInventoryKey=InventoryKey;
            dealerCancellation?.Cancel();dealerCancellation?.Dispose();dealerCancellation=new CancellationTokenSource();var token=dealerCancellation.Token;
            dealerStockKey=StockKey;dealerCatalog=game.Profile.catalog;dealerModels.Clear();dealerEntries.Clear();dealerRevision=-1;
            var probes=new List<Tuple<string,InspectionBuild,CarSimulation>>();
            foreach(var offer in game.Profile.vehicleStock.listings) {
                if(previous!=null&&previous.TryGetValue(offer.listingId,out var retained)&&retained.Known==VehicleInspection.Known(offer)&&retained.Offer.reportedCondition==offer.reportedCondition){
                    retained.Offer=offer;dealerModels.Add(offer.listingId,retained);dealerEntries.Add(offer.listingId,retained.Entry);
                    if(retained.Driving==null&&retained.Stats!=null)probes.Add(Tuple.Create(offer.listingId,retained.Stats.source,CarInspection.PrepareCar(retained.Stats.source)));
                    continue;
                }
                var vehicle=game.vehicles.First(v=>v.spec.id==offer.model);
                var entry=new OwnedCarEntry {Id=offer.listingId,StockCondition=offer.used?"Used":"New",Name=vehicle.spec.displayName,Model=vehicle.spec.displayName,Order=dealerModels.Count,Status="Unavailable",RatingPending=true};
                var model=new DealerModel {Offer=offer,Entry=entry,Known=VehicleInspection.Known(offer)};dealerModels.Add(offer.listingId,model);dealerEntries.Add(offer.listingId,entry);
                try {
                    // Only this isolated factory profile is initialized. No existing owned assets are normalized or healed.
                    var publicOffer=VehicleInspection.PublicOffer(dealerCatalog,offer,vehicle.CreateStockTune());
                    model.Exterior=VehicleInspection.Hardware(dealerCatalog,offer);
                    var factory=VehicleCollection.Preview(dealerCatalog,publicOffer);
                    var source=InspectionBuild.Capture(factory,factory.SelectedCar);
                    model.Stats=NativePartBrowser.Inspect(source,partPreviewCache);
                    var healthySource=InspectionBuild.Capture(factory,factory.SelectedCar,healthy:true);model.Healthy=NativePartBrowser.Inspect(healthySource,partPreviewCache);
                    var build=source.build;var stats=model.Stats;
                    entry.Hp=stats.peakHp;entry.Torque=stats.peakTorque;entry.Weight=stats.vehicleMassKg;entry.Condition=offer.reportedCondition*100;entry.Drive=build.spec.drive.ToString();entry.Type=BodyName(build.spec.bodyType);
                    entry.Gearbox=OwnedCarEntry.GearboxName(TransmissionDynamics.Configuration(build.spec,build.tune));entry.Induction=InductionName(new InductionProfile(build.spec,build.tune).type);entry.LockedCenter=build.spec.drive==DriveType.AWD&&AxleDriveDynamics.Configuration(build.spec,build.tune).mode==AwdMode.LockedCenter;
                    probes.Add(Tuple.Create(offer.listingId,source,CarInspection.PrepareCar(source)));
                }catch(Exception){entry.RatingPending=false;model.Blocker="Stock build unavailable";}
            }
            DealerProbeCount++;
            var updates=new ConcurrentQueue<KeyValuePair<string,CarInspection>>();dealerRatingUpdates=updates;
            dealerRatings=Task.Run(()=> {
                var results=new Dictionary<string,CarInspection>();foreach(var probe in probes){if(token.IsCancellationRequested)break;try{results[probe.Item1]=CarInspection.CalculatePrepared(probe.Item2,probe.Item3);}catch(Exception){results[probe.Item1]=null;}updates.Enqueue(new KeyValuePair<string,CarInspection>(probe.Item1,results[probe.Item1]));}return results;
            });
        }
        string DealerRating(DealerModel model)=>model.Entry.RatingPending?"Class …":model.Entry.Rating==null?"Class unavailable · PP —":"Class "+model.Entry.Rating.performanceClass+" · PP "+model.Entry.Rating.points;
        string DealerEstimate(DealerModel model)=>model.Entry.RatingPending?"…":model.Driving?.completed==true?model.Driving.elapsed.ToString("F2")+" s · "+DisplayUnits.Speed(model.Driving.trapMps,SharedUnitSettings.Current.speed).ToString("F0")+" "+DisplayUnits.SpeedLabel(SharedUnitSettings.Current.speed):"—";
        string DealerBlocker(DealerModel model)
        {
            if(model.Stats==null||model.Price<=0)return "Stock build unavailable";
            if(game.Profile.runCheckpoints.Any(run=>!run.settled))return "Close the active run first";
            if(game.Profile.reputation<model.Offer.reputation)return "Requires REP "+model.Offer.reputation;
            if(game.Profile.cash<model.Price)return "Need "+DisplayUnits.Money((long)model.Price-game.Profile.cash)+" more";
            return null;
        }
        void BuildDealerGrid()
        {
            foreach(var surface in dealerSurfaces)surfaces.Remove(surface);dealerSurfaces.Clear();
            var scroll=dealerScroll.scrollOffset;dealerGrid.Clear();dealerProfile=game.Profile;dealerRevision=dealerProfile.revision;dealerTorque=SharedUnitSettings.Current.torque;dealerSpeed=SharedUnitSettings.Current.speed;
            foreach(var pair in dealerModels) {
                string id=pair.Key;var model=pair.Value;
                try{model.Price=PaidEconomy.DealerPrice(dealerCatalog,game.Costs,game.PricingPolicy,model.Offer);}catch(Exception){model.Price=-1;}
                model.Entry.Affordable=model.Price>0?(bool?)(model.Price<=game.Profile.cash):null;
                model.Blocker=DealerBlocker(model);model.Entry.Status=model.Blocker==null?"Available":game.Profile.reputation<model.Offer.reputation?"Locked":model.Price>game.Profile.cash?"Unaffordable":"Unavailable";
                var card=new Button(()=>OpenDealerModel(id)) {name="dealerCar:"+id,tooltip=model.Entry.Name,tabIndex=-1,focusable=false};card.AddToClassList("owned-car-card");card.AddToClassList("clickable-panel");dealerGrid.Add(card);AddSurface(card,RacingSurface.Kind.Panel);dealerSurfaces.Add(card.Q<RacingSurface>());
                var heading=new VisualElement();heading.AddToClassList("car-card-heading");card.Add(heading);var title=new Label(model.Entry.Name);title.AddToClassList("car-card-title");heading.Add(title);
                var price=new Label(model.Price>0?DisplayUnits.Money(model.Price):"Unavailable") {name="dealerPrice:"+id};price.AddToClassList("dealer-price");heading.Add(price);
                var body=new VisualElement();body.AddToClassList("car-card-body");card.Add(body);var visual=new VisualElement();visual.AddToClassList("car-visual");body.Add(visual);
                if(model.Stats!=null)visual.Add(CarPreview(model.Exterior,model.Offer.paint));
                var info=DealerSummary(model);info.name="dealerSummary";body.Add(info);
                card.Query<VisualElement>().ForEach(element=>{if(element!=card)element.pickingMode=PickingMode.Ignore;});
            }
            if(dealerModels.Count==0){var empty=new Label("No dealership models");empty.AddToClassList("car-empty");dealerGrid.Add(empty);}
            dealerScroll.scrollOffset=scroll;ApplyOwnedQuery();RefreshDealerDetails();ExcludeTabTraversal();
        }
        VisualElement DealerSummary(DealerModel model)
        {
            var info=new VisualElement();info.AddToClassList("car-summary");
            if(model.Stats!=null){var values=new VisualElement();values.AddToClassList("car-card-stats");info.Add(values);CarFact(values,"HP",model.Stats.peakHp.ToString("F0"));CarFact(values,"Torque",DisplayUnits.Torque(model.Stats.peakTorque,dealerTorque).ToString("F0")+" "+DisplayUnits.TorqueLabel(dealerTorque));CarFact(values,"Weight",model.Stats.vehicleMassKg.ToString("F0")+" kg");}
            var rating=new Label(DealerRating(model)) {name="dealerClass:"+model.Offer.listingId};rating.AddToClassList("car-class");info.Add(rating);
            var configuration=new Label(model.Entry.DriveLabel+" · "+model.Entry.Type+" · "+model.Entry.Gearbox);configuration.AddToClassList("car-identity");info.Add(configuration);
            var condition=new Label((model.Offer.used?"Used · "+model.Offer.mileageKm.ToString("N0")+" km":"New")+" · "+(model.Offer.reportedCondition*100).ToString("F0")+"% condition");condition.AddToClassList("car-condition");info.Add(condition);
            if(model.Offer.used){var disclosure=new Label(model.Known?"Inspected":"Uninspected · estimated specs"){name="dealerDisclosure:"+model.Offer.listingId};disclosure.AddToClassList("car-identity");info.Add(disclosure);
                var operation=new Label((model.Offer.engineRuns?"Engine runs":"Engine does not run")+" · "+(model.Offer.drives?"Drives":"Does not drive")){name="dealerOperation:"+model.Offer.listingId};operation.AddToClassList("car-condition");operation.AddToClassList(model.Offer.drives?"car-ready":"car-warning");info.Add(operation);}
            var owned=new Label("Owned "+game.Profile.cars.Count(car=>car.vehicleId==model.Offer.model)+" · REP "+model.Offer.reputation);owned.AddToClassList("car-identity");info.Add(owned);
            var available=new Label(model.Blocker??"Available");available.AddToClassList("car-condition");available.AddToClassList(model.Blocker==null?"car-ready":"car-warning");info.Add(available);return info;
        }
        void OpenDealerModel(string id)
        {
            if(ModalOpen||!dealerMode||game.ScreenName!="Garage"||!dealerModels.ContainsKey(id))return;
            dealerReturnScroll=dealerScroll.scrollOffset;dealerDetailId=id;dealerError.text="";RefreshDealerDetails();Refresh();
        }
        void BackToDealer()
        {
            comparingDealerId=null;
            dealerDetailId=null;ClearDealerDetails();dealerError.text="";Refresh();dealerScroll.schedule.Execute(()=>dealerScroll.scrollOffset=dealerReturnScroll);
        }
        void ClearDealerDetails(){foreach(var surface in dealerStatsSurfaces)surfaces.Remove(surface);dealerStatsSurfaces.Clear();dealerContent.Clear();}
        void DecorateDealerStats(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);dealerStatsSurfaces.Add(element.Q<RacingSurface>());}
        void RefreshDealerDetails()
        {
            if(dealerDetailId==null)return;
            if(!dealerModels.TryGetValue(dealerDetailId,out var model)){BackToDealer();return;}
            ClearDealerDetails();root.Q<Label>("dealerDetailTitle").text=model.Entry.Name;
            var listen=root.Q<Button>("dealerHearEngine");listen.SetEnabled(model.Stats!=null&&(!model.Offer.used||model.Offer.engineRuns));listen.tooltip=listen.enabledSelf?"Listen to this car":"Engine does not run";
            var selected=InspectOwned(game.Profile.SelectedCar)?.Stats;
            bool comparing=comparingDealerId==dealerDetailId&&selected!=null&&model.Stats!=null;
            dealerCompare.SetEnabled(selected!=null&&model.Stats!=null);dealerCompare.EnableInClassList("selected",comparing);dealerCompare.tooltip=dealerCompare.enabledSelf?"Compare with the selected car":"Comparison unavailable: engine or build data missing";
            if(comparing)ComparisonHeading(dealerContent,model.Entry.Name);
            var body=new VisualElement();body.AddToClassList("car-card-body");body.AddToClassList("car-details-top");dealerContent.Add(body);
            var visual=new VisualElement();visual.AddToClassList("car-visual");body.Add(visual);if(model.Stats!=null)visual.Add(CarPreview(model.Exterior,model.Offer.paint));body.Add(DealerSummary(model));
            if(comparing)ComparisonSummary(body.Q(className:"car-summary"),selected,model.Stats,model.Entry,model.Offer.reportedCondition);
            if(model.Stats!=null){var dyno=new VisualElement();dyno.AddToClassList("car-dyno");body.Add(dyno);dyno.Add(comparing?ComparisonDyno(selected,model.Stats,"dealerDynoPlot",model.Known?"Viewed":"Estimated"):new NativeDynoPanel(new[] {new NativeDynoSeries("Current",model.Known?"Current":"Estimated",model.Stats,NativeDynoStyle.Primary),new NativeDynoSeries("NoWear","No wear",model.Healthy,NativeDynoStyle.Reference)},dealerTorque,"dealerDynoPlot"));
                if(model.Known){
                    BuildInspectionStats(body,dealerContent,model.Stats,comparing?selected:null,model.Offer.listingId,"dealerStatsToggle","dealerCarStats",dealerStatsShown,dealerStatsCollapsed,DecorateDealerStats,()=>DealerDriving(model),()=>OwnedDriving(game.Profile.SelectedCar));
                    var summary=body.Q(className:"car-summary");var actions=new VisualElement{name="dealerPrimaryActions"};actions.AddToClassList("car-detail-actions");summary.Add(actions);actions.Add(summary.Q<Button>("dealerStatsToggle"));
                    var partsInfo=new Button(()=>OpenFittedParts(model)){name="dealerFittedParts",text="Fitted parts",tabIndex=-1,focusable=false};
                    partsInfo.AddToClassList("action-button");partsInfo.AddToClassList("car-stats-button");partsInfo.AddToClassList("fitted-parts-button");partsInfo.AddToClassList("car-detail-action-last");partsInfo.Add(new FilledMenuIcon("Build"));actions.Add(partsInfo);DecorateDealerStats(partsInfo,RacingSurface.Kind.SecondaryAction);
                }
                var specs=new VisualElement();specs.AddToClassList("dealer-specs");dealerContent.Add(specs);var spec=model.Stats.source.build.spec;
                CarFact(specs,"Engine",spec.DisplacementLitres.ToString("F1")+" L · "+spec.sound.cylinders+" cylinders · "+model.Stats.fueling);
                CarFact(specs,"Induction",model.Entry.Induction);if(model.Known){CarFact(specs,"Fuel included",model.Stats.source.startingFuel.litres.ToString("F1")+" L");
                CarFact(specs,"Oil / coolant included",model.Stats.source.startingFluids.oilLitres.ToString("F1")+" / "+model.Stats.source.startingFluids.coolantLitres.ToString("F1")+" L");
                if(model.Offer.historyNotes.Count>0)CarFact(specs,"History",string.Join(" · ",model.Offer.historyNotes));}
                CarFact(specs,"Estimated ¼ mile",DealerEstimate(model));specs.Children().Last().Q<Label>(className:"car-fact-value").name="dealerQuarterEstimate";
                if(model.Known)foreach(var warning in model.Stats.warnings){var label=new Label(warning.title+" · "+warning.suggestion);label.AddToClassList("car-reason");dealerContent.Add(label);}
            }
            dealerInspect.EnableInClassList("hidden",!model.Offer.used||model.Known);dealerInspect.Q<Label>().text="Inspect · "+DisplayUnits.Money(game.Costs.vehicleInspectionFee);dealerInspect.SetEnabled(!model.Known&&game.Profile.cash>=game.Costs.vehicleInspectionFee);dealerInspect.tooltip="Reveal this listing's exact condition and history";
            dealerBuy.Q<Label>().text="Buy · "+(model.Price>0?DisplayUnits.Money(model.Price):"Unavailable");dealerBuy.SetEnabled(model.Blocker==null);dealerBuy.tooltip=model.Blocker??"Buy this exact listing; selects it after purchase";ExcludeTabTraversal();
        }
        internal static string FittedPartGroup(PartCategory category)
        {
            switch(category) {
                case PartCategory.Cooling:case PartCategory.OilPan:case PartCategory.OilPump:case PartCategory.OilFilter:
                case PartCategory.CoolantPump:case PartCategory.CoolingCap:case PartCategory.CoolingHoses:case PartCategory.OilCooler:return "Cooling & lubrication";
                case PartCategory.Clutch:case PartCategory.Flywheel:case PartCategory.Gearbox:case PartCategory.Differential:
                case PartCategory.Driveshaft:case PartCategory.FrontAxle:case PartCategory.RearAxle:case PartCategory.FrontDriveshaft:
                case PartCategory.RearDriveshaft:case PartCategory.FrontDifferential:case PartCategory.RearDifferential:case PartCategory.TransferCase:return "Drivetrain";
                case PartCategory.Wheels:case PartCategory.Tires:case PartCategory.Brakes:return "Wheels & brakes";
                case PartCategory.Controls:case PartCategory.RevLimiter:case PartCategory.ShiftLight:case PartCategory.TwoStep:return "Electronics";
                case PartCategory.Body:case PartCategory.Interior:case PartCategory.Accessories:return "Body & interior";
                default:return MechanicalCatalog.EngineSlot(category)?"Engine":"Other";
            }
        }
        void OpenFittedParts(DealerModel model)
        {
            if(!model.Known)return;
            OpenFittedParts(model.Offer.parts,dealerCatalog);
        }
        void OpenFittedParts(IEnumerable<PartInstance> fitted,MechanicalCatalog catalog)
        {
            OpenDialog("Fitted parts",null,new[]{new DialogAction("Close",CloseDialog)},populate:content=>{
                var list=new VisualElement{name="fittedPartsList"};list.AddToClassList("fitted-parts-list");content.Add(list);
                var parts=fitted.Select(p=>new {Part=p,Definition=catalog.Find(p.definitionId)}).ToArray();
                foreach(string title in new[]{"Engine","Cooling & lubrication","Drivetrain","Wheels & brakes","Body & interior","Electronics","Other"}) {
                    var entries=parts.Where(p=>FittedPartGroup(p.Definition.category)==title).OrderBy(p=>(int)p.Definition.category).ToArray();if(entries.Length==0)continue;
                    var frame=new VisualElement();frame.AddToClassList("stat-group");list.Add(frame);
                    var body=new VisualElement{name="fittedPartsBody:"+title};body.AddToClassList("hidden");var arrow=new FilledMenuIcon("Expand");arrow.AddToClassList("stat-chevron");arrow.AddToClassList("collapsed");
                    Button toggle=null;
                    void ToggleGroup(){bool closed=!body.ClassListContains("hidden");body.EnableInClassList("hidden",closed);arrow.EnableInClassList("collapsed",closed);toggle.tooltip=(closed?"Expand ":"Collapse ")+title;}
                    toggle=new Button(ToggleGroup){name="fittedPartsGroup:"+title,text=title+" · "+entries.Length,tooltip="Expand "+title,focusable=false,tabIndex=-1};
                    toggle.AddToClassList("action-button");toggle.AddToClassList("stat-group-toggle");toggle.Add(arrow);frame.Add(toggle);
                    AddSurface(toggle,RacingSurface.Kind.Button);dialogSurfaces.Add(toggle.Q<RacingSurface>());frame.Add(body);
                    foreach(var entry in entries) {
                        var p=entry.Part;var row=new VisualElement{name="fittedPart:"+p.instanceId,userData=entry.Definition.category};row.AddToClassList("fitted-part");body.Add(row);
                        var category=entry.Definition.category;
                        bool tires=category==PartCategory.Tires;
                        bool corners=tires||category==PartCategory.Wheels&&p.condition.elements.Length==4&&p.condition.elements.All(e=>e.role=="wheel");
                        var cornerDefinitions=corners?Enumerable.Range(0,4).Select(i=>catalog.Find(tires?TireFitments.DefinitionId(p,i):WheelFitments.DefinitionId(p,i))).ToArray():new PartDefinition[0];
                        string name=corners?cornerDefinitions.Select(d=>d.id).Distinct().Count()==1?cornerDefinitions[0].name:tires?"Mixed tires":"Mixed wheels":entry.Definition.name;
                        string failure=entry.Definition.category==PartCategory.Tires?TireDynamics.FailedCorners(p.condition):p.condition.failureReason;
                        CarFact(row,name,PrototypeGame.PartFailed(p)?"Failed"+(string.IsNullOrEmpty(failure)?"":" · "+failure):PrototypeGame.MaintenanceConditionHealth(p).ToString("P0"));
                        if(corners)for(int i=0;i<4;i++){
                            var member=p.condition.elements[i];
                            float health=tires?TireDynamics.RemainingHealth(p.condition.tires[i]):member.health;
                            string reason=tires?p.condition.tires[i].failureReason:member.fault==ElementFault.None?"":member.fault.ToString();
                            var corner=new VisualElement{name="fittedCorner:"+p.instanceId+":"+i,userData=member.id,tooltip=member.id};corner.AddToClassList("fitted-part-corner");row.Add(corner);
                            corner.AddToClassList(member.Failed?"car-critical":ServiceConditionClass(health));
                            CarFact(corner,new[]{"Front left","Front right","Rear left","Rear right"}[i]+" · "+cornerDefinitions[i].name,member.Failed?"Failed"+(string.IsNullOrEmpty(reason)?"":" · "+reason):health.ToString("P0"));
                        }
                        var actions=new VisualElement();actions.AddToClassList("fitted-part-actions");row.Add(actions);
                        var slot=new Label(PartSemantics.Friendly(entry.Definition.category.ToString()));slot.AddToClassList("fitted-part-slot");actions.Add(slot);
                        var shop=new Button(()=>{CloseDialog();game.QueueNativeNavigation("Parts",true,category);}){name="fittedPartShop:"+p.instanceId,text="Shop",tooltip=game.Profile.SelectedCar==null?"Buy a car first.":"Shop · "+PartSemantics.Friendly(category.ToString()),focusable=false,tabIndex=-1};
                        shop.AddToClassList("action-button");shop.AddToClassList("car-stats-button");shop.AddToClassList("fitted-part-shop");actions.Add(shop);shop.Add(new FilledMenuIcon("Business"));shop.SetEnabled(game.Profile.SelectedCar!=null);AddSurface(shop,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(shop.Q<RacingSurface>());
                        foreach(var element in p.condition.elements.Where(e=>e.fault!=ElementFault.None)) {
                            var fault=new Label((element.cylinder>=0?"Cylinder "+(element.cylinder+1)+" · ":"")+element.role+(element.valve>=0?" "+(element.valve+1):"")+" · "+element.fault);fault.AddToClassList("fitted-part-fault");row.Add(fault);
                        }
                    }
                }
            });
        }
        void OpenDealerInspection()
        {
            if(ModalOpen||!DealerDetailsOpen||!dealerModels.TryGetValue(dealerDetailId,out var model)||model.Known)return;
            PaymentQuote quote;try{quote=PaidEconomy.Quote(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=model.Offer},DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);}catch(Exception error){dealerError.text=error.Message;return;}
            string id=dealerDetailId;var errorLabel=new Label{name="dealerInspectionError"};errorLabel.AddToClassList("car-critical");
            OpenDialog("Inspect "+model.Entry.Name,"Reveal history, faults and exact specifications.",new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Inspect",()=>{
                if(!game.InspectNativeCar(quote)){errorLabel.text=game.NativeNotice;return;}
                CloseDialog();dealerStatsShown.Add(id);Refresh();
            },true)},populate:content=>{CarFact(content,"Fee",DisplayUnits.Money(quote.Cost));CarFact(content,"Balance",DisplayUnits.Money(game.Profile.cash));CarFact(content,"After inspection",DisplayUnits.Money((long)game.Profile.cash-quote.Cost));content.Add(errorLabel);});
        }
        void OpenDealerPurchase()
        {
            if(ModalOpen||!DealerDetailsOpen||!dealerModels.TryGetValue(dealerDetailId,out var model))return;
            PaymentQuote quote;
            try{quote=PaidEconomy.Quote(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,new PaymentRequest {kind=PaymentKind.Car,offer=model.Offer},DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                if(quote.Cost!=model.Price||DealerBlocker(model)!=null)throw new InvalidOperationException("Offer changed. Go back and reopen this model.");}
            catch(Exception error){dealerError.text=error.Message;return;}
            var errorLabel=new Label {name="dealerConfirmationError"};errorLabel.AddToClassList("car-critical");errorLabel.AddToClassList("car-selection-error");
            OpenDialog("Buy "+model.Entry.Name,null,new[] {new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=> {
                if(!game.BuyNativeCar(quote)){errorLabel.text=game.NativeNotice;root.Q<Button>("dialogConfirm").SetEnabled(false);return;}
                CloseDialog();dealerMode=false;dealerDetailId=null;ownedDetailId=null;ClearDealerDetails();ownedSearch.SetValueWithoutNotify(ownedQuery.Search);ownedSearch.Q("ownedCarSearchHint").EnableInClassList("hidden",!string.IsNullOrEmpty(ownedQuery.Search));InvalidateOwnedGrid();Refresh();ApplyOwnedQuery();OpenOwnedCar(game.Profile.selectedOwnedVehicleId);
            },true)},populate:content=> {
                CarFact(content,"Price",DisplayUnits.Money(quote.Cost));CarFact(content,"Balance",DisplayUnits.Money(game.Profile.cash));CarFact(content,"After purchase",DisplayUnits.Money((long)game.Profile.cash-quote.Cost));
                CarFact(content,"Reputation",game.Profile.reputation+" / "+model.Offer.reputation+" required");
                var included=new Label((model.Offer.used?(model.Known?"Exact inspected parts and liquids are retained.":"Uninspected. Actual faults and liquids transfer with the car."):"New fitted parts and liquids included. This car becomes current."));included.AddToClassList("dealer-includes");content.Add(included);content.Add(errorLabel);
            });
        }
    }
}
