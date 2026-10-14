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
            var button=new Button(()=>OpenDisposal(ownedDetailId)){name="ownedSellSalvage",text="Sell / Salvage"};button.AddToClassList("action-button");ownedUse.parent.Insert(1,button);AddSurface(button,RacingSurface.Kind.SecondaryAction);
        }
        void OpenDisposal(string id){if(ModalOpen||!game.Profile.cars.Exists(c=>c.instanceId==id))return;disposalCarId=id;disposalKey=null;Refresh();}
        void BackFromDisposal(){disposalCarId=null;disposalKey=null;Refresh();}
        void RefreshDisposal(string route)
        {
            if(route!="Garage"&&route!="Settings")disposalCarId=null;
            var car=game.Profile.cars.Find(c=>c.instanceId==disposalCarId);if(car==null)disposalCarId=null;
            bool show=route=="Garage"&&disposalCarId!=null&&!dealerMode;disposalPage.EnableInClassList("hidden",!show);if(!show)return;
            garagePages.AddToClassList("hidden");ownedDetails.AddToClassList("hidden");ownedScroll.AddToClassList("hidden");ownedToolbar.AddToClassList("hidden");
            string key=disposalCarId+"/"+game.Profile.revision;if(disposalKey==key)return;disposalKey=key;foreach(var surface in disposalSurfaces)surfaces.Remove(surface);disposalSurfaces.Clear();disposalContent.Clear();
            var name=new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)){name="disposalCarName",enableRichText=false};name.AddToClassList("car-detail-title");disposalContent.Add(name);
            var top=new VisualElement();top.AddToClassList("car-card-body");top.AddToClassList("car-details-top");disposalContent.Add(top);
            var visual=new VisualElement();visual.AddToClassList("car-visual");top.Add(visual);visual.Add(CarPreview(MechanicsOwnership.Resolve(game.Profile,car,game.Profile.catalog),car.paintHex));
            var facts=new VisualElement();facts.AddToClassList("car-summary");top.Add(facts);
            try{
                disposalQuote=InventoryPricing.Sell(game.Profile,ValuationKind.Car,car.instanceId,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks);
                CarFact(facts,"Offer",DisplayUnits.Money(disposalQuote.Total));facts.Q(className:"car-fact-value").name="disposalPrice";
                CarFact(facts,"Buyer",disposalQuote.BuyerName);CarFact(facts,"Condition",disposalQuote.Complete?"Complete":"Incomplete");CarFact(facts,"Faults",disposalQuote.Faulty?"Present":"None");
                CarFact(facts,"Fuel / oil / coolant",car.fuel.litres.ToString("F1")+" / "+car.fluids.oilLitres.ToString("F1")+" / "+car.fluids.coolantLitres.ToString("F1")+" L");
                var info=new Button(()=>OpenDialog("Price details",string.Join("\n\n",disposalQuote.Lines.Select(l=>l.Label+" · "+DisplayUnits.Money(l.Before,2)+" → "+DisplayUnits.Money(l.After,2)+"\n"+l.Explanation)),new[]{new DialogAction("Close",CloseDialog)})){name="disposalPriceInfo",text="i",tooltip="Price details"};info.AddToClassList("info-button");facts.Add(info);
                var refresh=new Button(()=>{disposalKey=null;Refresh();}){name="disposalRefresh",text="Refresh quote"};refresh.AddToClassList("action-button");facts.Add(refresh);DisposalSurface(refresh,RacingSurface.Kind.SecondaryAction);
                if(disposalQuote.Reserved){var reserved=new Label("Reserved · inspection only");reserved.AddToClassList("car-critical");facts.Add(reserved);}
                var parts=game.Profile.parts.Where(i=>disposalQuote.IncludedIds.Contains(i.instanceId)).ToArray();
                foreach(var section in parts.GroupBy(i=>NativePartBrowser.Section(game.Profile.catalog.Find(i.definitionId).category))){
                    var frame=new VisualElement();frame.AddToClassList("stat-group");disposalContent.Add(frame);var group=new VisualElement{name="disposalGroup:"+section.Key};
                    var toggle=new Button(()=>group.EnableInClassList("hidden",!group.ClassListContains("hidden"))){text=NativePartBrowser.SectionNames[(int)section.Key]+" · "+section.Count()};toggle.AddToClassList("action-button");toggle.AddToClassList("stat-group-toggle");frame.Add(toggle);DisposalSurface(toggle,RacingSurface.Kind.SecondaryAction);toggle.Insert(1,new FilledMenuIcon("Expand"));frame.Add(group);
                    foreach(var part in section){var def=game.Profile.catalog.Find(part.definitionId);var row=new VisualElement{name="disposalPart:"+part.instanceId,tooltip=part.instanceId};row.AddToClassList("car-fact");group.Add(row);
                        var label=new Label(def.name){enableRichText=false};label.AddToClassList("car-fact-label");row.Add(label);var condition=new Label(part.condition.health.ToString("P0")+(PrototypeGame.PartFailed(part)?" · Failed":"")+" · "+part.ItemIds.Count()+" identities");condition.AddToClassList("car-fact-value");condition.AddToClassList(PrototypeGame.PartFailed(part)?"car-critical":ServiceConditionClass(part.condition.health));row.Add(condition);
                    }
                }
            }catch(Exception error){disposalQuote=null;var message=new Label(error.Message){name="disposalError"};message.AddToClassList("car-critical");facts.Add(message);}
            ExcludeTabTraversal();
        }
    }
}
