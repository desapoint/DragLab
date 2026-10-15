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
    // Focused regressions found while reviewing refactor/per-tire-authority.
    // All save files are disposable fixtures; never touch the player's save.
    public static class BranchAuditVerification
    {
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit"));
            Directory.CreateDirectory(folder);
            var log=new StringBuilder(); int failures=0;
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures++;};
            var catalog=MechanicsTransactions.Catalog();
            var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);
            var build=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);
            var car=new CarSimulation(build.spec,build.tune,new DriverSettings(),p.parts);
            var pack=car.mechanics.Part(PartCategory.Tires);
            // Match the explicit used-stock seed path, including its float complement.
            var seeded=new ComponentCondition{health=.1f};TireDynamics.SeedNew(seeded);
            for(int i=0;i<4;i++){seeded.tires[i].id=pack.condition.tires[i].id;seeded.tires[i].definitionId=pack.condition.tires[i].definitionId;}
            pack.condition=seeded;var before=seeded.Copy();
            TireDynamics.Tick(car,1.0/120,car.state.distance);
            log.AppendLine("Idle tire health before="+before.tires[0].health.ToString("R")+" after="+pack.condition.tires[0].health.ToString("R"));
            check(pack.condition.tires[0].health<=before.tires[0].health,"A seeded worn tire cannot heal during a zero-work idle tick");
            check(RunPersistence.ConditionAdvances(before,pack.condition),"A zero-work worn-tire tick remains checkpointable");

            string oldPath=ProfileStore.VerificationPath,oldAudio=SharedAudioSettings.VerificationPath,oldUnits=SharedUnitSettings.VerificationPath;
            try
            {
                string fixtures=Path.Combine(folder,"fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtures);
                ProfileStore.VerificationPath=Path.Combine(fixtures,"save.sqlite3");
                SharedAudioSettings.VerificationPath=Path.Combine(fixtures,"audio.json");
                SharedUnitSettings.VerificationPath=Path.Combine(fixtures,"units.json");
                var current=ProfileStore.Load();
                if(!ProfileStore.TryReset(current,out current))throw new InvalidOperationException(ProfileStore.Status);
                current=ProfileStore.Load();
                check(File.Exists(ProfileStore.FilePath+".reset-generation"),"Normal reset establishes a recovery generation floor");
                // A damaged primary and recovery copy force the explicit reset flow.
                File.WriteAllText(ProfileStore.FilePath,"isolated incompatible database",Encoding.UTF8);
                File.WriteAllText(ProfileStore.FilePath+".bak","isolated incompatible backup",Encoding.UTF8);
                ProfileStore.Load();check(ProfileStore.ResetRequired,"Incompatible fixture requests explicit reset");
                string error;bool reset=ProfileStore.TryResetIncompatibleSave(out error);
                check(reset,"Explicit incompatible reset succeeds: "+error);
                ProfileStore.Load();log.AppendLine("Reload status: "+ProfileStore.Status);
                check(!ProfileStore.ResetRequired,"Explicit reset remains loadable after an earlier normal progression reset");
            }
            finally
            {
                RaceCheckpointStore.Drain();ProfileStore.VerificationPath=oldPath;
                SharedAudioSettings.VerificationPath=oldAudio;SharedUnitSettings.VerificationPath=oldUnits;
                ProfileStore.UseSlot("slot-1");
                File.WriteAllText(Path.Combine(folder,"focused-verification.txt"),log.ToString(),new UTF8Encoding(false));
            }
            if(failures!=0)throw new InvalidOperationException("Branch audit: "+failures+" regression assertions failed.");
        }

        public static void TireCartDiagnostic()=>TireCart(false);
        public static void TireCartExpressDiagnostic()=>TireCart(true);
        static void TireCart(bool emptyStock)
        {
            var catalog=MechanicsTransactions.Catalog();var vehicle=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(v=>v.spec.id=="juniper-economy");
            var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;
            VehicleCollection.Buy(p,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
            var owned=p.SelectedCar;var pack=p.parts.First(x=>x.instanceId==owned.installed.slots.First(s=>s.slot=="Tires").partInstanceId);
            foreach(var t in pack.condition.tires)t.id=MechanicsOwnership.NewId("tire");pack.condition.tires[2].health=.65f;pack.condition.tires[2].consumedLife=.35;TireDynamics.SyncSummary(pack.condition);
            string front=JsonUtility.ToJson(pack.condition.tires[0]),definition=catalog.Id(owned.vehicleId,PartCategory.Tires,3);int cash=p.cash;
            var lines=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=definition,role="tire",quantity=2,install=true,positions=new System.Collections.Generic.List<string>{"tire:2","tire:3"}}};
            if(emptyStock)foreach(var stock in p.inventory.market.parts)stock.units=0;
            PartCart.Preview(p,owned,lines);long now=DateTime.UtcNow.Ticks;var quote=PartCart.Quote(p,owned.instanceId,lines,now,60);
            PlayerProfile next;string error;bool ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,quote,now),q=>true,out next,out error);
            var fitted=next.parts.First(x=>x.instanceId==pack.instanceId);
            string text="Checkout="+ok+" / "+error+"\nPaid="+(cash-next.cash)+" Base price="+(2*InventoryPricing.NewUnit(catalog.Find(definition),4))+" Quoted="+quote.total
                +"\nFront unchanged="+(front==JsonUtility.ToJson(fitted.condition.tires[0]))+"\nRear definition="+TireFitments.DefinitionId(fitted,2)
                +"\nWorn original retained="+next.looseEngineElements.Any(u=>u.tireUnit&&u.definitionId==pack.definitionId&&u.tire.health==.65f)+"\n";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit/"+(emptyStock?"tire-cart-express-diagnostic.txt":"tire-cart-diagnostic.txt"))),text,new UTF8Encoding(false));
            if(!ok||cash-next.cash!=quote.total||front!=JsonUtility.ToJson(fitted.condition.tires[0])
                ||TireFitments.DefinitionId(fitted,2)!=definition||!next.looseEngineElements.Any(u=>u.tireUnit&&u.definitionId==pack.definitionId&&u.tire.health==.65f))
                throw new InvalidOperationException("Tire checkout did not preserve price, front members or removed tire.");
        }
    }
}
