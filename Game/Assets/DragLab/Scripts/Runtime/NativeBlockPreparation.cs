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
    public sealed partial class PrototypeGame
    {
        internal bool StagePreparedBlock(BlockPreparationPlan plan,long revision,string owner,string expected)
        {
            if(BlockForManualRefill())return false;
            string CurrentCartSignature(){PartCart.RecordRuntimeJsonCall();return JsonUtility.ToJson(new PartCart.Lines{entries=cart.Select(l=>l.Copy()).ToList()});}
            if(profile.revision!=revision||Build?.instanceId!=owner||expected!=CurrentCartSignature()){notice="Car or cart changed. Reopen block preparation.";return false;}
            if(!plan.Ready){notice=plan.Error??"Preparation unavailable.";return false;}
            try{if(PartCart.Total(profile,owner,plan.Lines,PricingPolicy)!=plan.Total)throw new InvalidOperationException("Prices changed. Review the preparation again.");cart.Clear();cart.AddRange(plan.Lines.Select(l=>l.Copy()));CartChanged();notice="";return true;}catch(Exception e){notice=e.Message;return false;}
        }
    }
    internal sealed partial class NativeMenuShell
    {
        void OpenBlockPreparation(NativePartEntry offer,int quantity=1,PartCartLine editing=null,BlockPreparationMode initialMode=BlockPreparationMode.Reuse,IDictionary<PartCategory,BlockPartChoice> prior=null,string expectedCart=null,long capturedRevision=-1,PartActionOrigin origin=null)
        {
            if(origin!=null&&!TryValidatePartAction(origin,out _,out var initialReason)){RejectPartAction(initialReason);return;}
            var p=game.Profile;string carId=p.SelectedCar.instanceId;long revision=capturedRevision<0?p.revision:capturedRevision;if(expectedCart==null)PartCart.RecordRuntimeJsonCall();string expected=expectedCart??JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            var block=editing?.Copy()??new PartCartLine{definitionId=offer.Definition.id,quantity=quantity,action=offer.Source=="New"?CartAction.NewSet:offer.Source=="Used"?CartAction.BuyUsed:CartAction.Move,assetId=offer.Source=="New"?null:offer.AssetId};
            block.quantity=quantity;
            if(editing?.preparedBlock==true&&prior==null){prior=game.CartLines.Where(l=>!l.preparedBlock&&l.preparationOwner==editing.preparationOwner).ToDictionary(l=>p.catalog.Find(l.definitionId).category,l=>new BlockPartChoice(l.action==CartAction.Move?l.destination==InventoryKind.Storage?"Missing":"Owned":"New",l.action==CartAction.Move?l.assetId:l.definitionId));initialMode=BlockPreparationMode.Manual;}
            if(editing==null){var old=game.CartLines.FirstOrDefault(l=>BlockPreparation.Owner(l)==BlockPreparation.Owner(block));if(old!=null&&block.action==CartAction.NewSet)block.quantity=old.quantity+quantity;}
            BlockPreparationMode mode=initialMode;var manual=prior==null?new Dictionary<PartCategory,BlockPartChoice>():new Dictionary<PartCategory,BlockPartChoice>(prior);var expanded=new HashSet<PartCategory>();BlockPreparationPlan plan=null;VisualElement review=null,choices=null;var error=new Label{name="blockPreparationError",enableRichText=false};error.AddToClassList("car-critical");Action update=null;
            bool Current(){if(origin==null)return true;if(TryValidatePartAction(origin,out _,out var reason))return true;error.text=reason;RejectPartAction(reason);return false;}
            OpenDialog("Prepare block installation",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add plan",()=>{if(!Current())return;if(!game.StagePreparedBlock(plan,revision,carId,expected)){error.text=game.NativeNotice;return;}CloseDialog();partsStateKey=null;cartPageKey=null;if(editing!=null)RefreshNativeCart("Parts");},true)},populate:content=>{
                content.Add(new Label(offer.Definition.name){enableRichText=false});QueryChoices(content,"blockPreparationMode","Components",new[]{"Reuse","Manual","OEM"},()=>mode.ToString(),value=>{mode=(BlockPreparationMode)Enum.Parse(typeof(BlockPreparationMode),value);update();},3);
                review=new VisualElement{name="blockPreparationReview"};content.Add(review);choices=new VisualElement{name="blockPreparationChoices"};content.Add(choices);content.Add(error);
                update=()=>{if(!Current())return;
                    foreach(var s in choices.Query<RacingSurface>().ToList().Concat(review.Query<RacingSurface>().ToList())){surfaces.Remove(s);dialogSurfaces.Remove(s);}choices.Clear();review.Clear();
                    plan=BlockPreparation.Build(game.Profile,carId,block,game.CartLines,mode,manual,game.PricingPolicy);error.text=plan.Error??"";
                    foreach(var choice in plan.Choices){var group=new VisualElement();group.AddToClassList("warning-item");choices.Add(group);group.Add(new Label(PartSemantics.Friendly(choice.Key.ToString())+" · "+BlockChoiceText(choice.Value)){enableRichText=false});
                        if(mode==BlockPreparationMode.Manual){PartCategory category=choice.Key;var optionBody=new VisualElement();optionBody.EnableInClassList("hidden",!expanded.Contains(category));var toggle=new Button(()=>{if(!Current())return;if(!expanded.Add(category))expanded.Remove(category);optionBody.EnableInClassList("hidden",!expanded.Contains(category));}){name="blockCategory:"+category,text="Choose",focusable=false,tabIndex=-1};toggle.AddToClassList("action-button");group.Add(toggle);AddSurface(toggle,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(toggle.Q<RacingSurface>());group.Add(optionBody);var options=new List<BlockPartChoice>{new BlockPartChoice("Missing")};var slot=p.SelectedCar.installed.slots.Find(s=>s.slot==category.ToString());if(slot!=null)options.Add(new BlockPartChoice("Keep",slot.partInstanceId));if(choice.Value.Source=="Cart")options.Add(choice.Value);
                            options.AddRange(p.parts.Where(i=>i.location==PartLocation.Inventory&&p.catalog.Find(i.definitionId)?.category==category&&p.catalog.Fits(p.catalog.Spec(p.SelectedCar.vehicleId),p.catalog.Find(i.definitionId))).Select(i=>new BlockPartChoice("Owned",i.instanceId)));
                            options.AddRange(p.catalog.parts.Where(d=>d.category==category&&p.catalog.Fits(p.catalog.Spec(p.SelectedCar.vehicleId),d)).OrderBy(d=>d.price).Select(d=>new BlockPartChoice("New",d.id)));
                            foreach(var option in options){var captured=option;var b=new Button(()=>{if(!Current())return;manual[category]=captured;update();}){name="blockChoice:"+category+":"+option.Source+":"+option.Id,text=BlockChoiceText(option),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");b.AddToClassList("block-choice-row");b.EnableInClassList("selected",choice.Value.Source==option.Source&&choice.Value.Id==option.Id);optionBody.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                        }
                    }
                    review.Add(new Label(plan.Proposed==null?"Total unavailable":"Cart total · "+DisplayUnits.Money(plan.Total)+" · Balance after · "+DisplayUnits.Money((long)p.cash-plan.Total)){name="blockPreparationTotal"});
                    if(plan.Proposed!=null){EngineComparison(review,plan.Current,plan.Proposed,"blockPreparation",(element,kind)=>{AddSurface(element,kind);dialogSurfaces.Add(element.Q<RacingSurface>());},false,plan.Healthy,(definition,panel)=>OpenDialog(definition.Label,definition.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)},dismiss:()=>root.schedule.Execute(()=>{if(game.ScreenName=="Parts")OpenBlockPreparation(offer,block.quantity,block,mode,manual,expected,revision,origin);})) ,proposedLabel:"With block");foreach(var w in plan.Proposed.warnings){var label=new Label(w.title+" · "+SharedUnitSettings.TorqueText(w.explanation)+"\n"+SharedUnitSettings.TorqueText(w.suggestion)){enableRichText=false};label.AddToClassList(w.critical?"car-critical":"car-warning");review.Add(label);}}
                    root.Q<Button>("dialogAddplan").SetEnabled(plan.Ready);ExcludeTabTraversal();
                };
            });update();
        }
        string BlockChoiceText(BlockPartChoice choice)
        {
            if(choice.Source=="Missing")return "Missing / no part";if(choice.Source=="Cart")return "Cart · "+game.Profile.catalog.Find(choice.Id)?.name;
            if(choice.Source=="New"){var d=game.Profile.catalog.Find(choice.Id);return "New · "+d.name+" · "+InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,d)+" units · "+DisplayUnits.Money(PartsMarket.Quote(game.Profile,d,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,d)).cost);}
            var part=game.Profile.parts.Find(i=>i.instanceId==choice.Id);return choice.Source+" · "+game.Profile.catalog.Find(part.definitionId).name+" · "+part.condition.health.ToString("P0")+(part.condition.failed?" · FAILED":"")+" · #"+part.instanceId.Substring(Math.Max(0,part.instanceId.Length-6));
        }
    }
}
