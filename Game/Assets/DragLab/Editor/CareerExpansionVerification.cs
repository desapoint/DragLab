using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DriveType = DragLab.Domain.DriveType;
namespace DragLab.Editor
{
    public static class CareerExpansionVerification
    {
        [MenuItem("DragLab/Prepare Expanded Career")]
        public static void Prepare()
        {
            var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            string[] models={"starter-hatch","starter-hatch","sport-coupe","utility-pickup","v8-muscle","v8-muscle","v8-muscle"};
            string previous="local-final";string[] tiers={"F","E","D","C","B","A","S"};
            for(int t=0;t<7;t++)for(int k=0;k<2;k++){
                string id="ladder-"+tiers[t].ToLowerInvariant()+(k==0?"-qualifier":"-rival");
                string path="Assets/DragLab/Resources/Data/Events/"+(10+t*2+k)+"-"+id+".asset";
                if(AssetDatabase.LoadAssetAtPath<CareerEventDefinition>(path)!=null){previous=id;continue;}
                var e=ScriptableObject.CreateInstance<CareerEventDefinition>();e.stableId=id;e.tier=tiers[t];e.eventKind=k==0?"Qualification":"Rival";e.displayName=tiers[t]+" "+e.eventKind;e.rivalName=new[]{"Mika","Jules","Ash","Reed","Rowan","Vale","Morgan"}[t];e.prerequisite=previous;e.reputationRequired=12+t*8+k*4;
                e.opponent=cars.First(v=>v.spec.id==models[t]);e.opponentTune=e.opponent.CreateStockTune();e.opponentTune.engineLevel=k==0?0:Math.Min(3,t/2);e.opponentTune.tireLevel=Math.Min(3,t/2);e.opponentTune.weightLevel=Math.Min(3,t/3);
                e.entryFee=50+t*25;e.cashWin=700+t*200;e.cashLoss=100+t*30;e.reputationWin=4;e.firstWinBonus=300+t*100;
                AssetDatabase.CreateAsset(e,path);previous=id;
            }
            for(int i=0;i<3;i++){
                string path="Assets/DragLab/Resources/Data/Events/30-special-"+i+".asset";if(AssetDatabase.LoadAssetAtPath<CareerEventDefinition>(path)!=null)continue;
                var e=ScriptableObject.CreateInstance<CareerEventDefinition>();e.stableId="special-"+i;e.tier="C";e.eventKind="Restricted challenge";e.displayName=new[]{"Front-drive club / under 2 litres","Turbo coupe showcase","Heavy AWD exhibition"}[i];e.rivalName="Reed";e.opponent=cars.First(v=>v.spec.id==new[]{"starter-hatch","sport-coupe","utility-pickup"}[i]);e.opponentTune=e.opponent.CreateStockTune();e.reputationRequired=12;e.prerequisite="local-final";e.restrictDrive=true;e.requiredDrive=new[]{DriveType.FWD,DriveType.RWD,DriveType.AWD}[i];e.maximumDisplacementL=i==0?2:0;e.turboOnly=i==1;e.minimumMassKg=i==2?1700:0;e.cashWin=950;e.cashLoss=150;e.firstWinBonus=400;e.reputationWin=2;e.entryFee=75;AssetDatabase.CreateAsset(e,path);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("DragLab/Verify Expanded Career")]
        public static void RunStandalone()
        {
            Prepare();var cat=MechanicsTransactions.Catalog();var events=Resources.LoadAll<CareerEventDefinition>("Data/Events").OrderBy(e=>e.reputationRequired).ThenBy(e=>e.stableId).ToArray();long now=DateTime.UtcNow.Ticks;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"reports/phases/3e");Directory.CreateDirectory(folder);var log=new StringBuilder();var matrix=new StringBuilder("model,event,eligible,reason\n");int checks=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(folder,"verification-progress.txt"),log.ToString(),Encoding.UTF8);};
            Func<string,PlayerProfile> fresh=model=>{var p=PlayerProfile.New(now);p.SelectedCar.vehicleId=model;p.selectedVehicleId=model;MechanicsOwnership.Ensure(p,cat);return p;};
            Func<PlayerProfile,Action<PlayerProfile>,PlayerProfile> commit=(p,op)=>{PlayerProfile next;string err;if(!MechanicsTransactions.Try(p,cat,p.revision,op,q=>true,out next,out err))throw new InvalidOperationException(err);return next;};
            Action<PlayerProfile,Action<PlayerProfile>,string> reject=(p,op,label)=>{string old=JsonUtility.ToJson(p);PlayerProfile next;string err;check(!MechanicsTransactions.Try(p,cat,p.revision,op,q=>true,out next,out err)&&old==JsonUtility.ToJson(p),label);};
            check(events.Length==21&&events.Select(e=>e.stableId).Distinct().Count()==21,"21 unique events retain all four legacy IDs.");
            check(events.Where(e=>e.stableId.StartsWith("ladder-")).Select(e=>e.tier).Distinct().Count()==7,"Seven authored tiers each have qualification and rival.");
            foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
                var p=fresh(v.spec.id);p.cash=100000;p.reputation=99;p.careerWins=events.Select(e=>e.stableId).ToList();
                foreach(var e in events){string err=CareerProgression.EntryError(p,p.SelectedCar,e);matrix.AppendLine(v.spec.id+","+e.stableId+","+(err==null)+",\""+(err??"Ready").Replace("\"","'")+"\"");if(!e.restrictDrive)check(err==null,"Installed stock entry: "+v.spec.id+" / "+e.stableId);}
                var restricted=events.First(e=>e.stableId=="special-0");check((CareerProgression.EntryError(p,p.SelectedCar,restricted)==null)==(v.spec.drive==DriveType.FWD&&v.spec.DisplacementLitres<=2),"Actual drive/displacement restriction: "+v.spec.id);
                PartOperations.Remove(p,p.SelectedCar.instanceId,PartCategory.Pistons);check(CareerProgression.EntryError(p,p.SelectedCar,events[0])!=null,"Incomplete engine blocks entry: "+v.spec.id);
            }
            var starter=fresh("starter-hatch");var first=events.First(e=>e.stableId=="first-pass");var locked=events.First(e=>e.stableId=="ladder-f-qualifier");check(CareerProgression.EntryError(starter,starter.SelectedCar,locked)!=null,"Fresh profile cannot skip qualification/REP.");
            var passes=new StringBuilder("model,event,player_et,rival_et,hp,won\n");
            foreach(string model in new[]{"v8-muscle","super-v8"})foreach(var e in events.Where(e=>e.stableId.StartsWith("ladder-"))){
                var buildProfile=fresh(model);buildProfile.cash=200000;buildProfile.reputation=99;
                foreach(var slot in new[]{PartCategory.HeadIntake,PartCategory.Tires,PartCategory.Body,PartCategory.Gearbox})PartOperations.Buy(buildProfile,cat,buildProfile.SelectedCar.instanceId,cat.Id(model,slot,3),true);
                foreach(var slot in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.Clutch,PartCategory.FuelSystem,PartCategory.Ignition})PartOperations.Buy(buildProfile,cat,buildProfile.SelectedCar.instanceId,cat.Id(model,slot,1),true);
                PartOperations.Remove(buildProfile,buildProfile.SelectedCar.instanceId,PartCategory.Interior);PartOperations.Remove(buildProfile,buildProfile.SelectedCar.instanceId,PartCategory.Accessories);MechanicsOwnership.Ensure(buildProfile,cat);
                var b=MechanicsOwnership.Resolve(buildProfile,buildProfile.SelectedCar,cat);var duel=RaceSessionFactory.CreateCaptured(b.spec,b.tune,buildProfile.driving,CareerOpponents.Capture(e,cat),e.distanceM,buildProfile.parts.FindAll(i=>i.ownerId==buildProfile.SelectedCar.instanceId||i.ownerId==buildProfile.SelectedCar.engineAssemblyId));int limit=0;while(duel.phase!=RacePhase.Finished&&limit++<20000)duel.Step(new DriverInput());
                passes.AppendLine(model+","+e.stableId+","+duel.player.state.elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+duel.opponent.state.elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+DragMath.PeakHorsepower(b.spec,b.tune).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+","+duel.playerWon);check(duel.playerWon,"Shared simulation viable build: "+model+" / "+e.stableId);
            }
            File.WriteAllText(Path.Combine(folder,"career-build-matrix.csv"),passes.ToString(),Encoding.UTF8);
            starter.cash=20000;var specialist=cat.parts.First(d=>d.reputationRequired>0&&cat.Fits(cat.Spec("starter-hatch"),d));reject(starter,p=>PartOperations.Buy(p,cat,p.SelectedCar.instanceId,specialist.id,true),"Locked new offer cannot be bought through domain.");
            PartOperations.Buy(starter,cat,starter.SelectedCar.instanceId,cat.Id("starter-hatch",PartCategory.HeadIntake,3),true);check(MechanicsOwnership.Definitions(starter,starter.SelectedCar)[PartCategory.HeadIntake]==cat.Id("starter-hatch",PartCategory.HeadIntake,3),"Direct highest mechanical rank purchase requires no preceding rank.");
            starter.reputation=8;PartOperations.Buy(starter,cat,starter.SelectedCar.instanceId,specialist.id,true);check(starter.parts.Any(i=>i.definitionId==specialist.id),"Independent specialist offer unlocks at REP 8.");
            var p0=fresh("starter-hatch");for(int n=0;n<2;n++){
                var b=MechanicsOwnership.Resolve(p0,p0.SelectedCar,cat);var race=RaceSessionFactory.CreateCaptured(b.spec,b.tune,p0.driving,CareerOpponents.Capture(first,cat),first.distanceM,p0.parts.FindAll(i=>i.ownerId==p0.SelectedCar.instanceId||i.ownerId==p0.SelectedCar.engineAssemblyId));race.player.SetFluids(p0.SelectedCar.fluids);race.player.state.fuelUsedLitres=p0.SelectedCar.fuel.cumulativeUsedLitres;race.player.SetMassInputs(b.tune.driverMassKg,b.tune.payloadMassKg,b.tune.payloadPositionM,b.tune.payloadHeightM,p0.SelectedCar.fuel.litres);
                string run="career-test-"+n,car=p0.SelectedCar.instanceId;int cash=p0.cash,rep=p0.reputation;p0=commit(p0,p=>{CareerProgression.Enter(p,run,car,first,false);MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(run,car,0,race.player));});int ticks=0;while(race.phase!=RacePhase.Finished&&ticks++<20000)race.Step(new DriverInput());check(race.playerWon,"Starter real shared simulation wins opening race.");
                string before=JsonUtility.ToJson(p0);PlayerProfile failed;string fail;check(!MechanicsTransactions.Try(p0,cat,p0.revision,p=>{MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(run,car,ticks,race.player,true));CareerProgression.Settle(p,run,race);},q=>false,out failed,out fail)&&before==JsonUtility.ToJson(p0),"Failed final write rolls back reward, records and resources together.");
                p0=commit(p0,p=>{MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(run,car,ticks,race.player,true));CareerProgression.Settle(p,run,race);});check(p0.cash==cash+first.cashWin&&p0.reputation==rep+(n==0?first.reputationWin:0)&&p0.SelectedCar.completedRuns==n+1,"Win/repeat settle once and retain records/wear/fuel.");reject(p0,p=>CareerProgression.Settle(p,run,race),"Duplicate result rejected without mutation.");
            }
            foreach(string outcome in new[]{"loss","dnf","redlight","practice","interrupted"}){
                var p=fresh("starter-hatch");var b=MechanicsOwnership.Resolve(p,p.SelectedCar,cat);var race=new RaceSimulation(new CarSimulation(b.spec,b.tune,p.driving),new CarSimulation(first.opponent.spec,first.opponentTune,new DriverSettings()),402.336);string id="case-"+outcome;CareerProgression.Enter(p,id,p.SelectedCar.instanceId,first,outcome=="practice");race.phase=RacePhase.Finished;race.player.state.finished=outcome=="loss"||outcome=="practice";race.player.state.disqualified=outcome=="redlight";
                int cash=p.cash;if(outcome=="interrupted")CareerProgression.Interrupted(p,id);else CareerProgression.Settle(p,id,race);check(p.cash==cash+(outcome=="loss"?first.cashLoss:0)&&p.reputation==0&&p.careerRuns.Last().complete,"Outcome ledger / no invented rewards: "+outcome);
            }
            var emptyCareer=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(emptyCareer,cat);
                check(emptyCareer.careerWins.Count==0&&emptyCareer.careerRuns.Count==0&&emptyCareer.cars.Count==0,"New-game career starts empty without invented progress or owned assets.");
            File.WriteAllText(Path.Combine(folder,"career-matrix.csv"),matrix.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+checks+"\n",Encoding.UTF8);Debug.Log("EXPANDED CAREER PASS: "+checks);
        }
    }
}
