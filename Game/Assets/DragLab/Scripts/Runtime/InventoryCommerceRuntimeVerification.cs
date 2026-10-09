using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator InventoryCommerceRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new StringBuilder("Shared inventories, set/unit offers, trade cart and dealer buyback / actual Windows mouse / isolated profile.\n");
            Action<bool,string> require=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);};
            var p=game.Profile;p.cash=100000;p.reputation=100;var car=p.SelectedCar;
            var original=p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);original.condition.elements[0].health=.3f;EngineElementService.RefreshAggregate(original.condition);string oldId=original.instanceId,condition=JsonUtility.ToJson(original.condition);
            ProfileStore.Save(p);game.ReturnToGarage();yield return WaitForInspection();
            string forged=p.catalog.Id(car.vehicleId,PartCategory.Pistons,1),bytes=File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8);
            using(var mouse=new WindowsKeyboardProbe()) {
                game.ShowParts(PartCategory.Pistons);game.SelectPartsPage("Shop");yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"cart-fit:"+forged);yield return WaitForInspection();
                require(game.CartLines.Count==1&&game.CartLines[0].action==CartAction.NewSet&&game.CartLines[0].quantity==1&&game.CartLines[0].install,"Default offer buys a complete piston set without selecting any cylinder");
                require(File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)==bytes,"Complete kit preview never saves or modifies assets");yield return Capture("complete-set-cart");
                yield return ClickPart(mouse,"cart-checkout");yield return WaitForInspection();
                require(game.Profile.parts.Any(i=>i.instanceId==oldId&&i.location==PartLocation.Inventory&&JsonUtility.ToJson(i.condition)==condition),"Installing the complete kit retains the old set and every worn element");
                yield return ClickPart(mouse,"parts-page:Inventory");yield return ClickPart(mouse,"move-target:Storage:storage");yield return ClickPart(mouse,"stage-move:"+oldId);
                require(game.Profile.parts.Find(i=>i.instanceId==oldId).storageId!=SharedInventory.Storage,"Staged storage move waits for confirmation");
                yield return ClickPart(mouse,"cart-checkout");yield return ClickPart(mouse,"inventory-storage:storage");
                require(ProfileStore.Load().parts.Any(i=>i.instanceId==oldId&&i.storageId==SharedInventory.Storage),"Actual container move survives disk reload");yield return Capture("storage-original-wear");
                yield return ClickPart(mouse,"cart-sell:"+oldId);yield return ClickPart(mouse,"cart-checkout");
                require(!game.Profile.parts.Any(i=>i.instanceId==oldId)&&game.Profile.inventory.dealer.Any(s=>s.Id==oldId&&JsonUtility.ToJson(s.part.condition)==condition),"Cart sale transfers the actual worn set to dealer stock");
                yield return ClickPart(mouse,"parts-page:Shop");yield return ClickPart(mouse,"dealer-cart:"+oldId);yield return ClickPart(mouse,"cart-checkout");
                require(game.Profile.parts.Any(i=>i.instanceId==oldId&&JsonUtility.ToJson(i.condition)==condition)&&!game.Profile.inventory.dealer.Any(s=>s.Id==oldId),"Buying back dealer stock retains identity and wear with no duplication");
                string role=InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,game.Profile.catalog.Find(forged))[0];
                yield return ClickPart(mouse,"offer-unit:"+forged+":"+role);yield return ClickPart(mouse,"cart-add:"+forged);
                require(game.CartLines.Single().action==CartAction.NewUnits&&game.CartLines.Single().positions.Count==0&&!game.CartLines.Single().install,"Individual units can be purchased without choosing a cylinder");
                yield return ClickPart(mouse,"cart-checkout");yield return ClickPart(mouse,"parts-page:Inventory");yield return ClickPart(mouse,"inventory-storage:garage");
                var loose=game.Profile.looseEngineElements.Single(u=>u.definitionId==forged&&u.element.health==1);var pack=game.Profile.parts.Find(i=>i.instanceId==game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);string position=pack.condition.elements[0].id,other=JsonUtility.ToJson(pack.condition.elements[1]);
                yield return ClickPart(mouse,"reuse-unit:"+loose.element.id+":"+position);
                pack=game.Profile.parts.Find(i=>i.instanceId==game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);
                require(pack.condition.elements[0].id==loose.element.id&&JsonUtility.ToJson(pack.condition.elements[1])==other,"Inventory unit installation changes only the selected piston and keeps the reused identity");
                var loaded=ProfileStore.Load();require(loaded.parts.Any(i=>i.condition.elements.Any(e=>e.id==loose.element.id))&&loaded.looseEngineElements.Any(u=>u.element.id==position),"Individual installation and retained old piston survive reload");
                yield return ClickPart(mouse,"parts-page:Shop");yield return ClickPart(mouse,"offer-set:"+forged);yield return ClickPart(mouse,"cart-add:"+forged);
                require(game.CartLines.Single().action==CartAction.NewSet,"Complete set remains directly selectable after individual buying");yield return ClickPart(mouse,"cart-clear");
            }
            require(errors==0,"Inventory commerce workflow reports zero runtime/GUI errors");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",new UTF8Encoding(false));
#else
            yield return null;throw new InvalidOperationException("Inventory commerce probe requires Windows development player.");
#endif
        }
    }
}
