using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DriveType=DragLab.Domain.DriveType;
namespace DragLab.Editor
{
    public static class AwdHardwareVerification
    {
        [Serializable] sealed class JournalData {public int version=1;public long baseRevision;public RunCheckpoint checkpoint;}
        [Serializable] sealed class JournalEnvelope {public string payload,sha256;}
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);log.AppendLine("PASS "+label);}
            var catalog=MechanicsTransactions.Catalog();var vehicles=Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles").Where(v=>v.spec.drive==DriveType.AWD).ToArray();
            foreach(var v in vehicles){
                var recipe=catalog.Recipe(v.spec.id);var baseline=catalog.Resolve(v.spec.id,recipe,v.CreateStockTune());
                var entries=catalog.parts.Where(d=>d.id.StartsWith(v.spec.id+"/",StringComparison.Ordinal)&&d.awdHardware!=null).ToArray();
                foreach(var d in entries){Check(d.awdHardware.Validate()==null&&PartCatalogOverrides.Validate(d,d.Copy())==null,d.id+" hardware data validates");var map=recipe.ToDictionary(p=>p.Key,p=>p.Value);map[d.category]=d.id;
                    foreach(AwdMode requested in Enum.GetValues(typeof(AwdMode))){
                        var tune=v.CreateStockTune();tune.awdConfigured=true;tune.awd=new AwdSpec{mode=requested,frontBias=.97f,minimumFront=0,maximumFront=1,primaryFront=!d.awdHardware.calibration.primaryFront,isolateFailedAxle=!d.awdHardware.calibration.isolateFailedAxle,centerSpeedRatio=1.5f,torqueBiasRatio=9,centerPreloadNm=800,maximumSecondary=1,responseSeconds=2,slipThreshold=.4f,freeShaftInertiaKgM2=.4f,freeShaftDragNm=9};
                        var build=catalog.Resolve(v.spec.id,map,tune);var actual=AxleDriveDynamics.Configuration(build.spec,build.tune);var installed=AwdHardware.Installed(build.spec);var h=installed.awdHardware;
                        Check(build.Drivable&&h.Supports(actual.mode)&&actual.Validate()==null,d.id+" rejects unsupported mode / "+requested);
                        Check(actual.primaryFront==h.calibration.primaryFront&&actual.isolateFailedAxle==h.calibration.isolateFailedAxle&&actual.freeShaftInertiaKgM2==h.calibration.freeShaftInertiaKgM2&&actual.freeShaftDragNm==h.calibration.freeShaftDragNm,d.id+" primary/geometry/disconnect specifications cannot be changed by tuning / "+requested);
                        Check(build.spec.frontDifferential.kind==baseline.spec.frontDifferential.kind&&build.spec.rearDifferential.kind==baseline.spec.rearDifferential.kind,d.id+" center replacement retains separate axle differentials / "+requested);
                        if(actual.mode==AwdMode.ActiveBias)Check(actual.minimumFront>=h.calibration.minimumFront&&actual.maximumFront<=h.calibration.maximumFront&&actual.frontBias>=actual.minimumFront&&actual.frontBias<=actual.maximumFront,d.id+" active limits stay within hardware range");
                        if(actual.mode==AwdMode.OnDemand)Check(actual.maximumSecondary<=h.calibration.maximumSecondary,d.id+" on-demand capacity is hardware bounded");
                        Check(AwdHardware.Allows(build.spec,AwdAdjustments.SpeedRatio)?actual.LockedRatio==1.5:actual.LockedRatio==1,d.id+" speed ratio requires its own mechanical gearsets");
                        var forged=build.tune.Copy();forged.awd.mode=(AwdMode)(((int)actual.mode+1)%6);var car=new CarSimulation(build.spec,forged,new DriverSettings{wearMode=WearMode.None});Check(h.Supports(car.awd.mode),d.id+" direct simulation construction still enforces captured hardware");
                    }
                    var modified=map.ToDictionary(p=>p.Key,p=>p.Value);modified[PartCategory.Controls]=catalog.Id(v.spec.id,PartCategory.Controls,1);var withEcu=catalog.Resolve(v.spec.id,modified,v.CreateStockTune());Check(AxleDriveDynamics.Configuration(withEcu.spec,withEcu.tune).mode==d.awdHardware.calibration.mode,d.id+" engine ECU upgrade does not convert center hardware");
                }
                var part=catalog.Find(v.spec.id+"/Differential/ActiveController");string key=PartsMarket.ProductKey(part);var copy=part.Copy();copy.awdHardware.calibration.maximumFront=.7f;Check(part.awdHardware.calibration.maximumFront==.85f,v.spec.id+" part/simulation copies own independent calibration data");part.awdHardware.calibration.maximumFront=.7f;Check(PartsMarket.ProductKey(part)!=key,v.spec.id+" nested hardware authoring edits invalidate product identity");part.awdHardware.calibration.maximumFront=.85f;
                var legacy=catalog.Find(catalog.Id(v.spec.id,PartCategory.Differential)).Copy();legacy.awdHardware=null;PartCatalogOverrides.Apply(catalog,new PartCatalogOverrideData{parts={legacy}});Check(catalog.Find(legacy.id).awdHardware!=null,v.spec.id+" older authoring overrides retain declared hardware metadata");
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=p.selectedVehicleId=v.spec.id;MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;var old=p.parts.Find(i=>i.slot=="Differential");old.condition.health=.53f;string condition=JsonUtility.ToJson(old.condition);string id=old.instanceId;
                PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,v.spec.id+"/Differential/OnDemandController",true);Check(p.parts.Any(i=>i.instanceId==id&&JsonUtility.ToJson(i.condition)==condition&&i.location==PartLocation.Inventory)&&MechanicsOwnership.Validate(p,catalog)==null,v.spec.id+" real replacement retains worn original identity and condition");
            }
            var raw=new VehicleSpec{drive=DriveType.AWD};foreach(AwdMode mode in Enum.GetValues(typeof(AwdMode))){var t=new Tune{awdConfigured=true,awd=new AwdSpec{mode=mode}};Check(AxleDriveDynamics.Configuration(raw,t).mode==mode,"Explicit raw engineering fixture remains available: "+mode);}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/awd-hardware"));Directory.CreateDirectory(folder);foreach(string model in new[]{"draglab-variety-v18","draglab-variety-v19"})CheckOldResourceJournal(catalog,folder,Check,model);log.AppendLine("Checks: "+checks+"; failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("AWD hardware checks passed: "+checks);
        }
        static void CheckOldResourceJournal(MechanicalCatalog catalog,string folder,Action<bool,string> check,string model)
        {
            string prior=ProfileStore.VerificationPath;try{
                ProfileStore.VerificationPath=Path.Combine(folder,"fixtures",model+"-"+Guid.NewGuid().ToString("N"),"resource-profile.json");Directory.CreateDirectory(Path.GetDirectoryName(ProfileStore.FilePath));
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);var build=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);var car=new CarSimulation(build.spec,build.tune,new DriverSettings(),p.parts);car.SetFluids(p.SelectedCar.fluids);car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);
                string run=MechanicsOwnership.NewId("old-journal");check(MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,RunPersistence.Capture(run,q.SelectedCar.instanceId,0,car)),ProfileStore.TrySave,out var entered,out var error),"Migration test enters a durable run: "+error);p=entered;
                var cp=RunPersistence.Capture(run,p.SelectedCar.instanceId,120,car);cp.model=model;cp.fuel.litres-=.05;cp.fuel.cumulativeUsedLitres+=.05;cp.components[0].condition.health=.8f;
                check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>MechanicsTransactions.ApplyCheckpoint(q,cp),q=>true,out _,out _),"Live submissions still reject obsolete physics captures");
                string payload=JsonUtility.ToJson(new JournalData{baseRevision=p.revision,checkpoint=cp}),hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
                File.WriteAllText(RaceCheckpointStore.JournalPath(ProfileStore.FilePath),JsonUtility.ToJson(new JournalEnvelope{payload=payload,sha256=hash}),new UTF8Encoding(false));var loaded=ProfileStore.Load(true);
                check(loaded.parts[0].condition.health==.8f&&loaded.SelectedCar.fuel.litres==cp.fuel.litres&&loaded.runCheckpoints.Single(r=>r.runId==run).settled&&loaded.cash==p.cash&&loaded.SelectedCar.completedRuns==0,"Known "+model+" resource journal retains damage/fuel on upgrade without replaying the race or awarding a result");
            }finally{ProfileStore.VerificationPath=prior;}
        }
    }
}
