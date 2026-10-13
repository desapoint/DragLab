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
        void WarningModelChecks()
        {
            var old=new[]{new SetupWarning("Remaining","100 Nm","Change gearing",true),new SetupWarning("Resolved","Old issue","Repair"),new SetupWarning("Component needs attention","Failed Cooling: probe","Service"),new SetupWarning("Component needs attention","Failed Clutch: probe","Service")};
            var next=new[]{new SetupWarning("Remaining","200 Nm","Change gearing"),new SetupWarning("Introduced","New issue","Fit supporting hardware",true),new SetupWarning("Component needs attention","Failed Clutch: probe","Service")};
            var changes=NativeWarningOverview.Combine(old,next,true);
            Check(changes.Length==5&&changes.Count(w=>w.State=="Introduced")==1&&changes.Count(w=>w.State=="Remaining")==2&&changes.Count(w=>w.State=="Resolved")==2,"Warnings identify introduced, remaining and resolved issues without merging different generic component messages");
            Check(changes.First().State=="Introduced"&&changes.Last().State=="Resolved","Introduced issues precede remaining issues and resolved history");
            Check(changes.Single(w=>w.Display.title=="Remaining").Current.critical&&!changes.Single(w=>w.Display.title=="Remaining").Planned.critical,"Changed measurements and severity stay one matched issue");
            Check(NativeWarningOverview.Combine(old.Concat(old),null,false).Length==4&&NativeWarningOverview.Combine(old,next,false).All(w=>w.State=="Current"),"Exact duplicate reports collapse and current-only inspection excludes planned-only issues");
            Check(NativeWarningOverview.Related(old[2])==PartSemantics.Friendly("Cooling")&&NativeWarningOverview.Related(new SetupWarning("Cam needs stronger valve springs","","" )).Contains("valve springs"),"Related systems come from authoritative issue titles/messages");
            Check(NativeWarningOverview.Combine(Array.Empty<SetupWarning>(),Array.Empty<SetupWarning>(),true).Length==0,"A healthy inspection has a valid empty overview");
        }
        string WarningText()=>string.Join("\n",shell.Root.Q<ScrollView>("modalContent").Query<Label>().ToList().Select(l=>l.text));
        void ShowWarningFixture(SetupWarning[] current,SetupWarning[] planned,bool comparison,string unavailable=null)
        {
            typeof(NativeMenuShell).GetMethod("OpenCombinedWarnings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{current,planned,comparison,"Warnings",unavailable});
        }
        IEnumerator NativeWarningsChecks(WindowsKeyboardProbe input)
        {
            WarningModelChecks();var p=game.Profile;var car=p.SelectedCar;var piston=p.componentsPart(PartCategory.Pistons);
            VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=car.vehicleId,price=1,reputation=0,stock=game.vehicles.First(v=>v.spec.id==car.vehicleId).CreateStockTune(),paint="#FFFFFF"});
            var bare=p.cars.Last();AssemblyOperations.Detach(p,bare.instanceId);p.selectedOwnedVehicleId=car.instanceId;p.revision++;ProfileStore.Save(p);
            string before=JsonUtility.ToJson(p);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"navGarage");yield return Click(input,"garageOwned");
            var scroll=shell.Root.Q<ScrollView>("ownedCars");scroll.ScrollTo(shell.Root.Q("ownedCar:"+car.instanceId));yield return new WaitForSecondsRealtime(.15f);
            int grids=shell.OwnedGridBuildCount,ratings=shell.OwnedRatingRunCount,captures=shell.InspectionCaptureCount;var offset=scroll.scrollOffset;
            var icon=shell.Root.Q<Button>("ownedWarnings:"+car.instanceId);
            Check(icon!=null&&icon.Q<FilledMenuIcon>()!=null&&icon.worldBound.width==40&&icon.pickingMode==PickingMode.Position,"Owned cards have a wider filled warning icon with its own hit area");
            var glyph=icon.Q<FilledMenuIcon>();Check(glyph.worldBound.xMin-icon.worldBound.xMin>=10&&icon.worldBound.xMax-glyph.worldBound.xMax>=10,"Warning glyph is centered with breathing room inside the slanted button");
            var set=typeof(NativeMenuShell).GetMethod("SetWarningButton",BindingFlags.Instance|BindingFlags.NonPublic);var originalIssues=(SetupWarning[])typeof(NativeMenuShell).GetMethod("CurrentOwnedWarnings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{car});
            foreach(string severity in new[]{"critical","attention","clear"}){
                var issues=severity=="clear"?Array.Empty<SetupWarning>():new[]{new SetupWarning("Visual fixture","","",severity=="critical")};set.Invoke(shell,new object[]{icon,issues,true});yield return new WaitForSecondsRealtime(.15f);
                Color expectedColor=severity=="critical"?RacingPalette.Accent(icon):severity=="attention"?(Color)new Color32(230,191,128,255):RacingPalette.Read(icon,new CustomStyleProperty<Color>("--racing-muted"),Color.gray);
                Check(((Vector4)glyph.resolvedStyle.color-(Vector4)expectedColor).sqrMagnitude<.0001f,"Warning glyph shows its actual severity color: "+severity);
                input.MoveMouseAt((int)icon.worldBound.center.x,(int)icon.worldBound.center.y);yield return new WaitForSecondsRealtime(.15f);Check(((Vector4)glyph.resolvedStyle.color-(Vector4)expectedColor).sqrMagnitude<.0001f,"Hover preserves warning severity: "+severity);yield return Capture("warning-icon-"+severity);
            }
            set.Invoke(shell,new object[]{icon,originalIssues,true});yield return new WaitForSecondsRealtime(.15f);
            yield return Click(input,icon.name);
            Check(shell.ModalOpen&&!shell.OwnedDetailsOpen&&p.SelectedCar==car,"Card warning click opens its dialog without selecting the car or opening its details");
            string actual=WarningText();Check(actual.Contains("Individual Pistons fault")&&actual.Contains("compression")&&actual.Contains("Pistons")&&actual.Contains("Static inspection"),"Owned warning dialog shows actual component faults, related parts and inspection scope");
            Check(shell.Root.Q("warningsGroup:Current")!=null&&shell.Root.Q<ScrollView>("modalContent").Query<Label>(className:"warning-suggestion").ToList().Count>1,"Current warnings include existing actionable suggestions");
            yield return Capture("owned-warning-dialog");yield return Click(input,"modalClose");
            Check(!shell.ModalOpen&&!shell.OwnedDetailsOpen&&(scroll.scrollOffset-offset).sqrMagnitude<1&&grids==shell.OwnedGridBuildCount&&ratings==shell.OwnedRatingRunCount&&captures==shell.InspectionCaptureCount,"X preserves the grid, scroll and cached inspection without new simulation work");
            scroll.ScrollTo(shell.Root.Q("ownedCar:"+bare.instanceId));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"ownedWarnings:"+bare.instanceId);
            Check(shell.ModalOpen&&!shell.OwnedDetailsOpen&&p.SelectedCar==car&&WarningText().Contains("Build cannot race")&&!WarningText().Contains("Individual Pistons fault"),"Nonselected engine-less car reports its own unavailable build without leaking the selected car's faults or switching ownership");yield return Click(input,"dialogClose");
            scroll.ScrollTo(shell.Root.Q("ownedCar:"+car.instanceId));yield return new WaitForSecondsRealtime(.15f);
            yield return Click(input,"ownedCar:"+car.instanceId);Check(shell.OwnedDetailsOpen,"Normal card click still opens the car details");
            yield return Click(input,"ownedDetailWarnings");Check(shell.ModalOpen&&WarningText()==actual,"Details reuse exactly the car list's current warning overview");yield return Click(input,"dialogClose");
            Check(shell.OwnedDetailsOpen&&p.SelectedCar==car,"Dialog Close returns to the same car details");yield return Click(input,"ownedBack");
            Check(before==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Car warning browsing never repairs, edits resources or saves player state");
            yield return Click(input,"navBuild");yield return WaitParts();game.ClearNativeCart();game.StageNativePurchase(piston.definitionId,null,1,true);yield return Click(input,"partsCart");
            int preview=shell.CartPreviewBuildCount;var current=shell.Root.Q<NativeStatsPanel>("cartStats").Current.Inspection;var planned=shell.Root.Q<NativeStatsPanel>("cartStats").Proposed.Inspection;
            var expected=NativeWarningOverview.Combine(current.warnings,planned.warnings,true);Check(expected.Any(w=>w.State=="Resolved"&&w.Display.title=="Individual Pistons fault"),"Real complete-set cart preview resolves the damaged piston fault");
            yield return Click(input,"cartWarnings");Check(shell.Root.Q("warningsGroup:Resolved")!=null&&WarningText().Contains("Individual Pistons fault"),"Cart exposes resolved faults together with any remaining warnings");
            Check(shell.Root.Q<Button>("cartWarnings").Q<Label>().text=="Warnings · "+expected.Length,"Cart button counts all combined issues, including resolved ones");yield return Capture("cart-warning-dialog");yield return Click(input,"modalClose");
            Check(shell.CartPageOpen&&preview==shell.CartPreviewBuildCount&&shell.Root.Q<Button>("cartCheckout").enabledSelf,"Read-only warning browsing reuses the cumulative plan and leaves valid checkout available");
            var syntheticCurrent=new[]{new SetupWarning("Clutch may slip instead of accelerating","Capacity 100 Nm","Fit a stronger clutch",true),new SetupWarning("Oil needs service","Old oil","Change the oil")};
            var syntheticPlan=new[]{new SetupWarning("Clutch may slip instead of accelerating","Capacity 200 Nm","Fit a stronger clutch"),new SetupWarning("Cam needs stronger valve springs","Lift exceeds spring travel","Fit supporting springs",true)};
            string cart=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            ShowWarningFixture(syntheticCurrent,syntheticPlan,true);yield return new WaitForSecondsRealtime(.2f);
            Check(new[]{"Introduced","Remaining","Resolved"}.All(s=>shell.Root.Q("warningsGroup:"+s)!=null)&&WarningText().Contains("Critical → Attention")&&WarningText().Contains("Current: ")&&WarningText().Contains("Planned: "),"Shared dialog presents all three sections and preserves changes in severity and measurements");
            foreach(int width in new[]{1024,1920}){
                Screen.SetResolution(width,width==1024?640:1080,false);yield return new WaitForSecondsRealtime(.5f);
                var view=shell.Root.Q<ScrollView>("modalContent");var close=shell.Root.Q("modalClose").worldBound;
                Check(close.xMax<=shell.Root.worldBound.xMax&&close.yMin>=0&&view.worldBound.yMax<shell.Root.Q("dialogClose").worldBound.yMin,"Warnings scroll while Close/X remain reachable at "+width);
                Check(view.Query<Label>().ToList().All(l=>l.worldBound.xMax<=view.contentViewport.worldBound.xMax+1),"Warning labels wrap within the shared dialog at "+width);yield return Capture("warnings-"+width);
            }
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);
            Check(shell.Panel.scale==2&&shell.Root.Q("modalClose").worldBound.xMax<=shell.Root.worldBound.xMax&&shell.Root.Q<ScrollView>("modalContent").worldBound.yMax<shell.Root.Q("dialogClose").worldBound.yMin,"Shared warnings retain crisp 4K scaling, bounded content and reachable actions");
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"warnings-4k.png"),image.EncodeToPNG());RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.4f);yield return Click(input,"dialogClose");
            ShowWarningFixture(syntheticCurrent,null,true,"Planned inspection unavailable");yield return new WaitForSecondsRealtime(.2f);
            Check(shell.Root.Q("warningUnavailable")!=null&&shell.Root.Q("warningsGroup:Current")!=null&&shell.Root.Q("warningsGroup:Resolved")==null,"Unavailable planned builds retain current issues without falsely resolving them");yield return Click(input,"modalClose");
            ShowWarningFixture(Array.Empty<SetupWarning>(),null,false);yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("warningsEmpty")!=null,"Empty inspection uses a concise no-warnings state");yield return Click(input,"dialogClose");
            yield return Key(input,0x77);yield return Key(input,0x77);yield return Click(input,"cartWarnings");Check(shell.ModalOpen&&shell.Root.Q("warningsGroup:Resolved")!=null,"F8 preserves cart state and the actual combined warnings");yield return Click(input,"modalClose");
            Check(before==JsonUtility.ToJson(p)&&cart==JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()})&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath))&&preview==shell.CartPreviewBuildCount,"Warning dialogs, resizing and F8 never modify the applied car, cart or disk or recapture previews");
            Check(shell.Root.Q<Button>("cartWarnings").tabIndex<0&&!shell.Root.Q<Button>("cartWarnings").focusable,"Warnings retain the shared mouse-only button navigation rules");game.ClearNativeCart();yield return Click(input,"cartBack");
        }
#endif
    }
}
