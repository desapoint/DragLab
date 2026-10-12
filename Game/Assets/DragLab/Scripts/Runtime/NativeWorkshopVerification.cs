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
#endif
    }
}
