using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Data;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator OwnedCarChecks(WindowsKeyboardProbe input)
        {
            var original = game.Profile;
            var profileField = typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);
            var fixture = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original)); fixture.catalog = original.catalog;
            string disk = File.ReadAllText(ProfileStore.FilePath);
            byte[] diskBytes=File.ReadAllBytes(ProfileStore.FilePath), backupBytes=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;
            try {
                fixture.cash = 1000000; fixture.reputation = 1000;
                foreach (var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                    VehicleCollection.Buy(fixture,fixture.catalog,new VehicleOffer { model=vehicle.spec.id,price=vehicle.purchasePrice,reputation=vehicle.reputationRequired,stock=vehicle.CreateStockTune(),paint="#DA3448" });
                var first = fixture.cars[0]; var second = fixture.cars[1];
                var worn = fixture.parts.Find(p => p.instanceId==first.installed.slots.First(s=>s.slot=="Pistons").partInstanceId); worn.condition.health=.25f;
                var failed = fixture.parts.Find(p => p.instanceId==second.installed.slots.First(s=>s.slot=="Brakes").partInstanceId); failed.condition.failed=true; failed.condition.health=0; failed.condition.failureReason="Fixture brake failure";
                AssemblyOperations.Detach(fixture,fixture.cars[2].instanceId);
                GarageBusiness.Assign(fixture,GarageBusiness.DefaultPolicy(),fixture.cars[3].instanceId,"photo-session",DateTime.UtcNow.Ticks);
                fixture.cars[4].fuel.litres=0;
                Check(MechanicsOwnership.Validate(fixture,fixture.catalog)==null,"Owned-car interaction fixture has valid complete reservation/asset identities");
                profileField.SetValue(game,fixture); game.QueueNativeNavigation("Garage"); yield return new WaitForSecondsRealtime(.4f);
                if (!shell.Visible) yield return Key(input,0x77);
                shell.InvalidateOwnedGrid(); shell.Refresh(); yield return new WaitForSecondsRealtime(.3f);
                string baseline = JsonUtility.ToJson(fixture);
                Check(shell.Root.Query(className:"owned-car-card").ToList().Count==fixture.cars.Count,"Grid shows every owned instance, including duplicate models");
                Check(shell.Root.Query(className:"car-current").ToList().Count==1,"Only the actual current owned instance is marked");
                foreach (var car in fixture.cars) {
                    var card = shell.Root.Q("ownedCar:"+car.instanceId);
                    Check(card!=null && card.Q("carPreview")?.Query<Image>().ToList().Count>0,"Layered preview and exact card ID exist: "+car.instanceId);
                }
                Check(((Label)shell.Root.Q("ownedCar:"+first.instanceId).Q(className:"car-condition")).text.Contains("worn"),"Worn installed component is visible without healing");
                Check(((Label)shell.Root.Q("ownedCar:"+second.instanceId).Q(className:"car-condition")).text.Contains("failed"),"Failed brakes remain a component warning instead of disabling all cars");
                Check(((Label)shell.Root.Q("ownedCar:"+fixture.cars[2].instanceId).Q(className:"car-condition")).text.Contains("Unavailable"),"Missing engine remains visibly unavailable");
                Check(((Label)shell.Root.Q("ownedCar:"+fixture.cars[3].instanceId).Q(className:"car-condition")).text.Contains("Reserved"),"Reserved car remains visible with its restriction");
                Check(((Label)shell.Root.Q("ownedCar:"+fixture.cars[4].instanceId).Q(className:"car-condition")).text.Contains("No fuel"),"Empty tank remains visible without refilling");
                int builds = shell.OwnedGridBuildCount;
                yield return new WaitForSecondsRealtime(.3f); shell.Refresh();
                Check(shell.OwnedGridBuildCount==builds,"Unchanged frames retain cached card geometry and previews");
                float deadline = Time.realtimeSinceStartup+45;
                while (shell.Root.Query<Label>(className:"car-class").ToList().Any(label=>label.text=="Class …") && Time.realtimeSinceStartup<deadline)
                    yield return new WaitForSecondsRealtime(.1f);
                Check(shell.Root.Query<Label>(className:"car-class").ToList().All(label=>label.text!="Class …"),"Shared background inspection finishes class estimates without UI rebuilding");
                Check(shell.OwnedGridBuildCount==builds,"Class results update labels without rebuilding cards");
                yield return Capture("owned-cars-1280");
                var cardRect=shell.Root.Q("ownedCar:"+first.instanceId).worldBound;
                input.MouseButtonAt((int)cardRect.center.x,(int)cardRect.center.y,true); yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)cardRect.center.x,(int)cardRect.center.y,false);
                yield return new WaitForSecondsRealtime(.2f);
                Check(shell.OwnedDetailsOpen && !shell.ModalOpen && shell.Root.Q("ownedCarDetails").tooltip==first.instanceId && shell.Root.Q("ownedCars").ClassListContains("hidden"),"Clicking a card replaces the grid with exact-instance details, without a dialog");
                Check(shell.Root.Q("ownedCarDetails").Q(className:"car-preview")!=null && shell.Root.Q("ownedCarDetails").Q(className:"car-card-stats").childCount==3,"Details reuse layered preview and common HP/torque/weight facts");
                yield return Key(input,0x77);Check(!shell.Visible,"F8 compares the whole Garage details page");
                yield return Key(input,0x77);Check(shell.OwnedDetailsOpen,"Returning from F8 retains inspected car details");
                yield return Key(input,0x50);Check(game.ScreenName=="Garage" && JsonUtility.ToJson(game.Profile)==baseline,"Details block practice shortcut without altering car state");
                yield return Capture("owned-car-details-1280");
                yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
                Check(shell.OwnedDetailsOpen && shell.Root.Q("ownedCarDetails").tooltip==first.instanceId,"Settings Cancel returns to the same inspected car page");
                yield return Click(input,"ownedBack");Check(!shell.OwnedDetailsOpen,"Back returns to the grid without selecting a car");
                yield return Key(input,0x77); yield return Key(input,0x77);
                Check(JsonUtility.ToJson(fixture)==baseline && File.ReadAllText(ProfileStore.FilePath)==disk,"Reading/clicking cards and F8 do not select, save, spend, heal or replace assets");
                foreach (int width in new[] {1024,1920}) {
                    Screen.SetResolution(width,width==1024?768:1080,false); yield return new WaitForSecondsRealtime(.4f);shell.Refresh();yield return new WaitForEndOfFrame();
                    var viewport=shell.Root.Q<ScrollView>("ownedCars").contentViewport.worldBound;
                    var cards=shell.Root.Query(className:"owned-car-card").ToList();
                    Check(cards.All(card=>card.worldBound.xMin>=viewport.xMin-1 && card.worldBound.xMax<=viewport.xMax+1),"Grid reflows without horizontal overflow at "+width);
                    Check(cards.All(card=>card.tabIndex==-1),"Cards do not introduce Tab traversal at "+width);
                    Check(cards.Count(card=>Mathf.Abs(card.worldBound.yMin-cards[0].worldBound.yMin)<1)==2,"Garage keeps exactly two cards per row at "+width);
                    Check(cards.All(card=>card.Q("carSummary").worldBound.xMin>=card.Q("carPreview").worldBound.xMax && card.Q("carSummary").worldBound.xMax<=card.worldBound.xMax && card.Q(className:"car-card-stats").childCount==3),"HP, torque and weight stay to the right of each preview at "+width);
                    yield return Capture("owned-cars-"+width);
                    if(width==1024) {
                        yield return Click(input,"ownedCar:"+first.instanceId);
                        var detail=shell.Root.Q("ownedDetails").worldBound;
                        Check(detail.xMin>=0 && detail.xMax<=shell.Root.worldBound.xMax && shell.Root.Q("ownedUseCar").worldBound.yMax<=shell.Root.worldBound.yMax,"Car detail actions remain reachable at compact resolution");
                        yield return Capture("owned-car-details-1024");
                        yield return Click(input,"ownedBack");
                    }
                }
                var target = new RenderTexture(3840,2160,24); target.Create();
                shell.Panel.targetTexture = target; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                var viewport4k = shell.Root.Q<ScrollView>("ownedCars").contentViewport.worldBound;
                Check(shell.Panel.scale==2 && shell.Root.Query(className:"owned-car-card").ToList().All(card=>card.worldBound.xMax<=viewport4k.xMax+1),"Owned grid fits true 4K target with shared two-times scale");
                var cards4k=shell.Root.Query(className:"owned-car-card").ToList();
                Check(cards4k.Count(card=>Mathf.Abs(card.worldBound.yMin-cards4k[0].worldBound.yMin)<1)==2,"Garage keeps exactly two cards per row at 4K");
                Check(cards4k.All(card=>card.Q("carSummary").worldBound.xMin>=card.Q("carPreview").worldBound.xMax && card.Q("carSummary").worldBound.xMax<=card.worldBound.xMax),"Right-side car summaries fit at 4K");
                var previous = RenderTexture.active; RenderTexture.active=target;
                var image = new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(folder,"owned-cars-4k-target.png"),image.EncodeToPNG());
                RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.3f);
                var scroll=shell.Root.Q<ScrollView>("ownedCars");scroll.scrollOffset=new Vector2(0,100);yield return new WaitForSecondsRealtime(.2f);
                yield return Click(input,"settingsButton"); yield return Click(input,"cancelSettings");
                Check(scroll.scrollOffset.y>50,"Garage scroll position survives Settings and Cancel");
                Check(File.ReadAllText(ProfileStore.FilePath)==disk,"Read-only grid and detail scenarios leave durable player assets untouched");
                yield return Click(input,"ownedCar:"+first.instanceId);
                yield return Click(input,"ownedBack");Check(scroll.scrollOffset.y>50,"Back from car details restores the grid scroll position");
                scroll.scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.2f);
                yield return Click(input,"ownedCar:"+first.instanceId);yield return Click(input,"ownedUseCar");
                yield return Capture("owned-car-use-result");
                Check(!shell.OwnedDetailsOpen && game.Profile.selectedOwnedVehicleId==first.instanceId,"Use this car selects the exact inspected owned instance: "+game.NativeNotice);
                var stored=JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(ProfileStore.FilePath));
                Check(stored.selectedOwnedVehicleId==first.instanceId && game.Profile.revision==fixture.revision+1,"Current-car change is saved through one profile transaction");
                var unchanged=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(game.Profile));unchanged.selectedOwnedVehicleId=fixture.selectedOwnedVehicleId;unchanged.selectedVehicleId=fixture.selectedVehicleId;unchanged.revision=fixture.revision;
                Check(JsonUtility.ToJson(unchanged)==baseline,"Selection preserves all car/part identities, wear, faults, resources, settings and records");
                fixture=game.Profile;
                Check(shell.Root.Query(className:"car-current").ToList().Count==1 && shell.Root.Q("ownedCar:"+first.instanceId).Q(className:"car-current")!=null,"CURRENT marker moves only to the newly selected exact car");
                string saved=File.ReadAllText(ProfileStore.FilePath);
                yield return Click(input,"ownedCar:"+first.instanceId);
                Check(!shell.Root.Q("ownedUseCar").enabledInHierarchy,"Already-current car cannot create a redundant save");
                yield return Key(input,0x1b);Check(!shell.OwnedDetailsOpen && File.ReadAllText(ProfileStore.FilePath)==saved,"Escape returns from current-car details without saving");
                yield return Click(input,"ownedCar:"+second.instanceId);
                long staleRevision=fixture.revision;fixture.revision++;
                Check(!game.SelectNativeCar(second.instanceId,staleRevision) && fixture.selectedOwnedVehicleId==first.instanceId && File.ReadAllText(ProfileStore.FilePath)==saved,"Stale selection command rejects the change and retains the current car/save");
                shell.Refresh();
                using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    yield return Click(input,"ownedUseCar");
                    Check(shell.OwnedDetailsOpen && !string.IsNullOrEmpty(shell.Root.Q<Label>("carSelectionError").text) && game.Profile.selectedOwnedVehicleId==first.instanceId && File.ReadAllText(ProfileStore.FilePath)==saved,"Failed selection save reports an inline error and rolls back the live choice");
                }
                yield return Click(input,"ownedUseCar");
                fixture=game.Profile;
                Check(!shell.OwnedDetailsOpen && fixture.selectedOwnedVehicleId==second.instanceId && fixture.parts.Find(part=>part.instanceId==failed.instanceId).condition.failed,"Retry after save failure selects the car while keeping failed brakes failed");
                yield return Click(input,"ownedCar:"+fixture.cars[2].instanceId);yield return Click(input,"ownedUseCar");fixture=game.Profile;
                Check(!shell.OwnedDetailsOpen && fixture.selectedOwnedVehicleId==fixture.cars[2].instanceId && string.IsNullOrEmpty(fixture.SelectedCar.engineAssemblyId),"Incomplete cars remain selectable for management without recreating an engine");
                yield return Capture("owned-cars-selected");
                saved=File.ReadAllText(ProfileStore.FilePath);
                fixture.cars.Clear();shell.InvalidateOwnedGrid();shell.Refresh();yield return Capture("owned-cars-empty");
                Check(shell.Root.Query(className:"owned-car-card").ToList().Count==0 && shell.Root.Q<Label>(className:"car-empty").text=="No owned cars","Empty garage shows no fabricated car");
                Check(File.ReadAllText(ProfileStore.FilePath)==saved,"Empty-state browsing does not write another save");
            } finally {
                if(shell.OwnedDetailsOpen)typeof(NativeMenuShell).GetMethod("BackToOwnedCars",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                profileField.SetValue(game,original);game.settings=original.driving;
                typeof(PrototypeGame).GetMethod("ShowGarageCar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                File.WriteAllBytes(ProfileStore.FilePath,diskBytes);
                if(backupBytes!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backupBytes);
                else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");
                shell.InvalidateOwnedGrid();shell.Refresh();
            }
        }
#endif
    }
}
