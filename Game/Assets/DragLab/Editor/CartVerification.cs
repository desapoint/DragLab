using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class CartVerification
    {
        public static void Run()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/shop-inventory"));Directory.CreateDirectory(folder);
            var log=new StringBuilder();int checks=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.AppendLine("PASS: "+label);checks++;};
            var cat=MechanicsTransactions.Catalog();long now=DateTime.UtcNow.Ticks;
            foreach(var model in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,cat);p.cash=1000000;p.reputation=100;
                if(p.SelectedCar.vehicleId!=model.spec.id)VehicleCollection.Buy(p,cat,new VehicleOffer{model=model.spec.id,price=1,reputation=0,stock=model.CreateStockTune(),paint="#FFFFFF"});
                var car=p.SelectedCar;
                // The dealership selects its purchased instance; keep references to real displaced parts.
                var lines=new List<PartCartLine>{new PartCartLine{definitionId=cat.Id(car.vehicleId,PartCategory.Cooling,1),quantity=2,install=true},new PartCartLine{definitionId=cat.Id(car.vehicleId,PartCategory.Exhaust,1),install=true},new PartCartLine{definitionId=cat.Id(car.vehicleId,PartCategory.Tires,1),quantity=2}};
                var old=car.installed.slots.Find(s=>s.slot=="Cooling").partInstanceId;p.parts.Find(i=>i.instanceId==old).condition.health=.6f;
                string bytes=JsonUtility.ToJson(p);int count=p.parts.Count,cash=p.cash;
                var combined=InspectionBuild.Capture(p,car,cart:lines);var preview=CarInspection.CalculatePrepared(combined,CarInspection.PrepareCar(combined),false);
                check(combined.build.spec.fittedParts.Any(d=>d.id==lines[0].definitionId)&&combined.build.spec.fittedParts.Any(d=>d.id==lines[1].definitionId)&&!combined.build.spec.fittedParts.Any(d=>d.id==lines[2].definitionId),model.spec.id+" combined preview installs checked entries only");
                check(JsonUtility.ToJson(p)==bytes,model.spec.id+" cart preview does not spend, assign identities or heal");
                var quote=PartCart.Quote(p,car.instanceId,lines,now,60);int writes=0;PlayerProfile next;string error;
                bool ok=MechanicsTransactions.Try(p,cat,p.revision,q=>PartCart.Checkout(q,quote,now),q=>{writes++;return false;},out next,out error);
                check(!ok&&writes==1&&JsonUtility.ToJson(p)==bytes&&ReferenceEquals(next,p),model.spec.id+" failed checkout save rolls back entire cart");
                writes=0;ok=MechanicsTransactions.Try(p,cat,p.revision,q=>PartCart.Checkout(q,quote,now),q=>{writes++;return true;},out next,out error);
                check(ok&&writes==1&&next.cash==cash-quote.total&&next.parts.Count==count+5&&next.economyReceipts.Last().operation=="Parts cart",model.spec.id+" one save and receipt publish whole cart / "+error);
                var removed=next.parts.Find(i=>i.instanceId==old);
                check(removed.location==PartLocation.Inventory&&removed.condition.health==.6f,model.spec.id+" displaced original retains real identity and wear");
                var installed=InspectionBuild.Capture(next,next.SelectedCar);var actual=CarInspection.CalculatePrepared(installed,CarInspection.PrepareCar(installed),false);
                check(preview.peakHp==actual.peakHp&&preview.coolingTransferPercent==actual.coolingTransferPercent&&preview.vehicleMassKg==actual.vehicleMassKg,model.spec.id+" combined installed stats match preview");
                var insufficient=JsonUtility.FromJson<PlayerProfile>(bytes);MechanicsOwnership.Ensure(insufficient,cat);insufficient.cash=0;var poor=PartCart.Quote(insufficient,car.instanceId,lines,now,60);writes=0;
                ok=MechanicsTransactions.Try(insufficient,cat,insufficient.revision,q=>PartCart.Checkout(q,poor,now),q=>{writes++;return true;},out next,out error);
                check(!ok&&writes==0&&insufficient.cash==0&&insufficient.parts.Count==count,model.spec.id+" insufficient funds never write or partially buy");
            }
            var owner=PlayerProfile.New(now);MechanicsOwnership.Ensure(owner,cat);owner.cash=100000;
            var target=owner.SelectedCar;var piston=owner.parts.Find(p=>p.slot=="Pistons"&&p.ownerId==target.engineAssemblyId);piston.condition.health=.3f;foreach(var e in piston.condition.elements){e.health=.3f;e.fault=ElementFault.CompressionLeak;e.severity=.4f;}
            string wornBytes=JsonUtility.ToJson(owner);var worn=CarInspection.CalculatePrepared(InspectionBuild.Capture(owner,target),CarInspection.PrepareCar(InspectionBuild.Capture(owner,target)),false);
            var freshSource=InspectionBuild.Capture(owner,target,healthy:true);var fresh=CarInspection.CalculatePrepared(freshSource,CarInspection.PrepareCar(freshSource),false);
            check(fresh.peakHp>worn.peakHp&&fresh.peakTorque>worn.peakTorque,"Individual faults/wear reduce dyno; healthy reference restores only copied condition");
            check(JsonUtility.ToJson(owner)==wornBytes&&freshSource.startingFuel.litres==target.fuel.litres&&JsonUtility.ToJson(freshSource.startingFluids)==JsonUtility.ToJson(target.fluids),"No-wear reference preserves actual ledger, fuel and liquids");
            var duplicate=new[]{new PartCartLine{definitionId=cat.Id(target.vehicleId,PartCategory.Cooling,1),install=true},new PartCartLine{definitionId=cat.Id(target.vehicleId,PartCategory.Cooling,2),install=true}};bool rejected=false;try{PartCart.Total(owner,target.instanceId,duplicate);}catch(InvalidOperationException){rejected=true;}check(rejected,"Conflicting installs rejected; spare offers remain allowed");
            var line=new[]{new PartCartLine{definitionId=cat.Id(target.vehicleId,PartCategory.Cooling,1),install=true}};var stale=PartCart.Quote(owner,target.instanceId,line,now,60);owner.cash++;int saves=0;PlayerProfile committed;string failure;
            check(!MechanicsTransactions.Try(owner,cat,owner.revision,q=>PartCart.Checkout(q,stale,now),q=>{saves++;return true;},out committed,out failure)&&saves==0,"Same-revision asset changes invalidate checkout quote");
            var missing=InspectionBuild.Capture(owner,target,remove:PartCategory.Pistons,healthy:true);
            check(!missing.build.Drivable&&!missing.components.Any(i=>i.slot=="Pistons"),"No-wear reference never creates a removed required component");
            var currentQuote=PartCart.Quote(owner,target.instanceId,line,now,60);string currentBytes=JsonUtility.ToJson(owner);
            check(!MechanicsTransactions.Try(owner,cat,owner.revision,q=>PartCart.Checkout(q,currentQuote,currentQuote.expires),q=>true,out committed,out failure)&&JsonUtility.ToJson(owner)==currentBytes,"Expired quote rejects before mutation");
            PlayerProfile once;check(MechanicsTransactions.Try(owner,cat,owner.revision,q=>PartCart.Checkout(q,currentQuote,now),q=>true,out once,out failure),"Valid quote commits once");
            string onceBytes=JsonUtility.ToJson(once);
            check(!MechanicsTransactions.Try(once,cat,once.revision,q=>PartCart.Checkout(q,currentQuote,now),q=>true,out committed,out failure)&&JsonUtility.ToJson(once)==onceBytes,"Replayed checkout cannot spend twice or duplicate parts");
            foreach(int quantity in new[]{0,100}){bool invalid=false;try{PartCart.Total(owner,target.instanceId,new[]{new PartCartLine{definitionId=line[0].definitionId,quantity=quantity}});}catch(InvalidOperationException){invalid=true;}check(invalid,"Quantity bound rejects "+quantity);}
            var legacyContact=new WheelContactState{normalLoadN=null,friction=null};check(legacyContact.Copy().normalLoadN.Length==4&&legacyContact.Copy().friction.Length==4,"Old wheel checkpoints safely acquire diagnostic arrays without altering physics");
            log.AppendLine("Checks: "+checks);File.WriteAllText(Path.Combine(folder,"cart-verification.txt"),log.ToString(),new UTF8Encoding(false));UnityEngine.Debug.Log("Cart checks: "+checks);
        }
    }
}
