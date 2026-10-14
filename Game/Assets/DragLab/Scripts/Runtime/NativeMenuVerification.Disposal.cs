using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Infrastructure;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator DisposalChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);yield return Click(input,"navGarage");yield return new WaitForSecondsRealtime(.3f);
            string id=game.Profile.SelectedCar.instanceId;yield return Click(input,"ownedCar:"+id);
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"ownedSellSalvage");Check(shell.DisposalOpen&&!shell.ModalOpen&&shell.Root.Q("ownedDetails").ClassListContains("hidden"),"Sell / Salvage opens a dedicated page from exact owned-car details");
            Check(shell.Root.Q("disposalBack").ClassListContains("car-back-button")&&shell.Root.Q("disposalBack").worldBound.width==40,"Sale page uses the original compact Back control");
            Check(shell.Root.Q<Label>("disposalCarName").text==shell.Root.Q<Label>("ownedDetailTitle").text,"Sale page identifies the actual named owned car");var quote=InventoryPricing.Sell(game.Profile,ValuationKind.Car,id,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks);
            Check(shell.Root.Q<Label>("disposalPrice").text==DisplayUnits.Money(quote.Total),"Sale price matches authoritative condition/history/assembly valuation");
            Check(game.Profile.parts.Where(p=>quote.IncludedIds.Contains(p.instanceId)).All(p=>shell.Root.Q("disposalPart:"+p.instanceId)!=null),"Grouped contents expose every actual included pack and its condition");
            var keep=shell.Root.Query<Toggle>().ToList().First(t=>t.name.StartsWith("disposalKeep:"));yield return Click(input,keep);string keptId=keep.name.Substring("disposalKeep:".Length);
            var retained=InventoryPricing.Sell(game.Profile,ValuationKind.Car,id,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks,new[]{keptId});Check(shell.Root.Q<Label>("disposalPrice").text==DisplayUnits.Money(retained.Total)&&keep.value,"Keeping a fitted set reprices the real quote without removing or replacing it");yield return Click(input,keep);
            yield return Click(input,"disposalMode:Keep engine");var chassis=InventoryPricing.Sell(game.Profile,ValuationKind.Chassis,game.Profile.SelectedCar.chassisId,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks);Check(shell.Root.Q<Label>("disposalPrice").text==DisplayUnits.Money(chassis.Total)&&shell.Root.Q<Label>("disposalSelection").text.Contains("liquids retained"),"Keep engine quotes the chassis and explicitly retains the attached engine/liquids / "+shell.Root.Q<Label>("disposalPrice").text+" expected "+DisplayUnits.Money(chassis.Total)+" / "+shell.Root.Q<Label>("disposalSelection").text+" / "+shell.Root.Q<Label>("disposalError").text);yield return Capture("disposal-keep-engine");yield return Click(input,"disposalMode:Sell car");
            yield return Capture("disposal-page");yield return Click(input,"disposalPriceInfo");Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Price details","Price explanation uses the shared scrollable dialog");yield return Click(input,"modalClose");
            yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.DisposalOpen,"F8 keeps the same sale page and asset");
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");Check(shell.DisposalOpen,"Clean Settings Cancel returns to the sale page");
            foreach(int width in new[]{1024,1920}){Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.3f);Check(shell.Root.Q("disposalScroll").worldBound.yMax<shell.Root.Q("footerBar").worldBound.yMin&&shell.Root.Q("disposalBack").worldBound.xMin>=0,"Sale content and Back fit "+width);yield return Capture("disposal-"+width);}
            var target4k=new RenderTexture(3840,2160,24);target4k.Create();shell.Panel.targetTexture=target4k;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2&&shell.Root.Q("disposalReview").worldBound.yMax<shell.Root.Q("footerBar").worldBound.yMin,"Sale actions remain reachable at crisp 4K scale");var active=RenderTexture.active;RenderTexture.active=target4k;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"disposal-4k.png"),image.EncodeToPNG());RenderTexture.active=active;shell.Panel.targetTexture=null;target4k.Release();Destroy(target4k);Destroy(image);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Click(input,"disposalBack");Check(!shell.DisposalOpen&&shell.OwnedDetailsOpen,"Back returns to the same owned-car details");
            Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Sale page navigation, Settings and F8 never spend, dispose, heal or save");
            yield return Click(input,"ownedSellSalvage");yield return Click(input,"disposalMode:Keep engine");
            var brake=game.Profile.parts.First(p=>game.Profile.SelectedCar.installed.slots.Any(slot=>slot.partInstanceId==p.instanceId)&&game.Profile.catalog.Find(p.definitionId).category==PartCategory.Brakes);var target=shell.Root.Q("disposalKeep:"+brake.instanceId);shell.Root.Q<ScrollView>("disposalScroll").ScrollTo(target);yield return new WaitForSecondsRealtime(.2f);yield return Click(input,target);
            var sale=InventoryPricing.Sell(game.Profile,ValuationKind.Chassis,game.Profile.SelectedCar.chassisId,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks,new[]{brake.instanceId});string engine=game.Profile.SelectedCar.engineAssemblyId;string fluids=JsonUtility.ToJson(game.Profile.SelectedCar.fluids);string condition=JsonUtility.ToJson(brake.condition);int cash=game.Profile.cash;
            var expired=InventoryPricing.Sell(game.Profile,ValuationKind.Car,id,game.PricingPolicy,"enthusiast",DateTime.UtcNow.AddHours(-1).Ticks);Check(!game.CommitNativeDisposal(id,expired)&&before==JsonUtility.ToJson(game.Profile),"Expired sale cannot dispose or credit anything");
            yield return Click(input,"disposalReview");Check(shell.ModalOpen&&shell.Root.Q<Button>("dialogSell")!=null,"Destructive sale opens a separate explicit Sell confirmation");yield return Capture("disposal-confirm");yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Cancel retains all car identities, fluids, stock and cash");
            yield return Click(input,"disposalReview");using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"dialogSell");Check(shell.ModalOpen&&shell.Root.Q<Label>("disposalCommitError").text.Length>0&&before==JsonUtility.ToJson(game.Profile),"Failed sale persistence publishes no money or ownership change and retains review");}
            yield return Click(input,"dialogSell");Check(!shell.ModalOpen&&!game.Profile.cars.Any(c=>c.instanceId==id)&&game.Profile.cash==cash+sale.Total,"Confirmed chassis sale disposes once and credits exactly its quote");
            var keptEngine=game.Profile.engines.Single(e=>e.instanceId==engine);Check(string.IsNullOrEmpty(keptEngine.parentVehicleId)&&JsonUtility.ToJson(keptEngine.fluids)==fluids&&game.Profile.parts.Any(p=>p.instanceId==brake.instanceId&&p.location==PartLocation.Inventory&&JsonUtility.ToJson(p.condition)==condition),"Stored engine/liquids and explicitly retained worn brakes preserve exact identities and condition");
            var reloaded=ProfileStore.Load();Check(reloaded.cash==game.Profile.cash&&reloaded.engines.Any(e=>e.instanceId==engine)&&MechanicsOwnership.Validate(reloaded,reloaded.catalog)==null,"Sale ownership and money survive a real SQLite reload");int after=game.Profile.cash;Check(!game.CommitNativeDisposal(id,sale)&&game.Profile.cash==after,"Replaying the same sale cannot pay twice");Check(shell.Root.Q("emptyGarageRecovery")!=null&&shell.Root.Q("garageDealer").enabledInHierarchy,"Last-car disposal leaves dealership and recovery navigation available");yield return Capture("disposal-after");
            var model=game.vehicles[0];Check(MechanicsTransactions.Try(game.Profile,game.Profile.catalog,game.Profile.revision,p=>VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=model.spec.id,price=1,reputation=0,stock=model.CreateStockTune(),paint="#FFFFFF"}),ProfileStore.TrySave,out var full,out var failure),"Isolated whole-car sale fixture is durably created / "+failure);typeof(PrototypeGame).GetField("profile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game,full);game.ReturnToGarage();shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.3f);
            string whole=full.SelectedCar.instanceId,wholeEngine=full.SelectedCar.engineAssemblyId;var wholeQuote=InventoryPricing.Sell(full,ValuationKind.Car,whole,game.PricingPolicy,"enthusiast",DateTime.UtcNow.Ticks);int wholeCash=full.cash;Check(game.CommitNativeDisposal(whole,wholeQuote)&&game.Profile.cash==wholeCash+wholeQuote.Total&&!game.Profile.engines.Any(e=>e.instanceId==wholeEngine)&&game.Profile.engines.Any(e=>e.instanceId==engine),"Whole-car sale removes only its captured engine tree and leaves the previously retained engine intact");Check(MechanicsOwnership.Validate(ProfileStore.Load(),game.Profile.catalog)==null,"Both sale modes leave a valid durable ownership ledger");
        }
#endif
    }
}
