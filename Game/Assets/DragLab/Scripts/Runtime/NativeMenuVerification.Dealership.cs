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
        VisualElement[] VisibleDealerCards()=>shell.Root.Q("dealerCarGrid").Children().Where(c=>c.ClassListContains("owned-car-card")&&!c.ClassListContains("hidden")).ToArray();
        IEnumerator DealerCardClick(WindowsKeyboardProbe input,string model)
        {shell.Root.Q<ScrollView>("dealerCars").ScrollTo(shell.Root.Q("dealerCar:"+model));yield return new WaitForSecondsRealtime(.1f);yield return Click(input,"dealerCar:"+model);}
        IEnumerator DealerAffordabilityChecks(WindowsKeyboardProbe input,PlayerProfile fixture)
        {
            int cash=fixture.cash,rep=fixture.reputation;
            var prices=game.vehicles.ToDictionary(v=>v.spec.id,v=>PaidEconomy.DealerPrice(fixture.catalog,game.Costs,game.PricingPolicy,game.AuthoredOffer(v.spec.id)));
            int boundary=prices.Values.Min();fixture.cash=boundary;fixture.reputation=0;shell.InvalidateDealer();shell.Refresh();
            string state=JsonUtility.ToJson(fixture);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);int probes=shell.DealerProbeCount;
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterAffordability:Affordable");
            Check(shell.OwnedQuery.Affordability=="All"&&VisibleDealerCards().Length==prices.Count,"Affordability choice remains a draft until Apply");
            Check(shell.Root.Q<Label>("ownedFilterPreview").text==prices.Count(p=>p.Value<=boundary)+" / "+prices.Count+" cars","Affordable draft preview uses actual dealer prices and includes exact cash equality");
            yield return Capture("dealer-affordability-filter");yield return Click(input,"dialogCancel");
            Check(shell.OwnedQuery.Affordability=="All"&&VisibleDealerCards().Length==prices.Count,"Cancel discards the affordability draft");
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterAffordability:Affordable");yield return Click(input,"dialogApply");
            Check(VisibleDealerCards().Select(c=>c.name.Substring("dealerCar:".Length)).OrderBy(s=>s).SequenceEqual(prices.Where(p=>p.Value<=boundary).Select(p=>p.Key).OrderBy(s=>s))&&shell.OwnedQuery.FilterCount==1,"Applied Affordable filter matches the inclusive cash budget and counts as one criterion");
            fixture.cash=boundary-1;shell.InvalidateDealer();shell.Refresh();Check(VisibleDealerCards().Length==0,"Affordable results refresh when cash drops below the cheapest price");
            fixture.cash=boundary;shell.InvalidateDealer();shell.Refresh();
            yield return Click(input,"ownedCarFilters");Check(shell.Root.Q<Button>("ownedFilterAffordability:Affordable").ClassListContains("selected"),"Reopened filters retain the applied affordability choice");
            yield return CarFilterClick(input,"ownedFilterAffordability:Unaffordable");yield return Click(input,"dialogApply");
            Check(VisibleDealerCards().Select(c=>c.name.Substring("dealerCar:".Length)).OrderBy(s=>s).SequenceEqual(prices.Where(p=>p.Value>boundary).Select(p=>p.Key).OrderBy(s=>s)),"Unaffordable is the strictly over-budget complement for valid prices");
            yield return Click(input,"garageOwned");yield return Click(input,"ownedCarFilters");
            Check(shell.OwnedQuery.Affordability=="All"&&shell.Root.Q("ownedFilterAffordability:Affordable")==null,"Owned cars retain independent filters without a purchase-budget control");yield return Click(input,"dialogCancel");
            yield return Click(input,"garageDealer");Check(shell.OwnedQuery.Affordability=="Unaffordable","Switching back retains the dealership affordability filter");
            yield return Click(input,"ownedCarClear");Check(shell.OwnedQuery.Affordability=="All"&&VisibleDealerCards().Length==prices.Count,"Toolbar reset clears affordability with the other criteria");
            fixture.cash=cash;shell.InvalidateDealer();shell.Refresh();
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterAffordability:Affordable");yield return CarFilterClick(input,"ownedFilterStatus:Locked");yield return Click(input,"dialogApply");
            Check(VisibleDealerCards().Length>0&&VisibleDealerCards().Length==game.vehicles.Count(v=>v.reputationRequired>0)&&shell.OwnedQuery.FilterCount==2,"Affordable combines independently with reputation Locked, without treating those models as purchasable");
            yield return Click(input,"ownedCarFilters");yield return Click(input,"dialogReset");
            Check(shell.OwnedQuery.Affordability=="Affordable"&&shell.Root.Q<Button>("ownedFilterAffordability:All").ClassListContains("selected"),"Dialog Reset clears affordability only in the draft");yield return Click(input,"dialogCancel");
            Check(shell.OwnedQuery.Affordability=="Affordable","Cancelling Reset preserves the previous affordability choice");
            yield return Click(input,"ownedCarFilters");yield return Click(input,"dialogReset");yield return Click(input,"dialogApply");
            Check(shell.OwnedQuery.FilterCount==0&&VisibleDealerCards().Length==prices.Count,"Applying Reset clears all dealership filters");
            fixture.cash=boundary;
            Check(JsonUtility.ToJson(fixture)==state&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk)&&shell.DealerProbeCount==probes,"Affordability browsing changes no player assets, saved bytes or cached simulation probes");
            var query=new OwnedCarQuery {Affordability="Affordable"};Check(query.Copy().Affordability=="Affordable"&&!query.Matches(new OwnedCarEntry {Affordable=null}),"Copied queries retain affordability and unknown prices never count as affordable");
            query.Affordability="Unaffordable";Check(!query.Matches(new OwnedCarEntry {Affordable=null}),"Unknown prices never count as unaffordable");
            fixture.cash=cash;fixture.reputation=rep;shell.InvalidateDealer();shell.Refresh();
        }
        IEnumerator DealershipChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);
            byte[] disk=File.ReadAllBytes(ProfileStore.FilePath),backup=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;
            var fixture=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original));fixture.catalog=original.catalog;fixture.cash=1000000;fixture.reputation=1000;
            MechanicsOwnership.Ensure(fixture,fixture.catalog); // Canonicalize empty inline objects introduced by JsonUtility in this isolated copy.
            fixture.vehicleStock.listings.RemoveAll(o=>o.used);
            fixture.parts.First(p=>p.slot=="Pistons").condition.health=.35f;
            Check(MechanicsOwnership.Validate(fixture,fixture.catalog)==null,"Dealership fixture starts with valid retained ownership and economy state");
            string[] oldCars=fixture.cars.Select(JsonUtility.ToJson).ToArray(),oldParts=fixture.parts.Select(JsonUtility.ToJson).ToArray();
            try {
                field.SetValue(game,fixture);game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.3f);if(!shell.Visible)yield return Key(input,0x77);
                shell.InvalidateOwnedGrid();shell.InvalidateDealer();shell.Refresh();
                yield return Click(input,"garageDealer");yield return new WaitForSecondsRealtime(.3f);
                string baseline=JsonUtility.ToJson(fixture);
                Check(VisibleDealerCards().Length==game.vehicles.Length&&shell.Root.Q("ownedCars").ClassListContains("hidden"),"Dealership has one card per authored model and hides the owned grid");
                Check(shell.Root.Q<Button>("garageDealer").ClassListContains("selected")&&shell.Root.Q<Label>("ownedCarCount").text==game.vehicles.Length+" / "+game.vehicles.Length,"Garage grouping and catalog count show the active dealership");
                var cards=VisibleDealerCards();Check(Math.Abs(cards[0].worldBound.y-cards[1].worldBound.y)<1&&cards[1].worldBound.x>cards[0].worldBound.x&&cards[2].worldBound.y>cards[0].worldBound.y,"Dealer models retain the two-per-row rounded accented grid");
                foreach(var vehicle in game.vehicles) {
                    var model=vehicle.spec.id;var price=PaidEconomy.DealerPrice(fixture.catalog,game.Costs,game.PricingPolicy,game.AuthoredOffer(model));
                    Check(shell.Root.Q<Label>("dealerPrice:"+model).text==DisplayUnits.Money(price),"Dealer price uses existing economy and money formatting: "+model);
                    Check(shell.Root.Q("dealerCar:"+model).Q("carPreview")?.Query<Image>().ToList().Count>0,"Factory model has a layered preview: "+model);
                }
                float deadline=Time.realtimeSinceStartup+45;
                while(shell.Root.Q("dealerCarGrid").Query<Label>(className:"car-class").ToList().Any(l=>l.text=="Class …")&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.1f);
                Check(shell.Root.Q("dealerCarGrid").Query<Label>(className:"car-class").ToList().All(l=>l.text!="Class …"),"Copied OEM quarter-mile ratings finish without selecting or saving player cars");
                yield return Capture("dealer-grid");int probes=shell.DealerProbeCount;
                yield return Click(input,"ownedCarSearch");yield return Key(input,0x50);yield return Key(input,0x49);yield return Key(input,0x43);
                yield return Capture("dealer-search");
                Check(VisibleDealerCards().Length==game.vehicles.Count(v=>v.spec.bodyType==CarBodyType.Pickup),"Real keyboard search filters dealership models: "+shell.Root.Q<TextField>("ownedCarSearch").value+" / "+string.Join(",",VisibleDealerCards().Select(c=>c.name)));
                yield return Click(input,"garageOwned");Check(shell.OwnedQuery.Search=="","Switching back restores independent owned-car query state");
                yield return Click(input,"garageDealer");Check(shell.OwnedQuery.Search.ToLowerInvariant()=="pic"&&VisibleDealerCards().Length==game.vehicles.Count(v=>v.spec.bodyType==CarBodyType.Pickup),"Returning to dealership retains its independent search");
                yield return Click(input,"ownedCarClear");yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterType:Coupe");yield return Click(input,"dialogApply");
                Check(VisibleDealerCards().Length==game.vehicles.Count(v=>v.spec.bodyType==CarBodyType.Coupe),"Shared dealership filters use actual model body metadata");
                yield return Click(input,"ownedCarClear");
                yield return DealerAffordabilityChecks(input,fixture);
                string id=game.vehicles[0].spec.id;var offer=game.AuthoredOffer(id);int cost=PaidEconomy.DealerPrice(fixture.catalog,game.Costs,game.PricingPolicy,offer);
                yield return DealerCardClick(input,id);
                Check(shell.DealerDetailsOpen&&!shell.ModalOpen&&shell.Root.Q("dealerCars").ClassListContains("hidden")&&shell.Root.Q<NativeDynoPlot>("dealerDynoPlot")!=null,"Dealer model opens a full details page with stock dyno, not a modal");
                var curve=shell.Root.Q<NativeDynoPlot>("dealerDynoPlot").Series[0].Inspection;
                Check(curve.source.components.All(p=>p.condition.health==1&&!p.condition.failed)&&curve.source.startingFuel.litres==offer.fuel.litres,"OEM preview uses saved new listing parts and included fuel independently: fuel "+curve.source.startingFuel.litres+" / "+offer.fuel.litres+" worn "+string.Join(",",curve.source.components.Where(p=>p.condition.health!=1||p.condition.failed).Select(p=>p.slot+":"+p.condition.health))+"");
                yield return Capture("dealer-details");yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.DealerDetailsOpen,"F8 retains the inspected dealer model");
                yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");Check(shell.DealerDetailsOpen,"Settings Cancel returns to the same dealer details");
                yield return Click(input,"dealerBack");yield return DealerCardClick(input,game.vehicles.First(v=>v.spec.bodyType==CarBodyType.Pickup).spec.id);
                yield return ComparisonChecks(input,true);yield return Click(input,"dealerBack");yield return DealerCardClick(input,id);
                yield return Click(input,"dealerBuy");Check(shell.ModalOpen&&shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy,"Buy opens a shared purchase confirmation: "+shell.Root.Q<Label>("dealerPurchaseError").text);
                Check(shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text==DisplayUnits.Money(fixture.cash))&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text==DisplayUnits.Money((long)fixture.cash-cost)),"Purchase confirmation formats current and resulting cash balances");
                yield return Capture("dealer-confirmation");yield return Click(input,"dialogCancel");
                Check(JsonUtility.ToJson(fixture)==baseline&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk)&&shell.DealerProbeCount==probes,"Browsing, filters, stock inspections and cancelled purchase retain exact live/disk assets and cached probes");
                fixture.cash=0;shell.InvalidateDealer();shell.Refresh();Check(!shell.Root.Q<Button>("dealerBuy").enabledInHierarchy&&shell.Root.Q("dealerContent").Query<Label>().ToList().Any(l=>l.text.Contains("Need ")),"Insufficient funds keep model inspectable but disable Buy with a cash shortfall");
                fixture.cash=1000000;var locked=game.vehicles.First(v=>v.reputationRequired>0);fixture.reputation=0;shell.InvalidateDealer();shell.Refresh();yield return Click(input,"dealerBack");yield return DealerCardClick(input,locked.spec.id);
                Check(!shell.Root.Q<Button>("dealerBuy").enabledInHierarchy&&shell.Root.Q("dealerContent").Query<Label>().ToList().Any(l=>l.text=="Requires REP "+locked.reputationRequired),"Locked model details show actual REP requirement and cannot be bought");
                fixture.reputation=1000;shell.InvalidateDealer();shell.Refresh();yield return Click(input,"dealerBack");yield return DealerCardClick(input,id);
                yield return Click(input,"dealerBuy");fixture.cash--;string changed=JsonUtility.ToJson(fixture);yield return Click(input,"dialogConfirm");
                Check(shell.ModalOpen&&!shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy&&!string.IsNullOrEmpty(shell.Root.Q<Label>("dealerConfirmationError").text)&&JsonUtility.ToJson(game.Profile)==changed,"Changed live balance without revision invalidates confirmation and cannot publish a partial purchase");
                yield return Click(input,"dialogCancel");fixture.cash++;shell.InvalidateDealer();shell.Refresh();
                yield return Click(input,"dealerBuy");var vehicleOffer=VehicleListings.Find(fixture,id);int authored=vehicleOffer.price;
                try{vehicleOffer.price++;yield return Click(input,"dialogConfirm");Check(shell.ModalOpen&&game.Profile.cars.Count==fixture.cars.Count&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Changed saved offer invalidates its exact quote");}finally{vehicleOffer.price=authored;}
                yield return Click(input,"dialogCancel");yield return Click(input,"dealerBuy");
                using(var fileLock=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    yield return Click(input,"dialogConfirm");Check(shell.ModalOpen&&!string.IsNullOrEmpty(shell.Root.Q<Label>("dealerConfirmationError").text)&&JsonUtility.ToJson(game.Profile)==baseline,"Failed durable save rolls back cash, car and every new part; error stays visible");
                }
                yield return Click(input,"dialogCancel");yield return Click(input,"dealerBuy");
                int count=fixture.cars.Count,receipts=fixture.economyReceipts.Count;long revision=fixture.revision;var stale=PaidEconomy.Quote(fixture,fixture.catalog,game.Costs,game.PricingPolicy,new PaymentRequest {kind=PaymentKind.Car,offer=game.AuthoredOffer(id)},DateTime.UtcNow.Ticks);
                yield return Click(input,"dialogConfirm");var bought=game.Profile;
                Check(!shell.ModalOpen&&shell.OwnedDetailsOpen&&bought.cars.Count==count+1&&bought.cash==fixture.cash-cost&&bought.revision==revision+1&&bought.economyReceipts.Count==receipts+1,"Successful confirmation commits one priced purchase and opens the new current owned car");
                Check(bought.SelectedCar.vehicleId==id&&!fixture.cars.Any(c=>c.instanceId==bought.SelectedCar.instanceId)&&MechanicsOwnership.Validate(bought,bought.catalog)==null,"Purchase creates an independent valid car/engine/component identity and selects it");
                Check(oldCars.SequenceEqual(bought.cars.Take(count).Select(JsonUtility.ToJson))&&oldParts.SequenceEqual(bought.parts.Take(fixture.parts.Count).Select(JsonUtility.ToJson)),"Existing car names, tunes, fuel, fluids, parts and wear survive buying unchanged");
                string committed=File.ReadAllText(ProfileStore.FilePath);Check(!game.BuyNativeCar(stale)&&File.ReadAllText(ProfileStore.FilePath)==committed,"Replayed stale purchase cannot charge or grant another car");
                yield return Click(input,"ownedBack");yield return Click(input,"garageDealer");yield return DealerCardClick(input,id);yield return Click(input,"dealerBuy");yield return Click(input,"dialogConfirm");
                Check(game.Profile.cars.Count==count+2&&game.Profile.cars.Where(c=>c.vehicleId==id).Select(c=>c.instanceId).Distinct().Count()==game.Profile.cars.Count(c=>c.vehicleId==id),"A fresh second confirmation buys another distinct copy of the same model");
                Check(shell.Root.Q<Button>("ownedCompareCar").ClassListContains("hidden"),"Currently selected owned car hides the unnecessary Compare button");
                yield return Click(input,"ownedBack");shell.ResetOwnedQuery();string comparedId=game.Profile.cars.First(c=>c.instanceId!=game.Profile.selectedOwnedVehicleId).instanceId;
                shell.Root.Q<ScrollView>("ownedCars").ScrollTo(shell.Root.Q("ownedCar:"+comparedId));yield return new WaitForSecondsRealtime(.1f);yield return Click(input,"ownedCar:"+comparedId);
                yield return ComparisonChecks(input,false);
                yield return Click(input,"ownedBack");yield return Click(input,"garageDealer");
                shell.Root.Q<ScrollView>("dealerCars").scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.2f);
                Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.5f);CarFilterToolbarLayout(1024);yield return Capture("dealer-compact");
                Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);CarFilterToolbarLayout(1920);yield return Capture("dealer-1080p");
                var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);
                Check(shell.Panel.scale==2&&VisibleDealerCards()[0].worldBound.y>shell.Root.Q("ownedCarToolbar").worldBound.yMax,"Dealer grid reflows under grouped navigation on the true 4K render target");
                var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"dealer-4k.png"),image.EncodeToPNG());RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
                Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.4f);
                yield return DealerCardClick(input,id);yield return Key(input,0x1B);Check(!shell.DealerDetailsOpen&&!shell.ModalOpen,"Escape goes back from dealer model details without buying");
                Check(shell.Root.Query<VisualElement>().ToList().All(e=>e.tabIndex==-1),"Dealer controls preserve the approved no-Tab-navigation contract");
            }finally {
                if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                typeof(NativeMenuShell).GetMethod("SwitchGaragePage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{false});
                if(shell.OwnedDetailsOpen)typeof(NativeMenuShell).GetMethod("BackToOwnedCars",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                field.SetValue(game,original);game.settings=original.driving;typeof(PrototypeGame).GetMethod("ShowGarageCar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                File.WriteAllBytes(ProfileStore.FilePath,disk);if(backup!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backup);else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");shell.InvalidateOwnedGrid();shell.InvalidateDealer();shell.Refresh();
            }
        }
#endif
    }
}
