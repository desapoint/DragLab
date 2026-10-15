using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
            foreach(float health in new[]{.001f,.01f,.1f,.3f,.65f,.9f,.999f,1f})foreach(var wear in new[]{WearMode.None,WearMode.Partial,WearMode.Full})foreach(bool oldRounding in new[]{false,true}){
                var condition=new ComponentCondition{health=health};TireDynamics.SeedNew(condition);
                for(int i=0;i<4;i++){condition.tires[i].id=pack.condition.tires[i].id;condition.tires[i].definitionId=pack.condition.tires[i].definitionId;if(oldRounding)condition.tires[i].consumedLife=1-health;}
                pack.condition=condition;car.driver.wearMode=wear;
                for(int tick=0;tick<120;tick++){var prior=condition.Copy();TireDynamics.Tick(car,1.0/120,car.state.distance);check(condition.tires.All(t=>t.health<=health)&&RunPersistence.ConditionAdvances(prior,condition),"Idle health/checkpoint / "+health+" / "+wear+" / old rounding "+oldRounding+" / "+tick);}
                if(wear!=WearMode.None){var prior=condition.Copy();double distance=car.state.distance;car.state.distance+=5;car.state.contact.slipWorkJ[2]+=1000;TireDynamics.Tick(car,1.0/120,distance);check(condition.tires[2].consumedLife>prior.tires[2].consumedLife&&RunPersistence.ConditionAdvances(prior,condition),"Contact work consumes life without healing / "+health+" / "+wear+" / old rounding "+oldRounding);}
                car.state.distance=0;Array.Clear(car.state.contact.slipWorkJ,0,4);
            }
            foreach(var tire in pack.condition.tires){tire.health=1;tire.consumedLife=0;tire.failed=false;}
            var exhausted=pack.condition.tires[0];exhausted.consumedLife=1;
            check(TireDynamics.Grip(car.mechanics.Definition(pack),exhausted)==.025&&RunPersistence.FailureCount(car)==1&&TireDynamics.AnyFailed(pack.condition),"Exhausted life alone consistently means failed grip and one failed tire");
            exhausted.consumedLife=.9;var reference=exhausted.Copy();reference.health=.1f;reference.consumedLife=.9;
            check(Math.Abs(TireDynamics.Grip(car.mechanics.Definition(pack),exhausted)-TireDynamics.Grip(car.mechanics.Definition(pack),reference))<1e-7,"Grip uses remaining life even when a retained health field is higher");
            var ownedTires=p.parts.First(part=>catalog.Find(part.definitionId).category==PartCategory.Tires);ownedTires.condition.tires[0].health=1;ownedTires.condition.tires[0].consumedLife=1;
            var values=Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;
            check(Valuations.Capture(p,catalog,values,ValuationKind.Car,p.SelectedCar.instanceId,"wholesale",DateTime.UtcNow.Ticks).Faulty,"A car valuation reports an exhausted tire even without a redundant failed flag");
            check(InspectionBuild.Capture(p,p.SelectedCar).build.warnings.Any(w=>w=="Front left tire failed")&&MechanicsOwnership.Resolve(p,p.SelectedCar,catalog).warnings.Any(w=>w=="Front left tire failed"),"Read-only inspection and owned build both disclose an exhausted tire");
            string alternative=catalog.Id(p.SelectedCar.vehicleId,PartCategory.Tires,1);
            var spare=new TireCondition{id="audit-exhausted-spare",definitionId=alternative,health=1,consumedLife=1};p.looseEngineElements.Add(new LooseEngineElement{definitionId=alternative,tireUnit=true,element=spare});
            var plan=new SetupPlan();SavedTireFitments.Plan(p,p.SelectedCar,new SetupPart{slot="Tires",tireDefinitions=Enumerable.Repeat(alternative,4).ToArray()},new SetupPlanLine{itemId=ownedTires.instanceId,definitionId=ownedTires.definitionId},plan);
            check(plan.tires.Count==4&&plan.tires.All(line=>line.purchase),"Saved setups never reuse an exhausted loose tire as working hardware");
            var obsolete=new RunSnapshot{vehicle=build.spec.Copy(),tune=build.tune.Copy(),driving=new DriverSettings(),model="draglab-variety-v19"};
            check(PhysicalDrivingSession.Validate(obsolete)?.StartsWith("Different physics model:")==true,"Older driving traces explicitly require rerecording after the tire physics correction");
            var clutch=car.mechanics.parts.First(part=>part.slot=="Clutch");clutch.condition.clutchOilCondition=.99999910527475766;
            var checkpoint=car.CaptureDynamics();var decoded=JsonUtility.FromJson<CarDynamicsCheckpoint>(JsonUtility.ToJson(checkpoint));ExactPhysicsNumbers.Decode(decoded);
            check(BitConverter.DoubleToInt64Bits(decoded.components.First(part=>part.slot=="Clutch").condition.clutchOilCondition)==BitConverter.DoubleToInt64Bits(clutch.condition.clutchOilCondition),"Nested clutch-fluid condition survives JSON continuation with exact double bits");

            string oldPath=ProfileStore.VerificationPath,oldAudio=SharedAudioSettings.VerificationPath,oldUnits=SharedUnitSettings.VerificationPath;
            var injection=typeof(ProfileStore).Assembly.GetType("DragLab.Infrastructure.SqliteStateStore").GetField("FailureInjection",BindingFlags.NonPublic|BindingFlags.Static);var oldInjection=injection.GetValue(null);
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
                File.WriteAllText(ProfileStore.FilePath+".reset-intent","2|1",Encoding.UTF8);
                string preferences=Path.Combine(fixtures,"driving-settings.json");File.WriteAllText(preferences,"{\"version\":1,\"driving\":"+JsonUtility.ToJson(new DriverSettings{wearMode=WearMode.Partial,shifting=ShiftMode.Sequential})+",\"difficulty\":2}",Encoding.UTF8);byte[] preferencesBefore=File.ReadAllBytes(preferences);
                var originals=new Dictionary<string,byte[]>();foreach(string suffix in new[]{"",".bak",".reset-generation",".reset-intent"})originals.Add(ProfileStore.FilePath+suffix,File.ReadAllBytes(ProfileStore.FilePath+suffix));
                ProfileStore.Load();check(ProfileStore.ResetRequired,"Incompatible fixture requests explicit reset");
                injection.SetValue(null,(Action<string>)(phase=>{if(phase=="before-commit")throw new IOException("Injected incompatible reset write failure");}));
                check(!ProfileStore.TryResetIncompatibleSave(out var injectedError)&&ProfileStore.ResetRequired,"A failed incompatible reset stays blocked: "+injectedError);injection.SetValue(null,null);
                check(originals.All(pair=>File.Exists(pair.Key)&&pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))),"Failed incompatible reset restores databases and reset markers exactly");
                string error;bool reset=ProfileStore.TryResetIncompatibleSave(out error);
                check(reset,"Explicit incompatible reset succeeds: "+error);
                var fresh=ProfileStore.Load();log.AppendLine("Reload status: "+ProfileStore.Status);
                check(!ProfileStore.ResetRequired,"Explicit reset remains loadable after an earlier normal progression reset");
                check(originals.All(pair=>Directory.GetFiles(fixtures,Path.GetFileName(pair.Key)+".before-reset-*").Any(file=>pair.Value.SequenceEqual(File.ReadAllBytes(file)))),"Successful incompatible reset archives exact databases and both recovery markers");
                check(preferencesBefore.SequenceEqual(File.ReadAllBytes(preferences))&&fresh.driving.wearMode==WearMode.Partial&&fresh.driving.shifting==ShiftMode.Sequential&&fresh.difficulty==2,"Incompatible reset preserves and reloads separate driving preferences");
            }
            finally
            {
                RaceCheckpointStore.Drain();ProfileStore.VerificationPath=oldPath;
                injection.SetValue(null,oldInjection);
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
