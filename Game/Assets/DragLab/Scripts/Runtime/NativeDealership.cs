using System;
using System.Collections.Generic;
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
            public CarInspection Stats, Driving;
            public OwnedCarEntry Entry;
            public int Price;
            public string Blocker;
        }
        bool dealerMode;
        string dealerDetailId;
        Vector2 dealerReturnScroll;
        VisualElement garagePages,dealerGrid,dealerDetails,dealerContent;
        ScrollView dealerScroll;
        Label dealerNoMatches,dealerError;
        Button ownedPageButton,dealerPageButton,dealerBuy;
        readonly Dictionary<string,OwnedCarEntry> dealerEntries=new Dictionary<string,OwnedCarEntry>();
        readonly Dictionary<string,DealerModel> dealerModels=new Dictionary<string,DealerModel>();
        readonly List<RacingSurface> dealerSurfaces=new List<RacingSurface>();
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
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");dealerDetails.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            var scroll=new ScrollView {name="dealerDetailScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("car-details-scroll");frame.Add(scroll);
            dealerContent=new VisualElement {name="dealerContent"};dealerContent.AddToClassList("car-details-content");scroll.Add(dealerContent);
            var actions=new VisualElement();actions.AddToClassList("car-details-actions");dealerDetails.Add(actions);
            dealerError=new Label {name="dealerPurchaseError"};dealerError.AddToClassList("car-critical");dealerError.AddToClassList("car-selection-error");actions.Add(dealerError);
            dealerBuy=new Button(OpenDealerPurchase) {name="dealerBuy",text="Buy",tabIndex=-1,focusable=false};dealerBuy.AddToClassList("action-button");dealerBuy.AddToClassList("primary-action");actions.Add(dealerBuy);AddSurface(dealerBuy,RacingSurface.Kind.Action);
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
            garagePages.EnableInClassList("hidden",route!="Garage");ownedPageButton.EnableInClassList("selected",!dealerMode);dealerPageButton.EnableInClassList("selected",dealerMode);
            dealerScroll.EnableInClassList("hidden",route!="Garage"||!dealerMode||dealerDetailId!=null);
            dealerDetails.EnableInClassList("hidden",route!="Garage"||!dealerMode||dealerDetailId==null);
            if(route!="Garage"||!dealerMode)return;
            var profile=game.Profile;var units=SharedUnitSettings.Current.torque;
            if(dealerCatalog!=profile.catalog)PrepareDealerModels();
            if(dealerProfile!=profile||dealerRevision!=profile.revision||dealerTorque!=units||dealerSpeed!=SharedUnitSettings.Current.speed)BuildDealerGrid();
            if(dealerRatings==null||!dealerRatings.IsCompleted)return;
            var finished=dealerRatings;dealerRatings=null;
            try {foreach(var pair in finished.GetAwaiter().GetResult())if(dealerModels.TryGetValue(pair.Key,out var model)) {
                model.Driving=pair.Value;model.Entry.RatingPending=false;
                model.Entry.Rating=pair.Value!=null&&pair.Value.completed?Benchmark.Rating(pair.Value.elapsed,pair.Value.trapMps*3.6):null;
                var label=dealerGrid.Q<Label>("dealerClass:"+pair.Key);if(label!=null)label.text=DealerRating(model);
            }}catch(Exception){foreach(var model in dealerModels.Values){model.Entry.RatingPending=false;model.Entry.Rating=null;}}
            ApplyOwnedQuery();RefreshDealerDetails();
        }
        void PrepareDealerModels()
        {
            dealerCancellation?.Cancel();dealerCancellation?.Dispose();dealerCancellation=new CancellationTokenSource();var token=dealerCancellation.Token;
            dealerCatalog=game.Profile.catalog;dealerModels.Clear();dealerEntries.Clear();dealerRevision=-1;
            var probes=new List<Tuple<string,InspectionBuild,CarSimulation>>();
            foreach(var vehicle in game.vehicles) {
                var offer=game.AuthoredOffer(vehicle.spec.id);
                var entry=new OwnedCarEntry {Id=offer.model,Name=vehicle.spec.displayName,Model=vehicle.spec.displayName,Order=dealerModels.Count,Status="Unavailable",RatingPending=true};
                var model=new DealerModel {Offer=offer,Entry=entry};dealerModels.Add(offer.model,model);dealerEntries.Add(offer.model,entry);
                try {
                    // Only this isolated factory profile is initialized. No existing owned assets are normalized or healed.
                    var factory=new PlayerProfile {catalog=dealerCatalog,cash=int.MaxValue,reputation=int.MaxValue};
                    VehicleCollection.Buy(factory,dealerCatalog,offer);
                    var source=InspectionBuild.Capture(factory,factory.SelectedCar);
                    model.Stats=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);
                    var build=source.build;var stats=model.Stats;
                    entry.Hp=stats.peakHp;entry.Torque=stats.peakTorque;entry.Weight=stats.vehicleMassKg;entry.Condition=stats.condition*100;entry.Drive=build.spec.drive.ToString();entry.Type=BodyName(build.spec.bodyType);
                    entry.Gearbox=OwnedCarEntry.GearboxName(TransmissionDynamics.Configuration(build.spec,build.tune));entry.Induction=InductionName(new InductionProfile(build.spec,build.tune).type);entry.LockedCenter=build.spec.drive==DriveType.AWD&&AxleDriveDynamics.Configuration(build.spec,build.tune).mode==AwdMode.LockedCenter;
                    probes.Add(Tuple.Create(offer.model,source,CarInspection.PrepareCar(source)));
                }catch(Exception){entry.RatingPending=false;model.Blocker="Stock build unavailable";}
            }
            DealerProbeCount++;
            dealerRatings=Task.Run(()=> {
                var results=new Dictionary<string,CarInspection>();foreach(var probe in probes){if(token.IsCancellationRequested)break;try{results[probe.Item1]=CarInspection.CalculatePrepared(probe.Item2,probe.Item3);}catch(Exception){results[probe.Item1]=null;}}return results;
            });
        }
        string DealerRating(DealerModel model)=>model.Entry.RatingPending?"Class …":model.Entry.Rating==null?"Class unavailable · PP —":"Class "+model.Entry.Rating.performanceClass+" · PP "+model.Entry.Rating.points;
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
                if(model.Stats!=null)visual.Add(CarPreview(model.Stats.source.build,model.Offer.paint));
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
            var rating=new Label(DealerRating(model)) {name="dealerClass:"+model.Offer.model};rating.AddToClassList("car-class");info.Add(rating);
            var configuration=new Label(model.Entry.DriveLabel+" · "+model.Entry.Type+" · "+model.Entry.Gearbox);configuration.AddToClassList("car-identity");info.Add(configuration);
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
            dealerDetailId=null;ClearDealerDetails();dealerError.text="";Refresh();dealerScroll.schedule.Execute(()=>dealerScroll.scrollOffset=dealerReturnScroll);
        }
        void ClearDealerDetails()=>dealerContent.Clear();
        void RefreshDealerDetails()
        {
            if(dealerDetailId==null)return;
            if(!dealerModels.TryGetValue(dealerDetailId,out var model)){BackToDealer();return;}
            ClearDealerDetails();root.Q<Label>("dealerDetailTitle").text=model.Entry.Name;
            var body=new VisualElement();body.AddToClassList("car-card-body");body.AddToClassList("car-details-top");dealerContent.Add(body);
            var visual=new VisualElement();visual.AddToClassList("car-visual");body.Add(visual);if(model.Stats!=null)visual.Add(CarPreview(model.Stats.source.build,model.Offer.paint));body.Add(DealerSummary(model));
            if(model.Stats!=null){var dyno=new VisualElement();dyno.AddToClassList("car-dyno");body.Add(dyno);dyno.Add(new NativeDynoPanel(new[] {new NativeDynoSeries("Stock","Stock",model.Stats,NativeDynoStyle.Primary)},dealerTorque,"dealerDynoPlot"));
                var specs=new VisualElement();specs.AddToClassList("dealer-specs");dealerContent.Add(specs);var spec=model.Stats.source.build.spec;
                CarFact(specs,"Engine",spec.DisplacementLitres.ToString("F1")+" L · "+spec.sound.cylinders+" cylinders · "+model.Stats.fueling);
                CarFact(specs,"Induction",model.Entry.Induction);CarFact(specs,"Fuel included",model.Stats.source.startingFuel.litres.ToString("F1")+" L");
                CarFact(specs,"Oil / coolant included",model.Stats.source.startingFluids.oilLitres.ToString("F1")+" / "+model.Stats.source.startingFluids.coolantLitres.ToString("F1")+" L");
                if(model.Entry.Rating!=null)CarFact(specs,"Estimated ¼ mile",model.Driving.elapsed.ToString("F2")+" s · "+DisplayUnits.Speed(model.Driving.trapMps,SharedUnitSettings.Current.speed).ToString("F0")+" "+DisplayUnits.SpeedLabel(SharedUnitSettings.Current.speed));
                foreach(var warning in model.Stats.warnings){var label=new Label(warning.title+" · "+warning.suggestion);label.AddToClassList("car-reason");dealerContent.Add(label);}
            }
            dealerBuy.Q<Label>().text="Buy · "+(model.Price>0?DisplayUnits.Money(model.Price):"Unavailable");dealerBuy.SetEnabled(model.Blocker==null);dealerBuy.tooltip=model.Blocker??"Buy a new OEM car; selects it after purchase";ExcludeTabTraversal();
        }
        void OpenDealerPurchase()
        {
            if(ModalOpen||!DealerDetailsOpen||!dealerModels.TryGetValue(dealerDetailId,out var model))return;
            PaymentQuote quote;
            try{quote=PaidEconomy.Quote(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,new PaymentRequest {kind=PaymentKind.Car,offer=game.AuthoredOffer(dealerDetailId)},DateTime.UtcNow.Ticks);
                if(quote.Cost!=model.Price||DealerBlocker(model)!=null)throw new InvalidOperationException("Offer changed. Go back and reopen this model.");}
            catch(Exception error){dealerError.text=error.Message;return;}
            var errorLabel=new Label {name="dealerConfirmationError"};errorLabel.AddToClassList("car-critical");errorLabel.AddToClassList("car-selection-error");
            OpenDialog("Buy "+model.Entry.Name,null,new[] {new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=> {
                if(!game.BuyNativeCar(quote)){errorLabel.text=game.NativeNotice;root.Q<Button>("dialogConfirm").SetEnabled(false);return;}
                CloseDialog();dealerMode=false;dealerDetailId=null;ownedDetailId=null;ClearDealerDetails();ownedSearch.SetValueWithoutNotify(ownedQuery.Search);ownedSearch.Q("ownedCarSearchHint").EnableInClassList("hidden",!string.IsNullOrEmpty(ownedQuery.Search));InvalidateOwnedGrid();Refresh();ApplyOwnedQuery();OpenOwnedCar(game.Profile.selectedOwnedVehicleId);
            },true)},populate:content=> {
                CarFact(content,"Price",DisplayUnits.Money(quote.Cost));CarFact(content,"Balance",DisplayUnits.Money(game.Profile.cash));CarFact(content,"After purchase",DisplayUnits.Money((long)game.Profile.cash-quote.Cost));
                CarFact(content,"Reputation",game.Profile.reputation+" / "+model.Offer.reputation+" required");
                var included=new Label("New OEM parts, fuel, oil and coolant included. This car becomes current.");included.AddToClassList("dealer-includes");content.Add(included);content.Add(errorLabel);
            });
        }
    }
}
