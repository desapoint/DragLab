using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator InterruptedRun()
        {
            var report=new System.Text.StringBuilder("2K forced process interruption / real race Update / isolated owned save.\n");
            Action<bool,string> require=(ok,label)=>{Check(ok,label);report.AppendLine("PASS: "+label);};
            bool stage=Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-recovery-stage")>=0;
            if(stage){
                game.settings.ApplyPreset(DrivingPreset.Automatic);game.settings.ApplySimulationPreset(SimulationDifficulty.Simulation);
                game.verificationInput=()=>new DriverInput{throttle=1};game.StartPractice();
                while(game.Race.time<5)yield return null;
                var checkpointed=ProfileStore.Load();
                require(checkpointed.runCheckpoints.Count==1&&checkpointed.runCheckpoints[0].tick>=120,"Real Update asynchronously journals the captured owned run.");
                game.Race.player.mechanics.Part(PartCategory.Driveshaft).condition.health=.000001f;
                float until=Time.realtimeSinceStartup+10;while(!game.Race.player.state.drivetrainFailed&&Time.realtimeSinceStartup<until)yield return null;
                require(game.Race.player.state.drivetrainFailed&&!game.Race.player.state.engineFailed,"Actual transmitted torque fails weakened shaft while engine keeps running.");
                // Freeze input/ticks without invoking pause's full-save path, then kill after journal durability.
                game.enabled=false;RaceCheckpointStore.Drain();
                var saved=ProfileStore.Load();
                var baseSave=JsonUtility.FromJson<PlayerProfile>(ProfileSaveCodec.ReadJson(ProfileStore.FilePath));
                require(saved.runCheckpoints.Count==1&&!saved.runCheckpoints[0].settled&&saved.parts.Any(p=>p.slot=="Driveshaft"&&p.condition.failed),"Critical failure journal retains damaged state before quit callbacks or full save.");
                require(baseSave.runCheckpoints.Single().tick==0&&!baseSave.parts.Any(p=>p.condition.failed),"Full profile remains at entry; restart must merge the separate journal.");
                require(saved.SelectedCar.fuel.litres<game.Race.player.spec.fuel.capacityLitres&&saved.cash==1200&&saved.SelectedCar.completedRuns==0,"In-progress practice consumes fuel and adds no reward/record.");
                yield return Capture("interruption-before-kill");File.WriteAllText(Path.Combine(output,"expected-before-kill.json"),JsonUtility.ToJson(saved,true));File.WriteAllText(Path.Combine(output,"stage-checks.txt"),report.ToString());File.WriteAllText(Path.Combine(output,"recovery-ready.txt"),"Own isolated player ready for forced termination.");
                // The host kills this exact fixture process, skipping OnApplicationQuit.
                while(true)yield return null;
            }
            var expected=JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path.Combine(output,"expected-before-kill.json")));var actual=game.Profile;
            report.Append(File.ReadAllText(Path.Combine(output,"stage-checks.txt")));
            require(actual.SelectedCar.instanceId==expected.SelectedCar.instanceId&&actual.cash==expected.cash&&actual.reputation==expected.reputation&&actual.SelectedCar.completedRuns==0,"Forced restart retains ownership/cash and adds no reward or record.");
            require(actual.runCheckpoints.Count==1&&actual.runCheckpoints[0].settled&&game.Race==null,"Forced restart closes the interrupted receipt without resuming a race.");
            require(Math.Abs(actual.SelectedCar.fuel.litres-expected.SelectedCar.fuel.litres)<1e-9&&Math.Abs(actual.SelectedCar.fluids.oilLitres-expected.SelectedCar.fluids.oilLitres)<1e-9,"Forced restart retains committed fuel and oil quantities.");
            var shaft=actual.parts.Find(p=>p.slot=="Driveshaft");string id=shaft.instanceId;int price=actual.catalog.Find(shaft.definitionId).price;
            require(shaft.condition.failed&&shaft.condition.failureReason=="Torque overload","Failed shaft identity and cause survive forced termination.");
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            game.ShowRepairs();yield return new WaitForSecondsRealtime(.2f);yield return Capture("interruption-recovered-repairs");
            using(var mouse=new WindowsKeyboardProbe()){yield return ClickPart(mouse,"repair-buy:"+id);}
            require(game.Profile.cash==expected.cash-price&&game.Profile.parts.Find(p=>p.instanceId==id).location==PartLocation.Inventory&&game.Profile.parts.Find(p=>p.instanceId==id).condition.failed,"Actual repair button charges once and retains the failed original.");
            game.ReturnToGarage();game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};game.StartPractice();
            yield return AwaitFinish();require(game.Race.player.state.finished&&!game.Race.player.state.drivetrainFailed,"Replacement after interrupted run restores a complete real practice pass.");
            game.ReturnToGarage();require(game.Profile.runCheckpoints.All(r=>r.settled)&&game.Profile.cash==expected.cash-price,"Garage return closes all run resources with no practice payout.");
            yield return Capture("interruption-repaired-garage");
#endif
            require(errors==0,"Interruption/recovery/replacement have no runtime, GUI or DSP errors.");
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),report+"Runtime errors: "+errors+"\n");
        }
    }
}
