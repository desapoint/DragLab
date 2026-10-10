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
            try {
                fixture.cash = 1000000; fixture.reputation = 1000;
                foreach (var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                    VehicleCollection.Buy(fixture,fixture.catalog,new VehicleOffer { model=vehicle.spec.id,price=vehicle.purchasePrice,reputation=vehicle.reputationRequired,stock=vehicle.CreateStockTune(),paint="#DA3448" });
                var first = fixture.cars[0]; var second = fixture.cars[1];
                var worn = fixture.parts.Find(p => p.instanceId==first.installed.slots.First(s=>s.slot=="Pistons").partInstanceId); worn.condition.health=.25f;
                var failed = fixture.parts.Find(p => p.instanceId==second.installed.slots.First(s=>s.slot=="Brakes").partInstanceId); failed.condition.failed=true; failed.condition.health=0; failed.condition.failureReason="Fixture brake failure";
                AssemblyOperations.Detach(fixture,fixture.cars[2].instanceId); fixture.cars[3].garageJobId="grid-fixture-job";
                fixture.cars[4].fuel.litres=0;
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
                fixture.cars.Clear();shell.InvalidateOwnedGrid();shell.Refresh();yield return Capture("owned-cars-empty");
                Check(shell.Root.Query(className:"owned-car-card").ToList().Count==0 && shell.Root.Q<Label>(className:"car-empty").text=="No owned cars","Empty garage shows no fabricated car");
                Check(File.ReadAllText(ProfileStore.FilePath)==disk,"All grid scenarios leave durable player assets untouched");
            } finally { profileField.SetValue(game,original);shell.InvalidateOwnedGrid();shell.Refresh(); }
        }
#endif
    }
}
