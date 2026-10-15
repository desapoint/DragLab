using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator SupplyPricingChecks(WindowsKeyboardProbe input)
        {
            game.Profile.cash=1000000;game.Profile.reputation=1000;
            InterfaceComparison.SetReplacement("Parts",true);game.QueueNativeNavigation("Parts");yield return new WaitForSecondsRealtime(.3f);
            foreach(int fee in new[]{5,10})foreach(int stock in new[]{0,1,4})foreach(bool individual in new[]{true,false})
            {
                var p=game.Profile;var car=p.SelectedCar;var pack=p.parts.Single(i=>i.instanceId==car.installed.slots.First(s=>s.slot=="Pistons").partInstanceId);
                var d=p.catalog.Find(pack.definitionId);var pool=p.inventory.market.parts.First(s=>s.definitions.Contains(d.id));pool.units=stock;pool.initialUnits=Math.Max(pool.initialUnits,stock);pool.deliveryPercent=fee;
                int count=pack.condition.elements.Length,unit=Math.Max(1,(int)Math.Ceiling(d.price/(double)count));
                int Cost(int price,int need,int available)=>price+(int)Math.Ceiling(price*(Math.Max(0,need-available)/(double)need)*fee/100.0);
                int expected=individual?Cost(unit,1,stock)+Cost(unit,1,Math.Max(0,stock-1)):Cost(d.price,count,stock);
                int cash=p.cash;string oldPack=pack.instanceId;string[] replaced=pack.condition.elements.Take(2).Select(e=>e.id).ToArray();
                var neighbours=pack.condition.elements.Skip(2).Select(e=>JsonUtility.ToJson(e)).ToArray();
                Check(ProfileStore.TrySave(p),"Pricing probe saves only its isolated shelf fixture");
                Check(game.StageNativePurchase(d.id,individual?pack.condition.elements[0].role:null,individual?2:1,true,individual?replaced:null),"Actual cart accepts the pricing case");
                yield return new WaitForSecondsRealtime(.2f);yield return Click(input,"partsCart");
                Check(shell.Root.Q<Label>("cartTotal").text==DisplayUnits.Money(expected)&&game.NativeCartTotal()==expected,"Displayed cart total matches independent supply math: fee="+fee+" local="+stock+" individual="+individual);
                string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
                yield return Click(input,"cartCheckout");yield return Click(input,"dialogCancel");
                Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Cancelling checkout does not charge or change owned assets");
                yield return Click(input,"cartCheckout");if(stock==0&&fee==10)yield return Capture(individual?"pricing-individual-express":"pricing-set-express");yield return Click(input,"dialogConfirm");
                Check(!shell.ModalOpen&&game.Profile.cash==cash-expected&&game.CartCount==0,"Real confirmation charges exactly the displayed amount and clears the cart");
                var fitted=game.Profile.parts.Single(i=>i.instanceId==game.Profile.SelectedCar.installed.slots.First(s=>s.slot=="Pistons").partInstanceId);
                bool retained=individual
                    ? fitted.instanceId==oldPack&&neighbours.SequenceEqual(fitted.condition.elements.Skip(2).Select(e=>JsonUtility.ToJson(e)))&&replaced.All(id=>game.Profile.looseEngineElements.Any(u=>u.element.id==id))
                    : fitted.instanceId!=oldPack&&game.Profile.parts.Single(i=>i.instanceId==oldPack).location==PartLocation.Inventory;
                Check(retained,"Payment installs the intended members/set and retains displaced assets");
                Check(ProfileStore.Load().cash==cash-expected,"Paid balance survives actual SQLite reload");
                yield return Click(input,"cartBack");
            }
            // The older market fixture bypassed transaction normalization when
            // creating fresh tire packs. Confirm the actual Store checkout does
            // publish four typed tires and a valid reloadable profile.
            var owner=game.Profile;var tirePack=owner.parts.Single(i=>i.instanceId==owner.SelectedCar.installed.slots.First(s=>s.slot=="Tires").partInstanceId);
            var tireDefinition=owner.catalog.Find(tirePack.definitionId);var shelf=owner.inventory.market.parts.First(s=>s.definitions.Contains(tireDefinition.id));shelf.units=0;shelf.deliveryPercent=10;
            int tireCost=tireDefinition.price+(int)Math.Ceiling(tireDefinition.price*.1),balance=owner.cash;var originalIds=owner.parts.Select(i=>i.instanceId).ToArray();
            Check(ProfileStore.TrySave(owner)&&game.StageNativePurchase(tireDefinition.id,null,1,false),"A new tire set can be staged for storage with an empty shelf");
            yield return new WaitForSecondsRealtime(.2f);yield return Click(input,"partsCart");
            Check(shell.Root.Q<Label>("cartTotal").text==DisplayUnits.Money(tireCost),"Stored tire set shows its full express total");
            yield return Click(input,"cartCheckout");yield return Click(input,"dialogConfirm");
            var stored=game.Profile.parts.Single(i=>!originalIds.Contains(i.instanceId));
            Check(!shell.ModalOpen&&game.Profile.cash==balance-tireCost&&stored.location==PartLocation.Inventory&&stored.condition.elements.Length==4&&stored.condition.elements.All(e=>e is TireCondition&&!string.IsNullOrEmpty(e.id)),"Actual Store checkout charges once and publishes four typed tire members");
            var restored=ProfileStore.Load();Check(!ProfileStore.ResetRequired&&restored.parts.Any(i=>i.instanceId==stored.instanceId)&&MechanicsOwnership.Validate(restored,restored.catalog)==null,"Stored new tire set survives durable reload and permits future commerce");
        }
#endif
    }
}
