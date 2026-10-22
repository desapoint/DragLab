using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class MechanicalRecoveryVerification
    {
        public static string OutputFolder;
        public static void RunStandalone(){string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));Debug.Log("2K RECOVERY PASS: "+(Run(root)+OwnershipVerification.Run(root)+InventoryVerification.Run(root)));}
        public static int Run(string root,Func<VehicleDefinition,bool> includeVehicle=null)
        {
            string folder=OutputFolder??Path.Combine(root,"reports/phases/2k");Directory.CreateDirectory(folder);
            int count=0;var log=new StringBuilder();var rows=new StringBuilder("car,scenario,part_id,definition,health_before,health_after,work_j,stress,temperature_c,failure_tick,cause\n");
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"recovery-verification.txt"),log+"Checks: "+count);if(!ok)throw new InvalidOperationException(label);count++;};
            var catalog=MechanicsTransactions.Catalog();string previous=ProfileStore.VerificationPath;
            try{foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Where(v=>includeVehicle==null||includeVehicle(v))){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.cars[0].vehicleId=p.selectedVehicleId=v.spec.id;p.cash=100000;MechanicsOwnership.Ensure(p,catalog);
                ProfileStore.VerificationPath=Path.Combine(folder,v.spec.id+"-save","profile-v1.json");Directory.CreateDirectory(Path.GetDirectoryName(ProfileStore.FilePath));
                File.WriteAllText(ProfileStore.FilePath,JsonUtility.ToJson(p));ProfileStore.Load();
                var build=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);var driver=new DriverSettings {wearMode=WearMode.Full,optimalThermals=false,throttleAssist=false};
                var car=new CarSimulation(build.spec,build.tune,driver,p.parts);car.SetFluids(p.SelectedCar.fluids);car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);
                string run=MechanicsOwnership.NewId("recovery-test");PlayerProfile next;string error;
                check(MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,0,car)),ProfileStore.TrySave,out next,out error),v.spec.id+": initial owned run is durable / "+error);p=next;
                var baseline=car.mechanics.parts.Select(x=>x.Copy()).ToArray();
                foreach(var input in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,42)){
                    car.IndependentStep(DragMath.FixedStep,input.input,input.neutral,input.staging);
                    long tick=input.tick+1;
                    if(tick%RunPersistence.PeriodTicks==0){var capture=RunPersistence.Capture(run,p.SelectedCar.instanceId,tick,car);bool committed=MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,capture),ProfileStore.TrySave,out next,out error);if(!committed){File.WriteAllText(Path.Combine(folder,"rejected-before.json"),JsonUtility.ToJson(p,true));File.WriteAllText(Path.Combine(folder,"rejected-after.json"),JsonUtility.ToJson(capture,true));}check(committed,v.spec.id+": physical checkpoint "+tick+" / "+error);p=next;}
                }
                check(!car.state.engineFailed&&!car.state.drivetrainFailed&&car.mechanics.parts.All(x=>!x.condition.failed),v.spec.id+": benign full pull/coast/brake survives.");
                string expectedConditions=JsonUtility.ToJson(p.parts[0].condition);double fuel=p.SelectedCar.fuel.litres;int cash=p.cash;
                check(!ProfileStore.Load().runCheckpoints.Single(x=>x.runId==run).settled,v.spec.id+": ordinary save inspection does not close a live run.");
                var loaded=ProfileStore.Load(true);check(Math.Abs(loaded.SelectedCar.fuel.litres-fuel)<1e-10&&p.parts.All(a=>SameConditions(a.condition,loaded.parts.Find(b=>b.instanceId==a.instanceId).condition))&&loaded.cash==cash&&loaded.SelectedCar.completedRuns==0,v.spec.id+": interrupted load retains committed fuel/wear without reward or record.");
                check(loaded.runCheckpoints.Single(x=>x.runId==run).settled&&ProfileStore.Load().runCheckpoints.Single(x=>x.runId==run).settled,v.spec.id+": interrupted run closes durably and idempotently.");
                var closed=RunPersistence.Capture(run,loaded.SelectedCar.instanceId,5000,car);
                check(!MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,closed),x=>true,out next,out error),v.spec.id+": recovered run cannot commit again.");
                string newRun=MechanicsOwnership.NewId("test-live");var retry=new CarSimulation(build.spec,build.tune,driver,loaded.parts);retry.SetFluids(loaded.SelectedCar.fluids);retry.SetMassInputs(75,0,1.5,.45,loaded.SelectedCar.fuel.litres);retry.state.fuelUsedLitres=loaded.SelectedCar.fuel.cumulativeUsedLitres;
                retry.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=.4},true,false);var valid=RunPersistence.Capture(newRun,loaded.SelectedCar.instanceId,1,retry);
                string before=JsonUtility.ToJson(loaded);
                check(!MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,valid),q=>false,out next,out error)&&ReferenceEquals(next,loaded)&&before==JsonUtility.ToJson(loaded),v.spec.id+": failed persistence publishes no candidate and cannot discard live wear.");
                check(MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,valid),ProfileStore.TrySave,out next,out error),v.spec.id+": failed write can be retried with the same live checkpoint / "+error);loaded=next;
                Action<Action<RunCheckpoint>,string> reject=(edit,label)=>{var bad=RunPersistence.Capture(newRun,loaded.SelectedCar.instanceId,2,retry);edit(bad);check(!MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,bad),q=>true,out next,out error),v.spec.id+": rejects "+label);};
                reject(c=>c.fuel.litres+=1,"fuel refill checkpoint");reject(c=>c.fuel.cumulativeUsedLitres=0,"fuel history rewind");reject(c=>c.components.RemoveAt(0),"missing installed state");reject(c=>c.components[0].ownerId="foreign","foreign owner metadata");reject(c=>c.model="old-model","wrong simulation model");reject(c=>c.fluids.oilLitres+=1,"oil refill checkpoint");
                reject(c=>{var tires=c.components.Find(x=>x.slot=="Tires");tires.condition.tires[0].health=1;},"individual tire healing");
                check(MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(newRun,loaded.SelectedCar.instanceId,1,retry,true,"abandoned")),ProfileStore.TrySave,out next,out error),v.spec.id+": same-tick final closure is accepted once / "+error);loaded=next;
                var damaged=new CarSimulation(build.spec,build.tune,driver,loaded.parts);damaged.SetMassInputs(75,0,1.5,.45,loaded.SelectedCar.fuel.litres);damaged.state.fuelUsedLitres=loaded.SelectedCar.fuel.cumulativeUsedLitres;damaged.SetFluids(loaded.SelectedCar.fluids);var shaft=damaged.mechanics.Part(PartCategory.Driveshaft);shaft.condition.health=.000001f;
                // Weak retained hardware fails under real transmitted work, rather than a whole-car flag.
                var ownedShaft=loaded.parts.Find(x=>x.instanceId==shaft.instanceId);ownedShaft.condition.health=shaft.condition.health;long failureTick=-1;
                for(int i=0;i<2400;i++){damaged.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=1},false,i<120);if(shaft.condition.failed){failureTick=i;break;}}
                check(failureTick>=0&&damaged.state.drivetrainFailed&&!damaged.state.engineFailed,v.spec.id+": real shaft overload disconnects drive only.");
                string failRun=MechanicsOwnership.NewId("failure");
                check(MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(failRun,loaded.SelectedCar.instanceId,failureTick+1,damaged,true,"failure")),ProfileStore.TrySave,out next,out error),v.spec.id+": failed condition persists atomically / "+error);loaded=ProfileStore.Load();
                string failedId=shaft.instanceId;check(loaded.parts.Find(x=>x.instanceId==failedId).condition.failed,v.spec.id+": reload retains failed shaft.");
                check(MechanicsTransactions.Try(loaded,catalog,loaded.revision,q=>PartOperations.Buy(q,catalog,q.SelectedCar.instanceId,shaft.definitionId,true),ProfileStore.TrySave,out next,out error),v.spec.id+": compatible replacement restores owned drive / "+error);loaded=next;
                var restored=MechanicsOwnership.Resolve(loaded,loaded.SelectedCar,catalog);var repaired=new CarSimulation(restored.spec,restored.tune,driver,loaded.parts.Where(x=>x.location!=PartLocation.Inventory));
                check(!repaired.state.drivetrainFailed&&loaded.parts.Find(x=>x.instanceId==failedId).condition.failed&&loaded.parts.Find(x=>x.instanceId==failedId).location==PartLocation.Inventory,v.spec.id+": repair retains original failed identity and cause.");
                foreach(var part in car.mechanics.parts){var old=baseline.First(x=>x.instanceId==part.instanceId);rows.AppendLine(string.Join(",",v.spec.id,"benign",part.instanceId,part.definitionId,F(old.condition.health),F(part.condition.health),F(part.condition.slipEnergyJ),F(part.condition.stressExposure),F(part.condition.temperatureC),-1,part.condition.failureReason));}
                rows.AppendLine(string.Join(",",v.spec.id,"weak-shaft",shaft.instanceId,shaft.definitionId,F(.000001),F(shaft.condition.health),F(shaft.condition.slipEnergyJ),F(shaft.condition.stressExposure),F(shaft.condition.temperatureC),failureTick,shaft.condition.failureReason));
            }}finally{ProfileStore.VerificationPath=previous;}
            File.WriteAllText(Path.Combine(folder,"recovery-matrix.csv"),rows.ToString());File.WriteAllText(Path.Combine(folder,"recovery-verification.txt"),log+"Checks: "+count+"\n");return count;
        }
        static bool SameConditions(ComponentCondition a,ComponentCondition b){return a.health==b.health&&a.failed==b.failed&&a.failureReason==b.failureReason&&typeof(ComponentCondition).GetFields().Where(f=>f.FieldType==typeof(double)).All(f=>Math.Abs((double)f.GetValue(a)-(double)f.GetValue(b))<Math.Max(1e-9,Math.Abs((double)f.GetValue(a))*1e-12))&&a.elements.Length==b.elements.Length&&a.elements.Select((e,i)=>e.id==b.elements[i].id&&e.health==b.elements[i].health&&e.fault==b.elements[i].fault).All(v=>v);}
        static string F(double v){return v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    }
}
