using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
public static class VerifyStagedSettlement
{
    public static string Recovery()
    {
        var root=Path.GetFullPath("../reports/staged-settlement-20261016");var log=new StringBuilder();int checks=0;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;log.AppendLine("PASS: "+text);}
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var begin=typeof(ProfileStore).GetMethod("BeginValidatedSave",flags);var complete=typeof(ProfileStore).GetMethod("CompleteValidatedSave",flags);
        var store=typeof(ProfileStore).Assembly.GetType("DragLab.Infrastructure.SqliteStateStore");var injection=store.GetField("FailureInjection",flags);
        string prior=ProfileStore.VerificationPath;object oldInjection=injection.GetValue(null);
        try{
            ProfileStore.VerificationPath=Path.GetFullPath("../reports/player-fixtures/finish-stutter-safe-20261016/save-slot-1.sqlite3");var source=ProfileStore.Load();
            ProfileStore.VerificationPath=Path.GetFullPath("../reports/player-fixtures/staged-recovery-"+Guid.NewGuid().ToString("N")+"/save-slot-1.sqlite3");source.storageCommit=0;
            Check(ProfileStore.TrySave(source),"Isolated recovery fixture is durably initialized");
            PlayerProfile Copy(PlayerProfile p){var c=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));c.catalog=p.catalog;c.storageCommit=p.storageCommit;return c;}
            System.Threading.Tasks.Task<long> Begin(PlayerProfile p)=>(System.Threading.Tasks.Task<long>)begin.Invoke(null,new object[]{p});
            string Condition(PlayerProfile p)=>JsonUtility.ToJson(p.parts)+JsonUtility.ToJson(p.cars)+JsonUtility.ToJson(p.engines);
            int cash=source.cash;var candidate=Copy(source);candidate.cash+=137;candidate.revision++;string condition=Condition(candidate);
            var committed=Begin(candidate);committed.GetAwaiter().GetResult();
            Check(source.cash==cash&&source.storageCommit!=committed.Result,"Completed worker does not publish into the live source before acknowledgement");
            var recovered=ProfileStore.Load();
            Check(recovered.cash==cash+137&&recovered.storageCommit==committed.Result,"Reload after commit but before publication recovers exactly the committed balance");
            Check(Condition(recovered)==condition,"Committed recovery retains all component identities, condition, fluids and ownership");
            Check(ProfileStore.Load().cash==cash+137,"Repeated recovery does not apply the delta twice");
            complete.Invoke(null,new object[]{candidate,committed});
            Check(candidate.storageCommit==recovered.storageCommit,"Publication acknowledges the same durable commit");
            var failed=Copy(recovered);failed.cash+=251;failed.revision++;injection.SetValue(null,(Action<string>)(stage=>{if(stage=="before-commit")throw new IOException("Recovery rollback fixture");}));
            bool rejected=false;try{Begin(failed).GetAwaiter().GetResult();}catch(IOException){rejected=true;}finally{injection.SetValue(null,oldInjection);}
            var afterFailure=ProfileStore.Load();Check(rejected&&afterFailure.cash==recovered.cash&&afterFailure.storageCommit==recovered.storageCommit,"Failed transaction leaves durable balance and commit unchanged");
            Check(Condition(afterFailure)==condition,"Rollback preserves actual component condition and fluids");
            var stale=Copy(source);stale.cash+=999;stale.revision=afterFailure.revision+1;rejected=false;
            try{Begin(stale).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&ProfileStore.Load().cash==recovered.cash,"Stale writer cannot overwrite a completed background commit");
            log.AppendLine("Checks: "+checks+" / failures: 0");File.WriteAllText(Path.Combine(root,"recovery.txt"),log.ToString(),new UTF8Encoding(false));return log.ToString();
        }finally{injection.SetValue(null,oldInjection);ProfileStore.Flush();ProfileStore.VerificationPath=prior;}
    }
    public static string Run()
    {
        var root=Path.GetFullPath("../reports/staged-settlement-20261016");Directory.CreateDirectory(root);
        string prior=ProfileStore.VerificationPath;var log=new StringBuilder();int checks=0;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;log.AppendLine("PASS: "+text);}
        var oldMarket=Path.GetFullPath("../reports/parts-market/verification.txt");var oldBytes=File.Exists(oldMarket)?File.ReadAllBytes(oldMarket):null;
        try{
            ProfileStore.VerificationPath=Path.GetFullPath("../reports/player-fixtures/finish-stutter-safe-20261016/save-slot-1.sqlite3");var baseline=ProfileStore.Load();var catalog=baseline.catalog;
            PlayerProfile Copy(PlayerProfile p){var c=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));c.catalog=catalog;c.storageCommit=p.storageCommit;return c;}
            string Stock(PlayerProfile p)=>Regex.Replace(JsonUtility.ToJson(p.vehicleStock)+JsonUtility.ToJson(p.inventory),"[a-f0-9]{32}","<identity>");
            foreach(int seed in new[]{1,42,12345}){
                var p=Copy(baseline);p.driving.guaranteedFittingParts=seed==1?0:seed==42?5:10;VehicleListings.Initialize(p,catalog,seed);p.vehicleStock.completedRuns=2;
                var sync=Copy(p);var staged=Copy(p);string before=Stock(p);VehicleListings.AfterRace(sync,catalog);int steps=0;
                foreach(var unused in VehicleListings.AfterRaceSteps(staged,catalog)){steps++;System.Threading.Thread.Yield();}
                Check(Stock(sync)==Stock(staged),"Seed "+seed+": staged stock exactly matches synchronous definitions/prices/quantities/conditions, excluding fresh random GUID identities");
                Check(Stock(p)==before&&steps>20,"Seed "+seed+": market candidate remains isolated and yields its work");
                Check(PartsMarket.Validate(staged)==null&&VehicleListings.Validate(staged,catalog)==null,"Seed "+seed+": staged parts/cars preserve fitment, unique actual identities and market validation");
            }
            var build=MechanicsOwnership.Resolve(baseline,baseline.SelectedCar,catalog);var driver=new DriverSettings{launch=LaunchMode.Automatic,shifting=ShiftMode.Automatic,wearMode=WearMode.None,optimalThermals=true};
            var race=RaceSessionFactory.Create(build.spec,build.tune,driver,build.spec,build.tune,driver.Copy(),18.288);for(int tick=0;tick<120*50&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput{throttle=1});
            Check(race.player.state.finished,"Reward-policy fixture reaches a real shared-simulation finish");
            foreach(var evt in Resources.LoadAll<CareerEventDefinition>("Data/Events"))foreach(bool win in new[]{false,true}){
                var p=Copy(baseline);p.cash=100000-evt.entryFee;p.reputation=100000;p.vehicleStock.completedRuns=0;p.careerWins.Clear();string id="staged-terms-"+evt.stableId+win;
                p.careerRuns.Add(new CareerRun{runId=id,carId=p.SelectedCar.instanceId,eventId=evt.stableId,fee=evt.entryFee,winCash=evt.cashWin,lossCash=evt.cashLoss,reputation=evt.reputationWin,firstBonus=evt.firstWinBonus});
                var a=Copy(p);var b=Copy(p);race.playerWon=win;CareerProgression.Settle(a,id,race);CareerProgression.Settle(b,id,race,false);foreach(var unused in VehicleListings.AfterRaceSteps(b,catalog)){}
                Check(a.cash==b.cash&&a.reputation==b.reputation&&a.careerWins.SequenceEqual(b.careerWins)&&Stock(a)==Stock(b),evt.stableId+" / "+win+": staged policy matches captured synchronous fee/reward/first-win/market behavior");
                Check(b.cash==p.cash+(win?evt.cashWin+evt.firstWinBonus:evt.cashLoss)&&b.reputation==p.reputation+(win?evt.reputationWin:0),evt.stableId+" / "+win+": exact captured payout and reputation");
                int cash=b.cash;bool rejected=false;try{CareerProgression.Settle(b,id,race,false);}catch(InvalidOperationException){rejected=true;}Check(rejected&&b.cash==cash,evt.stableId+" / "+win+": duplicate settlement is rejected without another reward");
            }
            DragLab.Editor.PartsMarketVerification.Run();File.Copy(oldMarket,Path.Combine(root,"parts-market.txt"),true);
            log.AppendLine("Checks: "+checks+" / failures: 0");File.WriteAllText(Path.Combine(root,"domain.txt"),log.ToString(),new UTF8Encoding(false));return log.ToString().Split('\n').Last(s=>s.StartsWith("Checks:"));
        }finally{ProfileStore.VerificationPath=prior;if(oldBytes!=null)File.WriteAllBytes(oldMarket,oldBytes);}
    }
}
