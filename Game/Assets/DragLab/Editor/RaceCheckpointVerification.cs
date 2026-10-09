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
    public static class RaceCheckpointVerification
    {
        public static void RunStandalone()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string folder=Path.Combine(root,"reports/async-race-saves");Directory.CreateDirectory(folder);
            string fixtures=Path.Combine(folder,"fixtures",Guid.NewGuid().ToString("N"));
            var report=new StringBuilder();int checks=0;
            Action<bool,string> check=(ok,label)=>{report.AppendLine(((ok?"PASS: ":"FAIL: ")+label).TrimEnd());File.WriteAllText(Path.Combine(folder,"verification.txt"),report+"Checks: "+checks,new UTF8Encoding(false));if(!ok)throw new InvalidOperationException(label);checks++;};
            string previous=ProfileStore.VerificationPath;
            try {
                var catalog=MechanicsTransactions.Catalog();
                foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                    ProfileStore.VerificationPath=Path.Combine(fixtures,vehicle.spec.id,"profile-v1.json");
                    var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=p.selectedVehicleId=vehicle.spec.id;
                    MechanicsOwnership.Ensure(p,catalog);check(ProfileStore.TrySave(p),vehicle.spec.id+": isolated full save");
                    p=ProfileStore.Load();var build=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);
                    var car=new CarSimulation(build.spec,build.tune,new DriverSettings {optimalThermals=false},p.parts);
                    car.SetFluids(p.SelectedCar.fluids);car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);
                    string run=MechanicsOwnership.NewId("journal");PlayerProfile next;string error;
                    check(MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,0,car)),ProfileStore.TrySave,out next,out error),vehicle.spec.id+": full entered base / "+error);p=next;
                    string fullBefore=File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8),memoryBefore=JsonUtility.ToJson(p);
                    var cp=RunPersistence.Capture(run,p.SelectedCar.instanceId,120,car);
                    cp.fuel.litres-=.05;cp.fuel.cumulativeUsedLitres+=.05;cp.fluids.coolantLitres-=.01;cp.fluids.coolantLostLitres+=.01;
                    cp.components[0].condition.health=.8f;cp.components[0].condition.temperatureC=97;
                    double expectedFuel=cp.fuel.litres;
                    check(ProfileStore.TryCheckpoint(p,cp,out error),vehicle.spec.id+": queue copied car-only data / "+error);
                    cp.fuel.litres=0;cp.components[0].condition.health=0; // Caller mutation cannot alter the worker's copy.
                    RaceCheckpointStore.Drain();
                    check(RaceCheckpointStore.Error==null&&File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)==fullBefore&&JsonUtility.ToJson(p)==memoryBefore,vehicle.spec.id+": worker changes neither full save nor live profile");
                    var loaded=ProfileStore.Load();
                    check(MechanicsOwnership.Validate(loaded,catalog)==null,vehicle.spec.id+": journal overlay preserves ledger/receipt revision invariants");
                    check(loaded.runCheckpoints.Single(r=>r.runId==run).tick==120&&loaded.SelectedCar.fuel.litres==expectedFuel&&loaded.parts[0].condition.health==.8f&&loaded.parts[0].condition.temperatureC==97,vehicle.spec.id+": copied fuel/wear/heat survive journal load");
                    check(loaded.cash==p.cash&&loaded.SelectedCar.completedRuns==0&&!loaded.runCheckpoints.Single(r=>r.runId==run).settled,vehicle.spec.id+": no reward/record or unrequested closure");
                    check(!ProfileStore.TryCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,120,car),out error),vehicle.spec.id+": duplicate queued tick rejected");

                    // Rapid ordered writes coalesce, retaining all monotonic damage in the newest capture.
                    for(int tick=121;tick<=150;tick++) {
                        var rapid=RunPersistence.Capture(run,p.SelectedCar.instanceId,tick,car);
                        rapid.fuel.litres=expectedFuel;rapid.fuel.cumulativeUsedLitres=car.state.fuelUsedLitres+.05;
                        rapid.fluids.coolantLitres-=.01;rapid.fluids.coolantLostLitres+=.01;
                        rapid.components[0].condition.health=.7f;rapid.components[0].condition.temperatureC=100;
                        check(ProfileStore.TryCheckpoint(p,rapid,out error),vehicle.spec.id+": ordered queue "+tick+" / "+error);
                    }
                    RaceCheckpointStore.Drain();
                    loaded=ProfileStore.Load();check(loaded.runCheckpoints.Single(r=>r.runId==run).tick==150&&loaded.parts[0].condition.health==.7f,vehicle.spec.id+": latest coalesced state wins");
                    var healing=RunPersistence.Capture(run,p.SelectedCar.instanceId,151,car);
                    check(!ProfileStore.TryCheckpoint(p,healing,out error),vehicle.spec.id+": later queue cannot heal earlier queued wear/refill");
                    string journal=RaceCheckpointStore.JournalPath(ProfileStore.FilePath);
                    check(new FileInfo(journal).Length<new FileInfo(ProfileStore.FilePath).Length,vehicle.spec.id+": journal smaller than one-car ledger");
                    string validJournal=File.ReadAllText(journal,Encoding.UTF8);
                    File.Copy(journal,journal+".bak",true);File.WriteAllText(journal,"partial crash data",Encoding.UTF8);
                    loaded=ProfileStore.Load();check(loaded.runCheckpoints.Single(r=>r.runId==run).tick==150,vehicle.spec.id+": corrupt primary journal falls back to durable backup");
                    File.WriteAllText(journal,validJournal,Encoding.UTF8);
                    var recovered=ProfileStore.Load(true);
                    check(recovered.runCheckpoints.Single(r=>r.runId==run).settled&&recovered.parts[0].condition.health==.7f&&recovered.SelectedCar.fuel.litres==expectedFuel&&recovered.cash==p.cash,vehicle.spec.id+": interrupted recovery retains resources and closes without payout");
                    check(!File.Exists(journal)&&!File.Exists(journal+".bak"),vehicle.spec.id+": full recovery retires journal after durable commit");
                    File.WriteAllText(journal,validJournal,Encoding.UTF8);loaded=ProfileStore.Load();
                    check(loaded.runCheckpoints.Single(r=>r.runId==run).settled&&loaded.parts[0].condition.health==.7f,vehicle.spec.id+": stale journal cannot reopen settled run");
                    check(ProfileStore.TrySave(loaded),vehicle.spec.id+": superseded journal cleanup");

                    // New run: queued failure must survive a simultaneous full boundary without late overwrite.
                    p=loaded;build=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);
                    car=new CarSimulation(build.spec,build.tune,new DriverSettings(),p.parts);car.SetFluids(p.SelectedCar.fluids);
                    car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);car.state.fuelUsedLitres=p.SelectedCar.fuel.cumulativeUsedLitres;
                    run=MechanicsOwnership.NewId("failure");
                    check(MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,0,car)),ProfileStore.TrySave,out next,out error),vehicle.spec.id+": second entry / "+error);p=next;
                    var shaft=car.mechanics.Part(PartCategory.Driveshaft);shaft.condition.health=0;shaft.condition.failed=true;shaft.condition.failureReason="Explicit checkpoint fixture";
                    cp=RunPersistence.Capture(run,p.SelectedCar.instanceId,1,car);
                    check(ProfileStore.TryCheckpoint(p,cp,out error),vehicle.spec.id+": immediate critical failure queue");
                    check(MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,2,car,true,"Garage exit")),ProfileStore.TrySave,out next,out error),vehicle.spec.id+": full exit drains pending failure / "+error);p=next;
                    loaded=ProfileStore.Load();check(loaded.runCheckpoints.Single(r=>r.runId==run).tick==2&&loaded.runCheckpoints.Single(r=>r.runId==run).settled&&loaded.parts.Single(x=>x.instanceId==shaft.instanceId).condition.failed&&!File.Exists(journal),vehicle.spec.id+": exit cannot be overwritten by older worker");
                    // Atomic validation: reject a late-invalid condition without applying an earlier valid part.
                    var bad=RunPersistence.Capture("invalid",p.SelectedCar.instanceId,1,car);bad.components[0].condition.health=.1f;bad.components.Last().condition.health=float.NaN;
                    string untouched=JsonUtility.ToJson(p);try{MechanicsTransactions.ApplyCheckpoint(p,bad);}catch(InvalidOperationException){}
                    check(JsonUtility.ToJson(p)==untouched,vehicle.spec.id+": complete validation precedes any ledger mutation");
                }
                VerifyFailureAndReset(fixtures,check);
            }finally{RaceCheckpointStore.Drain();ProfileStore.VerificationPath=previous;}
            File.WriteAllText(Path.Combine(folder,"verification.txt"),report+"Checks: "+checks+"\n",new UTF8Encoding(false));Debug.Log("ASYNC CHECKPOINT PASS: "+checks);
        }
        static void VerifyFailureAndReset(string fixtures,Action<bool,string> check)
        {
            ProfileStore.VerificationPath=Path.Combine(fixtures,"io-failure","profile-v1.json");
            var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,MechanicsTransactions.Catalog());ProfileStore.TrySave(p);p=ProfileStore.Load();
            var build=MechanicsOwnership.Resolve(p,p.SelectedCar,p.catalog);var car=new CarSimulation(build.spec,build.tune,p.driving,p.parts);
            car.SetFluids(p.SelectedCar.fluids);car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);
            string run=MechanicsOwnership.NewId("io");PlayerProfile next;string error;
            check(MechanicsTransactions.Try(p,p.catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,0,car)),ProfileStore.TrySave,out next,out error),"I/O fixture entered");p=next;
            string temp=RaceCheckpointStore.JournalPath(ProfileStore.FilePath)+".tmp";Directory.CreateDirectory(temp);
            check(ProfileStore.TryCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,120,car),out error),"I/O fixture queued");RaceCheckpointStore.Drain();
            check(RaceCheckpointStore.Error!=null&&ProfileStore.Load().runCheckpoints.Single().tick==0,"Write failure reported; prior durable base preserved");
            Directory.Delete(temp); // Exact empty fixture directory, not a recursive delete.
            check(ProfileStore.TryCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,121,car),out error),"Retry queue after I/O recovery");RaceCheckpointStore.Drain();
            check(RaceCheckpointStore.Error==null&&ProfileStore.Load().runCheckpoints.Single().tick==121,"Retry is durable and clears failure");
            check(ProfileStore.TrySave(p)&&MechanicsOwnership.Validate(p,p.catalog)==null&&p.runCheckpoints.Single().tick==121,"Ordinary settings save merges journal without invalidating receipt revision");
            check(ProfileStore.TryCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,122,car),out error),"Journal resumes against new full-save base");
            p.driving.soundVolume=.61f;
            check(ProfileStore.TryReset(p,out next),"Reset drains and retires live journal");
            var loaded=ProfileStore.Load(true);check(loaded.runCheckpoints.Count==0&&loaded.driving.soundVolume==.61f&&!File.Exists(RaceCheckpointStore.JournalPath(ProfileStore.FilePath)),"Reset retains preferences and cannot restore previous run");
        }
    }
}
