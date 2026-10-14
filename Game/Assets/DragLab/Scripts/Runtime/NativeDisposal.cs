using System;
using System.Linq;
using System.Collections.Generic;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        string disposalCarId,disposalKey;
        VisualElement disposalPage,disposalContent;
        ValuationQuote disposalQuote;
        int disposalMode,disposalBuyer;
        readonly HashSet<string> disposalKept=new HashSet<string>();
        readonly Dictionary<string,bool> disposalGroups=new Dictionary<string,bool>();
        readonly Dictionary<string,Label> disposalPartStates=new Dictionary<string,Label>();
        Label disposalPrice,disposalSummary,disposalError;
        Button disposalConfirm;
        string DisposalBuyer=>game.PricingPolicy.buyers[disposalBuyer].id;
        readonly List<SwitchSurface> disposalSwitches=new List<SwitchSurface>();
        readonly List<RacingSurface> disposalSurfaces=new List<RacingSurface>();
        void DisposalSurface(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);disposalSurfaces.Add(element.Q<RacingSurface>());}
        internal bool DisposalOpen=>disposalCarId!=null&&game.ScreenName=="Garage"&&Visible;
        void InitializeDisposal()
        {
            disposalPage=new VisualElement{name="disposalPage"};disposalPage.AddToClassList("car-details-page");disposalPage.AddToClassList("hidden");root.Q(className:"workspace").Add(disposalPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");disposalPage.Add(heading);
            var back=new Button(BackFromDisposal){name="disposalBack",tooltip="Back to car"};back.AddToClassList("action-button");back.AddToClassList("car-back-button");heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);back.Add(new FilledMenuIcon("Back"));
            var title=new Label("Sell / Salvage");title.AddToClassList("car-detail-title");heading.Add(title);
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");disposalPage.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            var scroll=new ScrollView{name="disposalScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("car-details-scroll");frame.Add(scroll);
            disposalContent=new VisualElement{name="disposalContent"};disposalContent.AddToClassList("car-details-content");scroll.Add(disposalContent);
            var actions=new VisualElement();actions.AddToClassList("car-details-actions");disposalPage.Add(actions);
            disposalConfirm=new Button(OpenDisposalReview){name="disposalReview",text="Review sale"};disposalConfirm.AddToClassList("action-button");disposalConfirm.AddToClassList("primary-action");actions.Add(disposalConfirm);AddSurface(disposalConfirm,RacingSurface.Kind.Action);
            var button=new Button(()=>OpenDisposal(ownedDetailId)){name="ownedSellSalvage",text="Sell / Salvage"};button.AddToClassList("action-button");ownedUse.parent.Insert(1,button);AddSurface(button,RacingSurface.Kind.SecondaryAction);
        }
        void OpenDisposal(string id){if(ModalOpen||!game.Profile.cars.Exists(c=>c.instanceId==id))return;disposalCarId=id;disposalKey=null;disposalMode=disposalBuyer=0;disposalKept.Clear();Refresh();}
        void BackFromDisposal(){disposalCarId=null;disposalKey=null;Refresh();}
        void RefreshDisposal(string route)
        {
            if(route!="Garage"&&route!="Settings")disposalCarId=null;
            var car=game.Profile.cars.Find(c=>c.instanceId==disposalCarId);if(car==null)disposalCarId=null;
            bool show=route=="Garage"&&disposalCarId!=null&&!dealerMode;disposalPage.EnableInClassList("hidden",!show);if(!show)return;
            bool expired=disposalQuote!=null&&DateTime.UtcNow.Ticks>=disposalQuote.ExpiresUtcTicks;disposalConfirm.SetEnabled(disposalQuote!=null&&!disposalQuote.Reserved&&game.CartCount==0&&!CartReservations.Active(game.Profile)&&!expired);if(expired&&disposalError!=null)disposalError.text="Quote expired. Refresh.";
            garagePages.AddToClassList("hidden");ownedDetails.AddToClassList("hidden");ownedScroll.AddToClassList("hidden");ownedToolbar.AddToClassList("hidden");
            string key=disposalCarId+"/"+game.Profile.revision;if(disposalKey==key)return;disposalKey=key;foreach(var surface in disposalSurfaces)surfaces.Remove(surface);disposalSurfaces.Clear();foreach(var surface in disposalSwitches)switchSurfaces.Remove(surface);disposalSwitches.Clear();disposalContent.Clear();
            var name=new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)){name="disposalCarName",enableRichText=false};name.AddToClassList("car-detail-title");disposalContent.Add(name);
            var top=new VisualElement();top.AddToClassList("car-card-body");top.AddToClassList("car-details-top");disposalContent.Add(top);
            var visual=new VisualElement();visual.AddToClassList("car-visual");top.Add(visual);visual.Add(CarPreview(MechanicsOwnership.Resolve(game.Profile,car,game.Profile.catalog),car.paintHex));
            var facts=new VisualElement();facts.AddToClassList("car-summary");top.Add(facts);
            CarFact(facts,"Offer","—");disposalPrice=facts.Q<Label>(className:"car-fact-value");disposalPrice.name="disposalPrice";
            CarFact(facts,"Fuel / oil / coolant",car.fuel.litres.ToString("F1")+" / "+car.fluids.oilLitres.ToString("F1")+" / "+car.fluids.coolantLitres.ToString("F1")+" L");
            var modes=new VisualElement();modes.AddToClassList("choice-group");modes.style.flexDirection=FlexDirection.Row;modes.style.minHeight=32;facts.Add(modes);
            PartChoices(modes,"disposalMode",new[]{"Sell car","Keep engine"},()=>disposalMode==0?"Sell car":"Keep engine",v=>{disposalMode=v=="Sell car"?0:1;disposalKey=null;Refresh();});
            modes.Q<Button>("disposalMode:Keep engine").SetEnabled(!string.IsNullOrEmpty(car.engineAssemblyId));
            var buyers=new VisualElement();buyers.AddToClassList("choice-group");buyers.style.flexDirection=FlexDirection.Row;buyers.style.minHeight=32;disposalContent.Add(buyers);
            PartChoices(buyers,"disposalBuyer",game.PricingPolicy.buyers.Select(b=>b.name).ToArray(),()=>game.PricingPolicy.buyers[disposalBuyer].name,v=>{disposalBuyer=game.PricingPolicy.buyers.FindIndex(b=>b.name==v);RefreshDisposalQuote();});
            var info=new Button(()=>{if(disposalQuote!=null)OpenDialog("Price details",string.Join("\n\n",disposalQuote.Lines.Select(l=>l.Label+" · "+DisplayUnits.Money(l.Before,2)+" → "+DisplayUnits.Money(l.After,2)+"\n"+l.Explanation)),new[]{new DialogAction("Close",CloseDialog)});}){name="disposalPriceInfo",text="i",tooltip="Price details"};info.AddToClassList("info-button");facts.Add(info);
            var refresh=new Button(RefreshDisposalQuote){name="disposalRefresh",text="Refresh quote"};refresh.AddToClassList("action-button");refresh.style.alignSelf=Align.FlexStart;facts.Add(refresh);DisposalSurface(refresh,RacingSurface.Kind.SecondaryAction);
            disposalSummary=new Label{name="disposalSelection",enableRichText=false};disposalContent.Add(disposalSummary);
            disposalError=new Label{name="disposalError",enableRichText=false};disposalError.AddToClassList("car-critical");disposalContent.Add(disposalError);
            disposalContent.Add(new Label("Checked parts stay in storage."));disposalPartStates.Clear();
            var parts=game.Profile.parts.Where(i=>car.installed.slots.Any(slot=>slot.partInstanceId==i.instanceId)).ToArray();
            disposalKept.RemoveWhere(id=>!parts.Any(i=>i.instanceId==id)||disposalMode==1&&MechanicalCatalog.EngineSlot(game.Profile.catalog.Find(game.Profile.parts.Find(i=>i.instanceId==id).definitionId).category));
            foreach(var section in parts.GroupBy(i=>NativePartBrowser.Section(game.Profile.catalog.Find(i.definitionId).category))){
                string sectionId=section.Key.ToString();var frame=new VisualElement();frame.AddToClassList("stat-group");disposalContent.Add(frame);var group=new VisualElement{name="disposalGroup:"+sectionId};group.EnableInClassList("hidden",disposalGroups.TryGetValue(sectionId,out bool closed)&&closed);
                var expand=new Button(()=>{bool closed=!group.ClassListContains("hidden");disposalGroups[sectionId]=closed;group.EnableInClassList("hidden",closed);}){text=NativePartBrowser.SectionNames[(int)section.Key]+" · "+section.Count()};expand.AddToClassList("action-button");expand.AddToClassList("stat-group-toggle");frame.Add(expand);DisposalSurface(expand,RacingSurface.Kind.SecondaryAction);expand.Insert(1,new FilledMenuIcon("Expand"));frame.Add(group);
                foreach(var part in section){var def=game.Profile.catalog.Find(part.definitionId);bool engineKept=disposalMode==1&&MechanicalCatalog.EngineSlot(def.category);var row=new VisualElement{name="disposalPart:"+part.instanceId,tooltip=part.instanceId};row.AddToClassList("setting-row");group.Add(row);
                    var caption=new Label(def.name){enableRichText=false};caption.AddToClassList("car-fact-label");row.Add(caption);
                    int units=Math.Max(1,Math.Max(part.condition.elements.Length,part.condition.tires.Length));var state=new Label(part.condition.health.ToString("P0")+" · "+units+" units"+(PrototypeGame.PartFailed(part)?" · Failed":"")+(engineKept?" · Kept with engine":""));state.AddToClassList("car-fact-value");state.AddToClassList(PrototypeGame.PartFailed(part)?"car-critical":ServiceConditionClass(part.condition.health));row.Add(state);disposalPartStates[part.instanceId]=state;
                    if(!engineKept){string id=part.instanceId;var keep=new Toggle{name="disposalKeep:"+id,value=disposalKept.Contains(id),tooltip="Keep this component set in storage",focusable=false,tabIndex=-1};keep.AddToClassList("setting-toggle");row.Add(keep);var surface=new SwitchSurface(keep);keep.Q(className:"unity-toggle__input").Add(surface);switchSurfaces.Add(surface);disposalSwitches.Add(surface);surface.SetValue(keep.value);keep.RegisterValueChangedCallback(e=>{if(e.newValue)disposalKept.Add(id);else disposalKept.Remove(id);surface.SetValue(e.newValue);RefreshDisposalQuote();});}
                }
            }
            foreach(var element in disposalContent.Query<RacingSurface>().ToList())if(!disposalSurfaces.Contains(element))disposalSurfaces.Add(element);
            RefreshDisposalQuote();
            ExcludeTabTraversal();
        }
        void OpenDisposalReview()
        {
            var quote=disposalQuote;var car=game.Profile.cars.Find(c=>c.instanceId==disposalCarId);if(quote==null||car==null)return;
            string invalid=Valuations.Validate(quote,game.Profile,game.Profile.catalog,game.PricingPolicy,DateTime.UtcNow.Ticks);if(invalid!=null){disposalError.text=invalid;return;}
            var error=new Label{name="disposalCommitError"};error.AddToClassList("car-critical");string id=car.instanceId;
            OpenDialog("Confirm sale",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Sell",()=>{
                if(!game.CommitNativeDisposal(id,quote)){error.text=game.NativeNotice;return;}
                CloseDialog();disposalCarId=null;ownedDetailId=null;ClearOwnedDetailContent();InvalidateOwnedGrid();Refresh();
            },true)},populate:content=>{
                content.Add(new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)+" · "+(quote.Kind==ValuationKind.Chassis?"Chassis / engine kept":"Car")){enableRichText=false});
                content.Add(new Label("Receive "+DisplayUnits.Money(quote.Total)+" · Balance after "+DisplayUnits.Money((long)game.Profile.cash+quote.Total)));
                if(game.Profile.cars.Count==1)content.Add(new Label("Last car · dealership and recovery remain available."));
                var included=game.Profile.parts.Where(p=>quote.IncludedIds.Contains(p.instanceId)).ToArray();
                content.Add(new Label("Sell · "+included.Length+" components"));foreach(var part in included)content.Add(new Label(game.Profile.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0")){enableRichText=false});
                var kept=game.Profile.parts.Where(p=>car.installed.slots.Any(b=>b.partInstanceId==p.instanceId)&&!quote.IncludedIds.Contains(p.instanceId)).ToArray();
                content.Add(new Label("Keep · "+kept.Length+" components"+(quote.Kind==ValuationKind.Chassis?" · Engine assembly, oil and coolant":"")));
                foreach(var part in kept)content.Add(new Label(game.Profile.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0")){enableRichText=false});content.Add(error);
            });
        }
        void RefreshDisposalQuote()
        {
            var car=game.Profile.cars.Find(c=>c.instanceId==disposalCarId);if(car==null)return;
            try{
                disposalQuote=InventoryPricing.Sell(game.Profile,disposalMode==0?ValuationKind.Car:ValuationKind.Chassis,disposalMode==0?car.instanceId:car.chassisId,game.PricingPolicy,DisposalBuyer,DateTime.UtcNow.Ticks,disposalKept);
                disposalPrice.text=DisplayUnits.Money(disposalQuote.Total);
                int sold=disposalPartStates.Keys.Count(id=>disposalQuote.IncludedIds.Contains(id));int kept=disposalPartStates.Count-sold;
                disposalSummary.text="Sell "+sold+" components · Keep "+kept+(disposalMode==1?" · Engine and its liquids retained":"")+" · "+(disposalQuote.Complete?"Complete":"Incomplete")+(disposalQuote.Faulty?" · Faults present":"");
                disposalError.text=disposalQuote.Reserved?"Reserved · inspection only":(game.CartCount>0||CartReservations.Active(game.Profile))?"Checkout or clear the cart before selling.":"";
            }catch(Exception error){disposalQuote=null;disposalPrice.text="—";disposalError.text=error.Message;}
        }
    }
    public sealed partial class PrototypeGame
    {
        internal bool CommitNativeDisposal(string carId,ValuationQuote quote)
        {
            try{
                if(screen!="Garage"||pendingAction!=null||CartCount>0||CartReservations.Active(profile))throw new InvalidOperationException("Close pending actions or the cart before selling.");
                var car=profile.cars.Find(c=>c.instanceId==carId);if(car==null||quote==null||!(quote.Kind==ValuationKind.Car&&quote.AssetId==carId||quote.Kind==ValuationKind.Chassis&&quote.AssetId==car.chassisId))throw new InvalidOperationException("Sale target changed. Reopen the review.");
                if(!CommitParts(quote.Revision,p=>PaidEconomy.Sell(p,p.catalog,PricingPolicy,quote,DateTime.UtcNow.Ticks)))return false;
                ShowGarageCar();notice="Sale saved.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
}
