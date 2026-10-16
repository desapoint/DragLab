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
            var fixture = DragLab.Domain.Mechanics.EngineWorkshop.Copy(original); fixture.catalog = original.catalog;
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
                GarageBusiness.Assign(fixture,GarageBusiness.DefaultPolicy(),fixture.cars[3].instanceId,"photo-session",DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                fixture.cars[4].fuel.litres=0;
                Check(MechanicsOwnership.Validate(fixture,fixture.catalog)==null,"Owned-car interaction fixture has valid complete reservation/asset identities");
                profileField.SetValue(game,fixture); game.QueueNativeNavigation("Garage"); yield return new WaitForSecondsRealtime(.4f);
                if (!shell.Visible) yield return Key(input,0x77);
                shell.InvalidateOwnedGrid(); shell.Refresh(); yield return new WaitForSecondsRealtime(.3f);
                string baseline = JsonUtility.ToJson(fixture);
                Check(shell.Root.Q("ownedCarGrid").Query(className:"owned-car-card").ToList().Count==fixture.cars.Count,"Grid shows every owned instance, including duplicate models");
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
                yield return CarFilterChecks(input);
                CarFilterToolbarLayout(1280);yield return Capture("owned-cars-1280");
                var cardRect=shell.Root.Q("ownedCar:"+first.instanceId).worldBound;
                input.MouseButtonAt((int)cardRect.center.x,(int)cardRect.center.y,true); yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)cardRect.center.x,(int)cardRect.center.y,false);
                yield return new WaitForSecondsRealtime(.2f);
                Check(shell.OwnedDetailsOpen && !shell.ModalOpen && shell.Root.Q("ownedCarDetails").tooltip==first.instanceId && shell.Root.Q("ownedCars").ClassListContains("hidden"),"Clicking a card replaces the grid with exact-instance details, without a dialog");
                Check(shell.Root.Q("ownedCarDetails").Q(className:"car-preview")!=null && shell.Root.Q("ownedCarDetails").Q(className:"car-card-stats").childCount==3,"Details reuse layered preview and common HP/torque/weight facts");
                var dyno=shell.Root.Q<NativeDynoPlot>("ownedDynoPlot");
                var expectedSource=InspectionBuild.Capture(fixture,first);var expectedDyno=CarInspection.CalculatePrepared(expectedSource,CarInspection.PrepareCar(expectedSource),false);
                Check(dyno!=null&&dyno.Series[0].Inspection.curve.Count==expectedDyno.curve.Count&&Math.Abs(dyno.Series[0].Inspection.peakHp-expectedDyno.peakHp)<.001&&Math.Abs(dyno.Series[0].Inspection.peakTorque-expectedDyno.peakTorque)<.001,"Details dyno resolves the inspected applied build through authoritative inspection");
                Check(dyno.Series[1].Inspection!=null&&dyno.Series[1].Inspection.peakHp>dyno.Series[0].Inspection.peakHp&&worn.condition.health==.25f,"Details dyno shows reduced worn output and a detached no-wear reference without healing");
                Check(shell.Root.Q("ownedCarDyno").worldBound.xMin>=shell.Root.Q("ownedCarDetails").Q(className:"car-summary").worldBound.xMax&&shell.Root.Q("ownedCarDyno").worldBound.xMax<=shell.Root.Q("ownedDetailsTopRow").worldBound.xMax+1,"Dyno fits to the right of preview/stats in the first details row");
                shell.Refresh();Check(ReferenceEquals(dyno,shell.Root.Q<NativeDynoPlot>("ownedDynoPlot")),"Unchanged details frames retain the computed dyno without running another simulation");
                Check(shell.Root.Q("dynoCurrentLegend").Query<Label>().ToList().First().text=="Current"&&shell.Root.Q("dynoNoWearLegend").Query<Label>().ToList().First().text=="No wear"&&shell.Root.Q("dynoNoWearLegend").Query<Label>().ToList().Last().text.Contains(DisplayUnits.Torque(dyno.Series[1].Inspection.peakTorque,dyno.Units).ToString("F0")),"Dyno legend has separate neutral Current/No wear groups with peak figures");
                var detailBar=shell.Root.Q<ScrollView>("ownedDetailScroll").verticalScroller;
                Check(Mathf.Abs(shell.Root.Q(className:"car-details-frame").worldBound.xMax-detailBar.worldBound.xMax-10)<1&&detailBar.ClassListContains("settings-scrollbar"),"Details scrollbar shares Settings styling and its ten-point right inset");
                var plot=dyno.PlotRect;var point=dyno.LocalToWorld(new Vector2(plot.center.x,plot.yMin+plot.height*.2f));
                input.MoveMouseAt((int)point.x,(int)point.y);yield return new WaitForSecondsRealtime(.2f);
                double middleRpm=expectedSource.build.spec.redlineRpm*.5;var midpoint=expectedDyno.curve.OrderBy(p=>Math.Abs(p.rpm-middleRpm)).First();
                Check(dyno.HoverRpm.HasValue&&Math.Abs(dyno.HoverRpm.Value-middleRpm)<30&&dyno.HoverSamples[0].HasValue&&Math.Abs(dyno.HoverSamples[0].Value.hp-midpoint.hp)<2&&Math.Abs(dyno.HoverSamples[0].Value.torque-midpoint.torque)<2&&dyno.HoverSamples[1].HasValue,"Real pointer hover interpolates current/no-wear figures at the cursor RPM");
                var readout=shell.Root.Q("dynoHoverValues");Check(!readout.ClassListContains("hidden")&&readout.worldBound.xMin>=dyno.worldBound.xMin&&readout.worldBound.xMax<=dyno.worldBound.xMax+1,"Hover figures are visible and clamped inside the dyno sheet");
                yield return Capture("owned-car-dyno-hover");
                point=dyno.LocalToWorld(new Vector2(plot.xMin+2,plot.center.y));input.MoveMouseAt((int)point.x,(int)point.y);yield return new WaitForSecondsRealtime(.1f);
                Check(dyno.HoverRpm.HasValue&&!dyno.HoverSamples[0].HasValue&&!dyno.HoverSamples[1].HasValue,"Hover below the first curve sample does not fabricate idle/output figures");
                input.MoveMouseAt(10,100);yield return new WaitForSecondsRealtime(.1f);Check(!dyno.HoverRpm.HasValue&&readout.ClassListContains("hidden"),"Leaving the plot removes cursor line, intersections and readout");
                yield return EngineStatsChecks(input,dyno);
                // Exercise reuse as a different caller, with no profile or Garage dependency in the component.
                var otherSource=fixture.cars.Where(car=>!string.IsNullOrEmpty(car.engineAssemblyId)).Select(car=>InspectionBuild.Capture(fixture,car)).OrderByDescending(source=>source.build.spec.redlineRpm).First();
                var otherStats=CarInspection.CalculatePrepared(otherSource,CarInspection.PrepareCar(otherSource),false);
                var series=new[] {
                    new NativeDynoSeries("Applied","Applied",dyno.Series[0].Inspection,NativeDynoStyle.Primary),
                    new NativeDynoSeries("Draft","Draft",otherStats,NativeDynoStyle.Alternative),
                    new NativeDynoSeries("Baseline","Applied without wear",dyno.Series[1].Inspection,NativeDynoStyle.Reference)
                };
                var reused=new NativeDynoPanel(series,TorqueUnit.PoundFeet,"comparisonDynoPlot");
                series[0]=series[1];
                Check(reused.Plot.Series.Count==3&&reused.Plot.Series[0].Id=="Applied","Shared dyno captures independent labelled series instead of retaining the caller's mutable list");
                var originalChart=shell.Root.Q("ownedCarDyno");var chartParent=originalChart.parent;int chartIndex=chartParent.IndexOf(originalChart);
                originalChart.RemoveFromHierarchy();reused.AddToClassList("car-dyno");chartParent.Insert(chartIndex,reused);yield return new WaitForSecondsRealtime(.2f);
                Check(reused.Q("dynoDraftLegend").Query<Label>().ToList().First().text=="Draft"&&reused.Q("dynoBaselineLegend").Query<Label>().ToList().First().text=="Applied without wear","Shared legend labels belong to the supplied comparison, without hardcoded Current/No wear headings");
                Check(reused.Plot.RpmLimit>dyno.RpmLimit&&Math.Abs(reused.Plot.RpmLimit-reused.Plot.Series.Max(item=>item.Inspection.source.build.spec.redlineRpm))<.001&&Math.Abs(reused.Plot.HpScale-reused.Plot.Series.Max(item=>item.Inspection.peakHp)*1.08)<.001&&Math.Abs(reused.Plot.TorqueScale-reused.Plot.Series.Max(item=>item.Inspection.peakTorque)*1.08)<.001,"Comparison axes include every supplied build rather than clipping to the first build's limits");
                Check(reused.Q("dynoDraftLegend").Query<Label>().ToList().Last().text.Contains("lb-ft")&&reused.Plot.Units==TorqueUnit.PoundFeet,"Shared comparison converts displayed torque units while retaining Nm inspections");
                var reusePoint=reused.Plot.LocalToWorld(reused.Plot.PlotRect.center);input.MoveMouseAt((int)reusePoint.x,(int)reusePoint.y);yield return new WaitForSecondsRealtime(.2f);
                Check(reused.Plot.HoverRpm.HasValue&&reused.Plot.HoverSamples.All(sample=>sample.HasValue)&&reused.Q<Label>("dynoHoverDraft").text.StartsWith("Draft  ")&&reused.Q<Label>("dynoHoverBaseline").text.StartsWith("Applied without wear  "),"Real pointer inspection synchronizes three labelled builds at one RPM");
                Check(NativeDynoPlot.CurveColor(reused.Plot,true,NativeDynoStyle.Alternative)!=NativeDynoPlot.CurveColor(reused.Plot,true,NativeDynoStyle.Primary)&&NativeDynoPlot.CurveColor(reused.Plot,true,NativeDynoStyle.Alternative)!=NativeDynoPlot.CurveColor(reused.Plot,true,NativeDynoStyle.Reference),"Optional proposed curve has a distinct shared palette and dash pattern");
                Check(!reused.Plot.focusable&&reused.Plot.tabIndex==-1&&JsonUtility.ToJson(fixture)==baseline&&File.ReadAllText(ProfileStore.FilePath)==disk,"Shared comparison rendering does not focus, mutate, heal, refill or save player assets");
                yield return Capture("shared-dyno-three-series");
                input.MoveMouseAt(10,100);yield return new WaitForSecondsRealtime(.1f);
                Check(!reused.Plot.HoverRpm.HasValue&&reused.Plot.HoverSamples.All(sample=>!sample.HasValue),"Shared comparison clears all series on pointer leave");
                reused.RemoveFromHierarchy();chartParent.Insert(chartIndex,originalChart);yield return new WaitForSecondsRealtime(.1f);
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
                    var cards=shell.Root.Q("ownedCarGrid").Query(className:"owned-car-card").ToList();
                    Check(cards.All(card=>card.worldBound.xMin>=viewport.xMin-1 && card.worldBound.xMax<=viewport.xMax+1),"Grid reflows without horizontal overflow at "+width);
                    Check(cards.All(card=>card.tabIndex==-1),"Cards do not introduce Tab traversal at "+width);
                    Check(cards.Count(card=>Mathf.Abs(card.worldBound.yMin-cards[0].worldBound.yMin)<1)==2,"Garage keeps exactly two cards per row at "+width);
                    Check(cards.All(card=>card.Q("carSummary").worldBound.xMin>=card.Q("carPreview").worldBound.xMax && card.Q("carSummary").worldBound.xMax<=card.worldBound.xMax && card.Q(className:"car-card-stats").childCount==3),"HP, torque and weight stay to the right of each preview at "+width);
                    CarFilterToolbarLayout(width);yield return Capture("owned-cars-"+width);
                    {
                        yield return Click(input,"ownedCar:"+first.instanceId);
                        var detail=shell.Root.Q("ownedDetails").worldBound;
                        Check(detail.xMin>=0 && detail.xMax<=shell.Root.worldBound.xMax && shell.Root.Q("ownedUseCar").worldBound.yMax<=shell.Root.worldBound.yMax,"Car detail actions remain reachable at compact resolution");
                        var chart=shell.Root.Q("ownedCarDyno");
                        Check(chart.worldBound.width>250&&chart.worldBound.xMax<=shell.Root.Q("ownedDetailsTopRow").worldBound.xMax+1,"Details dyno remains readable and within its first row at "+width);
                        yield return Capture("owned-car-details-"+width);
                        var sizedPlot=shell.Root.Q<NativeDynoPlot>("ownedDynoPlot");var sizedPoint=sizedPlot.LocalToWorld(sizedPlot.PlotRect.center);
                        input.MoveMouseAt((int)sizedPoint.x,(int)sizedPoint.y);yield return new WaitForSecondsRealtime(.15f);
                        var sizedReadout=shell.Root.Q("dynoHoverValues");Check(sizedPlot.HoverRpm.HasValue&&sizedReadout.worldBound.xMax<=sizedPlot.worldBound.xMax+1&&sizedReadout.worldBound.yMax<=sizedPlot.worldBound.yMax+1,"Hover readout fits the dyno sheet at "+width);
                        yield return Capture("owned-car-dyno-hover-"+width);
                        yield return Click(input,"ownedStatsToggle");
                        var statScroll=shell.Root.Q<ScrollView>("ownedDetailScroll");statScroll.scrollOffset=new Vector2(0,320);yield return new WaitForSecondsRealtime(.15f);
                        var statPanel=shell.Root.Q<NativeStatsPanel>("ownedCarStats");var statViewport=statScroll.contentViewport.worldBound;
                        Check(!statPanel.ClassListContains("hidden")&&statPanel.Query(className:"stat-row").ToList().All(row=>row.worldBound.xMin>=statViewport.xMin-1&&row.worldBound.xMax<=statViewport.xMax+1),"Expanded Engine stats reflow inside the details viewport at "+width);
                        Check(statPanel.Query<Label>(className:"stat-value").ToList().All(label=>label.worldBound.xMax<=statViewport.xMax+1)&&shell.Root.Q("ownedUseCar").worldBound.yMax<=shell.Root.worldBound.yMax,"Engine values and car actions remain reachable at "+width);
                        yield return Capture("engine-stats-"+width);yield return StatGroupLayoutChecks(input,width);statScroll.scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"ownedStatsToggle");

                        yield return Click(input,"ownedBack");
                    }
                }
                var target = new RenderTexture(3840,2160,24); target.Create();
                shell.Panel.targetTexture = target; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                CarFilterToolbarLayout(3840);
                var viewport4k = shell.Root.Q<ScrollView>("ownedCars").contentViewport.worldBound;
                Check(shell.Panel.scale==2 && shell.Root.Q("ownedCarGrid").Query(className:"owned-car-card").ToList().All(card=>card.worldBound.xMax<=viewport4k.xMax+1),"Owned grid fits true 4K target with shared two-times scale");
                var cards4k=shell.Root.Q("ownedCarGrid").Query(className:"owned-car-card").ToList();
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
                var stored=JsonUtility.FromJson<PlayerProfile>(ProfileSaveCodec.ReadJson(ProfileStore.FilePath));
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
                yield return RenameCarChecks(input,first.instanceId);
                fixture=game.Profile;
                yield return MaintenanceChecks(input,first.instanceId,second.instanceId,fixture.cars[3].instanceId);
                fixture=game.Profile;
                saved=File.ReadAllText(ProfileStore.FilePath);
                fixture.cars.Clear();shell.InvalidateOwnedGrid();shell.Refresh();yield return Capture("owned-cars-empty");
                Check(shell.Root.Q("ownedCarGrid").Query(className:"owned-car-card").ToList().Count==0 && shell.Root.Q<Label>(className:"car-empty").text=="No owned cars","Empty garage shows no fabricated car");
                Check(File.ReadAllText(ProfileStore.FilePath)==saved,"Empty-state browsing does not write another save");
            } finally {
                if(shell.OwnedDetailsOpen)typeof(NativeMenuShell).GetMethod("BackToOwnedCars",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                profileField.SetValue(game,original);game.settings=original.driving;
                typeof(PrototypeGame).GetMethod("ShowGarageCar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                ProfileStore.Flush();SqliteStateStore.Invalidate();File.WriteAllBytes(ProfileStore.FilePath,diskBytes);
                if(backupBytes!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backupBytes);
                else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");
                shell.InvalidateOwnedGrid();shell.Refresh();
            }
        }
        IEnumerator RenameCarChecks(WindowsKeyboardProbe input, string id)
        {
            var before = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(game.Profile)); before.catalog=game.Profile.catalog;
            var originalCar = before.cars.Find(car=>car.instanceId==id);
            string originalName=originalCar.customName, saved=File.ReadAllText(ProfileStore.FilePath);
            var oldCar=JsonUtility.FromJson<OwnedCar>("{\"vehicleId\":\"starter-hatch\"}");
            Check(VehicleCollection.DisplayName(before.catalog,oldCar)==before.catalog.Spec(oldCar.vehicleId).displayName,"Existing owned-car data without a custom name falls back to the model name");
            yield return Click(input,"ownedCar:"+id);yield return Click(input,"renameOwnedCar");
            var field=shell.Root.Q<TextField>("ownedCarName");
            Check(shell.ModalOpen && !shell.Root.Q("ownedDetails").enabledInHierarchy && field.maxLength==48 && field.tabIndex==-1,"Pencil opens a themed rename form with modal ownership and a bounded editable input");
            Check(!shell.Root.Q("dialogSave").enabledInHierarchy,"Unchanged name cannot create a redundant save");
            yield return Click(input,"ownedCarName");
            foreach(char letter in "track build")yield return Key(input,(ushort)(letter==' '?0x20:char.ToUpperInvariant(letter)));
            Check(field.value=="track build" && shell.EditingText,"Real keyboard text entry edits the name draft");
            yield return Key(input,0x77);Check(shell.Visible && shell.ModalOpen && field.value=="track build","F8 cannot bypass a rename draft or confirmation");
            yield return Key(input,0x09);Check(shell.EditingText && shell.ModalOpen,"Rename form preserves the no-Tab-navigation policy");
            yield return Capture("owned-car-rename-dialog");
            yield return Click(input,"dialogCancel");
            Check(!shell.ModalOpen && shell.OwnedDetailsOpen && JsonUtility.ToJson(game.Profile)==JsonUtility.ToJson(before) && File.ReadAllText(ProfileStore.FilePath)==saved,"Cancel discards the typed name without leaving details or saving");
            yield return Click(input,"renameOwnedCar");shell.Root.Q<TextField>("ownedCarName").value="discard x";yield return Click(input,"modalClose");
            Check(!shell.ModalOpen && shell.OwnedDetailsOpen && File.ReadAllText(ProfileStore.FilePath)==saved,"Rename X discards the draft and retains the details page");
            yield return Click(input,"renameOwnedCar");shell.Root.Q<TextField>("ownedCarName").value="discard escape";yield return Key(input,0x1b);
            Check(!shell.ModalOpen && shell.OwnedDetailsOpen && File.ReadAllText(ProfileStore.FilePath)==saved,"Escape closes only the rename dialog, without cascading to Back");
            yield return Click(input,"renameOwnedCar");field=shell.Root.Q<TextField>("ownedCarName");field.value="  Track build  ";
            Check(JsonUtility.ToJson(game.Profile)==JsonUtility.ToJson(before) && File.ReadAllText(ProfileStore.FilePath)==saved,"Previewing a changed name does not mutate assets or write saves");
            yield return Click(input,"dialogSave");
            Check(!shell.ModalOpen && shell.OwnedDetailsOpen && shell.Root.Q<Label>("ownedDetailTitle").text=="Track build","Confirming a name retains details and updates its heading");
            var stored=JsonUtility.FromJson<PlayerProfile>(ProfileSaveCodec.ReadJson(ProfileStore.FilePath)); stored.catalog=before.catalog;
            Check(stored.cars.Find(car=>car.instanceId==id).customName=="Track build" && game.Profile.revision==before.revision+1,"Trimmed per-instance name survives save deserialization with exactly one transaction");
            var unchanged=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(game.Profile));unchanged.revision=before.revision;unchanged.cars.Find(car=>car.instanceId==id).customName=originalName;
            Check(JsonUtility.ToJson(unchanged)==JsonUtility.ToJson(before),"Renaming preserves selection, all other names, parts, condition, resources, settings and records");
            Check(VehicleCollection.Label(stored.catalog,stored.cars.Find(car=>car.instanceId==id)).StartsWith("Track build / "),"Shared legacy ownership labels also identify the renamed build");
            yield return Click(input,"ownedBack");
            Check(shell.Root.Q("ownedCar:"+id).Q<Label>(className:"car-card-title").text=="Track build" && game.Profile.cars.Where(car=>car.vehicleId==originalCar.vehicleId && car.instanceId!=id).All(car=>car.customName==before.cars.Find(old=>old.instanceId==car.instanceId).customName),"Grid uses the custom name without renaming duplicate models");
            yield return Capture("owned-car-renamed-grid");
            saved=File.ReadAllText(ProfileStore.FilePath);
            Check(game.RenameNativeCar(id,game.Profile.revision,"Track build") && File.ReadAllText(ProfileStore.FilePath)==saved,"Same-name command is a no-op without a save");
            Check(!game.RenameNativeCar(id,game.Profile.revision-1,"stale") && !game.RenameNativeCar("missing-car",game.Profile.revision,"missing") && !game.RenameNativeCar(id,game.Profile.revision,new string('x',49)) && !game.RenameNativeCar(id,game.Profile.revision,"bad\nname") && File.ReadAllText(ProfileStore.FilePath)==saved,"Stale/missing targets and overlong/multiline names reject without saving");
            yield return Click(input,"ownedCar:"+id);yield return Click(input,"renameOwnedCar");field=shell.Root.Q<TextField>("ownedCarName");field.value="Retry build";
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                yield return Click(input,"dialogSave");
                Check(shell.ModalOpen && field.value=="Retry build" && !string.IsNullOrEmpty(shell.Root.Q<Label>("renameCarError").text) && game.Profile.cars.Find(car=>car.instanceId==id).customName=="Track build" && File.ReadAllText(ProfileStore.FilePath)==saved,"Failed rename save retains the draft, reports an inline error and rolls back the name");
            }
            yield return Click(input,"dialogSave");Check(!shell.ModalOpen && game.Profile.cars.Find(car=>car.instanceId==id).customName=="Retry build","Rename can retry after a save failure");
            yield return Click(input,"renameOwnedCar");field=shell.Root.Q<TextField>("ownedCarName");field.value=new string('W',48);
            Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.4f);shell.Refresh();yield return new WaitForEndOfFrame();
            Check(shell.Root.Q("dialogSave").worldBound.xMax<=shell.Root.worldBound.xMax && field.worldBound.xMin>=0 && field.worldBound.xMax<=shell.Root.worldBound.xMax,"Rename input and confirmation fit the compact window");
            yield return Capture("owned-car-rename-1024");yield return Click(input,"dialogSave");yield return new WaitForEndOfFrame();
            Check(shell.Root.Q("renameOwnedCar").worldBound.xMax<=shell.Root.worldBound.xMax && !shell.Root.Q<Label>("ownedDetailTitle").enableRichText,"Maximum-length headings keep the pencil reachable and render names as plain text");
            yield return Capture("owned-car-long-name-1024");
            yield return Click(input,"renameOwnedCar");shell.Root.Q<TextField>("ownedCarName").value="";yield return Click(input,"dialogSave");
            Check(shell.Root.Q<Label>("ownedDetailTitle").text==before.catalog.Spec(originalCar.vehicleId).displayName && game.Profile.cars.Find(car=>car.instanceId==id).customName=="","Clearing the name explicitly restores the model name");
            yield return Click(input,"ownedBack");
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
        }
#endif
    }
}
