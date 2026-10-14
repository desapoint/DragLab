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
            yield return Capture("disposal-page");yield return Click(input,"disposalPriceInfo");Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Price details","Price explanation uses the shared scrollable dialog");yield return Click(input,"modalClose");
            yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.DisposalOpen,"F8 keeps the same sale page and asset");
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");Check(shell.DisposalOpen,"Clean Settings Cancel returns to the sale page");
            foreach(int width in new[]{1024,1920}){Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.3f);Check(shell.Root.Q("disposalScroll").worldBound.yMax<shell.Root.Q("footerBar").worldBound.yMin&&shell.Root.Q("disposalBack").worldBound.xMin>=0,"Sale content and Back fit "+width);yield return Capture("disposal-"+width);}
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Click(input,"disposalBack");Check(!shell.DisposalOpen&&shell.OwnedDetailsOpen,"Back returns to the same owned-car details");
            Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Sale page navigation, Settings and F8 never spend, dispose, heal or save");
        }
#endif
    }
}
