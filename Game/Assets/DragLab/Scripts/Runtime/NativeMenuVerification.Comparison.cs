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
            var scroll=shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll");scroll.ScrollTo(element);
            yield return new WaitForSecondsRealtime(.1f);
            scroll.scrollOffset=new Vector2(0,Mathf.Max(0,element.worldBound.yMin-scroll.contentContainer.worldBound.yMin-scroll.contentViewport.worldBound.height*.35f));
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
            if(panel.DifferencesOnly)yield return ComparisonClick(input,"statsDifferencesOnly",dealer);
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
            yield return ComparisonClick(input,"statInfo:peakHp",dealer);Check(shell.ModalOpen&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text==NativeStatCatalog.Engine.Definitions.First(d=>d.Id=="peakHp").InfoExplanation(panel.Current,panel.Proposed)),"Comparison info shows More, Less and direction-specific drawbacks without duplicated figures");
            yield return Capture(dealer?"stat-help-dealer-power":"stat-help-owned-power");yield return Key(input,0x1B);
            var delta=panel.Q<Label>("statDelta:displacement");var scroll=shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll");
            scroll.scrollOffset=new Vector2(0,Mathf.Max(0,delta.worldBound.yMin-scroll.contentContainer.worldBound.yMin-100));yield return new WaitForSecondsRealtime(.15f);
            float scale=NativeMenuShell.DisplayScale(Screen.width,Screen.height);input.MoveMouseAt((int)(delta.worldBound.center.x*scale),(int)(delta.worldBound.center.y*scale));yield return new WaitForSecondsRealtime(.15f);
            string explanation=NativeStatCatalog.Engine.Definitions.First(d=>d.Id=="displacement").InfoExplanation(panel.Current,panel.Proposed);
            Check(delta.ClassListContains("stat-tradeoff")&&!panel.Q("statHelpBubble").ClassListContains("hidden")&&panel.Q<Label>("statHelpText").text==explanation,"Orange Tradeoff hover shows the useful general explanation in a native bubble");
            yield return Capture(dealer?"dealer-tradeoff-info":"owned-tradeoff-info");
            scroll.scrollOffset=new Vector2(0,Mathf.Max(0,delta.worldBound.yMax-scroll.contentContainer.worldBound.yMin-scroll.contentViewport.worldBound.height+12));
            input.MoveMouseAt(10,100);yield return new WaitForSecondsRealtime(.15f);
            input.MoveMouseAt((int)(delta.worldBound.center.x*scale),(int)(delta.worldBound.center.y*scale));yield return new WaitForSecondsRealtime(.15f);
            var bubble=panel.Q("statHelpBubble");var bounds=scroll.contentViewport.worldBound;
            Check(!bubble.ClassListContains("hidden")&&bubble.worldBound.yMin>=bounds.yMin-1&&bubble.worldBound.yMax<=bounds.yMax+1,"Tradeoff hint remains inside the visible scroll viewport near its bottom edge");
            yield return ComparisonClick(input,"statInfo:displacement",dealer);Check(shell.ModalOpen&&panel.Q("statHelpBubble").ClassListContains("hidden")&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text==explanation),"Tradeoff hint and info dialog share More/Less and the comparison drawback; opening the dialog hides the hint");
            yield return Capture(dealer?"stat-help-dealer-displacement":"stat-help-owned-displacement");yield return Key(input,0x1B);
            yield return DifferenceFilterChecks(input,panel,dealer);
            yield return ComparisonClick(input,"statsGroup:Performance",dealer);
            shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll").scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.1f);
            int captures=shell.InspectionCaptureCount;yield return Key(input,0x77);yield return Key(input,0x77);
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
            Check(shell.Root.Q<NativeStatsPanel>(panelId).Proposed!=null&&shell.Root.Q<NativeStatsPanel>(panelId).DifferencesOnly,"F8 and Settings Cancel retain car comparison mode and differences filter");
            yield return Click(input,compare);Check(shell.Root.Q<NativeStatsPanel>(panelId).Proposed==null&&shell.Root.Q<NativeDynoPlot>(plotId).Series[0].Id=="Current","Second Compare click restores the normal dyno and single-car stats");Check(shell.Root.Q<NativeStatsPanel>(panelId).Q("statsDifferencesOnly")==null,"Single-car stats do not expose a comparison-only filter");
            yield return Click(input,compare);Check(shell.Root.Q<NativeStatsPanel>(panelId).DifferencesOnly,"Re-entering comparison retains the UI-session filter choice");Check(shell.InspectionCaptureCount==captures&&shell.DealerProbeCount==probes,"Repeated comparison uses cached snapshots and stock estimates rather than rerunning on gestures");yield return Click(input,compare);
            Check(profile.selectedOwnedVehicleId==selected&&JsonUtility.ToJson(game.Profile)==before&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Compare, hover, stats and info leave selection, money, parts, wear, resources and saved bytes unchanged");
            Check(shell.Root.Query<VisualElement>().ToList().All(e=>e.tabIndex==-1),"Comparison controls preserve no Tab navigation");
        }
        IEnumerator DifferenceFilterChecks(WindowsKeyboardProbe input,NativeStatsPanel panel,bool dealer)
        {
            var units=SharedUnitSettings.Current;var curve=shell.Root.Q<NativeDynoPlot>(dealer?"dealerDynoPlot":"ownedDynoPlot");var series=curve.Series;
            yield return ComparisonClick(input,"statsDifferencesOnly",dealer);
            Check(panel.DifferencesOnly&&panel.Q("statsDifferencesOnly").ClassListContains("selected"),"Differences only is enabled by its native button");
            Check(panel.Q("stat:engineLayout").ClassListContains("hidden")&&!panel.Q("stat:peakHp").ClassListContains("hidden"),"Filter hides matching text settings and keeps changed numeric stats");
            var filter=panel.Q<Button>("statsDifferencesOnly");var viewport=shell.Root.Q<ScrollView>(dealer?"dealerDetailScroll":"ownedDetailScroll").contentViewport.worldBound;
            Check(filter.worldBound.xMin>=viewport.xMin&&filter.worldBound.xMax<=viewport.xMax&&filter.worldBound.height>=30,"Shared filter button fits within the stat viewport");
            yield return Capture(dealer?"dealer-differences-filter":"owned-differences-filter");
            yield return ComparisonClick(input,"statsGroup:Transmission",dealer);
            var gearing=NativeStatCatalog.Groups(panel.Current.Inspection,panel.Proposed.Inspection).First(g=>g.Id=="Transmission").Children.First(g=>g.Id=="Gearing");
            var expected=gearing.Definitions.Where(d=>d.Differs(panel.Current,panel.Proposed)).ToArray();
            if(expected.Length>0){
                if(panel.Q("statsBody:Gearing").ClassListContains("hidden"))yield return ComparisonClick(input,"statsGroup:Gearing",dealer);
                Check(expected.All(d=>panel.Q("stat:"+d.Id)!=null&&!panel.Q("stat:"+d.Id).ClassListContains("hidden")),"Every changed nested gear/CVT stat is built and visible");
            }else Check(panel.Q("statsGroupFrame:Gearing").ClassListContains("hidden"),"Identical gearing stays hidden when comparing different models with the same ratios");
            var rows=panel.Query<VisualElement>(className:"stat-row").ToList();
            Check(rows.All(row=>row.ClassListContains("hidden")==!((NativeStatDefinition)row.userData).Differs(panel.Current,panel.Proposed)),"Filtering covers every built stat row and nested gear group");
            yield return Capture(dealer?"dealer-differences-only":"owned-differences-only");
            var equal=new NativeStatsPanel(panel.Current.Inspection,units,NativeStatCatalog.Groups(panel.Current.Inspection,panel.Current.Inspection),new System.Collections.Generic.Dictionary<string,bool>{{"Performance",false}},_=>{},(_,__)=>{},panel.Current.Inspection,true);
            equal.SetDrivingInspection(panel.Current.Driving,units,_=>{},panel.Current.Driving);
            Check(equal.Query<VisualElement>(className:"stat-group").ToList().All(g=>g.ClassListContains("hidden"))&&!equal.Q("statsNoDifferences").ClassListContains("hidden"),"Identical builds hide empty groups and show No differing stats");
            equal.SetDrivingInspection(panel.Current.Driving,units,_=>{},panel.Proposed.Driving);
            Check(!equal.Q("statsGroupFrame:Performance").ClassListContains("hidden")&&equal.Q("statsBody:Performance").Query<VisualElement>(className:"stat-row").ToList().All(row=>row.ClassListContains("hidden")==!((NativeStatDefinition)row.userData).Differs(equal.Current,equal.Proposed)),"Completed performance refresh reapplies filtering to new rows and empty-group visibility");
            var definition=new NativeStatDefinition {Number=s=>ReferenceEquals(s,panel.Current)?(double?)null:0};
            Check(definition.Differs(panel.Current,panel.Proposed),"Unavailable numeric stat differs from a real zero");
            definition.Number=_=>null;Check(!definition.Differs(panel.Current,panel.Proposed),"Two unavailable numeric stats match");
            definition.Number=s=>ReferenceEquals(s,panel.Current)?100:100.000001;Check(!definition.Differs(panel.Current,panel.Proposed),"Numeric filter ignores floating-point residue using the delta-bar tolerance");
            definition.Number=s=>ReferenceEquals(s,panel.Current)?100:100.0001;Check(definition.Differs(panel.Current,panel.Proposed),"Numeric filter keeps a meaningful nonzero delta");
            bool gearingWasClosed=panel.Q("statsBody:Gearing").ClassListContains("hidden");
            yield return ComparisonClick(input,"statsDifferencesOnly",dealer);
            Check(!panel.DifferencesOnly&&panel.Query<VisualElement>(className:"stat-row").ToList().All(row=>!row.ClassListContains("hidden"))&&panel.Q("statsBody:Gearing").ClassListContains("hidden")==gearingWasClosed,"Turning filtering off restores rows and retains nested disclosure state");
            Check(ReferenceEquals(curve.Series,series),"Stat filtering leaves the dyno series unchanged");
            yield return ComparisonClick(input,"statsGroup:Transmission",dealer);
            yield return ComparisonClick(input,"statsDifferencesOnly",dealer);
        }
#endif
    }
}
