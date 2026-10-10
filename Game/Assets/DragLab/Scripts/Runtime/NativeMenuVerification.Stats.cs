using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator EngineStatsChecks(WindowsKeyboardProbe input,NativeDynoPlot dyno)
        {
            string baseline=JsonUtility.ToJson(game.Profile),disk=System.IO.File.ReadAllText(ProfileStore.FilePath);
            var panel=shell.Root.Q<NativeStatsPanel>("ownedCarStats");
            Check(panel!=null&&panel.ClassListContains("hidden")&&ReferenceEquals(panel.Current.Inspection,dyno.Series[0].Inspection),"Stats starts hidden and shares the cached applied dyno inspection without another simulation");
            yield return Click(input,"ownedStatsToggle");
            Check(!panel.ClassListContains("hidden")&&shell.Root.Q("ownedStatsToggle").ClassListContains("selected")&&!shell.ModalOpen,"Stats button reveals inline Engine details without changing route or opening a dialog");
            Check(panel.Q("statsBody:Engine").Query(className:"stat-row").ToList().Count==NativeStatCatalog.Engine.Definitions.Count&&panel.Query<TextField>().ToList().Count==0&&panel.Query<Slider>().ToList().Count==0,"Engine catalog renders every approved row as read-only content");
            var stats=panel.Current.Inspection;var spec=stats.source.build.spec;
            Check(panel.Q<Label>("statValue:peakHp").text==stats.peakHp.ToString("F0")+" HP"&&panel.Q<Label>("statValue:peakTorque").text==DisplayUnits.Torque(stats.peakTorque,dyno.Units).ToString("F0")+" "+DisplayUnits.TorqueLabel(dyno.Units),"Detailed power and torque match the worn applied dyno and selected units");
            Check(panel.Q<Label>("statValue:displacement").text==spec.DisplacementLitres.ToString("F2")+" L"&&panel.Q<Label>("statValue:cylinders").text==spec.sound.cylinders.ToString()&&panel.Q<Label>("statValue:redlineRpm").text==spec.redlineRpm.ToString("F0")+" RPM","Engine configuration and RPM limits use the inspected instance's resolved hardware");
            Check(panel.Q<Label>("statValue:limiterRpm").text==panel.Current.Engine.limitRpm.ToString("F0")+" RPM"&&panel.Query<Button>().ToList().All(button=>button.tabIndex==-1),"Limiter target resolves installed permissions; new controls do not add Tab traversal");
            yield return Capture("engine-stats-open");
            yield return Click(input,"statInfo:peakHp");
            Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Peak power","Individual stat info uses the shared themed dialog");
            yield return Key(input,0x77);Check(shell.Visible&&shell.ModalOpen,"Stats info blocks F8 while its modal is open");
            yield return Key(input,0x1B);Check(!shell.ModalOpen&&shell.OwnedDetailsOpen&&!panel.ClassListContains("hidden"),"Escape closes only the stat explanation and retains the expanded details page");
            yield return Click(input,"statsGroup:Engine");Check(panel.Q("statsBody:Engine").ClassListContains("hidden"),"Engine header collapses its rows without hiding maintenance or the graph");
            yield return Key(input,0x77);yield return Key(input,0x77);
            Check(shell.OwnedDetailsOpen&&!panel.ClassListContains("hidden")&&panel.Q("statsBody:Engine").ClassListContains("hidden"),"Whole-interface F8 comparison retains Stats visibility and group state");
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
            Check(shell.OwnedDetailsOpen&&shell.Root.Q<NativeStatsPanel>("ownedCarStats").Q("statsBody:Engine").ClassListContains("hidden"),"Settings Cancel retains the inspected car and collapsed Engine state");
            yield return Click(input,"statsGroup:Engine");
            var scroll=shell.Root.Q<ScrollView>("ownedDetailScroll");scroll.scrollOffset=new Vector2(0,320);yield return new WaitForSecondsRealtime(.15f);
            Check(panel.Q("stat:camTiming").worldBound.xMax<=scroll.contentViewport.worldBound.xMax+1,"Engine rows fit within the details viewport without horizontal overflow");
            yield return Capture("engine-stats-lower");scroll.scrollOffset=Vector2.zero;yield return new WaitForSecondsRealtime(.15f);
            // Same definitions serve comparison callers without capturing or editing the profile.
            var comparison=new NativeStatsPanel(stats,new UnitPreferences {torque=TorqueUnit.PoundFeet},new[] {NativeStatCatalog.Engine},new Dictionary<string,bool>(),_=>{},(_,__)=>{},dyno.Series[1].Inspection);
            Check(comparison.Q<Label>("statValue:peakTorque").text.Contains(" → ")&&comparison.Q<Label>("statValue:peakTorque").text.Contains("lb-ft")&&comparison.Query(className:"stat-better").ToList().Count>0,"Shared rows support current-to-proposed values, converted torque units and favorable delta bars");
            var draftSource=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(car=>car.instanceId==shell.Root.Q("ownedCarDetails").tooltip));
            draftSource.build.spec.valvetrain.camTimingDegrees=3;
            var draft=CarInspection.CalculatePrepared(draftSource,CarInspection.PrepareCar(draftSource),false);
            var tradeoff=new NativeStatsPanel(stats,new UnitPreferences(),new[] {NativeStatCatalog.Engine},new Dictionary<string,bool>(),_=>{},(_,__)=>{},draft);
            Check(tradeoff.Q("stat:camTiming").Query(className:"stat-tradeoff").ToList().Count>0,"Timing changes are labelled tradeoffs rather than treating every increase as better");
            draftSource.build.spec.engineBoreMm=0;draftSource.build.spec.valvesPerCylinder=0;draftSource.build.spec.engineBehavior.limiter=LimiterStyle.None;draftSource.build.tune.limiter=LimiterStyle.None;
            var absent=new NativeStatsPanel(draft,new UnitPreferences(),new[] {NativeStatCatalog.Engine},new Dictionary<string,bool>(),_=>{},(_,__)=>{});
            Check(absent.Q<Label>("statValue:bore").text=="—"&&absent.Q<Label>("statValue:valvesPerCylinder").text=="—"&&absent.Q<Label>("statValue:limiterRpm").text=="—","Unspecified dimensions and absent limiter targets do not fabricate values");
            yield return RemainingStatsChecks(input,stats);
            shell.Refresh();Check(ReferenceEquals(panel,shell.Root.Q<NativeStatsPanel>("ownedCarStats")),"Unchanged frames retain the stat panel and captured engine values");
            Check(JsonUtility.ToJson(game.Profile)==baseline&&System.IO.File.ReadAllText(ProfileStore.FilePath)==disk,"Stats, explanations, collapse and copied comparisons do not select, save, spend, heal or refill assets");
            yield return Click(input,"ownedStatsToggle");Check(panel.ClassListContains("hidden"),"Stats closes inline and restores the compact maintenance-first view");
        }
#endif
    }
}
