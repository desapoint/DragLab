using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Data;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain
{
    [Serializable] public sealed class CareerRun
    {
        public string runId, carId, eventId, outcome;
        public int winCash, lossCash, reputation, firstBonus, fee;
        public bool practice, complete;
        public RivalEntry rival;
    }
    public static class CareerProgression
    {
        public static void Ensure(PlayerProfile p)
        { if(p.careerRuns==null)p.careerRuns=new List<CareerRun>();foreach(var r in p.careerRuns)if(r?.rival!=null&&string.IsNullOrEmpty(r.rival.model)&&(r.rival.hardware==null||r.rival.hardware.Count==0))r.rival=null; }
        public static string EntryError(PlayerProfile p, OwnedCar car, CareerEventDefinition e)
        {
            if(car==null)return "Select an owned car.";
            try { EngineRebuilds.RequireAvailable(car); } catch(Exception x){return x.Message;}
            if(p.runCheckpoints.Any(r=>!r.settled))return "Close the active run first.";
            var b=MechanicsOwnership.Resolve(p,car,p.catalog);
            if(!b.Drivable)return string.Join("; ",b.errors);
            if(e==null)return null;
            if(e.opponent==null||e.cashWin<0||e.cashLoss<0||e.reputationWin<0||e.entryFee<0||e.firstWinBonus<0)return "Invalid event authoring.";
            if(p.reputation<e.reputationRequired)return "Requires REP "+e.reputationRequired+".";
            if(!string.IsNullOrEmpty(e.prerequisite)&&!p.careerWins.Contains(e.prerequisite))return "Win "+e.prerequisite+" first.";
            if(p.cash<e.entryFee)return "Entry costs " + DisplayUnits.Money(e.entryFee)+". Use free races or garage work.";
            double hp=DragMath.PeakHorsepower(b.spec,b.tune), mass=b.spec.massKg*b.tune.MassMultiplier;
            if(e.minimumHp>0&&hp<e.minimumHp)return "Requires at least "+e.minimumHp+" hp from installed hardware.";
            if(e.maximumHp>0&&hp>e.maximumHp)return "Maximum "+e.maximumHp+" hp.";
            if(e.restrictDrive&&b.spec.drive!=e.requiredDrive)return e.requiredDrive+" only.";
            if(e.minimumMassKg>0&&mass<e.minimumMassKg||e.maximumMassKg>0&&mass>e.maximumMassKg)return "Installed dry mass outside event limits.";
            if(e.maximumDisplacementL>0&&b.spec.DisplacementLitres>e.maximumDisplacementL)return "Maximum "+e.maximumDisplacementL+" litre engine.";
            var induction=new InductionProfile(b.spec,b.tune).type;
            bool turbo=induction==InductionType.Turbo||induction==InductionType.TwinTurbo||induction==InductionType.Twincharged;
            if(e.turboOnly&&!turbo)return "Turbo hardware required.";
            if(e.naturallyAspiratedOnly&&induction!=InductionType.NaturallyAspirated)return "Naturally aspirated hardware required.";
            if(!string.IsNullOrEmpty(e.requiredModel)&&car.vehicleId!=e.requiredModel)return "Requires model "+e.requiredModel+".";
            return null;
        }
        public static void Enter(PlayerProfile p,string runId,string carId,CareerEventDefinition e,bool practice,RivalEntry rival=null)
        {
            Ensure(p);var c=p.cars.Find(v=>v.instanceId==carId);string error=EntryError(p,c,e);if(error!=null)throw new InvalidOperationException(error);
            if(CareerOpponents.ValidateEntry(rival)!=null)throw new InvalidOperationException(CareerOpponents.ValidateEntry(rival));
            if(string.IsNullOrWhiteSpace(runId)||p.careerRuns.Any(r=>r.runId==runId))throw new InvalidOperationException("Duplicate/invalid entry.");
            int fee=e==null?0:e.entryFee;p.cash=checked(p.cash-fee);
            p.careerRuns.Add(new CareerRun{rival=rival,runId=runId,carId=carId,eventId=e?.stableId,practice=practice,fee=fee,winCash=e==null?120:e.cashWin,lossCash=e==null?40:e.cashLoss,reputation=e==null?0:e.reputationWin,firstBonus=e==null?0:e.firstWinBonus});
            while(p.careerRuns.Count>256){var old=p.careerRuns.Find(r=>r.complete);if(old==null)throw new InvalidOperationException("Too many active entries.");p.careerRuns.Remove(old);}
        }
        public static void Settle(PlayerProfile p,string runId,RaceSimulation race,bool refreshMarket=true)
        {
            var r=p.careerRuns.Find(t=>t.runId==runId)??throw new InvalidOperationException("Missing captured race entry.");
            if(r.complete)throw new InvalidOperationException("Race already settled.");
            if(race==null||!race.ResultReady)throw new InvalidOperationException("Race has not finished.");
            var c=p.cars.Find(v=>v.instanceId==r.carId)??throw new InvalidOperationException("Missing race owner.");
            bool valid=race.player.state.finished&&!race.player.state.disqualified;
            r.outcome=race.player.state.disqualified?"Disqualified":!valid?"Mechanical / incomplete DNF":race.playerWon?"Win":"Loss";
            if(valid){c.Record(race);if(Math.Abs(race.distance-402.336)<.01&&(p.bestQuarterSeconds<=0||race.player.state.elapsed<p.bestQuarterSeconds))p.bestQuarterSeconds=race.player.state.elapsed;
                if(!r.practice){bool first=race.playerWon&&!string.IsNullOrEmpty(r.eventId)&&!p.careerWins.Contains(r.eventId);p.cash=checked(p.cash+(race.playerWon?r.winCash:r.lossCash)+(first?r.firstBonus:0));if(first){p.careerWins.Add(r.eventId);p.reputation=checked(p.reputation+r.reputation);}}
            }
            r.complete=true;if(valid&&!r.practice&&refreshMarket)VehicleListings.AfterRace(p,p.catalog);
        }
        public static void Interrupted(PlayerProfile p,string id){var r=p.careerRuns.Find(t=>t.runId==id);if(r!=null&&!r.complete){r.complete=true;r.outcome="Interrupted / no payout";}}
        public static string Validate(PlayerProfile p)
        {
            if(p.careerRuns==null||p.careerRuns.Count>256)return "Invalid career ledger.";
            var ids=new HashSet<string>();foreach(var r in p.careerRuns)if(r==null||string.IsNullOrEmpty(r.runId)||!ids.Add(r.runId)||string.IsNullOrEmpty(r.carId)||r.winCash<0||r.lossCash<0||r.reputation<0||r.firstBonus<0||r.fee<0||!r.complete&&!p.cars.Any(c=>c.instanceId==r.carId))return "Invalid career entry.";foreach(var r in p.careerRuns)if(CareerOpponents.ValidateEntry(r.rival)!=null)return CareerOpponents.ValidateEntry(r.rival);return null;
        }
    }
}
