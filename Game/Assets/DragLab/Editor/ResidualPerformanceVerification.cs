using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class ResidualPerformanceVerification
    {
        public static void Checkpoints(){RaceCheckpointVerification.OutputFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/residual-performance/checkpoint"));RaceCheckpointVerification.RunStandalone();}
        public static void Recovery(){MechanicalRecoveryVerification.OutputFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/residual-performance/recovery"));MechanicalRecoveryVerification.RunStandalone();}
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);log.AppendLine("PASS "+label);}
            var catalog=MechanicsTransactions.Catalog();long now=DateTime.UtcNow.Ticks;
            foreach(var d in catalog.parts){var copy=d.Copy();copy.id="";checks++;if(PartsMarket.ProductKey(d)!=JsonUtility.ToJson(copy))throw new InvalidOperationException("Product serialization changed: "+d.id);}log.AppendLine("PASS Exact product serialization across "+catalog.parts.Count+" authored definitions");
            var edited=catalog.parts.First(d=>d.category==PartCategory.Pistons);string previousKey=PartsMarket.ProductKey(edited);edited.price++;Check(PartsMarket.ProductKey(edited)!=previousKey,"In-place authored price edits invalidate cached product identity");edited.price--;Check(PartsMarket.ProductKey(edited)==previousKey,"Restoring the authored price restores exact product identity");
            foreach(int seed in new[]{12345,-7,987654}){
                var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,catalog);VehicleListings.Initialize(p,catalog,seed);
                foreach(var original in p.vehicleStock.listings){
                    foreach(string failure in new[]{"none","Pistons","Clutch","Brakes"}){
                        var offer=JsonUtility.FromJson<VehicleOffer>(JsonUtility.ToJson(original));if(failure!="none")offer.parts.First(i=>i.slot==failure).condition.failed=true;
                        string before=JsonUtility.ToJson(offer);var build=VehicleInspection.Hardware(catalog,offer);
                        var baseline=new CarSimulation(build.spec,build.tune,new DriverSettings{wearMode=WearMode.None,throttleAssist=false},offer.parts);baseline.SetFluids(offer.fluids);baseline.SetMassInputs(build.tune.driverMassKg,build.tune.payloadMassKg,build.tune.payloadPositionM,build.tune.payloadHeightM,offer.fuel.litres);
                        var expected=VehicleAssessmentCache.Run(baseline);var actual=VehicleAssessmentCache.Prepare(catalog,offer,build).GetAwaiter().GetResult();
                        Check(actual.engineRuns==expected.engineRuns&&actual.drives==expected.drives&&before==JsonUtility.ToJson(offer),seed+" "+offer.model+" "+failure+" isolated worker equals main-thread physical inspection without mutating the offer");
                    }
                }
                double max=0;var iterator=VehicleListings.WarmNext(p).GetEnumerator();while(true){var timer=Stopwatch.StartNew();bool more=iterator.MoveNext();max=Math.Max(max,timer.Elapsed.TotalMilliseconds);if(!more)break;}
                var wait=Stopwatch.StartNew();while(VehicleAssessmentCache.Pending&&wait.Elapsed.TotalSeconds<15)Thread.Sleep(1);Check(!VehicleAssessmentCache.Pending,"Seed "+seed+" next stock inspections finish before publication");
                int misses=VehicleAssessmentCache.Misses;PartsMarket.Measurement=(phase,ms)=>log.AppendLine("TIMING parts "+phase+" ms "+ms.ToString("F2"));var refresh=Stopwatch.StartNew();for(int i=0;i<3;i++)VehicleListings.AfterRace(p,catalog);refresh.Stop();PartsMarket.Measurement=null;
                Check(VehicleAssessmentCache.Misses==misses,"Seed "+seed+" market refresh reuses every prepared exact inspection");log.AppendLine("TIMING seed "+seed+" warm capture max ms "+max.ToString("F2")+" / prepared refresh ms "+refresh.Elapsed.TotalMilliseconds.ToString("F2"));
            }
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/residual-performance"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"fixtures/profile-v1.json");Directory.CreateDirectory(Path.GetDirectoryName(file));string prior=ProfileStore.VerificationPath;
            try{
                ProfileStore.VerificationPath=file;foreach(string suffix in new[]{"",".bak",".legacy-v9"})if(File.Exists(file+suffix))File.Delete(file+suffix);
                var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,catalog);p.driving.guaranteedFittingParts=13;string json=JsonUtility.ToJson(p);byte[] encoded=null;for(int run=0;run<3;run++){var timer=Stopwatch.StartNew();encoded=ProfileSaveCodec.Encode(json,MechanicsOwnership.SaveVersion);log.AppendLine("TIMING compression run "+run+" ms "+timer.Elapsed.TotalMilliseconds.ToString("F2"));}
                Check(ProfileSaveCodec.Decode(Encoding.UTF8.GetString(encoded))==json&&encoded.Length<Encoding.UTF8.GetByteCount(json)/3,"Compressed save roundtrip is exact and less than one third of the original bytes");
                Check(ProfileSaveCodec.Decode(json)==json,"Legacy flat JSON remains readable");
                p.saveVersion=9;string old=JsonUtility.ToJson(p);File.WriteAllText(file,old,new UTF8Encoding(false));var loaded=ProfileStore.Load();Check(loaded.saveVersion==MechanicsOwnership.SaveVersion&&loaded.driving.guaranteedFittingParts==13&&JsonUtility.ToJson(loaded.parts)==JsonUtility.ToJson(p.parts)&&File.ReadAllText(file+".legacy-v9",Encoding.UTF8)==old,"Schema-9 migration preserves hardware, custom settings and original backup");
                Check(ProfileStore.TrySave(loaded),"Compressed full save succeeds");byte[] bytes=File.ReadAllBytes(file);
                using(var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(!ProfileStore.TrySave(loaded),"Failed publication reports failure instead of publishing the action");
                Check(bytes.SequenceEqual(File.ReadAllBytes(file)),"Failed publication preserves the previous complete save");
                File.WriteAllText(file,"corrupt",new UTF8Encoding(false));var recovered=ProfileStore.Load();Check(ProfileStore.Status.Contains("Recovered")&&recovered.SelectedCar.instanceId==loaded.SelectedCar.instanceId,"Corrupt compressed primary recovers the valid previous save");
                string future="{\"saveVersion\":"+MechanicsOwnership.SaveVersion+",\"codecVersion\":2,\"encoding\":\"gzip-base64\",\"payload\":\"\"}";File.WriteAllText(file,future,new UTF8Encoding(false));var protectedProfile=ProfileStore.Load();Check(!ProfileStore.TrySave(protectedProfile)&&File.ReadAllText(file,Encoding.UTF8)==future,"Unknown codec cannot be overwritten even when a valid backup can be read");
                log.AppendLine("Save bytes: flat "+Encoding.UTF8.GetByteCount(json)+" / encoded "+encoded.Length);
            }finally{ProfileStore.VerificationPath=prior;}
            log.AppendLine("Checks: "+checks+"; failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));UnityEngine.Debug.Log("Residual performance checks passed: "+checks);
        }
    }
}
