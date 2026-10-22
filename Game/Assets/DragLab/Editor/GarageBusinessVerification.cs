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
namespace DragLab.Editor
{
    public static class GarageBusinessVerification
    {
        [MenuItem("DragLab/Prepare Garage Business Policy")]
        public static void Prepare()
        {
            const string path="Assets/DragLab/Resources/Data/Economy/GarageBusinessPolicy.asset";
            if(AssetDatabase.LoadAssetAtPath<GarageBusinessPolicyDefinition>(path)!=null)return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GarageBusinessPolicyDefinition>(),path);AssetDatabase.SaveAssets();
        }
        [MenuItem("DragLab/Verify Garage Jobs and Idle")]
        public static void RunStandalone()
        {
            var businessAsset=Resources.Load<GarageBusinessPolicyDefinition>("Data/Economy/GarageBusinessPolicy");
            if(businessAsset==null||businessAsset.policy==null)throw new InvalidOperationException("Business policy missing. Run DragLab/Prepare Garage Business Policy explicitly, then verify.");
            var policy=businessAsset.policy;policy.Validate();var cat=MechanicsTransactions.Catalog();var values=Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;var costs=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy").policy;long now=DateTime.UtcNow.Ticks;
            var args=Environment.GetCommandLineArgs();int reportAt=Array.IndexOf(args,"--garage-business-report");
            string folder=reportAt>=0&&reportAt+1<args.Length?Path.GetFullPath(args[reportAt+1]):Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,"reports/phases/3f");Directory.CreateDirectory(folder);var log=new StringBuilder();var matrix=new StringBuilder("model,offer,eligible,reason\n");int count=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(folder,"verification-progress.txt"),log.ToString(),Encoding.UTF8);};
            Func<string,PlayerProfile> fresh=model=>{var p=PlayerProfile.New(now);p.SelectedCar.vehicleId=model;p.selectedVehicleId=model;p.cash=100000;p.reputation=99;MechanicsOwnership.Ensure(p,cat);return p;};
            Func<PlayerProfile,Action<PlayerProfile>,PlayerProfile> commit=(p,op)=>{PlayerProfile next;string error;if(!MechanicsTransactions.Try(p,cat,p.revision,op,q=>true,out next,out error))throw new InvalidOperationException(error);return next;};
            Action<PlayerProfile,Action<PlayerProfile>,string> reject=(p,op,label)=>{string old=JsonUtility.ToJson(p);PlayerProfile next;string error;check(!MechanicsTransactions.Try(p,cat,p.revision,op,q=>true,out next,out error)&&JsonUtility.ToJson(p)==old,label);};
            for(int level=1;level<=4;level++){
                var p=fresh("starter-hatch");p.garageLevel=level;p.business.ratePerHour=policy.hourlyRates[level-1];p.business.capHours=policy.capsHours[level-1];long future=now+TimeSpan.TicksPerHour*72;int cash=p.cash;
                check(GarageBusiness.Pending(p,policy,future)==policy.hourlyRates[level-1]*policy.capsHours[level-1],"Offline cap at garage level "+level);
                p=commit(p,a=>GarageBusiness.Claim(a,policy,future));check(p.cash==cash+policy.hourlyRates[level-1]*policy.capsHours[level-1],"Capped durable claim at level "+level);int settled=p.cash;p=commit(p,a=>GarageBusiness.Claim(a,policy,future));check(p.cash==settled,"Same-time repeat claim pays nothing at level "+level);reject(p,a=>GarageBusiness.Claim(a,policy,now),"Backwards clock cannot rewind accrual at level "+level);
                var reload=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));reload.Sanitize(now);MechanicsOwnership.Ensure(reload,cat);check(reload.business.accruedThroughUtcTicks==future&&reload.idleClaimUtcTicks==future&&GarageBusiness.Pending(reload,policy,now)==0,"Rollback clock survives Sanitize/reload at level "+level);
            }
            var fraction=fresh("starter-hatch");fraction=commit(fraction,p=>GarageBusiness.Claim(p,policy,now+TimeSpan.TicksPerSecond*18));check(Math.Abs(fraction.business.bank-.5)<1e-8,"Fractional dollar retained on zero-value claim.");int beforeUpgrade=fraction.cash;long hour=now+TimeSpan.TicksPerHour;fraction=commit(fraction,p=>GarageBusiness.Upgrade(p,policy,hour));check(fraction.cash==beforeUpgrade+100-policy.upgradeCosts[0]&&fraction.garageLevel==2&&GarageBusiness.Pending(fraction,policy,hour+TimeSpan.TicksPerHour)==180,"Expansion settles old rate before using new rate/cap.");
            var repriced=fresh("starter-hatch");var changed=JsonUtility.FromJson<GarageBusinessPolicy>(JsonUtility.ToJson(policy));changed.hourlyRates[0]=200;check(GarageBusiness.Pending(repriced,changed,hour)==100,"Authored rate change cannot retrospectively double income.");repriced=commit(repriced,p=>GarageBusiness.Claim(p,changed,hour));check(GarageBusiness.Pending(repriced,changed,hour+TimeSpan.TicksPerHour)==200,"New authored rate takes effect after old-rate settlement.");
            var legacy=fresh("starter-hatch");legacy.business=null;legacy.garageLevel=4;GarageBusiness.Ensure(legacy);check(GarageBusiness.Pending(legacy,policy,now+24*TimeSpan.TicksPerHour)==4000,"Legacy higher-level income retains original eight-hour cap.");legacy=commit(legacy,p=>GarageBusiness.Claim(p,policy,now+24*TimeSpan.TicksPerHour));check(legacy.business.capHours==24&&!legacy.business.legacyEightHourCap,"Legacy claim transitions to expanded cap without retroactive grant.");
            foreach(var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
                var p=fresh(asset.spec.id);p.garageLevel=4;string carId=p.SelectedCar.instanceId,engineId=p.SelectedCar.engineAssemblyId;
                foreach(var o in policy.offers){string error=GarageBusiness.Eligibility(p,p.SelectedCar,o);matrix.AppendLine(asset.spec.id+","+o.id+","+(error==null)+",\""+(error??"Ready").Replace("\"","'")+"\"");}
                p.SelectedCar.fuel.litres=7;var piston=p.parts.Find(i=>i.instanceId==p.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);piston.condition.health=.8f;piston.condition.elements[0].health=.9f;
                string physical=JsonUtility.ToJson(p.SelectedCar.fuel)+JsonUtility.ToJson(p.SelectedCar.fluids)+string.Join("",p.parts.Select(i=>JsonUtility.ToJson(i.condition)));
                var quote=Valuations.Capture(p,cat,values,ValuationKind.Car,carId,"enthusiast",now);p=commit(p,a=>GarageBusiness.Assign(a,policy,carId,"photo-session",now));var job=p.business.jobs.Single();check(p.SelectedCar.garageJobId==job.id&&job.partIds.Length==p.SelectedCar.installed.slots.Count,"Exact whole-build reservation: "+asset.spec.id);
                check(CareerProgression.EntryError(p,p.SelectedCar,null)!=null,"Assigned car cannot race: "+asset.spec.id);
                reject(p,a=>PartOperations.Remove(a,carId,PartCategory.Pistons),"Assigned part cannot be removed: "+asset.spec.id);reject(p,a=>AssemblyOperations.Detach(a,carId),"Assigned engine cannot detach: "+asset.spec.id);reject(p,a=>new TuneDraft(a.SelectedCar,a.revision).ApplyTo(a),"Assigned tune cannot apply: "+asset.spec.id);reject(p,a=>PaidEconomy.Quote(a,cat,costs,values,new PaymentRequest{kind=PaymentKind.Fuel,carId=carId,litres=1},now),"Assigned car cannot buy operating resources: "+asset.spec.id);
                check(Valuations.Capture(p,cat,values,ValuationKind.Car,carId,"enthusiast",now).Reserved&&Valuations.Capture(p,cat,values,ValuationKind.Engine,engineId,"builder",now).Reserved&&Valuations.Capture(p,cat,values,ValuationKind.Chassis,p.SelectedCar.chassisId,"recycler",now).Reserved,"All nested assembly valuations are inspection-only: "+asset.spec.id);
                reject(p,a=>PaidEconomy.Sell(a,cat,values,quote,now),"Pre-assignment quote cannot dispose assigned assets: "+asset.spec.id);reject(p,a=>GarageBusiness.Finish(a,job.id,now),"Early job claim rejected: "+asset.spec.id);
                var reload=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));reload.Sanitize(now);MechanicsOwnership.Ensure(reload,cat);check(GarageBusiness.Validate(reload)==null&&reload.business.jobs.Single().id==job.id,"Reservation and captured job survive serialization: "+asset.spec.id);
                string old=JsonUtility.ToJson(p);PlayerProfile fail;string err;check(!MechanicsTransactions.Try(p,cat,p.revision,a=>GarageBusiness.Finish(a,job.id,job.readyUtcTicks),a=>false,out fail,out err)&&old==JsonUtility.ToJson(p),"Failed write retains reservation and unpaid job: "+asset.spec.id);
                int cash=p.cash;p=commit(p,a=>GarageBusiness.Finish(a,job.id,job.readyUtcTicks));check(p.cash==cash+job.reward&&p.business.jobs.Count==0&&string.IsNullOrEmpty(p.SelectedCar.garageJobId),"Captured reward/release settles once: "+asset.spec.id);
                check(physical==JsonUtility.ToJson(p.SelectedCar.fuel)+JsonUtility.ToJson(p.SelectedCar.fluids)+string.Join("",p.parts.Select(i=>JsonUtility.ToJson(i.condition))),"Idle job does not consume/restore fuel, fluids or wear: "+asset.spec.id);reject(p,a=>GarageBusiness.Finish(a,job.id,job.readyUtcTicks),"Repeat claim cannot duplicate payment: "+asset.spec.id);
                p=commit(p,a=>GarageBusiness.Assign(a,policy,carId,"photo-session",job.readyUtcTicks));string cancel=p.business.jobs.Single().id;cash=p.cash;p=commit(p,a=>GarageBusiness.Finish(a,cancel,job.readyUtcTicks,true));check(p.cash==cash&&p.business.jobs.Count==0,"Cancel releases with no payout: "+asset.spec.id);
                var sale=Valuations.Capture(p,cat,values,ValuationKind.Chassis,p.SelectedCar.chassisId,"recycler",job.readyUtcTicks);p=commit(p,a=>PaidEconomy.Sell(a,cat,values,sale,job.readyUtcTicks));check(p.cars.Count==0&&GarageBusiness.Validate(p)==null,"Post-release salvage leaves no dangling reservation: "+asset.spec.id);
            }
            var slots=fresh("starter-hatch");var model=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(v=>v.spec.id=="starter-hatch");string second=VehicleCollection.Buy(slots,cat,new VehicleOffer{model=model.spec.id,price=10000,stock=model.CreateStockTune()});string first=slots.cars[0].instanceId;
            slots=commit(slots,p=>GarageBusiness.Assign(p,policy,first,"photo-session",now));reject(slots,p=>GarageBusiness.Assign(p,policy,second,"photo-session",now),"Level one limits simultaneous jobs.");slots=commit(slots,p=>GarageBusiness.Upgrade(p,policy,now));slots=commit(slots,p=>GarageBusiness.Assign(p,policy,second,"photo-session",now));check(slots.business.jobs.Count==2&&slots.business.jobs.Select(j=>j.carId).Distinct().Count()==2,"Expansion allows two independent duplicate-model assignments.");
            reject(slots,p=>GarageBusiness.Assign(p,policy,first,"photo-session",now),"Same owned instance cannot be assigned twice.");
            var overflow=fresh("starter-hatch");overflow.cash=int.MaxValue;reject(overflow,p=>GarageBusiness.Claim(p,policy,hour),"Cash overflow rolls claim back.");var bad=fresh("starter-hatch");bad.business.version=99;check(GarageBusiness.Validate(bad)!=null,"Future business version blocked.");
            File.WriteAllText(Path.Combine(folder,"business-matrix.csv"),matrix.ToString(),Encoding.UTF8);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count+"\n",Encoding.UTF8);Debug.Log("GARAGE BUSINESS PASS: "+count);
        }
    }
}
