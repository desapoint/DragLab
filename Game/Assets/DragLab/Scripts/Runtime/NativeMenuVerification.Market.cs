using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime {
 internal sealed partial class NativeMenuVerification {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
 IEnumerator StarterPurchaseChecks(WindowsKeyboardProbe input){
 yield return new WaitForSecondsRealtime(.4f);
 Check(shell.Visible&&game.ScreenName=="Garage"&&game.Profile.cars.Count==0&&VisibleDealerCards().Length==3,"Fresh game opens fixed three-choice used dealership without a gifted car");
 string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
 yield return Capture("starter-selection");yield return Key(input,0x77);Check(!shell.Visible&&game.Profile.cars.Count==0,"F8 exposes legacy empty-garage dealership without gifting assets");yield return Key(input,0x77);Check(shell.Visible&&VisibleDealerCards().Length==3,"F8 restores fixed starter selection");
 yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"dealerCondition:New");
 Check(VisibleDealerCards().Length==3,"New/used choice stays a draft");yield return Click(input,"dialogCancel");
 yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"dealerCondition:New");yield return Click(input,"dialogApply");
 Check(VisibleDealerCards().Length==0,"New condition filter excludes fixed used starters");yield return Click(input,"ownedCarClear");
 var listing=VehicleListings.Find(game.Profile,"starter:starter-hatch");
 yield return DealerCardClick(input,listing.listingId);
 var dyno=shell.Root.Q<NativeDynoPlot>("dealerDynoPlot");
 Check(dyno.Series.Count==2&&dyno.Series[0].Inspection.source.components.Any(p=>p.condition.health<1),"Used details show actual worn and no-wear dyno curves");
 shell.Root.Q<UnityEngine.UIElements.ScrollView>("dealerDetailScroll").ScrollTo(shell.Root.Q("dealerFittedParts"));yield return new WaitForSecondsRealtime(.1f);yield return Click(input,"dealerFittedParts");Check(shell.ModalOpen,"Actual fitted hardware opens reusable information dialog");yield return Click(input,"dialogClose");
 yield return Click(input,"dealerBuy");yield return Click(input,"dialogCancel");
 Check(JsonUtility.ToJson(game.Profile)==before&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Filtering, inspecting and cancelling first purchase leave saved assets unchanged");
 yield return Click(input,"dealerBuy");yield return Click(input,"dialogConfirm");
 Check(!shell.ModalOpen&&shell.OwnedDetailsOpen&&!game.Profile.firstPurchasePending&&game.Profile.cars.Count==1&&game.Profile.cash==4700-listing.price,"First confirmation buys one starter and leaves a maintenance reserve");
 Check(listing.parts.All(p=>game.Profile.parts.Any(q=>q.instanceId==p.instanceId&&q.condition.health==p.condition.health)),"First purchase transfers exact seller identities and wear");
 Check(MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null&&game.Profile.vehicleStock.listings.Count==20,"First purchase unlocks persisted mixed stock with valid ownership");
 yield return Capture("starter-owned");yield return Click(input,"ownedBack");
 }
#endif
 }
}
