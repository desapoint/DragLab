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
        IEnumerator TireSwapChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var fixture=EngineWorkshop.Copy(original);fixture.cash=1000000;fixture.reputation=1000;var car=fixture.SelectedCar;var pack=fixture.parts.Single(p=>p.instanceId==car.installed.slots.First(s=>s.slot=="Tires").partInstanceId);
            var units=new System.Collections.Generic.List<LooseEngineElement>();
            for(int i=0;i<5;i++){var unit=InventoryUnits.Buy(fixture,car.instanceId,pack.definitionId,"tire");unit.tire.health=i<4?.8f:.4f;unit.tire.consumedLife=1-unit.tire.health;unit.tire.distanceM=1000*(i+1);units.Add(unit);}
            var old=pack.condition.tires.Select(t=>t.Copy()).ToArray();string before=JsonUtility.ToJson(fixture);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            try{
                field.SetValue(game,fixture);game.settings=fixture.driving;game.QueueNativeNavigation("Parts",true);yield return new WaitForSecondsRealtime(.3f);yield return WaitParts();yield return Click(input,"partsTireSwap");
                Check(shell.Root.Query<DropdownField>().ToList().Count==0&&shell.Root.Q("tireSwapTargets").ClassListContains("hidden"),"Stored-tire flow starts with tire selection and has no dropdowns");
                Check(shell.Root.Q("tireSwapStock").Query<Button>().ToList().Count==2&&shell.Root.Q("tireSwapStock:0").Q<Label>(className:"position-wear").text=="× 4","Identical specification and wear appears once with quantity four; different wear stays separate");
                yield return Capture("tire-swap-stock");yield return Click(input,"tireSwapStock:0");
                Check(Enumerable.Range(0,4).All(i=>shell.Root.Q("tireSwapCorner:"+i).Q<RacingSurface>()!=null)&&shell.Root.Q("tireSwapPreset:Front").Q<RacingSurface>()!=null,"Four replacement cards and presets reuse the purchase flow surfaces");
                foreach(int width in new[]{1024,1280}){
                    Screen.SetResolution(width,720,false);yield return new WaitForSecondsRealtime(.4f);DialogScrollBounds("Tire swap "+width);
                    foreach(int corner in Enumerable.Range(0,4)){var row=shell.Root.Q("tireSwapCorner:"+corner);Check(row.worldBound.width>row.parent.worldBound.width*.9,"Replacement card uses the available dialog width / "+width+" / "+corner);}
                    shell.Root.Q<ScrollView>("modalContent").scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.1f);yield return Click(input,"tireSwapPreset:Front");
                    Check(Enumerable.Range(0,4).All(i=>shell.Root.Q("tireSwapCorner:"+i).ClassListContains("selected")==(i<2)),"Front preset selects only the two front slots / "+width);
                    yield return Capture("tire-swap-front-"+width);
                }
                yield return Click(input,"tireSwapPreset:Rear");Check(Enumerable.Range(0,4).All(i=>shell.Root.Q("tireSwapCorner:"+i).ClassListContains("selected")==(i>=2)),"Rear preset selects only the two rear slots");
                yield return Click(input,"tireSwapPreset:All");Check(Enumerable.Range(0,4).All(i=>shell.Root.Q("tireSwapCorner:"+i).ClassListContains("selected")),"All preset respects the four replacement slots");
                yield return Click(input,"tireSwapPreset:Clear");Check(!shell.Root.Q("dialogSwaptires").enabledSelf,"Clear removes all draft replacements and disables confirmation");
                yield return Click(input,"tireSwapBack");yield return Click(input,"tireSwapStock:1");
                Check(new[]{"All","Front","Rear"}.All(p=>!shell.Root.Q("tireSwapPreset:"+p).enabledSelf),"One available tire cannot select multi-tire presets");
                yield return Click(input,"tireSwapCorner:0");Check(shell.Root.Q("tireSwapCorner:0").ClassListContains("selected")&&!shell.Root.Q("tireSwapCorner:1").enabledSelf,"One stored tire fills one slot and cannot be duplicated");
                yield return Click(input,"tireSwapCorner:0");Check(shell.Root.Q("tireSwapCorner:1").enabledSelf&&!shell.Root.Q("dialogSwaptires").enabledSelf,"Deselecting a slot releases its physical tire");
                yield return Click(input,"tireSwapCorner:1");yield return Click(input,"tireSwapBack");yield return Click(input,"tireSwapStock:0");yield return Click(input,"tireSwapCorner:2");
                Check(shell.Root.Q("tireSwapCorner:1").ClassListContains("selected")&&shell.Root.Q("tireSwapCorner:2").ClassListContains("selected"),"Different wear groups can be mixed in one four-slot draft");
                var focus=shell.Root.panel.focusController.focusedElement;yield return Key(input,0x09);Check(ReferenceEquals(focus,shell.Root.panel.focusController.focusedElement),"Tab does not traverse replacement controls");
                yield return Capture("tire-swap-mixed");yield return Click(input,"dialogCancel");
                Check(JsonUtility.ToJson(fixture)==before&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Selection, Back, presets and Cancel leave assets and saved bytes unchanged");
                yield return Click(input,"partsTireSwap");yield return Click(input,"tireSwapStock:0");yield return Click(input,"tireSwapPreset:All");yield return Click(input,"dialogSwaptires");yield return new WaitForSecondsRealtime(.3f);
                Check(!shell.ModalOpen&&game.Profile.SelectedCar.installed.slots.Single(s=>s.slot=="Tires").partInstanceId==pack.instanceId,"Confirmation completes the existing atomic tire swap");
                var fitted=game.Profile.parts.Single(p=>p.instanceId==pack.instanceId).condition.tires;
                Check(fitted.Select(t=>t.id).OrderBy(id=>id).SequenceEqual(units.Take(4).Select(u=>u.element.id).OrderBy(id=>id))&&fitted.Select(t=>t.id).Distinct().Count()==4,"Four distinct inventory identities reach the selected corners");
                Check(old.All(t=>game.Profile.looseEngineElements.Any(u=>u.element.id==t.id&&JsonUtility.ToJson(u.tire)==JsonUtility.ToJson(t))),"Removed tires retain identity, condition and lifetime history in storage");
                Check(game.Profile.cash==fixture.cash&&fitted.All(t=>Math.Abs(t.health-.8f)<.0001)&&game.Profile.looseEngineElements.Any(u=>u.element.id==units[4].element.id),"Swap preserves incoming wear and unselected stock without charging a purchase");
                var loaded=ProfileStore.Load();Check(loaded.parts.Single(p=>p.instanceId==pack.instanceId).condition.tires.Select(t=>t.id).SequenceEqual(fitted.Select(t=>t.id)),"Confirmed tire identities survive database reload");
            }finally{if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(shell,null);field.SetValue(game,original);game.settings=original.driving;game.QueueNativeNavigation("Garage",true);Screen.SetResolution(1280,720,false);}
        }
        IEnumerator NavigationDefaultsChecks(WindowsKeyboardProbe input)
        {
            Check(shell.Visible&&game.ScreenName=="Garage","New Garage is the startup default with an owned car, without pressing F8");
            Check(new[]{"Garage","Settings","Parts","Tuning","Race setup","Business","Stats"}.All(InterfaceComparison.IsReplacement),"All native menu routes initially select the new presentation");
            string initial=JsonUtility.ToJson(game.Profile);byte[] initialDisk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Key(input,0x77);Check(!shell.Visible&&!InterfaceComparison.IsReplacement("Garage"),"F8 switches the default new Garage to the old interface");
            yield return Key(input,0x77);Check(shell.Visible&&InterfaceComparison.IsReplacement("Garage")&&initial==JsonUtility.ToJson(game.Profile)&&initialDisk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"F8 returns to the new interface without changing the save");
            yield return Click(input,"navGarage");
            string state=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            string car=game.Profile.SelectedCar.instanceId;
            yield return Click(input,"ownedCar:"+car);Check(shell.OwnedDetailsOpen,"Owned detail opens normally");
            yield return Click(input,"navBuild");yield return Click(input,"navGarage");
            Check(!shell.OwnedDetailsOpen&&!shell.DealerPageOpen&&!shell.DisposalOpen&&shell.Root.Q("ownedCars").resolvedStyle.display!=DisplayStyle.None,"Garage re-entry opens the owned-car grid");
            yield return Click(input,"garageDealer");yield return Click(input,"navGarage");Check(!shell.DealerPageOpen,"Clicking the current main menu also restores its default tab");
            shell.Root.Q<TextField>("ownedCarSearch").value="no matching cars";yield return Click(input,"navBuild");yield return Click(input,"navGarage");
            Check(shell.OwnedQuery.Search==""&&shell.Root.Q<TextField>("ownedCarSearch").value==""&&shell.Root.Q<ScrollView>("ownedCars").scrollOffset==Vector2.zero,"Garage resets search and scroll");
            yield return Click(input,"navBuild");yield return WaitParts();
            Check(game.PartsPage=="Shop"&&shell.Root.Q<Button>("partsTab0").ClassListContains("selected")&&!shell.CartPageOpen,"Build defaults to the Engine shop grid");
            Check(shell.PartQuery.Compatibility=="Compatible"&&shell.PartQuery.InStockOnly&&shell.PartQuery.Availability=="All","Part defaults combine Compatible and In stock independently of price");
            var definition=shell.PartBrowser.Entries.First().Definition;
            var sample=new NativePartEntry{Definition=definition,Compatibility="Compatible",Source="New",StockUnits=1,RequiresExpress=true,Price=200};
            var query=new NativePartQuery();Check(query.Matches(sample,0),"Partial stock remains visible even when a full set needs express or is unaffordable");
            sample.StockUnits=0;Check(!query.Matches(sample,1000),"Zero-stock new offers are hidden by default");query.InStockOnly=false;Check(query.Matches(sample,1000),"All stock statuses can be requested explicitly");
            query.Availability="Affordable";Check(!query.Matches(sample,199)&&query.Matches(sample,200),"Affordability remains an independent inclusive price filter");
            query.InStockOnly=true;Check(!query.Matches(sample,200),"Affordable does not override In stock");sample.Source="Used";Check(query.Matches(sample,200),"An actual used listing is available stock");sample.Source="Owned";Check(query.Matches(sample,200),"Stock default does not hide owned inventory");
            yield return Click(input,"partsFilters");
            Check(shell.Root.Q("partAdvancedFilters").ClassListContains("hidden"),"Advanced filters start collapsed");
            Check(shell.Root.Q("partAdvancedToggle").worldBound.yMax<=shell.Root.Q<ScrollView>("modalContent").contentViewport.worldBound.yMax,"Common filters and Advanced fit the initial window without scrolling");
            Check(shell.Root.Q("partStockSection").Contains(shell.Root.Q("partStockStatus:In stock"))&&!shell.Root.Q("partStockSection").Contains(shell.Root.Q("partAvailability:Affordable")),"Stock has its own filter section apart from price");
            Check(shell.Root.Q<Button>("partAvailability:In stock")==null&&shell.Root.Q<Button>("partAvailability:Express")==null,"Stock and express are not mixed into the price choices");
            yield return Capture("parts-filters-basic");DialogScrollBounds("basic part filters");
            yield return PartDialogClick(input,"partAvailability:Affordable");yield return PartDialogClick(input,"partStockStatus:All");yield return PartDialogClick(input,"partAdvancedToggle");
            Check(!shell.Root.Q("partAdvancedFilters").ClassListContains("hidden"),"Advanced can be expanded with the shared disclosure button");
            yield return Capture("parts-filters-advanced");yield return PartDialogClick(input,"partTuning:Fixed");yield return Capture("parts-filters-advanced-options");yield return Click(input,"dialogCancel");
            Check(shell.PartQuery.InStockOnly&&shell.PartQuery.Availability=="All"&&shell.PartQuery.Tuning=="All","Cancel discards all basic and advanced edits");
            yield return Click(input,"partsFilters");yield return PartDialogClick(input,"partAvailability:Affordable");yield return PartDialogClick(input,"partStockStatus:In stock");yield return PartDialogClick(input,"partTuning:Fixed");yield return Click(input,"dialogApply");yield return WaitParts();
            Check(shell.PartQuery.InStockOnly&&shell.PartQuery.Availability=="Affordable"&&shell.PartQuery.Tuning=="Fixed","Apply combines stock, price and advanced filters");
            yield return Click(input,"partsFilters");Check(!shell.Root.Q("partAdvancedFilters").ClassListContains("hidden"),"Applied advanced criteria are revealed on reopening");
            yield return Click(input,"dialogReset");Check(shell.Root.Q("partAdvancedFilters").ClassListContains("hidden")&&shell.Root.Q<Button>("partStockStatus:In stock").ClassListContains("selected")&&shell.Root.Q<Button>("partCompatibility:Compatible").ClassListContains("selected"),"Dialog Reset restores Compatible and In stock, with Advanced collapsed");
            yield return Click(input,"dialogApply");yield return WaitParts();Check(shell.PartQuery.InStockOnly&&shell.PartQuery.Availability=="All"&&shell.PartQuery.Tuning=="All","Reset draft applies its defaults explicitly");
            shell.PartQuery.InStockOnly=false;shell.PartQuery.Search="unfindable";yield return Click(input,"partsClear");
            Check(shell.PartQuery.InStockOnly&&shell.PartQuery.Compatibility=="Compatible"&&shell.PartQuery.Search=="","Toolbar clear uses the same defaults");
            yield return Click(input,"partsSource:Inventory");yield return Click(input,"partsTab3");shell.Root.Q<TextField>("partsSearch").value="unfindable";yield return Click(input,"partsEngines");
            yield return Click(input,"navGarage");yield return Click(input,"navBuild");yield return WaitParts();
            Check(game.PartsPage=="Shop"&&shell.Root.Q<Button>("partsTab0").ClassListContains("selected")&&shell.PartQuery.Search==""&&!shell.PartsPlanned&&shell.Root.Q("enginePage").resolvedStyle.display==DisplayStyle.None,"Build re-entry closes the engine workshop and resets source, category, search and reference");
            var card=shell.Root.Q("partsGrid").Children().First(e=>!e.ClassListContains("hidden"));shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.2f);yield return Click(input,card);
            yield return Click(input,"settingsButton");yield return Click(input,"tabSound");yield return Click(input,"cancelSettings");
            Check(game.ScreenName=="Parts"&&shell.Root.Q("partsDetail").resolvedStyle.display!=DisplayStyle.None,"Settings Cancel retains the interrupted detail context");
            yield return Click(input,"settingsButton");Check(shell.Category==0,"Fresh Settings navigation always starts on Difficulty / Driving");yield return Click(input,"tabUnits");yield return Click(input,"settingsButton");Check(shell.Category==0,"Reselecting Settings resets its tab without changing preferences");yield return Click(input,"cancelSettings");
            yield return Click(input,"settingsButton");yield return Click(input,"tabSound");float appliedVolume=SharedAudioSettings.MasterVolume;var volume=shell.Root.Q<Slider>("masterVolume");volume.value=volume.value<.5f?.8f:.2f;yield return Click(input,"settingsButton");
            Check(shell.SettingsDirty&&shell.Category==0&&SharedAudioSettings.MasterVolume==appliedVolume,"Reselecting Settings preserves unapplied edits while resetting its category");yield return Click(input,"navBuild");Check(shell.ModalOpen&&game.ScreenName=="Settings","Main navigation still guards unsaved settings");yield return Click(input,"dialogDiscard");
            Check(game.ScreenName=="Parts"&&shell.Root.Q("partsDetail").resolvedStyle.display==DisplayStyle.None&&SharedAudioSettings.MasterVolume==appliedVolume,"Discard returns to the destination default without applying preferences");
            yield return Click(input,"navBuild");Check(shell.Root.Q("partsDetail").resolvedStyle.display==DisplayStyle.None,"Main Build navigation closes a part detail");
            yield return Click(input,"navTuning");yield return Click(input,"tuningTabWheels");yield return Click(input,"tuningLoadSetup");
            yield return Click(input,"navBuild");yield return Click(input,"navTuning");Check(!shell.SetupLibraryOpen&&shell.Root.Q<Button>("tuningTabEngine").ClassListContains("selected")&&game.WheelTuneAxle==0,"Tuning re-entry closes saved setups and resets category/axle view");
            yield return Click(input,"navRace");yield return Click(input,"raceMode:Records");yield return Click(input,"navGarage");yield return Click(input,"navRace");
            Check(shell.Root.Q<Button>("raceMode:Career").ClassListContains("selected")&&shell.Root.Q<TextField>("raceSearch").value=="","Race re-entry starts on Career with empty event search");
            yield return Click(input,"raceMode:Practice");yield return Click(input,"navRace");Check(shell.Root.Q<Button>("raceMode:Career").ClassListContains("selected"),"Reselecting Race resets its mode too");
            yield return Click(input,"navBuild");yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.PartQuery.InStockOnly,"F8 comparison keeps the current page state");
            Check(state==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Navigation/filtering does not save or alter assets, money, wear, market stock or preferences");
            string stocked=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.StockUnits>0&&e.Compatibility=="Compatible").Definition.id;game.AddToCart(stocked);yield return null;int count=game.CartCount;
            Check(count>0,"Isolated fixture stages a real cart line");yield return Click(input,"partsCart");yield return Click(input,"navBuild");
            Check(!shell.CartPageOpen&&game.CartCount==count,"Main navigation closes cart presentation while preserving reserved purchases");
            yield return Click(input,"navGarage");yield return Click(input,"navBuild");Check(game.CartCount==count,"Reserved cart survives leaving and returning to Build");
            yield return Capture("navigation-build-default");yield return FittedShopChecks(input);game.ClearNativeCart();yield return TireMaintenanceStateChecks(input);
        }
        IEnumerator IncompatibleSaveChecks(WindowsKeyboardProbe input)
        {
            string marker=Path.Combine(folder,"reset-clicked.txt"),beforeReport=Path.Combine(folder,"reset-before.txt");
            if(File.Exists(marker)){
                report.Append(File.ReadAllText(beforeReport,System.Text.Encoding.UTF8));checks+=int.Parse(File.ReadAllText(marker,System.Text.Encoding.UTF8));
                Check(!game.ResetRequired&&!ProfileStore.ResetRequired&&game.Profile.firstPurchasePending&&game.Profile.cars.Count==0,"Explicit reset reloads the scene into the empty starting dealership");
                Check(game.Profile.driving.wearMode==WearMode.Partial&&game.Profile.driving.shifting==ShiftMode.Sequential,"Startup reset preserves separate driving preferences");
                foreach(string suffix in new[]{"",".bak",".reset-generation",".reset-intent"}){string original=File.ReadAllText(Path.Combine(folder,"reset-original"+suffix+".bin"),System.Text.Encoding.UTF8);Check(Directory.GetFiles(folder,"save-slot-1.sqlite3"+suffix+".before-reset-*").Any(file=>File.ReadAllText(file,System.Text.Encoding.UTF8)==original),"Startup reset archives original save-family file / "+suffix);}
                Check(ProfileStore.Load().firstPurchasePending&&!ProfileStore.ResetRequired,"Fresh startup database remains loadable after scene reload");
                yield return Capture("incompatible-save-reset-complete");yield break;
            }
            Check(game.ResetRequired&&shell.ModalOpen&&!shell.Root.Q("navBar").enabledSelf&&shell.Root.Q("modalClose").resolvedStyle.display==DisplayStyle.None,"Incompatible startup uses the blocking themed modal with no close control");
            byte[] originalBytes=File.ReadAllBytes(ProfileStore.FilePath);yield return Key(input,0x1B);yield return Key(input,0x77);
            Check(game.ResetRequired&&shell.ModalOpen&&originalBytes.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Escape and F8 cannot dismiss the reset requirement or rewrite the original save");
            DialogScrollBounds("incompatible save");yield return Capture("incompatible-save-reset-required");
            Check(errors==0,"Blocked startup and reset-modal input produce no runtime errors");
            File.WriteAllText(beforeReport,report.ToString(),new System.Text.UTF8Encoding(false));File.WriteAllText(marker,checks.ToString(),new System.Text.UTF8Encoding(false));
            yield return Click(input,"dialogResetandstartnewgame");
            yield return new WaitForSecondsRealtime(5);throw new InvalidOperationException("Reset did not reload the scene.");
        }
        IEnumerator TireMaintenanceStateChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var profileField=typeof(PrototypeGame).GetField("profile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            try{
                foreach(double life in new[]{.8,1.0}){
                    var copy=EngineWorkshop.Copy(original);var car=copy.SelectedCar;var binding=car.installed.slots.First(s=>s.slot=="Tires");var tire=copy.parts.First(p=>p.instanceId==binding.partInstanceId).condition.tires[0];tire.health=1;tire.consumedLife=life;tire.failed=false;profileField.SetValue(game,copy);game.settings=copy.driving;
                    yield return Click(input,"navGarage");yield return Click(input,"ownedCar:"+car.instanceId);var summary=shell.Root.Q<Label>("maintenanceCondition");
                    Check(summary.ClassListContains(life<1?"car-warning":"car-critical")&&summary.text.Contains(life<1?"0 failed · 1 need service":"1 failed · 0 need service"),"Maintenance summary uses actual remaining tire life and matching severity / "+life);
                    if(life>=1)yield return Capture("maintenance-exhausted-tire");
                }
            }finally{profileField.SetValue(game,original);game.settings=original.driving;shell.InvalidateOwnedGrid();}
            yield return Click(input,"navBuild");Check(disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Temporary tire-maintenance inspection fixtures do not save or change player assets");
        }
        IEnumerator FittedShopChecks(WindowsKeyboardProbe input)
        {
            string before=JsonUtility.ToJson(game.Profile),selected=game.Profile.SelectedCar.instanceId;byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);int cartCount=game.CartCount;
            var offer=game.Profile.vehicleStock.listings.First(VehicleInspection.Known);
            var samples=offer.parts.GroupBy(p=>NativePartBrowser.Section(game.Profile.catalog.Find(p.definitionId).category)).Select(g=>g.First()).ToArray();
            foreach(var part in samples){
                var category=game.Profile.catalog.Find(part.definitionId).category;var section=NativePartBrowser.Section(category);string group=NativeMenuShell.FittedPartGroup(category);
                shell.PartQuery.Search="old search";shell.PartQuery.Availability="Affordable";shell.PartQuery.InStockOnly=false;shell.PartQuery.Sort="Price";shell.PartQuery.Descending=true;
                yield return Click(input,"navGarage");yield return Click(input,"garageDealer");yield return DealerCardClick(input,offer.listingId);
                var scroll=shell.Root.Q<ScrollView>("dealerDetailScroll");scroll.ScrollTo(shell.Root.Q("dealerFittedParts"));yield return new WaitForSecondsRealtime(.15f);
                yield return Click(input,"dealerFittedParts");yield return PartDialogClick(input,"fittedPartsGroup:"+group);
                var shop=shell.Root.Q<Button>("fittedPartShop:"+part.instanceId);shell.Root.Q<ScrollView>("modalContent").ScrollTo(shop);yield return new WaitForSecondsRealtime(.15f);
                Check(shop.worldBound.width>=88&&shop.worldBound.xMax<=shop.parent.worldBound.xMax+1&&shop.worldBound.xMin>=shop.parent.Q<Label>().worldBound.xMax,"Fitted Shop button fits beside its component type: "+category);
                yield return Capture("fitted-parts-shop-"+section);yield return Click(input,shop);
                yield return WaitParts();
                Check(game.ScreenName=="Parts"&&!shell.ModalOpen&&game.PartsPage=="Shop"&&shell.Root.Q<Button>("partsTab"+(int)section).ClassListContains("selected"),"Fitted Shop navigates to the matching Build section: "+category);
                var query=shell.PartQuery;
                Check(query.Compatibility=="Compatible"&&query.InStockOnly&&query.Availability=="All"&&query.Condition=="All"&&query.Search==""&&query.Sort=="Name"&&!query.Descending&&!shell.PartsPlanned&&query.Categories.SetEquals(new[]{category})&&query.Brands.Count==0&&query.BrandFocus=="All"&&query.Tuning=="All"&&query.Direction.All(d=>d=="Any")&&query.Minimum.All(m=>!m.HasValue),"Fitted Shop uses default filters plus only the clicked part type: "+category);
                var ids=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList().Where(c=>!c.ClassListContains("hidden")).Select(c=>c.name.Substring("partCard:".Length)).ToHashSet();
                Check(shell.PartBrowser.Entries.Where(e=>ids.Contains(e.Id)).All(e=>e.Definition.category==category&&e.Compatibility=="Compatible"&&(e.Source!="New"||e.StockUnits>0)),"Only fitting, stocked parts of that type are displayed: "+category);
                Check(game.Profile.SelectedCar.instanceId==selected&&game.CartCount==cartCount,"Shop shortcut preserves current car and reserved purchases: "+category);
            }
            Check(samples.Length>=4,"Fitted Shop exercises multiple component systems / sections");
            yield return Click(input,"partsFilters");Check(shell.Root.Q("partAdvancedFilters").ClassListContains("hidden")==false,"The selected part type is visible in Advanced on opening filters");yield return Capture("fitted-shop-filter");yield return Click(input,"dialogCancel");
            yield return Click(input,"navBuild");Check(shell.PartQuery.Categories.Count==0&&shell.PartQuery.InStockOnly&&shell.PartQuery.Compatibility=="Compatible","Ordinary main navigation clears the contextual part-type filter");
            Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Fitted Shop does not buy, install, switch cars, inspect, reroll stock or save");
        }
#endif
    }
}
