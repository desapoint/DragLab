using System;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        GarageBusinessPolicyDefinition businessPolicyAsset;
        internal GarageBusinessPolicy BusinessPolicy{get{if(businessPolicyAsset==null)businessPolicyAsset=Resources.Load<GarageBusinessPolicyDefinition>("Data/Economy/GarageBusinessPolicy");return businessPolicyAsset==null?GarageBusiness.DefaultPolicy():businessPolicyAsset.policy;}}
        internal void OpenBusiness(){DiscardPreviews();screen="Business";scroll=Vector2.zero;partRects.Clear();}
        void BusinessGUI()
        {
            var policy=BusinessPolicy;long now=DateTime.UtcNow.Ticks;int level=profile.garageLevel-1;
            GUILayout.Label("GARAGE BUSINESS / level "+profile.garageLevel,heading);
            GUILayout.Label("$"+policy.hourlyRates[level]+" / hour / "+policy.capsHours[level]+" hour offline cap / "+profile.business.jobs.Count+" / "+policy.jobSlots[level]+" job bays.");
            GUILayout.Label("Current accrual contract: $"+profile.business.ratePerHour+" / hour / "+profile.business.capHours+" hour cap. A claim settles that contract before adopting authored rate changes.");
            GUILayout.Label("Display jobs reserve the exact car, engine and installed components. Engines stay off: jobs consume no driving fuel, fluid or wear. Claim or cancel to release; cancellation pays nothing. Prices and rewards are fictional prototype calibration.");
            if(profile.business.legacyEightHourCap)GUILayout.Label("Migrated income keeps its previous eight-hour cap until first claim; subsequent accrual uses this garage's cap.");
            int amount=GarageBusiness.Pending(profile,policy,now);GUI.enabled=amount>0&&now>=profile.business.accruedThroughUtcTicks;
            if(PartButton("business-idle","Claim capped income / $"+amount))QueuePartAction(p=>GarageBusiness.Claim(p,policy,DateTime.UtcNow.Ticks));GUI.enabled=true;
            if(level<3){int cost=policy.upgradeCosts[level];GUI.enabled=(long)profile.cash+amount>=cost;if(PartButton("business-upgrade","Expand garage / $"+cost+" / "+policy.hourlyRates[level+1]+" per hour / "+policy.capsHours[level+1]+" hour cap / "+policy.jobSlots[level+1]+" bays"))QueuePartAction(p=>GarageBusiness.Upgrade(p,policy,DateTime.UtcNow.Ticks));GUI.enabled=true;}
            foreach(var job in profile.business.jobs.ToArray()){
                GUILayout.Label(job.name+" / "+job.carId+" / $"+job.reward+" / "+(now>=job.readyUtcTicks?"Ready to claim":Math.Ceiling((job.readyUtcTicks-now)/(double)TimeSpan.TicksPerSecond)+" seconds left"));
                GUI.enabled=now>=job.readyUtcTicks;if(PartButton("job-claim:"+job.id,"Claim and release car")){string id=job.id;QueuePartAction(p=>GarageBusiness.Finish(p,id,DateTime.UtcNow.Ticks));}GUI.enabled=true;
                if(PartButton("job-cancel:"+job.id,"Cancel / release without reward")){string id=job.id;QueuePartAction(p=>GarageBusiness.Finish(p,id,DateTime.UtcNow.Ticks,true));}
            }
            GUILayout.Label("Assign selected car: "+(Build==null?"No car selected":VehicleCollection.Label(profile.catalog,Build)));
            foreach(var offer in policy.offers){string error=GarageBusiness.Eligibility(profile,Build,offer);GUILayout.Label(offer.name+" / "+offer.seconds+" s / $"+offer.reward+" / level "+offer.levelRequired+" / REP "+offer.reputationRequired);if(error!=null)GUILayout.Label(error);
                GUI.enabled=error==null&&profile.business.jobs.Count<policy.jobSlots[level];if(PartButton("job-assign:"+offer.id,"Assign this car")){string id=offer.id,car=Build.instanceId;QueuePartAction(p=>GarageBusiness.Assign(p,policy,car,id,DateTime.UtcNow.Ticks));}GUI.enabled=true;}
            if(PartButton("business-collection","Select another owned car / dealership"))pendingAction=OpenCollection;
            if(PartButton("business-recovery","Recovery work / receipts"))pendingAction=OpenRecovery;
        }
    }
}
