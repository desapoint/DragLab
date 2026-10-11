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
        IEnumerator ComparisonClick(WindowsKeyboardProbe input,string name,bool dealer)
        {
            var element=shell.Root.Q(dealer?"dealerContent":"ownedCarDetails").Q(name);
            shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll").ScrollTo(element);
            yield return new WaitForSecondsRealtime(.1f);var rect=element.worldBound;float scale=NativeMenuShell.DisplayScale(Screen.width,Screen.height);
            input.MouseButtonAt((int)(rect.center.x*scale),(int)(rect.center.y*scale),true);yield return new WaitForSecondsRealtime(.08f);
            input.MouseButtonAt((int)(rect.center.x*scale),(int)(rect.center.y*scale),false);yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator ComparisonChecks(WindowsKeyboardProbe input,bool dealer)
        {
            string compare=dealer?"dealerCompareCar":"ownedCompareCar",panelId=dealer?"dealerCarStats":"ownedCarStats",plotId=dealer?"dealerDynoPlot":"ownedDynoPlot";
            var profile=game.Profile;string before=JsonUtility.ToJson(profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            string selected=profile.selectedOwnedVehicleId;int probes=shell.DealerProbeCount;
            yield return Click(input,compare);
            var panel=shell.Root.Q<NativeStatsPanel>(panelId);var plot=shell.Root.Q<NativeDynoPlot>(plotId);
            Check(panel.Proposed!=null&&!panel.ClassListContains("hidden")&&shell.Root.Q<Button>(compare).ClassListContains("selected"),(dealer?"Dealer":"Owned")+" Compare opens inline selected-to-viewed stats without changing selection");
            Check(plot.Series.Count==2&&plot.Series[0].Id=="Selected"&&plot.Series[1].Id=="Viewed"&&ReferenceEquals(plot.Series[0].Inspection,panel.Current.Inspection)&&ReferenceEquals(plot.Series[1].Inspection,panel.Proposed.Inspection),"Comparison dyno overlays the same actual-condition inspections used by full stats");
            var source=InspectionBuild.Capture(profile,profile.SelectedCar);var actual=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);
            Check(Math.Abs(actual.peakHp-panel.Current.Inspection.peakHp)<.001&&Math.Abs(actual.condition-panel.Current.Inspection.condition)<.001,"Selected comparison baseline uses retained hardware, tune and wear rather than a healthy OEM estimate");
            foreach(string id in new[]{"HP","Torque","Weight","Condition"})Check(shell.Root.Q<Label>("compareSummary:"+id).text.Contains(" → "),"Basic comparison shows both values: "+id);
            string[] groups={"Engine","Induction","Transmission","Tires","Weight","Cooling","Components","Performance"};
            Check(groups.All(g=>panel.Q("statsGroup:"+g)!=null),"Comparison contains every reusable detailed stat group");
            if(panel.Q("statsBody:Engine").ClassListContains("hidden"))yield return ComparisonClick(input,"statsGroup:Engine",dealer);
            Check(panel.Q<Label>("statValue:peakHp").text==panel.Current.Inspection.peakHp.ToString("F0")+" HP → "+panel.Proposed.Inspection.peakHp.ToString("F0")+" HP","Detailed power values match the selected and viewed curves");
            yield return Capture(dealer?"dealer-comparison":"owned-comparison");
            shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll").scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.1f);
            var points=plot.PlotRect;var pointer=plot.worldBound.position+new Vector2(points.x+points.width*.55f,points.y+points.height*.5f);input.MoveMouseAt((int)pointer.x,(int)pointer.y);yield return new WaitForSecondsRealtime(.15f);
            Check(plot.HoverRpm.HasValue&&plot.HoverSamples.Count==2&&plot.HoverSamples.All(s=>s.HasValue),"Comparison dyno hover follows both curves on the shared RPM axis");
            foreach(string group in new[]{"Transmission","Components"}) {
                yield return ComparisonClick(input,"statsGroup:"+group,dealer);
                Check(panel.Q("statsBody:"+group).Query<Label>().ToList().Any(l=>l.name?.StartsWith("statValue:")==true&&l.text.Contains(" → ")),"Compared "+group+" rows retain both saved values");
                yield return ComparisonClick(input,"statsGroup:"+group,dealer);
            }
            if(panel.Q("statsBody:Performance").ClassListContains("hidden"))yield return ComparisonClick(input,"statsGroup:Performance",dealer);
            float deadline=Time.realtimeSinceStartup+30;
            while((panel.Current.DrivingPending||panel.Proposed.DrivingPending)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(panel.Current.Driving.probeComplete&&panel.Proposed.Driving.probeComplete&&panel.Current.DrivingError==null&&panel.Proposed.DrivingError==null,"Performance resolves independent copied-build driving estimates for both compared cars: "+panel.Current.DrivingPending+" / "+panel.Proposed.DrivingPending+" / "+panel.Current.DrivingError+" / "+panel.Proposed.DrivingError);
            Check(panel.Q<Label>("statValue:probeStatus").text.Contains(" → ")&&panel.Q<Label>("statValue:quarterTime").text.Contains(" → "),"Completed performance refresh retains both comparison columns");
            yield return ComparisonClick(input,"statInfo:peakHp",dealer);Check(shell.ModalOpen&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text.Contains("Selected\n")&&l.text.Contains("Viewed\n")),"Comparison info explains both sides through the shared modal");yield return Key(input,0x1B);
            yield return ComparisonClick(input,"statsGroup:Performance",dealer);
            shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll").scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.1f);
            int captures=shell.InspectionCaptureCount;yield return Key(input,0x77);yield return Key(input,0x77);
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
            Check(shell.Root.Q<NativeStatsPanel>(panelId).Proposed!=null,"F8 and Settings Cancel retain car comparison mode");
            yield return Click(input,compare);Check(shell.Root.Q<NativeStatsPanel>(panelId).Proposed==null&&shell.Root.Q<NativeDynoPlot>(plotId).Series[0].Id==(dealer?"Stock":"Current"),"Second Compare click restores the normal dyno and single-car stats");
            yield return Click(input,compare);Check(shell.InspectionCaptureCount==captures&&shell.DealerProbeCount==probes,"Repeated comparison uses cached snapshots and stock estimates rather than rerunning on gestures");yield return Click(input,compare);
            Check(profile.selectedOwnedVehicleId==selected&&JsonUtility.ToJson(game.Profile)==before&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Compare, hover, stats and info leave selection, money, parts, wear, resources and saved bytes unchanged");
            Check(shell.Root.Query<VisualElement>().ToList().All(e=>e.tabIndex==-1),"Comparison controls preserve no Tab navigation");
        }
#endif
    }
}
