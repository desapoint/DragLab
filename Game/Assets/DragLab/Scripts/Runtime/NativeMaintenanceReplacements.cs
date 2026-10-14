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
        void ReplacementActions(VisualElement parent,OwnedCar car,bool available)
        {
            var row=new VisualElement();row.AddToClassList("maintenance-part-actions");parent.Add(row);
            MaintenanceButton(row,"maintenanceReplaceBroken","Replace broken",()=>OpenReplacements(car.instanceId,true),available&&MaintenanceReplacements.Candidates(game.Profile,car.instanceId,true).Length>0);
            MaintenanceButton(row,"maintenanceReplaceWorn","Replace under 15%",()=>OpenReplacements(car.instanceId,false),available);
        }
        void OpenReplacements(string carId,bool brokenOnly)
        {
            if(ModalOpen||!OwnedDetailsOpen||ownedDetailId!=carId)return;
            long revision=game.Profile.revision;string snapshot=JsonUtility.ToJson(game.Profile);float threshold=15;
            var excluded=new HashSet<string>(StringComparer.Ordinal);var selected=new HashSet<string>(StringComparer.Ordinal);
            var list=new VisualElement{name="maintenanceReplacementTargets"};
            var summary=new Label{name="maintenanceReplacementSummary",enableRichText=false};var cost=new Label{name="maintenanceReplacementCost",enableRichText=false};
            var delivery=new Label{name="maintenanceReplacementDelivery",enableRichText=false};delivery.AddToClassList("car-warning");
            var error=new Label{name="maintenanceReplacementError",enableRichText=false};error.AddToClassList("car-critical");ReplacementPlan plan=null;
            var review=new VisualElement{name="maintenanceReplacementReview"};review.AddToClassList("part-purchase-review");review.Add(summary);review.Add(cost);review.Add(delivery);review.Add(error);
            Action reprice=null,rebuild=null;
            OpenDialog(brokenOnly?"Replace broken":"Replace worn",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=>{
                if(plan==null||plan.lines.Count==0)return;
                if(!game.CommitNativeReplacements(carId,revision,snapshot,brokenOnly,threshold,selected.ToArray(),plan.cost)){error.text=game.NativeNotice;return;}
                CloseDialog();InvalidateOwnedGrid();Refresh();
            },true)},dismiss:()=>review.RemoveFromHierarchy(),populate:content=>{
                var car=game.Profile.cars.Find(c=>c.instanceId==carId);content.Add(new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)){name="maintenanceReplacementCar",enableRichText=false});
                content.Add(new Label("New matching parts. Removed parts keep their condition in inventory."){enableRichText=false});
                if(!brokenOnly){content.Add(new Label("Below remaining condition"));var range=new NativeTuningRange("maintenanceThreshold",()=>threshold,v=>threshold=v,()=>0,()=>100,()=>15,1,.1f,"%",()=>rebuild(),message=>error.text=message);range.style.flexGrow=1;content.Add(range);}
                content.Add(list);
            });
            var actions=root.Q("modalActions");actions.parent.Insert(actions.parent.IndexOf(actions),review);
            reprice=()=>{
                try{plan=MaintenanceReplacements.Preview(game.Profile,carId,brokenOnly,threshold,selected);int units=plan.targets.Sum(t=>t.count);summary.text=units+(units==1?" unit selected":" units selected");cost.text="Total "+DisplayUnits.Money(plan.cost)+" · Balance after "+DisplayUnits.Money((long)game.Profile.cash-plan.cost);delivery.text="Stock "+plan.localUnits+" units · Express "+plan.expressUnits+" units"+(plan.deliveryFee>0?" · Fee "+DisplayUnits.Money(plan.deliveryFee)+" included":"");error.text=game.Profile.cash<plan.cost?"Insufficient cash.":game.CartCount>0?"Checkout or clear the cart first.":"";root.Q<Button>("dialogConfirm").SetEnabled(plan.lines.Count>0&&error.text.Length==0);}
                catch(Exception e){plan=null;error.text=e.Message;root.Q<Button>("dialogConfirm").SetEnabled(false);}
            };
            rebuild=()=>{
                ClearPartCartSwitches();list.Clear();selected.Clear();var candidates=MaintenanceReplacements.Candidates(game.Profile,carId,brokenOnly,threshold);
                foreach(var section in candidates.GroupBy(t=>NativePartBrowser.Section(t.category))){var group=new VisualElement();group.AddToClassList("owned-query-group");list.Add(group);group.Add(new Label(NativePartBrowser.SectionNames[(int)section.Key]));
                    foreach(var set in section.GroupBy(t=>t.partId)){var def=game.Profile.catalog.Find(game.Profile.parts.Find(p=>p.instanceId==set.Key).definitionId);group.Add(new Label(def.name){enableRichText=false});
                        foreach(var target in set){if(!excluded.Contains(target.key))selected.Add(target.key);var row=new VisualElement();row.AddToClassList("setting-row");group.Add(row);var captionGroup=new VisualElement();captionGroup.AddToClassList("setting-caption");row.Add(captionGroup);string label=target.complete?(target.count>1?"Complete set · "+target.count+" units":"Complete part"):InventoryUnits.PositionLabel(game.Profile,game.Profile.cars.Find(c=>c.instanceId==carId),target.definitionId,target.position);var caption=new Label(label+" · "+target.condition.ToString("P1")+(target.broken?" · Fault":"")){enableRichText=false,tooltip=target.key};caption.AddToClassList("maintenance-part-name");caption.AddToClassList(target.broken?"car-critical":ServiceConditionClass(target.condition));captionGroup.Add(caption);
                            var toggle=new Toggle{name="maintenanceTarget:"+target.key,value=selected.Contains(target.key),focusable=false,tabIndex=-1};toggle.AddToClassList("setting-toggle");row.Add(toggle);var surface=new SwitchSurface(toggle);toggle.Q(className:"unity-toggle__input").Add(surface);switchSurfaces.Add(surface);partCartSwitches.Add(surface);surface.SetValue(toggle.value);toggle.RegisterValueChangedCallback(e=>{if(e.newValue){excluded.Remove(target.key);selected.Add(target.key);}else{excluded.Add(target.key);selected.Remove(target.key);}surface.SetValue(e.newValue);reprice();});
                        }
                    }
                }
                if(candidates.Length==0)list.Add(new Label("No matching components"));reprice();ExcludeTabTraversal();
            };
            rebuild();
        }
    }
    public sealed partial class PrototypeGame
    {
        internal bool CommitNativeReplacements(string carId,long revision,string snapshot,bool brokenOnly,double threshold,string[] keys,int shownCost)
        {
            try{
                if(screen!="Garage"||pendingAction!=null||profile.revision!=revision||!SnapshotEquality.Profile(snapshot,profile,profile.catalog))throw new InvalidOperationException("Car or stock changed. Reopen maintenance.");
                if(CartCount>0)throw new InvalidOperationException("Checkout or clear the cart first.");
                var plan=MaintenanceReplacements.Preview(profile,carId,brokenOnly,threshold,keys);if(plan.lines.Count==0||plan.cost!=shownCost)throw new InvalidOperationException("Replacement selection or price changed.");
                long now=DateTime.UtcNow.Ticks;var quote=PartCart.Quote(profile,carId,plan.lines,now,300,PricingPolicy);if(quote.total!=shownCost)throw new InvalidOperationException("Replacement price changed.");
                if(!CommitParts(revision,p=>PartCart.Checkout(p,quote,now,PricingPolicy)))return false;
                if(carId==profile.selectedOwnedVehicleId)ShowGarageCar();notice="Replacements saved.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
}
