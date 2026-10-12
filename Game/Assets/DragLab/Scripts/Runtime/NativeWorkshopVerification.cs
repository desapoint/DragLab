using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        IEnumerator NativeMoveChecks(WindowsKeyboardProbe input)
        {
            var p=game.Profile;var car=p.SelectedCar;string part=PartOperations.Buy(p,p.catalog,car.instanceId,p.componentsPart(PartCategory.Pistons).definitionId,false);
            p.inventory.storage.Add(new InventoryContainer{kind=InventoryKind.Storage,id="workshop-shelf",name="Workshop shelf"});p.revision++;MechanicsOwnership.Ensure(p,p.catalog);ProfileStore.Save(p);
            yield return Click(input,"partsSource:Inventory");yield return WaitParts();
            var entry=shell.PartBrowser.Entries.First(e=>e.AssetId==part);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(shell.Root.Q("partCard:"+entry.Id));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"partCard:"+entry.Id);
            string before=JsonUtility.ToJson(p);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"partMove");Check(shell.ModalOpen&&!shell.Root.Q<Button>("dialogAddmove").enabledSelf,"Move starts as an unselected, nonmutating destination draft");
            yield return PartDialogClick(input,"inventoryMoveTarget:Storage:workshop-shelf");Check(shell.Root.Q<Button>("dialogAddmove").enabledSelf&&game.CartCount==0,"Storage destination preview is valid without staging or saving");yield return Capture("inventory-move");yield return Click(input,"dialogCancel");
            Check(before==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath))&&game.CartCount==0,"Move cancellation preserves exact ownership, condition, resources and disk");
            yield return Click(input,"partMove");yield return PartDialogClick(input,"inventoryMoveTarget:Storage:workshop-shelf");yield return Click(input,"dialogAddmove");Check(game.CartLines.Single().destinationId=="workshop-shelf"&&before==JsonUtility.ToJson(p),"Confirmed Move stages an exact destination without applying it");
            yield return Click(input,"partBack");yield return Click(input,"partsCart");yield return Click(input,"cartCheckout");yield return Click(input,"dialogConfirm");yield return new WaitForSecondsRealtime(.3f);
            Check(game.Profile.parts.Find(i=>i.instanceId==part).storageId=="workshop-shelf"&&game.Profile.parts.Find(i=>i.instanceId==part).definitionId==entry.Definition.id&&game.CartCount==0,"Atomic cart checkout moves the retained part without replacing its identity");
            yield return Click(input,"cartBack");yield return Click(input,"partsSource:Inventory");yield return WaitParts();yield return Click(input,"partsFilters");yield return PartDialogClick(input,"partContainer:workshop-shelf");yield return Click(input,"dialogApply");Check(shell.PartQuery.Container=="workshop-shelf"&&shell.Root.Q("partCard:"+part)!=null&&!shell.Root.Q("partCard:"+part).ClassListContains("hidden"),"Inventory location filter selects the exact storage container");
            yield return Click(input,"partsClear");game.ClearNativeCart();
        }
        void EngineDomainChecks()
        {
            foreach(var vehicle in game.vehicles){var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,new MechanicalCatalog(game.vehicles.Select(v=>v.spec)));p.cash=1000000;p.reputation=1000;
                for(int i=0;i<2;i++)VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                var receiver=p.cars[p.cars.Count-2];var donor=p.cars.Last();string oldEngine=receiver.engineAssemblyId,id=donor.engineAssemblyId;donor.fluids.oilLitres=Math.Min(2,donor.fluids.oilLitres);donor.fluids.coolantLitres=Math.Min(3,donor.fluids.coolantLitres);var piston=p.parts.First(part=>part.ownerId==id&&p.catalog.Find(part.definitionId).category==PartCategory.Pistons);piston.condition.elements[0].health=.6f;piston.condition.elements[0].fault=ElementFault.CompressionLeak;piston.condition.elements[0].severity=.2f;EngineElementService.RefreshAggregate(piston.condition);
                string member=piston.condition.elements[0].id;AssemblyOperations.Detach(p,donor.instanceId);AssemblyOperations.Rename(p,id,"  Spare "+vehicle.spec.displayName+"  ");
                var stored=p.engines.Find(e=>e.instanceId==id);Check(stored.customName=="Spare "+vehicle.spec.displayName&&stored.fluids.oilLitres==2&&stored.slots.Any(s=>s.partInstanceId==piston.instanceId),"Named stored engine retains its real parts and liquids: "+vehicle.spec.id);
                string before=JsonUtility.ToJson(p);var preview=EngineWorkshop.Preview(p,id,receiver.instanceId);Check(before==JsonUtility.ToJson(p)&&preview.cars.Find(c=>c.instanceId==receiver.instanceId).engineAssemblyId==id&&preview.engines.Find(e=>e.instanceId==oldEngine).parentVehicleId=="","Copied swap retains the old engine without editing the live profile: "+vehicle.spec.id);
                PlayerProfile committed;string error;Check(!MechanicsTransactions.Try(p,p.catalog,p.revision,q=>EngineWorkshop.Swap(q,id,receiver.instanceId),_=>false,out committed,out error)&&ReferenceEquals(committed,p)&&JsonUtility.ToJson(p)==before,"Failed swap persistence rolls back identities, fluids and condition: "+vehicle.spec.id);
                Check(MechanicsTransactions.Try(p,p.catalog,p.revision,q=>EngineWorkshop.Swap(q,id,receiver.instanceId),_=>true,out committed,out error)&&MechanicsOwnership.Validate(committed,committed.catalog)==null&&committed.parts.Find(i=>i.instanceId==piston.instanceId).condition.elements.Any(e=>e.id==member&&e.health==.6f&&e.fault==ElementFault.CompressionLeak),"Atomic swap retains singular wear/faults with valid exclusive ownership: "+vehicle.spec.id);
                Check(committed.cars.Find(c=>c.instanceId==receiver.instanceId).fluids.oilLitres==2&&committed.cars.Find(c=>c.instanceId==receiver.instanceId).fluids.coolantLitres==3&&committed.engines.Find(e=>e.instanceId==oldEngine).fluids!=null,"Engine liquids move while old-engine supplies remain retained: "+vehicle.spec.id);
                Check(!MechanicsTransactions.Try(p,p.catalog,p.revision-1,q=>AssemblyOperations.Rename(q,id,"stale"),_=>true,out committed,out error)&&!MechanicsTransactions.Try(p,p.catalog,p.revision,q=>AssemblyOperations.Rename(q,id,new string('x',49)),_=>true,out committed,out error),"Stale or invalid engine naming rejects without publication: "+vehicle.spec.id);
                var preparation=EngineWorkshop.Copy(p);string head=preparation.engines.Find(e=>e.instanceId==id).slots.First(s=>s.slot==PartCategory.HeadIntake.ToString()).partInstanceId;string upgraded=preparation.catalog.Id(vehicle.spec.id,PartCategory.HeadIntake,1);EngineWorkshop.BuyPart(preparation,id,upgraded,InventoryPricing.NewSet(preparation.catalog.Find(upgraded)));
                Check(preparation.parts.Find(i=>i.instanceId==head).location==PartLocation.Inventory&&preparation.parts.Any(i=>i.ownerId==id&&i.definitionId==upgraded)&&JsonUtility.ToJson(p)==before,"Stored complete-set buying retains originals and leaves the source preview untouched: "+vehicle.spec.id);
                var spare=new LooseEngineElement{definitionId=piston.definitionId,element=piston.condition.elements[0].Copy()};spare.element.id=MechanicsOwnership.NewId("element");spare.element.health=.4f;spare.element.fault=ElementFault.None;spare.element.severity=0;preparation.looseEngineElements.Add(spare);EngineElementService.ReplaceStored(preparation,id,piston.instanceId,member,spare.element.id);
                Check(preparation.parts.Find(i=>i.instanceId==piston.instanceId).condition.elements.Any(e=>e.id==spare.element.id&&e.health==.4f)&&preparation.looseEngineElements.Any(e=>e.element.id==member&&e.element.health==.6f&&e.element.fault==ElementFault.CompressionLeak),"Stored singular reuse keeps incoming wear and outgoing actual faults: "+vehicle.spec.id);
                AssemblyOperations.RemovePart(preparation,id,PartCategory.Crankshaft);MechanicsOwnership.Ensure(preparation,preparation.catalog);var incomplete=EngineWorkshop.Preview(preparation,id,receiver.instanceId);
                Check(!MechanicsOwnership.Resolve(incomplete,incomplete.cars.Find(c=>c.instanceId==receiver.instanceId),incomplete.catalog).Drivable&&!preparation.engines.Find(e=>e.instanceId==id).slots.Any(s=>s.slot==PartCategory.Crankshaft.ToString()),"Incomplete engine preview reports missing hardware without recreating it: "+vehicle.spec.id);
            }
        }
        IEnumerator NativeEngineChecks(WindowsKeyboardProbe input)
        {
            EngineDomainChecks();var p=game.Profile;var receiver=p.SelectedCar;var vehicle=game.vehicles.First(v=>v.spec.id==receiver.vehicleId);
            VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var donor=p.cars.Last();string id=donor.engineAssemblyId,receiverId=receiver.instanceId;
            var pack=p.parts.First(i=>i.ownerId==id&&p.catalog.Find(i.definitionId).category==PartCategory.Pistons);pack.condition.elements[0].health=.6f;pack.condition.elements[0].fault=ElementFault.CompressionLeak;pack.condition.elements[0].severity=.2f;EngineElementService.RefreshAggregate(pack.condition);string partId=pack.instanceId,member=pack.condition.elements[0].id;
            p.selectedOwnedVehicleId=receiverId;p.revision++;MechanicsOwnership.Ensure(p,p.catalog);ProfileStore.Save(p);yield return Click(input,"partsEngines");
            Check(shell.Root.Q("enginePage")!=null&&!shell.Root.Q("enginePage").ClassListContains("hidden")&&shell.Root.Q("partsPage").ClassListContains("hidden"),"Engines replace Build content with the shared full-screen frame");
            yield return Click(input,"engineCard:"+id);string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);int builds=shell.EngineViewBuildCount;
            yield return Click(input,"engineRename");shell.Root.Q<TextField>("engineName").value="Canceled name";yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Engine naming cancel keeps the exact attached engine and disk");
            yield return Click(input,"engineRename");shell.Root.Q<TextField>("engineName").value="Spare café build";yield return Click(input,"dialogSave");Check(game.Profile.engines.Find(e=>e.instanceId==id).customName=="Spare café build"&&shell.Root.Q<Label>("engineTitle").text=="Spare café build","Unicode engine name is saved and replaces its displayed heading");
            yield return Click(input,"engineDetach");yield return Click(input,"dialogConfirm");Check(game.Profile.cars.Find(c=>c.instanceId==donor.instanceId).engineAssemblyId==""&&game.Profile.engines.Find(e=>e.instanceId==id).slots.Any(s=>s.partInstanceId==partId),"Confirmed detach stores exact engine descendants without changing the selected car");
            yield return PartEngineClick(input,"engineGroup:Engine");yield return PartEngineClick(input,"engineMembers:"+partId);yield return PartDialogClick(input,"engineMemberBuy:"+member);yield return Capture("engine-member-confirm");yield return Click(input,"dialogConfirm");
            Check(game.Profile.looseEngineElements.Any(e=>e.element.id==member&&e.element.health==.6f&&e.element.fault==ElementFault.CompressionLeak)&&game.Profile.parts.Find(i=>i.instanceId==partId).condition.elements.All(e=>e.id!=member),"Stored-engine singular replacement retains the actual worn/faulty original as a spare");
            before=JsonUtility.ToJson(game.Profile);disk=File.ReadAllBytes(ProfileStore.FilePath);yield return Click(input,"engineSwap");Check(shell.Root.Q<NativeDynoPlot>("engineSwapDyno")!=null&&shell.Root.Q<NativeStatsPanel>("engineSwapSummary")!=null,"Swap previews the receiving car using shared dyno and stat comparison controls");yield return Capture("engine-swap");yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Swap cancellation retains both engines, resources and saved bytes");
            builds=shell.EngineViewBuildCount;yield return new WaitForSecondsRealtime(.3f);Check(builds==shell.EngineViewBuildCount,"Idle engine details reuse cached inspection and UI without rebuilding");
            Screen.SetResolution(1024,640,false);yield return new WaitForSecondsRealtime(.4f);Check(shell.Root.Q("engineActions").worldBound.yMax<shell.Root.Q("footerBar").worldBound.yMin&&shell.Root.Q("engineScroll").worldBound.yMax<shell.Root.Q("engineActions").worldBound.yMin,"Compact engine view retains scrolling content and fixed actions");yield return Capture("engine-compact");Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.Root.Q<Label>("engineTitle").text=="Spare café build","F8 retains the named engine detail context");
            string oldEngine=game.Profile.SelectedCar.engineAssemblyId;yield return Click(input,"engineSwap");yield return PartDialogClick(input,"engineSwapTarget:"+receiverId);yield return Click(input,"dialogConfirm");Check(game.Profile.SelectedCar.engineAssemblyId==id&&game.Profile.engines.Find(e=>e.instanceId==oldEngine).parentVehicleId==""&&game.Profile.engines.Find(e=>e.instanceId==id).customName=="Spare café build"&&MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null,"One confirmed swap installs the named engine and retains the old assembly in storage");
            yield return Capture("engine-detail-installed");yield return Click(input,"engineBack");Check(shell.Root.Q("engineCard:"+id)!=null,"Named assemblies return to the owned-engine grid");yield return Click(input,"engineBack");Check(!shell.Root.Q("partsPage").ClassListContains("hidden"),"Shared Back returns to Build after the engine grid");
            string cooling=game.Profile.componentsPart(PartCategory.Cooling).definitionId;game.StageNativePurchase(cooling,null,1,false);var stale=game.QuoteNativeCart();game.StageNativePurchase(cooling,null,1,false);before=JsonUtility.ToJson(game.Profile);
            Check(!game.CommitEngineSwap(game.Profile.revision,oldEngine,receiverId,stale)&&before==JsonUtility.ToJson(game.Profile)&&game.CartCount==2,"An edited cart invalidates the captured swap without losing its pending purchases");
            var combined=game.QuoteNativeCart();PlayerProfile rejected;string failure;Check(!MechanicsTransactions.Try(game.Profile,game.Profile.catalog,combined.revision,q=>{PartCart.Checkout(q,combined,DateTime.UtcNow.Ticks);EngineWorkshop.Swap(q,oldEngine,receiverId);},_=>false,out rejected,out failure)&&before==JsonUtility.ToJson(game.Profile),"Combined paid cart and engine-swap save failure leaves the original profile untouched");
            int cash=game.Profile.cash,total=game.NativeCartTotal();yield return Click(input,"partsEngines");yield return Click(input,"engineCard:"+oldEngine);yield return Click(input,"engineSwap");yield return Click(input,"dialogConfirm");
            Check(game.Profile.SelectedCar.engineAssemblyId==oldEngine&&game.Profile.cash==cash-total&&game.CartCount==0&&game.Profile.engines.Find(e=>e.instanceId==id).parentVehicleId=="","Combined confirmation charges the pending basket once, swaps and retains the removed engine");yield return Click(input,"engineBack");yield return Click(input,"engineBack");
        }
        IEnumerator PartEngineClick(WindowsKeyboardProbe input,string id){shell.Root.Q<ScrollView>("engineScroll").ScrollTo(shell.Root.Q(id));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,id);}
#endif
    }
}
