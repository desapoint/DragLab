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
            var held=game.Profile.inventory.cart;
            if(held?.carId==car.instanceId&&held.lines.Count>0)MaintenanceButton(row,"maintenanceCart","View cart",()=>{
                if(!game.SelectNativeCar(car.instanceId,game.Profile.revision)){root.Q<Label>("carSelectionError").text=game.NativeNotice;return;}
                cartPageRequestedOwner=car.instanceId;game.QueueNativeNavigation("Parts");
            },true);
        }
        void OpenReplacements(string carId,bool brokenOnly)
        {
            if(ModalOpen||!OwnedDetailsOpen||ownedDetailId!=carId)return;
            long revision=game.Profile.revision;string snapshot=JsonUtility.ToJson(game.Profile);float threshold=15;
            var excluded=new HashSet<string>(StringComparer.Ordinal);var selected=new HashSet<string>(StringComparer.Ordinal);
            var pills=new Dictionary<string,Label>();
            var list=new VisualElement{name="maintenanceReplacementTargets"};
            var summary=new Label{name="maintenanceReplacementSummary",enableRichText=false};var cost=new Label{name="maintenanceReplacementCost",enableRichText=false};
            var delivery=new Label{name="maintenanceReplacementDelivery",enableRichText=false};delivery.AddToClassList("car-warning");
            var error=new Label{name="maintenanceReplacementError",enableRichText=false};error.AddToClassList("car-critical");ReplacementPlan plan=null;
            var review=new VisualElement{name="maintenanceReplacementReview"};review.AddToClassList("part-purchase-review");review.Add(summary);review.Add(cost);review.Add(delivery);review.Add(error);
            Action reprice=null,rebuild=null;
            string cartSnapshot=game.NativeReplacementCartSnapshot(carId);
            OpenDialog(brokenOnly?"Replace broken":"Replace worn",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add to cart",()=>{
                if(plan==null||plan.lines.Count==0)return;
                if(!game.StageNativeReplacements(carId,revision,snapshot,cartSnapshot,brokenOnly,threshold,selected.ToArray())){error.text=game.NativeNotice;return;}
                CloseDialog();InvalidateOwnedGrid();Refresh();
            },true,id:"dialogConfirm")},dismiss:()=>review.RemoveFromHierarchy(),populate:content=>{
                var car=game.Profile.cars.Find(c=>c.instanceId==carId);content.Add(new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)){name="maintenanceReplacementCar",enableRichText=false});
                content.Add(new Label("Matching parts · Install checked · Pay at checkout"){enableRichText=false});
                if(!brokenOnly){
                    content.Add(new Label("Below remaining condition"));NativeTuningRange range=null;bool pending=false;
                    range=new NativeTuningRange("maintenanceThreshold",()=>threshold,v=>threshold=v,()=>0,()=>100,()=>15,1,.1f,"%",()=>{
                        if(range.Dragging){pending=true;root.Q<Button>("dialogConfirm").SetEnabled(false);}else rebuild();
                    },message=>error.text=message);
                    range.GestureFinished+=()=>{if(pending){pending=false;rebuild();}};
                    range.style.flexGrow=1;content.Add(range);
                }
                content.Add(list);
            });
            var actions=root.Q("modalActions");actions.parent.Insert(actions.parent.IndexOf(actions),review);
            reprice=()=>{
                try{
                    plan=MaintenanceReplacements.Preview(game.Profile,carId,brokenOnly,threshold,selected);int units=plan.targets.Sum(t=>t.count);summary.text=units+(units==1?" unit selected":" units selected");
                    var combined=game.PreviewNativeReplacements(carId,plan);cost.text="Cart total "+DisplayUnits.Money(combined.cost)+" · Balance after "+DisplayUnits.Money((long)game.Profile.cash-combined.cost);
                    delivery.text="Stock "+combined.localUnits+" units · Express "+combined.expressUnits+" units"+(combined.deliveryFee>0?" · Fee "+DisplayUnits.Money(combined.deliveryFee)+" included":"");
                    foreach(var pair in pills){
                        var definitions=plan.targets.Where(t=>t.partId==pair.Key).Select(t=>t.definitionId).ToArray();
                        var quotes=combined.lines.Where(l=>l.install&&definitions.Contains(l.definitionId)).Select(l=>combined.supply.TryGetValue(l.action+"/"+l.definitionId+"/"+l.role,out var q)?q:null).Where(q=>q!=null).ToArray();
                        int missing=quotes.Sum(q=>q.expressUnits),stock=quotes.Sum(q=>q.localUnits),fee=quotes.Sum(q=>q.fee),total=stock+missing;
                        pair.Value.text=missing>0?"Express · "+missing+"/"+total:"In stock · "+stock;
                        pair.Value.tooltip="Shop stock: "+stock+" of "+total+" units · Express: "+missing+(fee>0?" · +"+DisplayUnits.Money(fee)+" included in cart total":" · No delivery fee");
                        pair.Value.EnableInClassList("hidden",quotes.Length==0);pair.Value.EnableInClassList("car-warning",missing>0);
                    }
                    error.text=game.Profile.cash<combined.cost?"Not enough cash to checkout yet.":"";root.Q<Button>("dialogConfirm").SetEnabled(plan.lines.Count>0);
                }
                catch(Exception e){plan=null;error.text=e.Message;root.Q<Button>("dialogConfirm").SetEnabled(false);}
            };
            rebuild=()=>{
                ClearPartCartSwitches();list.Clear();selected.Clear();pills.Clear();var candidates=MaintenanceReplacements.Candidates(game.Profile,carId,brokenOnly,threshold);
                foreach(var section in candidates.GroupBy(t=>NativePartBrowser.Section(t.category))){var group=new VisualElement();group.AddToClassList("owned-query-group");list.Add(group);group.Add(new Label(NativePartBrowser.SectionNames[(int)section.Key]));
                    foreach(var set in section.GroupBy(t=>t.partId)){var def=game.Profile.catalog.Find(game.Profile.parts.Find(p=>p.instanceId==set.Key).definitionId);var heading=new VisualElement();heading.AddToClassList("maintenance-supply-heading");group.Add(heading);heading.Add(new Label(def.name){enableRichText=false});var pill=new Label{name="maintenanceSupply:"+set.Key,enableRichText=false};pill.AddToClassList("maintenance-supply-pill");heading.Add(pill);pills[set.Key]=pill;
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
        PartCartLine[] NativeReplacementCart(string carId)
        {
            var saved=profile.inventory.cart;
            if(saved!=null&&saved.carId!=carId)throw new InvalidOperationException("The cart belongs to another car. Checkout or clear it first.");
            if(cart.Count>0){if(cartOwner!=carId)throw new InvalidOperationException("The cart belongs to another car. Checkout or clear it first.");return cart.Select(l=>l.Copy()).ToArray();}
            if(saved==null)return Array.Empty<PartCartLine>();
            return saved.lines.Select(l=>l.Copy()).ToArray();
        }
        internal string NativeReplacementCartSnapshot(string carId)
        {try{return JsonUtility.ToJson(new PartCart.Lines{entries=NativeReplacementCart(carId).ToList()});}catch(InvalidOperationException){return null;}}
        internal ReplacementCartPlan PreviewNativeReplacements(string carId,ReplacementPlan plan)
        {
            var existing=NativeReplacementCart(carId);
            if(plan.lines.Count==0&&existing.Length==0)return new ReplacementCartPlan{lines=new List<PartCartLine>()};
            return MaintenanceReplacements.AddToCart(profile,carId,plan,existing,PricingPolicy);
        }
        internal bool StageNativeReplacements(string carId,long revision,string snapshot,string cartSnapshot,bool brokenOnly,double threshold,string[] keys)
        {
            if(BlockForManualRefill())return false;
            try{
                if(screen!="Garage"||pendingAction!=null||profile.revision!=revision||!SnapshotEquality.Profile(snapshot,profile,profile.catalog))throw new InvalidOperationException("Car or stock changed. Reopen maintenance.");
                if(cartSnapshot==null||cartSnapshot!=NativeReplacementCartSnapshot(carId))throw new InvalidOperationException("Cart changed. Reopen maintenance.");
                var plan=MaintenanceReplacements.Preview(profile,carId,brokenOnly,threshold,keys);if(plan.lines.Count==0)throw new InvalidOperationException("Select at least one replacement.");
                var combined=PreviewNativeReplacements(carId,plan);
                if(!CartReservations.SameCart(profile,carId,combined.lines)&&!CommitParts(revision,p=>CartReservations.Capture(p,carId,combined.lines)))return false;
                cartOwner=carId;cart.Clear();cart.AddRange(combined.lines.Select(l=>l.Copy()));CartChanged();notice="Replacements added to cart. Pay and install at checkout.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
}
