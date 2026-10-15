using System;
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
    public static class EngineRebuildFlowVerification
    {
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit/rebuild"));
            Directory.CreateDirectory(folder);var log=new StringBuilder();int checks=0;
            void Check(bool ok,string label){log.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)throw new InvalidOperationException(label);checks++;}
            var catalog=MechanicsTransactions.Catalog();var costs=new OperatingPolicy();var prices=ValuationPolicy.Defaults();
            string prior=ProfileStore.VerificationPath,priorAudio=SharedAudioSettings.VerificationPath,priorUnits=SharedUnitSettings.VerificationPath;
            try
            {
                foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                {
                    string label=vehicle.spec.id+": ";long now=DateTime.UtcNow.Ticks;
                    string fixtures=Path.Combine(folder,"fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtures);
                    ProfileStore.VerificationPath=Path.Combine(fixtures,"save.sqlite3");
                    SharedAudioSettings.VerificationPath=Path.Combine(fixtures,"audio.json");SharedUnitSettings.VerificationPath=Path.Combine(fixtures,"units.json");ProfileStore.UseSlot("slot-1");
                    var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;
                    VehicleCollection.Buy(p,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                    string carId=p.SelectedCar.instanceId;var car=p.SelectedCar;
                    PartInstance Installed(PartCategory category)=>p.parts.First(i=>i.instanceId==p.cars.First(c=>c.instanceId==carId).installed.slots.First(s=>s.slot==category.ToString()).partInstanceId);
                    var filter=catalog.Find(catalog.Id(car.vehicleId,PartCategory.OilFilter,1));
                    if(filter!=null)PartOperations.Buy(p,catalog,carId,filter.id,true);
                    string filterDefinition=ServiceOperations.OilFilter(p,car).id;
                    var damaged=Installed(PartCategory.Pistons);damaged.condition.health=.2f;
                    foreach(var e in damaged.condition.elements)e.health=.2f;
                    damaged.condition.elements[0].fault=ElementFault.Seized;
                    var repairable=Installed(PartCategory.CylinderBlock);repairable.condition.health=.6f;repairable.condition.leakSeverity=.2;
                    var tire=Installed(PartCategory.Tires);tire.condition.tires[0].health=.4f;tire.condition.tires[0].consumedLife=.6;
                    var cooling=Installed(PartCategory.Cooling);cooling.condition.health=.55f;cooling.condition.leakSeverity=.1;
                    car.fluids.oilLitres=1;car.fluids.oilCondition=.3;car.fluids.coolantLitres=1;car.fuel.litres=3;
                    foreach(var row in p.inventory.market.parts)row.units=0;
                    Check(ProfileStore.TrySave(p),label+"damaged build saved to isolated SQLite / "+ProfileStore.Status);
                    p=ProfileStore.Load();car=p.SelectedCar;now=DateTime.UtcNow.Ticks;Check(!ProfileStore.ResetRequired,label+"damaged build reloads without reset");
                    damaged=Installed(PartCategory.Pistons);repairable=Installed(PartCategory.CylinderBlock);
                    string oldPistonId=damaged.instanceId,oldPistonJson=JsonUtility.ToJson(damaged.condition),blockId=repairable.instanceId;
                    string tireJson=JsonUtility.ToJson(Installed(PartCategory.Tires).condition),coolingJson=JsonUtility.ToJson(Installed(PartCategory.Cooling).condition),tuneJson=JsonUtility.ToJson(car.tune);
                    double fuel=car.fuel.litres,coolant=car.fluids.coolantLitres;int cash=p.cash;
                    var request=new PaymentRequest{kind=PaymentKind.Rebuild,carId=carId};string before=JsonUtility.ToJson(p);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
                    var preview=PaidEconomy.Preview(p,catalog,costs,prices,request,now);var quote=PaidEconomy.Quote(p,catalog,costs,prices,request,now);
                    var core=EngineRebuilds.Quote(p,car,now,costs);
                    Check(quote.Cost==preview.cost&&quote.Cost==core.paidPrice+(int)Math.Ceiling(MechanicsOwnership.Resolve(p,car,catalog).spec.fluids.oilCapacityLitres*costs.oilPerLitre),label+"shown/payment prices include oil and exact rebuild supply");
                    Check(before==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),label+"preview/cancel changes no state or save");
                    Check(core.targets.Single(t=>t.partId==oldPistonId).replace&&!core.targets.Single(t=>t.partId==blockId).replace,label+"unsalvageable pistons replace; serviceable block reconditions");
                    PlayerProfile next=null;string error=null;
                    bool Transaction(Action<PlayerProfile> operation,Func<PlayerProfile,bool> save)=>MechanicsTransactions.Try(p,catalog,p.revision,operation,save,out next,out error);
                    Check(!Transaction(q=>PaidEconomy.Pay(q,catalog,costs,prices,quote,now),q=>false)&&before==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),label+"failed persistence rolls back payment, stock and job");
                    Check(Transaction(q=>PaidEconomy.Pay(q,catalog,costs,prices,quote,now),ProfileStore.TrySave),label+"paid rebuild starts atomically");p=next;
                    car=p.SelectedCar;var job=car.engineRebuild;string jobId=job.id;long ready=job.readyUtcTicks;
                    Check(p.cash==cash-quote.Cost&&job.paidPrice==quote.Cost&&ready>now&&!MechanicsOwnership.Resolve(p,car,catalog).Drivable,label+"one charge, saved deadline and reserved engine");
                    string reserved=JsonUtility.ToJson(p);
                    Check(!Transaction(q=>PaidEconomy.Pay(q,catalog,costs,prices,quote,now),q=>true)&&reserved==JsonUtility.ToJson(p),label+"replayed payment rejected");
                    Check(!Transaction(q=>PartOperations.Remove(q,carId,PartCategory.Pistons),q=>true)&&reserved==JsonUtility.ToJson(p),label+"reserved installed components cannot be removed");
                    Check(!Transaction(q=>EngineRebuilds.Collect(q,carId,jobId,ready-1),q=>true)&&reserved==JsonUtility.ToJson(p),label+"early collection rejected");
                    p=ProfileStore.Load();car=p.SelectedCar;
                    Check(car.engineRebuild.id==jobId&&car.engineRebuild.readyUtcTicks==ready&&p.cash==cash-quote.Cost,label+"reservation, payment and deadline survive SQLite reload");
                    reserved=JsonUtility.ToJson(p);disk=File.ReadAllBytes(ProfileStore.FilePath);
                    Check(!Transaction(q=>EngineRebuilds.Collect(q,carId,jobId,ready),q=>false)&&reserved==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),label+"failed collection save preserves paid reservation and all conditions");
                    Check(Transaction(q=>EngineRebuilds.Collect(q,carId,jobId,ready),ProfileStore.TrySave),label+"ready collection commits");p=next;car=p.SelectedCar;
                    Check(car.engineRebuild==null&&p.cash==cash-quote.Cost,label+"collection clears reservation without charging again");
                    Check(Installed(PartCategory.Pistons).instanceId!=oldPistonId&&Installed(PartCategory.Pistons).condition.elements.All(e=>e.health==1&&e.fault==ElementFault.None),label+"replacement creates healthy correctly typed internals");
                    var removed=p.parts.Single(i=>i.instanceId==oldPistonId);
                    Check(removed.location==PartLocation.Inventory&&JsonUtility.ToJson(removed.condition)==oldPistonJson,label+"removed damaged parts retain their identities and condition");
                    Check(Installed(PartCategory.CylinderBlock).instanceId==blockId&&Installed(PartCategory.CylinderBlock).condition.health>=.95f,label+"reconditioning retains original block identity");
                    Check(JsonUtility.ToJson(Installed(PartCategory.Tires).condition)==tireJson&&JsonUtility.ToJson(Installed(PartCategory.Cooling).condition)==coolingJson&&JsonUtility.ToJson(car.tune)==tuneJson&&car.fuel.litres==fuel&&car.fluids.coolantLitres==coolant,label+"unrelated wear, coolant, fuel and tune unchanged");
                    Check(car.fluids.oilCondition==1&&car.fluids.oilLitres==MechanicsOwnership.Resolve(p,car,catalog).spec.fluids.oilCapacityLitres&&ServiceOperations.OilFilter(p,car).id==filterDefinition,label+"renews oil and keeps fitted filter specification");
                    Check(!Transaction(q=>EngineRebuilds.Collect(q,carId,jobId,ready),q=>true),label+"repeat collection rejected");
                    p=ProfileStore.Load();Check(p.SelectedCar.engineRebuild==null&&MechanicsOwnership.Validate(p,catalog)==null,label+"completed rebuild survives SQLite reload");
                    // Current hardware must be complete; a rebuild may not fabricate missing parts.
                    PartOperations.Remove(p,carId,PartCategory.Pistons);bool missingRejected=false;
                    try{EngineRebuilds.Quote(p,p.SelectedCar,now,costs);}catch(InvalidOperationException){missingRejected=true;}
                    Check(missingRejected,label+"incomplete engine rejected without manufacturing missing parts");
                }
            }
            finally
            {
                RaceCheckpointStore.Drain();ProfileStore.VerificationPath=prior;SharedAudioSettings.VerificationPath=priorAudio;SharedUnitSettings.VerificationPath=priorUnits;ProfileStore.UseSlot("slot-1");
                File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            }
            Debug.Log("ENGINE REBUILD FLOW PASS: "+checks);
        }
    }
}
