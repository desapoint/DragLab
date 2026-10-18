using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class EconomyBalanceVerification
    {
        internal static bool SetRaceRewards(CareerEventDefinition e)
        {
            string[] opening={"first-pass","club-run","coupe-rival","local-final"};
            int intro=Array.IndexOf(opening,e.stableId);
            if(intro>=0){e.cashWin=new[]{150,225,325,450}[intro];e.cashLoss=10;e.firstWinBonus=0;return true;}
            string[] tiers={"f","e","d","c","b","a","s"};
            int[] wins={525,575,650,725,825,925,1050,1175,1325,1500,1700,1925,2175,2450};
            for(int t=0;t<tiers.Length;t++)for(int k=0;k<2;k++)if(e.stableId=="ladder-"+tiers[t]+(k==0?"-qualifier":"-rival")){
                e.cashWin=wins[t*2+k];e.cashLoss=e.entryFee/2;e.firstWinBonus=150+t*50+k*25;return true;
            }
            for(int i=0;i<3;i++)if(e.stableId=="special-"+i){e.cashWin=new[]{400,500,450}[i];e.cashLoss=e.entryFee/2;e.firstWinBonus=new[]{175,225,200}[i];return true;}
            return false;
        }
        [MenuItem("DragLab/Apply Slower Career Rewards")]
        public static void ApplyRaceRewards()
        {
            foreach(var e in Resources.LoadAll<CareerEventDefinition>("Data/Events"))if(SetRaceRewards(e))EditorUtility.SetDirty(e);
            AssetDatabase.SaveAssets();
        }
        public static void VerifyRaceRewards()
        {
            var catalog=MechanicsTransactions.Catalog();var events=Resources.LoadAll<CareerEventDefinition>("Data/Events");
            int checks=0;var report=new StringBuilder();void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);report.AppendLine("PASS: "+label);}
            int previous=0;
            foreach(var e in events.Where(e=>!e.stableId.StartsWith("special-",StringComparison.Ordinal)).OrderBy(e=>e.reputationRequired)){
                int net=e.cashWin-e.entryFee;Check(net>previous,e.stableId+": take-home repeat reward grows through the main ladder");previous=net;
                Check(e.firstWinBonus<=net,e.stableId+": first-win bonus is bounded by the repeat prize");
            }
            foreach(var e in events){
                Check(e.cashWin>e.entryFee&&e.cashLoss<e.cashWin,e.stableId+": a win is worthwhile after entry and exceeds consolation");
                Check(e.entryFee==0?e.cashLoss<=10:e.cashLoss<=e.entryFee/2,e.stableId+": losing cannot farm the former large net payouts");
                var expected=UnityEngine.Object.Instantiate(e);Check(SetRaceRewards(expected)&&expected.cashWin==e.cashWin&&expected.cashLoss==e.cashLoss&&expected.firstWinBonus==e.firstWinBonus,e.stableId+": authored values match reproducible creation defaults");UnityEngine.Object.DestroyImmediate(expected);
            }
            var first=events.Single(e=>e.stableId=="first-pass");int upgrade=catalog.Find(catalog.Id("starter-hatch",PartCategory.HeadIntake,1)).price;
            int wins=(int)Math.Ceiling(upgrade/(double)(first.cashWin-first.entryFee));Check(wins>=4&&wins<=6,"A standard early airflow upgrade needs four to six repeat wins before operating costs");
            var driver=new DriverSettings{launch=LaunchMode.Automatic,shifting=ShiftMode.Automatic,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,optimalThermals=true};
            var build=catalog.Resolve("starter-hatch",catalog.Recipe("starter-hatch"));var rivalDriver=driver.Copy();rivalDriver.reactionSeconds=40;
            var race=RaceSessionFactory.Create(build.spec,build.tune,driver,build.spec,build.tune,rivalDriver,402.336);
            for(int tick=0;tick<20000&&!race.ResultReady;tick++)race.Step(new DriverInput{throttle=1});Check(race.playerWon,"Receipt fixtures use a real completed first-place pass");
            foreach(var e in events){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=e.opponent.spec.id;p.selectedVehicleId=e.opponent.spec.id;MechanicsOwnership.Ensure(p,catalog);p.cash=10000;p.reputation=1000;
                if(!string.IsNullOrEmpty(e.prerequisite))p.careerWins.Add(e.prerequisite);
                int cash=p.cash;var captured=UnityEngine.Object.Instantiate(e);string id="reward-"+e.stableId;
                CareerProgression.Enter(p,id,p.SelectedCar.instanceId,captured,false);captured.cashWin+=9999;captured.firstWinBonus+=9999;captured.entryFee=0;
                var restored=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(restored,catalog);p=restored;
                CareerProgression.Settle(p,id,race,false);Check(p.cash==cash-e.entryFee+e.cashWin+e.firstWinBonus,e.stableId+": captured terms survive reload and later definition changes");
                int paid=p.cash;bool rejected=false;try{CareerProgression.Settle(p,id,race,false);}catch(InvalidOperationException){rejected=true;}Check(rejected&&p.cash==paid,e.stableId+": duplicate settlement cannot pay twice");
                int rep=p.reputation;CareerProgression.Enter(p,id+"-repeat",p.SelectedCar.instanceId,e,false);CareerProgression.Settle(p,id+"-repeat",race,false);Check(p.cash==paid-e.entryFee+e.cashWin&&p.reputation==rep,e.stableId+": repeats never add first-win cash or reputation again");
                UnityEngine.Object.DestroyImmediate(captured);
            }
            var after=new StringBuilder("event,fee,win,repeat_net,completed_loss,loss_net,first_bonus\n");foreach(var e in events.OrderBy(e=>e.reputationRequired).ThenBy(e=>e.stableId))after.AppendLine(string.Join(",",e.stableId,e.entryFee,e.cashWin,e.cashWin-e.entryFee,e.cashLoss,e.cashLoss-e.entryFee,e.firstWinBonus));
            Directory.CreateDirectory("../reports/race-rewards-20261017");File.WriteAllText("../reports/race-rewards-20261017/after-matrix.csv",after.ToString(),new UTF8Encoding(false));
            report.AppendLine("Checks: "+checks+" / failures: 0");File.WriteAllText("../reports/race-rewards-20261017/verification.txt",report.ToString(),new UTF8Encoding(false));
        }
        public static void AuditRaceRewards()
        {
            var catalog=MechanicsTransactions.Catalog();
            var rows=new StringBuilder("event,name,tier,rep,prerequisite,fee,win,repeat_net,loss_net,first_bonus,rival_total_min,rival_total_max,finished_attempts\n");
            foreach(var e in Resources.LoadAll<CareerEventDefinition>("Data/Events").OrderBy(e=>e.reputationRequired).ThenBy(e=>e.stableId)){
                var times=new List<double>();
                foreach(int seed in new[]{42,43}){
                    var rival=CareerOpponents.Capture(e,catalog,1,seed);var player=rival.Create();
                    player.driver.launch=LaunchMode.Automatic;player.driver.shifting=ShiftMode.Automatic;player.driver.reactionSeconds=.18;
                    var race=new RaceSimulation(player,rival.Create(),e.distanceM,rival.behavior);
                    for(int tick=0;tick<20000&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput{throttle=1});
                    if(race.opponent.state.finished)times.Add(race.opponent.state.reaction+race.opponent.state.elapsed);
                }
                string Number(double value)=>value.ToString("F3",CultureInfo.InvariantCulture);
                rows.AppendLine(string.Join(",",e.stableId,"\""+e.displayName+"\"",e.tier,e.reputationRequired,e.prerequisite,e.entryFee,e.cashWin,e.cashWin-e.entryFee,e.cashLoss-e.entryFee,e.firstWinBonus,times.Count==0?"DNF":Number(times.Min()),times.Count==0?"DNF":Number(times.Max()),times.Count));
            }
            Directory.CreateDirectory("../reports/race-rewards-20261017");
            File.WriteAllText("../reports/race-rewards-20261017/current-matrix.csv",rows.ToString(),new UTF8Encoding(false));
        }
        public static void ProbeStarterFunding()
        {
            var cat=MechanicsTransactions.Catalog();var e=Resources.LoadAll<CareerEventDefinition>("Data/Events").Single(x=>x.stableId=="club-run");
            var rows=new StringBuilder("head,tires,gearbox,clutch,launch,shift,player_total,rival_total,win\n");
            foreach(int tires in new[]{0,1,2})foreach(int launch in new[]{3200,4500}){
                var costs=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy").policy;var values=Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;var business=Resources.Load<GarageBusinessPolicyDefinition>("Data/Economy/GarageBusinessPolicy").policy;
                var sample=new Scenario("fresh-career","starter-hatch",cat,costs,values,business,new StringBuilder(),DateTime.UtcNow.Ticks);
                // Diagnostic budget only, never used by the funded acceptance scenario.
                sample.p.cash=100000;foreach(var c in new[]{PartCategory.Body,PartCategory.Gearbox,PartCategory.Clutch,PartCategory.HeadIntake,PartCategory.Tires}){
                    int rank=c==PartCategory.Tires?tires:1;if(rank>0)sample.Buy(c,rank);
                }
                var p=sample.p;
                PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Interior);PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Accessories);p.SelectedCar.tune.launchRpm=launch;var b=MechanicsOwnership.Resolve(p,p.SelectedCar,cat);var rival=CareerOpponents.Capture(e,cat);
                var race=RaceSessionFactory.CreateCaptured(b.spec,b.tune,p.driving,rival,e.distanceM,p.parts.Where(i=>i.ownerId==p.SelectedCar.instanceId||i.ownerId==p.SelectedCar.engineAssemblyId));race.player.SetFluids(p.SelectedCar.fluids);race.player.ApplyRaceTemperatures(p.driving.raceTemperatures,0,DateTime.UtcNow.Ticks);
                for(int tick=0;tick<25000&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput());
                rows.AppendLine(string.Join(",",1,tires,1,1,launch,b.tune.shiftRpm,(race.player.state.elapsed+race.player.state.reaction).ToString("F3",CultureInfo.InvariantCulture),(race.opponent.state.elapsed+race.opponent.state.reaction).ToString("F3",CultureInfo.InvariantCulture),race.playerWon));
            }
            File.WriteAllText("../reports/race-rewards-20261017/starter-diagnostic.csv",rows.ToString(),new UTF8Encoding(false));
        }
        [MenuItem("DragLab/Prepare 3G Economy Calibration")]
        public static void Prepare()
        {
            PaidOperationsVerification.Prepare();GarageBusinessVerification.Prepare();CareerExpansionVerification.Prepare();
            var a=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy");
            if(a.policy.version<2){if(a.policy.recoverySeconds==30&&a.policy.recoveryReward==500){a.policy.recoverySeconds=60;a.policy.recoveryReward=250;}a.policy.version=2;a.policy.id="balanced-3g-operating-costs";EditorUtility.SetDirty(a);AssetDatabase.SaveAssets();}
            if(!File.ReadAllText(AssetDatabase.GetAssetPath(a),Encoding.UTF8).Contains("rebuildBaseLabour:")){a.policy.rebuildBaseLabour=150;a.policy.rebuildPerCylinder=25;EditorUtility.SetDirty(a);AssetDatabase.SaveAssets();}
        }
        sealed class Scenario
        {
            public PlayerProfile p;public long now,start;public string name;public MechanicalCatalog cat;public OperatingPolicy costs;public ValuationPolicy values;public GarageBusinessPolicy business;public StringBuilder ledger;public int steps;
            public Scenario(string name,string model,MechanicalCatalog cat,OperatingPolicy costs,ValuationPolicy values,GarageBusinessPolicy business,StringBuilder ledger,long now)
            {this.name=name;this.cat=cat;this.costs=costs;this.values=values;this.business=business;this.ledger=ledger;start=this.now=now;if(name=="fresh-career"){p=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(p,cat);var starter=p.vehicleStock.listings.Single(o=>o.model==model);Pay(new PaymentRequest{kind=PaymentKind.Car,offer=starter});}else{p=PlayerProfile.New(now);p.SelectedCar.vehicleId=model;p.selectedVehicleId=model;MechanicsOwnership.Ensure(p,cat);}}
            static double Fuel(PlayerProfile p){return p.cars.Sum(c=>c.fuel.litres);}
            public void Apply(string action,Action<PlayerProfile> op)
            {
                int before=p.cash;double fuel=Fuel(p);PlayerProfile next;string error;if(!MechanicsTransactions.Try(p,cat,p.revision,op,a=>true,out next,out error))throw new InvalidOperationException(name+" / "+action+": "+error);p=next;steps++;
                ledger.AppendLine(string.Join(",",name,steps,"\""+action.Replace("\"","'")+"\"",((now-start)/(double)TimeSpan.TicksPerMinute).ToString("F3",CultureInfo.InvariantCulture),before,p.cash,p.cash-before,fuel.ToString("F6",CultureInfo.InvariantCulture),Fuel(p).ToString("F6",CultureInfo.InvariantCulture),p.parts.Count,p.parts.Count(i=>i.condition.failed),p.garageLevel,p.reputation));
            }
            public void Pay(PaymentRequest r){var q=PaidEconomy.Quote(p,cat,costs,values,r,now);var normalized=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(normalized,cat);if(JsonUtility.ToJson(p)!=JsonUtility.ToJson(normalized)){string folder=Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"reports/phases/3g");File.WriteAllText(Path.Combine(folder,"quote-before.json"),JsonUtility.ToJson(p));File.WriteAllText(Path.Combine(folder,"quote-normalized.json"),JsonUtility.ToJson(normalized));}Apply("Pay "+r.kind+" / $"+q.Cost,a=>PaidEconomy.Pay(a,cat,costs,values,q,now,r.kind==PaymentKind.Car?string.IsNullOrEmpty(r.offer.listingId)?r.offer:VehicleListings.Find(a,r.offer.listingId):null));}
            public int Price(PartCategory c,int rank)=>PaidEconomy.Preview(p,cat,costs,values,new PaymentRequest{kind=PaymentKind.Part,carId=p.SelectedCar.instanceId,targetId=cat.Id(p.SelectedCar.vehicleId,c,rank),install=true},now).cost;
            public void Buy(PartCategory c,int rank){Pay(new PaymentRequest{kind=PaymentKind.Part,carId=p.SelectedCar.instanceId,targetId=cat.Id(p.SelectedCar.vehicleId,c,rank),install=true});}
            RunCheckpoint Snapshot(string id,string owner,long tick,CarSimulation car,bool closed=false,string reason=""){var result=RunPersistence.Capture(id,owner,tick,car,closed,reason);result.thermalUtcTicks=now;return result;}
            public void RecoverTo(int cash){int guard=0;while(p.cash<cash){if(++guard>100)throw new InvalidOperationException("Unbounded recovery route.");Apply("Start recovery",a=>PaidEconomy.StartRecovery(a,costs,now));var job=p.recoveryWork;now=job.readyUtcTicks;Apply("Claim recovery",a=>PaidEconomy.ClaimRecovery(a,job.id,now));}}
            public RaceSimulation Race(CareerEventDefinition e,bool forceLoss=false)
            {
                // The longer, slower-income route must budget for real service rather
                // than assuming the old short campaign's untouched fuel/condition.
                if(name=="fresh-career"){
                    if(p.SelectedCar.fuel.litres<3)Pay(new PaymentRequest{kind=PaymentKind.Fuel,carId=p.SelectedCar.instanceId,litres=10});
                    if(p.parts.Any(i=>i.ownerId==p.SelectedCar.engineAssemblyId&&EngineRebuilds.Scope(cat.Find(i.definitionId).category)&&i.condition.health<.88f)){
                        var service=PaidEconomy.Quote(p,cat,costs,values,new PaymentRequest{kind=PaymentKind.Rebuild,carId=p.SelectedCar.instanceId},now);
                        if(p.cash>=service.Cost+30){Pay(new PaymentRequest{kind=PaymentKind.Rebuild,carId=p.SelectedCar.instanceId});var job=p.SelectedCar.engineRebuild;now=job.readyUtcTicks;Apply("Collect paid preventive rebuild",a=>EngineRebuilds.Collect(a,a.SelectedCar.instanceId,job.id,now));}
                    }
                }
                var car=p.SelectedCar;var b=MechanicsOwnership.Resolve(p,car,cat);if(!b.Drivable)throw new InvalidOperationException("Unavailable before race: "+string.Join("; ",b.errors));
                var rival=CareerOpponents.Capture(e,cat);if(forceLoss)rival.driver.reactionSeconds=0;var race=RaceSessionFactory.CreateCaptured(b.spec,b.tune,p.driving,rival,e.distanceM,p.parts.FindAll(i=>i.ownerId==car.instanceId||i.ownerId==car.engineAssemblyId));race.player.SetFluids(car.fluids);race.player.SetMassInputs(b.tune.driverMassKg,b.tune.payloadMassKg,b.tune.payloadPositionM,b.tune.payloadHeightM,car.fuel.litres);race.player.state.fuelUsedLitres=car.fuel.cumulativeUsedLitres;
                race.player.ApplyRaceTemperatures(p.driving.raceTemperatures,car.thermalUtcTicks,now);
                race.RefreshOpponentStrategy();
                if(forceLoss)race.player.driver.reactionSeconds=3;
                string id=MechanicsOwnership.NewId("scenario-run"),owner=car.instanceId;Apply("Enter "+e.stableId,a=>{CareerProgression.Enter(a,id,owner,e,false,rival.Entry(race));MechanicsTransactions.ApplyCheckpoint(a,Snapshot(id,owner,0,race.player));});int ticks=0;while(race.phase!=RacePhase.Finished&&ticks++<25000)race.Step(new DriverInput());if(ticks>=25000)throw new InvalidOperationException("Race did not terminate.");
                // Persist the same finish/shutdown/garage boundaries as the running game.
                now+=TimeSpan.FromSeconds(race.time).Ticks;Apply("Settle "+e.stableId+" / "+(race.playerWon?"win":race.player.state.finished?"loss":"DNF"),a=>{MechanicsTransactions.ApplyCheckpoint(a,Snapshot(id,owner,ticks,race.player));CareerProgression.Settle(a,id,race);});
                int shutdown=0;while(race.player.state.speed>.1&&shutdown++<6000){race.Shutdown(new DriverInput());ticks++;}now+=TimeSpan.FromSeconds(shutdown*DragMath.FixedStep).Ticks;
                Apply("Shutdown / garage",a=>MechanicsTransactions.ApplyCheckpoint(a,Snapshot(id,owner,ticks,race.player,true,"Scenario returned to garage")));
                if(!race.playerWon){string folder=Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"reports/phases/3g");File.WriteAllText(Path.Combine(folder,name+"-loss-profile.json"),JsonUtility.ToJson(p));File.WriteAllText(Path.Combine(folder,name+"-loss-diagnostic.txt"),"Player ET "+race.player.state.elapsed+" / opponent ET "+race.opponent.state.elapsed+" / player reaction "+race.player.state.reaction+" / opponent reaction "+race.opponent.state.reaction+" / engine failed "+race.player.state.engineFailed+" / drivetrain failed "+race.player.state.drivetrainFailed+"\n"+string.Join("\n",p.parts.Where(i=>car.installed.slots.Any(b=>b.partInstanceId==i.instanceId)).Select(i=>i.slot+" / health "+i.condition.health+" / failed "+i.condition.failed+" / "+i.condition.failureReason)));}return race;
            }
        }
        [MenuItem("DragLab/Verify Full Economy Scenarios")]
        public static void RunStandalone()
        {
            var policyAsset=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy");
            if(policyAsset==null||policyAsset.policy==null||policyAsset.policy.version<2)throw new InvalidOperationException("Economy policy assets need preparation. Run DragLab/Prepare 3G Economy Calibration explicitly, then verify.");
            var cat=MechanicsTransactions.Catalog();var costs=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy").policy;var values=Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;var business=Resources.Load<GarageBusinessPolicyDefinition>("Data/Economy/GarageBusinessPolicy").policy;var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var events=Resources.LoadAll<CareerEventDefinition>("Data/Events");long now=DateTime.UtcNow.Ticks;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"reports/phases/3g","validation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var log=new StringBuilder();var ledger=new StringBuilder("scenario,step,action,elapsed_minutes,cash_before,cash_after,delta,fuel_before,fuel_after,parts,failed_packs,garage_level,rep\n");var market=new StringBuilder("model,dealer,best_car,best_engine_plus_chassis,margin\n");int checks=0;
            // Per-run append-only progress avoids repeatedly truncating CSVs that
            // Windows report viewers/indexers can still have memory-mapped.
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);File.AppendAllText(Path.Combine(folder,"verification-progress.txt"),"PASS: "+label+"\n",Encoding.UTF8);};
            Func<string,string,Scenario> scenario=(name,model)=>new Scenario(name,model,cat,costs,values,business,ledger,now);
            Func<VehicleDefinition,VehicleOffer> offer=v=>new VehicleOffer{model=v.spec.id,price=v.purchasePrice,reputation=v.reputationRequired,stock=v.CreateStockTune()};
            var opening=events.First(e=>e.stableId=="first-pass");
            var earned=scenario("fresh-career","starter-hatch");int attempts=0;
            earned.Apply("Strip optional cabin mass; retain original components",p=>{PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Interior);PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Accessories);});
            check(earned.Race(opening).playerWon,"Controlled paid-starter funding scenario wins against its recorded driver seed.");earned.Buy(PartCategory.Body,1);
            int StarterBudget(){var map=MechanicsOwnership.Definitions(earned.p,earned.p.SelectedCar);return new[]{PartCategory.HeadIntake,PartCategory.Tires,PartCategory.Body,PartCategory.Gearbox}.Where(c=>map[c]!=cat.Id("starter-hatch",c,3)).Sum(c=>earned.Price(c,3))+(map[PartCategory.Clutch]==cat.Id("starter-hatch",PartCategory.Clutch,1)?0:earned.Price(PartCategory.Clutch,1))+500;}
            int starterBudget=StarterBudget();var club=events.First(e=>e.stableId=="club-run");bool triedClub=false,launchTuned=false;
            int starterAttempts=(int)Math.Ceiling((starterBudget+3600)/(double)(opening.cashWin-opening.entryFee))+10;
            while(earned.p.cash<StarterBudget()){
                var fitted=MechanicsOwnership.Definitions(earned.p,earned.p.SelectedCar);
                if(earned.p.cash>=earned.Price(PartCategory.Gearbox,1)&&fitted[PartCategory.Gearbox]==cat.Id("starter-hatch",PartCategory.Gearbox,0))earned.Buy(PartCategory.Gearbox,1);
                else if(fitted[PartCategory.Gearbox]!=cat.Id("starter-hatch",PartCategory.Gearbox,0)&&earned.p.cash>=earned.Price(PartCategory.Clutch,1)&&fitted[PartCategory.Clutch]==cat.Id("starter-hatch",PartCategory.Clutch,0))earned.Buy(PartCategory.Clutch,1);
                else if(earned.p.cash>=earned.Price(PartCategory.HeadIntake,1)&&fitted[PartCategory.Gearbox]!=cat.Id("starter-hatch",PartCategory.Gearbox,0)&&fitted[PartCategory.HeadIntake]==cat.Id("starter-hatch",PartCategory.HeadIntake,0))earned.Buy(PartCategory.HeadIntake,1);
                else if((earned.p.careerWins.Contains(club.stableId)||fitted[PartCategory.Gearbox]==cat.Id("starter-hatch",PartCategory.Gearbox,3))&&fitted[PartCategory.HeadIntake]!=cat.Id("starter-hatch",PartCategory.HeadIntake,3)&&earned.p.cash>=earned.Price(PartCategory.HeadIntake,3)){earned.Buy(PartCategory.HeadIntake,3);triedClub=false;}
                else if(earned.p.cash>=earned.Price(PartCategory.Gearbox,3)&&triedClub&&!earned.p.careerWins.Contains(club.stableId)&&fitted[PartCategory.Gearbox]!=cat.Id("starter-hatch",PartCategory.Gearbox,3)){earned.Buy(PartCategory.Gearbox,3);triedClub=false;}
                else if(earned.p.cash>=earned.Price(PartCategory.Body,1)&&triedClub&&!earned.p.careerWins.Contains(club.stableId)&&fitted[PartCategory.Body]==cat.Id("starter-hatch",PartCategory.Body,0)){earned.Buy(PartCategory.Body,1);triedClub=false;}
                fitted=MechanicsOwnership.Definitions(earned.p,earned.p.SelectedCar);
                bool ready=fitted[PartCategory.HeadIntake]!=cat.Id("starter-hatch",PartCategory.HeadIntake,0)&&fitted[PartCategory.Clutch]!=cat.Id("starter-hatch",PartCategory.Clutch,0)&&fitted[PartCategory.Gearbox]!=cat.Id("starter-hatch",PartCategory.Gearbox,0);
                if(ready&&!launchTuned){earned.Apply("Apply supported launch target",p=>{var draft=new TuneDraft(p.SelectedCar,p.revision);draft.values.launchRpm=4500;draft.ApplyTo(p);});launchTuned=true;}
                var funding=earned.p.careerWins.Contains(club.stableId)||ready&&!triedClub?club:opening;if(funding==club)triedClub=true;
                var pass=earned.Race(funding);check(pass.player.state.finished&&!pass.player.state.disqualified,"Incremental funding advances to the more profitable verified event while retaining wear and costs.");
                if(++attempts>starterAttempts)throw new InvalidOperationException("Starter funding stalled under the authored income budget.");
            }
            if(MechanicsOwnership.Definitions(earned.p,earned.p.SelectedCar)[PartCategory.Clutch]!=cat.Id("starter-hatch",PartCategory.Clutch,1))earned.Buy(PartCategory.Clutch,1);
            foreach(var c in new[]{PartCategory.HeadIntake,PartCategory.Tires,PartCategory.Body,PartCategory.Gearbox})if(MechanicsOwnership.Definitions(earned.p,earned.p.SelectedCar)[c]!=cat.Id("starter-hatch",c,3))earned.Buy(c,3);
            earned.Apply("Apply tested starter gearing / launch",p=>{var draft=new TuneDraft(p.SelectedCar,p.revision);draft.values.finalDrive=3.4f;draft.values.launchRpm=5400;draft.ApplyTo(p);});
            foreach(string id in new[]{"club-run","coupe-rival"})check(earned.Race(events.First(e=>e.stableId==id)).playerWon,"Earned direct-rank starter progresses "+id);
            var hero=vehicles.First(v=>v.spec.id=="v8-muscle");int heroPrice=PaidEconomy.DealerPrice(cat,costs,values,offer(hero));
            int firstHeroUpgrades=(int)Math.Ceiling((cat.Find(cat.Id(hero.spec.id,PartCategory.HeadIntake,1)).price+cat.Find(cat.Id(hero.spec.id,PartCategory.Tires,2)).price)*1.1);
            var fundingRival=events.First(e=>e.stableId=="coupe-rival");int heroAttempts=(int)Math.Ceiling(Math.Max(0,heroPrice+firstHeroUpgrades+500-earned.p.cash)/(double)(fundingRival.cashWin-fundingRival.entryFee))+10;
            attempts=0;while(earned.p.cash<heroPrice+firstHeroUpgrades+500){check(earned.Race(fundingRival).playerWon,"Reliable higher-paying cleared rival funds the second build.");if(++attempts>heroAttempts)throw new InvalidOperationException("Hero funding stalled under the authored income budget.");}
            earned.Pay(new PaymentRequest{kind=PaymentKind.Car,offer=offer(hero)});earned.Buy(PartCategory.HeadIntake,1);earned.Buy(PartCategory.Tires,2);
            check(earned.Race(events.First(e=>e.stableId=="local-final")).playerWon,"Funded second build wins local championship.");
            // The second build already paid for rank-2 tires before racing; fully
            // cooled D/C starts cannot inherit warm tires as an invisible benefit.
            bool strengthened=false;
            foreach(var e in events.Where(e=>e.stableId.StartsWith("ladder-")).OrderBy(e=>e.reputationRequired)){
                if(!strengthened&&e.reputationRequired>=44){while(earned.p.cash<new[]{PartCategory.HeadIntake,PartCategory.Tires,PartCategory.Body,PartCategory.Gearbox}.Sum(c=>earned.Price(c,3))+new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.Clutch,PartCategory.FuelSystem,PartCategory.Ignition}.Sum(c=>earned.Price(c,1))+300)check(earned.Race(events.First(x=>x.stableId=="ladder-c-rival")).playerWon,"Earlier tier funds safe high-output build.");
                    foreach(var c in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.Clutch,PartCategory.FuelSystem,PartCategory.Ignition})earned.Buy(c,1);
                    foreach(var c in new[]{PartCategory.HeadIntake,PartCategory.Tires,PartCategory.Body,PartCategory.Gearbox})earned.Buy(c,3);
                    earned.Apply("Strip optional interior/accessories; retain originals",p=>{PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Interior);PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Accessories);});strengthened=true;}
                check(earned.Race(e).playerWon,"Funded qualification/rival path: "+e.stableId);
            }
            double used=earned.p.SelectedCar.fuel.cumulativeUsedLitres;earned.Pay(new PaymentRequest{kind=PaymentKind.Fuel,carId=earned.p.SelectedCar.instanceId,litres=100});check(earned.p.careerWins.Count>=18&&earned.p.reputation>=68&&earned.p.cash>0&&used>0,"Fresh-save full main ladder viable without injected income or physical resource bypass.");
            var loss=scenario("repeated-loss","starter-hatch");int cash=loss.p.cash;for(int i=0;i<8;i++)check(!loss.Race(opening,true).playerWon&&loss.p.careerRuns.Last().outcome=="Loss","Completed real loss retains records and authored consolation.");check(loss.p.cash==cash+opening.cashLoss*8&&loss.p.reputation==0&&loss.p.SelectedCar.fuel.cumulativeUsedLitres>0,"Loss route preserves physical costs without granting REP.");
            var low=scenario("low-fuel-recovery","starter-hatch");low.p.cash=0;low.p.SelectedCar.fuel.litres=0;string partsBefore=string.Join("",low.p.parts.Select(i=>JsonUtility.ToJson(i.condition)));low.RecoverTo(2);low.Pay(new PaymentRequest{kind=PaymentKind.Fuel,carId=low.p.SelectedCar.instanceId,litres=1});check(low.p.SelectedCar.fuel.litres==1&&partsBefore==string.Join("",low.p.parts.Select(i=>JsonUtility.ToJson(i.condition)))&&low.Race(opening).playerWon,"Zero cash/tank recovers via bounded work and paid small fill; no parts healed.");
            var abuse=scenario("abuse-repair","starter-hatch");var crank=abuse.p.parts.Find(i=>i.instanceId==abuse.p.SelectedCar.installed.slots.Find(s=>s.slot=="Crankshaft").partInstanceId);crank.condition.health=.00000001f;abuse.p.driving.wearMultiplier=1000;string worn=crank.instanceId;int pre=abuse.p.cash;var broken=abuse.Race(opening);check(broken.player.state.engineFailed&&abuse.p.careerRuns.Last().outcome.Contains("DNF")&&abuse.p.cash==pre,"Accelerated end-of-life abuse produces actual mechanical DNF without payout.");
            abuse.p.driving.wearMultiplier=1;foreach(var i in abuse.p.parts.Where(i=>abuse.p.SelectedCar.installed.slots.Any(s=>s.partInstanceId==i.instanceId)&&(i.condition.failed||i.condition.health<=0)).ToArray()){var d=cat.Find(i.definitionId);abuse.RecoverTo(d.price);abuse.Pay(new PaymentRequest{kind=PaymentKind.Part,carId=abuse.p.SelectedCar.instanceId,targetId=d.id,install=true});}check(abuse.p.parts.Find(i=>i.instanceId==worn).location==PartLocation.Inventory&&abuse.p.parts.Find(i=>i.instanceId==worn).condition.failed&&abuse.Race(opening).playerWon,"Paid repair/replacement preserves failed original and restores a real winning car.");
            var reuse=scenario("downgrade-reuse","starter-hatch");reuse.Buy(PartCategory.HeadIntake,1);string old=reuse.p.parts.First(i=>i.location==PartLocation.Inventory&&cat.Find(i.definitionId).category==PartCategory.HeadIntake).instanceId;cash=reuse.p.cash;reuse.Apply("Reuse original OEM intake",p=>PartOperations.Install(p,cat,p.SelectedCar.instanceId,old));check(reuse.p.cash==cash&&reuse.p.SelectedCar.tune.engineLevel==0&&reuse.p.parts.Find(i=>i.instanceId==old).ownerId==reuse.p.SelectedCar.engineAssemblyId,"Downgrade/reuse costs no cash and restores actual OEM part identity.");
            var rebuilt=scenario("profitable-rebuild-sale","starter-hatch");var failed=rebuilt.p.parts.Find(i=>i.instanceId==rebuilt.p.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);failed.condition.health=0;failed.condition.failed=true;failed.condition.failureReason="Known damaged acquisition fixture";foreach(var element in failed.condition.elements){element.health=0;element.fault=ElementFault.Disabled;}
            int damaged=values.buyers.Max(b=>Valuations.Capture(rebuilt.p,cat,values,ValuationKind.Engine,rebuilt.p.SelectedCar.engineAssemblyId,b.id,rebuilt.now).Total);var rebuildQuote=PaidEconomy.Quote(rebuilt.p,cat,costs,values,new PaymentRequest{kind=PaymentKind.Rebuild,carId=rebuilt.p.SelectedCar.instanceId},rebuilt.now);rebuilt.RecoverTo(rebuildQuote.Cost);rebuilt.Pay(new PaymentRequest{kind=PaymentKind.Rebuild,carId=rebuilt.p.SelectedCar.instanceId});var rebuildJob=rebuilt.p.SelectedCar.engineRebuild;rebuilt.now=rebuildJob.readyUtcTicks;rebuilt.Apply("Collect timed paid rebuild",p=>EngineRebuilds.Collect(p,p.SelectedCar.instanceId,rebuildJob.id,rebuilt.now));
            var engineSale=values.buyers.Select(b=>Valuations.Capture(rebuilt.p,cat,values,ValuationKind.Engine,rebuilt.p.SelectedCar.engineAssemblyId,b.id,rebuilt.now)).OrderByDescending(q=>q.Total).First();File.WriteAllText(Path.Combine(folder,"rebuild-margin-diagnostic.txt"),"Damaged offer $"+damaged+" / rebuilt $"+engineSale.Total+" / paid rebuild $"+rebuildQuote.Cost+" / filter $"+cat.Find(cat.Id("starter-hatch",PartCategory.OilFilter)).price+"\n");check(engineSale.Total>damaged+rebuildQuote.Cost,"Actual timed rebuild increases owned damaged engine sale value beyond paid cost.");rebuilt.Apply("Sell rebuilt assembly",p=>PaidEconomy.Sell(p,cat,values,engineSale,rebuilt.now));check(string.IsNullOrEmpty(rebuilt.p.SelectedCar.engineAssemblyId)&&rebuilt.p.parts.Find(i=>i.instanceId==failed.instanceId).location==PartLocation.Inventory,"Assembly sale retains chassis/fuel and replaced failed pack.");
            var idle=scenario("idle-reinvestment","starter-hatch");idle.p.cash=0;idle.now+=8*TimeSpan.TicksPerHour;idle.Apply("Claim capped overnight income",p=>GarageBusiness.Claim(p,business,idle.now));idle.Buy(PartCategory.HeadIntake,1);check(idle.p.cash==200&&idle.p.SelectedCar.tune.engineLevel==1,"Eight-hour capped income funds a real $600 component.");
            var empty=scenario("salvage-recovery","starter-hatch");string retained=empty.p.SelectedCar.engineAssemblyId;var chassisQuote=Valuations.Capture(empty.p,cat,values,ValuationKind.Chassis,empty.p.SelectedCar.chassisId,"recycler",empty.now);empty.Apply("Salvage chassis / retain full engine",p=>PaidEconomy.Sell(p,cat,values,chassisQuote,empty.now));empty.p.cash=0;var cheapest=vehicles.Where(v=>v.reputationRequired==0).OrderBy(v=>PaidEconomy.DealerPrice(cat,costs,values,offer(v))).First();int replacement=PaidEconomy.DealerPrice(cat,costs,values,offer(cheapest));check(replacement<=costs.recoveryCashLimit,"Lowest unrestricted replacement remains below recovery ceiling.");empty.RecoverTo(replacement);empty.Pay(new PaymentRequest{kind=PaymentKind.Car,offer=offer(cheapest)});check(empty.p.cars.Count==1&&empty.p.engines.Any(e=>e.instanceId==retained)&&MechanicsOwnership.Resolve(empty.p,empty.p.SelectedCar,cat).Drivable,"Empty garage funds paid replacement without losing retained engine.");
            foreach(var v in vehicles){var s=scenario("market-"+v.spec.id,v.spec.id);var c=s.p.SelectedCar;int dealer=PaidEconomy.DealerPrice(cat,costs,values,offer(v));int car=values.buyers.Max(b=>Valuations.Capture(s.p,cat,values,ValuationKind.Car,c.instanceId,b.id,s.now).Total);int engine=values.buyers.Max(b=>Valuations.Capture(s.p,cat,values,ValuationKind.Engine,c.engineAssemblyId,b.id,s.now).Total);int road=values.buyers.Max(b=>Valuations.Capture(s.p,cat,values,ValuationKind.Chassis,c.chassisId,b.id,s.now).Total);market.AppendLine(string.Join(",",v.spec.id,dealer,car,engine+road,dealer-Math.Max(car,engine+road)));check(dealer>Math.Max(car,engine+road),"No instantaneous new-car/assembly-part-out trade profit: "+v.spec.id);}
            foreach(var d in cat.parts){double max=values.buyers.Max(b=>d.price*values.usedPartFraction*b.parts*values.qualities.Where(q=>q.quality==d.quality).Select(q=>q.factor).DefaultIfEmpty(1).First());check(d.price>=max,"No same-definition new/loose sale profit: "+d.id);}
            var fuelMatrix=new StringBuilder("model,mode,used_litres,finish_et\n");
            foreach(var v in vehicles){double realistic=0,reduced=0;
                foreach(var mode in new[]{FuelUsageMode.Realistic,FuelUsageMode.Reduced,FuelUsageMode.None}){var s=scenario("fuel-"+v.spec.id+"-"+mode,v.spec.id);s.p.driving.fuelUsage=mode;var pass=s.Race(opening);double consumed=s.p.SelectedCar.fuel.cumulativeUsedLitres;fuelMatrix.AppendLine(v.spec.id+","+mode+","+consumed.ToString("F8",CultureInfo.InvariantCulture)+","+pass.player.state.elapsed.ToString("F3",CultureInfo.InvariantCulture));if(mode==FuelUsageMode.Realistic)realistic=consumed;else if(mode==FuelUsageMode.Reduced)reduced=consumed;else check(consumed==0&&s.p.SelectedCar.fuel.litres==cat.Spec(v.spec.id).fuel.capacityLitres,"None fuel usage preserves real tank mass: "+v.spec.id);}
                check(realistic>0&&reduced/realistic>.23&&reduced/realistic<.27,"Reduced consumption remains approximately 25% through actual race/shutdown: "+v.spec.id);
            }
            File.WriteAllText(Path.Combine(folder,"fuel-mode-matrix.csv"),fuelMatrix.ToString(),Encoding.UTF8);
            string owner=earned.p.SelectedCar.instanceId;earned.Apply("Assign previously raced build",p=>GarageBusiness.Assign(p,business,owner,"photo-session",earned.now));var reservedJob=earned.p.business.jobs.Single();
            for(int i=0;i<20;i++)earned.Apply("Independent save/transaction while job active",p=>{});
            check(GarageBusiness.Validate(earned.p)==null,"Previously raced job snapshot survives 20 JSON transaction roundtrips without false wear/quantity change.");
            earned.now=reservedJob.readyUtcTicks;earned.Apply("Claim previously raced reserved build",p=>GarageBusiness.Finish(p,reservedJob.id,earned.now));
            var payment=PaidEconomy.Quote(earned.p,cat,costs,values,new PaymentRequest{kind=PaymentKind.Part,carId=owner,targetId=cat.Id("v8-muscle",PartCategory.OilFilter)},earned.now);
            foreach(string change in new[]{"cash","fuel","health","failure"}){string before=JsonUtility.ToJson(earned.p);PlayerProfile next;string error;bool ok=MechanicsTransactions.Try(earned.p,cat,earned.p.revision,p=>{if(change=="cash")p.cash--;else if(change=="fuel")p.SelectedCar.fuel.litres-=.1;else if(change=="health")p.parts[0].condition.health-=.01f;else{p.parts[0].condition.failed=true;p.parts[0].condition.failureReason="Changed after capture";}PaidEconomy.Pay(p,cat,costs,values,payment,earned.now);},p=>true,out next,out error);check(!ok&&JsonUtility.ToJson(earned.p)==before,"Numeric snapshot fix still rejects meaningful same-revision "+change+" change atomically.");}
            string fixture=Path.Combine(folder,"profile-roundtrip-fixture.json");ProfileStore.VerificationPath=fixture;try{ProfileStore.Save(earned.p);var reload=ProfileStore.Load();check(reload.cash==earned.p.cash&&reload.careerWins.Count==earned.p.careerWins.Count&&MechanicsOwnership.Validate(reload,cat)==null,"Funded full-loop profile survives real disk persistence with old clock guard.");}finally{ProfileStore.VerificationPath=null;}
            costs.Validate();business.Validate();File.WriteAllText(Path.Combine(folder,"market-matrix.csv"),market.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(folder,"economy-ledger-matrix.csv"),ledger.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+checks+"\nLedger rows: "+ledger.ToString().Split('\n').Length+"\n",Encoding.UTF8);Debug.Log("FULL ECONOMY PASS: "+checks);
        }
    }
}
