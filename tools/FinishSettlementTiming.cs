using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
public static class FinishSettlementTiming
{
    public static string Run(bool validated=false)
    {
        string root=Path.GetFullPath("../reports/finish-stutter-20261016");Directory.CreateDirectory(root);var text=new StringBuilder();
        string source=Path.GetFullPath("../reports/player-fixtures/finish-stutter-before-20261016/save-slot-1.sqlite3"),previous=ProfileStore.VerificationPath;
        var txn=typeof(MechanicsTransactions).GetField("Measurement",BindingFlags.NonPublic|BindingFlags.Static);var save=typeof(ProfileStore).GetField("SaveMeasurement",BindingFlags.NonPublic|BindingFlags.Static);
        try{foreach(bool refresh in new[]{false,true}){
            string path=Path.Combine(root,refresh?"timing-paid.sqlite3":"timing-practice.sqlite3");File.Copy(source,path,true);ProfileStore.VerificationPath=path;var p=ProfileStore.Load();p.vehicleStock.completedRuns=2;
            var car=p.SelectedCar;var b=DragLab.Domain.Mechanics.MechanicsOwnership.Resolve(p,car,p.catalog);var d=new DriverSettings{launch=LaunchMode.Automatic,shifting=ShiftMode.Automatic,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,optimalThermals=true};
            var race=RaceSessionFactory.Create(b.spec,b.tune,d,b.spec,b.tune,d.Copy(),18.288,p.parts.FindAll(part=>part.ownerId==car.instanceId||part.ownerId==car.engineAssemblyId));race.player.SetMassInputs(b.tune.driverMassKg,b.tune.payloadMassKg,b.tune.payloadPositionM,b.tune.payloadHeightM,car.fuel.litres);race.player.state.fuelUsedLitres=car.fuel.cumulativeUsedLitres;race.player.SetFluids(car.fluids);
            string id="finish-timing-"+refresh;CareerProgression.Enter(p,id,car.instanceId,null,!refresh);MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(id,car.instanceId,0,race.player));p.revision++;if(!ProfileStore.TrySave(p))throw new Exception(ProfileStore.Status);
            long tick=0;while(race.phase!=RacePhase.Finished&&tick<120*100){race.Step(new DriverInput{throttle=1});tick++;}
            var checkpoint=RunPersistence.Capture(id,car.instanceId,tick,race.player,false,"Measured finish");
            text.AppendLine(refresh?"PAID MARKET REFRESH":"PRACTICE");txn.SetValue(null,(Action<string,double>)((name,ms)=>text.AppendLine("transaction "+name+" ms "+ms.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));save.SetValue(null,(Action<string,double>)((name,ms)=>text.AppendLine("save "+name+" ms "+ms.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));
            var timer=Stopwatch.StartNew();PlayerProfile next;string error;bool ok=MechanicsTransactions.Try(p,p.catalog,p.revision,c=>{MechanicsTransactions.ApplyCheckpoint(c,checkpoint);CareerProgression.Settle(c,id,race);},validated?(Func<PlayerProfile,bool>)(c=>(bool)typeof(ProfileStore).GetMethod("TrySaveValidated",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{c})):ProfileStore.TrySave,out next,out error);timer.Stop();text.AppendLine("total ms "+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" / "+ok+" / "+error);if(!ok)throw new Exception(error);txn.SetValue(null,null);save.SetValue(null,null);
        }}finally{ProfileStore.VerificationPath=previous;txn.SetValue(null,null);save.SetValue(null,null);}
        File.WriteAllText(Path.Combine(root,validated?"settlement-after.txt":"settlement-before.txt"),text.ToString(),new UTF8Encoding(false));return text.ToString();
    }
}
