using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class GarageJobOffer
    {
        public string id, name; public int seconds, reward, reputationRequired, levelRequired=1;
        public float minimumHp; public bool restrictDrive; public DriveType drive;
    }
    [Serializable] public sealed class GarageBusinessPolicy
    {
        public int version=1;
        public int[] hourlyRates={100,180,300,500}, capsHours={8,12,18,24}, upgradeCosts={1200,2400,3600}, jobSlots={1,2,3,4};
        public List<GarageJobOffer> offers=new List<GarageJobOffer>{
            new GarageJobOffer{id="photo-session",name="Local photo session",seconds=30,reward=20},
            new GarageJobOffer{id="car-show",name="Club car show",seconds=1800,reward=300},
            new GarageJobOffer{id="dyno-display",name="Dyno exhibit (engine off display)",seconds=7200,reward=1200,minimumHp=200,reputationRequired=4,levelRequired=2},
            new GarageJobOffer{id="awd-show",name="AWD promotional display",seconds=14400,reward=2200,restrictDrive=true,drive=DriveType.AWD,reputationRequired=8,levelRequired=3}};
        public void Validate(){if(version!=1||hourlyRates==null||hourlyRates.Length!=4||capsHours==null||capsHours.Length!=4||upgradeCosts==null||upgradeCosts.Length!=3||jobSlots==null||jobSlots.Length!=4||hourlyRates.Any(v=>v<1||v>100000)||capsHours.Any(v=>v<1||v>168)||upgradeCosts.Any(v=>v<1)||jobSlots.Any(v=>v<1||v>8)||offers==null||offers.Select(o=>o?.id).Distinct().Count()!=offers.Count)throw new InvalidOperationException("Invalid business policy.");foreach(var o in offers)if(o==null||string.IsNullOrWhiteSpace(o.id)||string.IsNullOrWhiteSpace(o.name)||o.seconds<1||o.seconds>604800||o.reward<1||o.reputationRequired<0||o.levelRequired<1||o.levelRequired>4||!MechanicsSchema.Finite(o.minimumHp)||o.minimumHp<0||!Enum.IsDefined(typeof(DriveType),o.drive))throw new InvalidOperationException("Invalid garage job offer.");}
    }
    [Serializable] public sealed class AssignedGarageJob
    {
        public string id, offerId, name, carId, engineId, snapshot;
        public string[] partIds;
        public long startedUtcTicks, readyUtcTicks;
        public int reward;
    }
    [Serializable] public sealed class GarageBusinessState
    {
        public int version, ratePerHour, capHours;public long accruedThroughUtcTicks;public double bank;public bool legacyEightHourCap;
        public List<AssignedGarageJob> jobs=new List<AssignedGarageJob>();
    }
    public static class GarageBusiness
    {
        public static GarageBusinessPolicy DefaultPolicy(){return new GarageBusinessPolicy();}
        public static void Ensure(PlayerProfile p)
        {
            if(p.business==null)p.business=new GarageBusinessState();var s=p.business;
            if(s.version>1)throw new InvalidOperationException("Unsupported business state.");
            if(s.version==0){if(s.bank!=0||s.jobs!=null&&s.jobs.Count>0)throw new InvalidOperationException("Invalid legacy business state.");s.accruedThroughUtcTicks=Math.Max(1,p.idleClaimUtcTicks);s.ratePerHour=new[]{100,180,300,500}[p.garageLevel-1];s.capHours=8;s.version=1;s.legacyEightHourCap=true;}
            if(s.jobs==null)s.jobs=new List<AssignedGarageJob>();
        }
        static void Guard(PlayerProfile p,long now)
        {
            Ensure(p);if(now<=0||now>DateTime.MaxValue.Ticks-TimeSpan.TicksPerDay||now<Math.Max(p.business.accruedThroughUtcTicks,p.lastEconomyUtcTicks))throw new InvalidOperationException("Clock moved backwards. Wait for the saved business clock.");
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the run before changing business state.");
        }
        static double PendingValue(PlayerProfile p,GarageBusinessPolicy policy,long now)
        {
            Ensure(p);policy.Validate();double cap=p.business.capHours*(double)p.business.ratePerHour;
            return Math.Min(cap,p.business.bank+Math.Max(0,(now-p.business.accruedThroughUtcTicks)/(double)TimeSpan.TicksPerHour)*p.business.ratePerHour);
        }
        public static int Pending(PlayerProfile p,GarageBusinessPolicy policy,long now){return (int)Math.Floor(PendingValue(p,policy,now)+1e-9);}
        static void Accrue(PlayerProfile p,GarageBusinessPolicy policy,long now){double value=PendingValue(p,policy,now);p.business.bank=value;p.business.accruedThroughUtcTicks=now;p.idleClaimUtcTicks=now;p.business.legacyEightHourCap=false;}
        public static int Claim(PlayerProfile p,GarageBusinessPolicy policy,long now,bool receipt=true)
        {
            Guard(p,now);int before=p.cash;Accrue(p,policy,now);int amount=(int)Math.Floor(p.business.bank+1e-9);p.cash=checked(p.cash+amount);p.business.bank=Math.Max(0,p.business.bank-amount);CaptureRates(p,policy);
            if(receipt&&amount>0)PaidEconomy.Record(p,MechanicsOwnership.NewId("idle"),"Idle income","garage",before,now);return amount;
        }
        public static void Upgrade(PlayerProfile p,GarageBusinessPolicy policy,long now)
        {
            Guard(p,now);policy.Validate();if(p.garageLevel>=4)throw new InvalidOperationException("Business fully expanded.");int cost=policy.upgradeCosts[p.garageLevel-1];if((long)p.cash+Pending(p,policy,now)<cost)throw new InvalidOperationException("Insufficient cash, including claimable income.");
            int before=p.cash;Claim(p,policy,now,false);p.cash=checked(p.cash-cost);p.garageLevel++;CaptureRates(p,policy);PaidEconomy.Record(p,MechanicsOwnership.NewId("garage-upgrade"),"Garage expansion","level "+p.garageLevel,before,now);
        }
        static void CaptureRates(PlayerProfile p,GarageBusinessPolicy policy){p.business.ratePerHour=policy.hourlyRates[p.garageLevel-1];p.business.capHours=policy.capsHours[p.garageLevel-1];}
        public static string Eligibility(PlayerProfile p,OwnedCar c,GarageJobOffer offer)
        {
            if(c==null||offer==null)return "Select a car and job.";
            try{EngineRebuilds.RequireAvailable(c);}catch(Exception e){return e.Message;}
            if(p.runCheckpoints.Any(r=>!r.settled))return "Close the active run first.";
            if(p.garageLevel<offer.levelRequired||p.reputation<offer.reputationRequired)return "Requires garage level "+offer.levelRequired+" / REP "+offer.reputationRequired+".";
            var b=MechanicsOwnership.Resolve(p,c,p.catalog);if(!b.Drivable)return "Needs a complete available build: "+string.Join("; ",b.errors);
            if(offer.restrictDrive&&b.spec.drive!=offer.drive)return offer.drive+" required.";
            if(DragMath.PeakHorsepower(b.spec,b.tune)<offer.minimumHp)return "Requires "+offer.minimumHp+" installed hp.";return null;
        }
        static string Snapshot(PlayerProfile p,OwnedCar c)
        {
            string text=JsonUtility.ToJson(c.fuel)+JsonUtility.ToJson(c.fluids)+JsonUtility.ToJson(c.tune)+c.paintHex;
            foreach(var s in c.installed.slots.OrderBy(s=>s.partInstanceId,StringComparer.Ordinal)){text+=s.slot+JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==s.partInstanceId));}
            using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        public static void Assign(PlayerProfile p,GarageBusinessPolicy policy,string carId,string offerId,long now)
        {
            Guard(p,now);policy.Validate();if(p.business.jobs.Count>=policy.jobSlots[p.garageLevel-1])throw new InvalidOperationException("All job bays occupied.");var offer=policy.offers.Find(o=>o.id==offerId);var car=p.cars.Find(c=>c.instanceId==carId);string error=Eligibility(p,car,offer);if(error!=null)throw new InvalidOperationException(error);
            var job=new AssignedGarageJob{id=MechanicsOwnership.NewId("garage-job"),offerId=offer.id,name=offer.name,carId=carId,engineId=car.engineAssemblyId,partIds=car.installed.slots.Select(s=>s.partInstanceId).OrderBy(s=>s,StringComparer.Ordinal).ToArray(),snapshot=Snapshot(p,car),startedUtcTicks=now,readyUtcTicks=checked(now+TimeSpan.TicksPerSecond*offer.seconds),reward=offer.reward};
            car.garageJobId=job.id;p.business.jobs.Add(job);p.lastEconomyUtcTicks=now;
        }
        public static void Finish(PlayerProfile p,string jobId,long now,bool cancel=false)
        {
            Guard(p,now);var job=p.business.jobs.Find(j=>j.id==jobId)??throw new InvalidOperationException("Job already released or missing.");if(!cancel&&now<job.readyUtcTicks)throw new InvalidOperationException("Job is still running.");
            var c=p.cars.Find(v=>v.instanceId==job.carId);if(c==null||c.garageJobId!=job.id||Snapshot(p,c)!=job.snapshot)throw new InvalidOperationException("Reserved job contents changed.");
            int before=p.cash;if(!cancel)p.cash=checked(p.cash+job.reward);c.garageJobId="";p.business.jobs.Remove(job);PaidEconomy.Record(p,job.id,cancel?"Cancel garage job":"Garage job",job.carId,before,now);
        }
        public static string Validate(PlayerProfile p)
        {
            var s=p.business;if(s==null||s.version!=1||s.ratePerHour<1||s.ratePerHour>100000||s.capHours<1||s.capHours>168||s.jobs==null||s.jobs.Count>8||s.accruedThroughUtcTicks<1||s.accruedThroughUtcTicks>DateTime.MaxValue.Ticks||!MechanicsSchema.Finite(s.bank)||s.bank<0||s.bank>100000000)return "Invalid business state.";
            var ids=new HashSet<string>();var cars=new HashSet<string>();foreach(var j in s.jobs){var c=p.cars.Find(v=>v.instanceId==j?.carId);if(j==null||string.IsNullOrEmpty(j.id)||!ids.Add(j.id)||!cars.Add(j.carId)||c==null||c.garageJobId!=j.id||c.engineAssemblyId!=j.engineId||j.partIds==null||!j.partIds.OrderBy(v=>v,StringComparer.Ordinal).SequenceEqual(c.installed.slots.Select(b=>b.partInstanceId).OrderBy(v=>v,StringComparer.Ordinal))||j.reward<1||j.startedUtcTicks<1||j.readyUtcTicks<=j.startedUtcTicks||j.readyUtcTicks>DateTime.MaxValue.Ticks||j.snapshot!=Snapshot(p,c)||p.economyReceipts.Any(r=>r.id==j.id))return "Invalid/dangling garage reservation.";}
            if(p.cars.Any(c=>!string.IsNullOrEmpty(c.garageJobId)&&!s.jobs.Any(j=>j.id==c.garageJobId&&j.carId==c.instanceId)))return "Missing job reservation.";return null;
        }
        public static bool Reserved(PlayerProfile p,string id){return p.business.jobs.Any(j=>j.carId==id||j.engineId==id||j.partIds.Contains(id));}
    }
}
