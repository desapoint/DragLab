using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class AssemblySetupVerification
    {
        [MenuItem("DragLab/Verify Stored Assemblies and Saved Setups")]
        public static void RunStandalone()
        {
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var folder = Path.Combine(root, "reports/phases/3b"); Directory.CreateDirectory(folder);
            var log = new StringBuilder(); int count = 0;
            Action<bool,string> check = (ok,label) => { if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(folder,"verification-progress.txt"),log.ToString()); };
            var catalog = MechanicsTransactions.Catalog();
            Func<PlayerProfile,Action<PlayerProfile>,PlayerProfile> commit = (p,op) => { PlayerProfile next;string error; if(!MechanicsTransactions.Try(p,catalog,p.revision,op,q=>true,out next,out error))throw new InvalidOperationException(error);return next; };
            Func<string,PlayerProfile> fresh = model => { var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.cars[0].vehicleId=model;p.selectedVehicleId=model;p.cash=200000;p.reputation=99;MechanicsOwnership.Ensure(p,catalog);return p; };
            Action<Action> rejects = action => { bool failed=false;try{action();}catch(InvalidOperationException){failed=true;}check(failed,"Invalid operation rejected without granting hardware."); };
            foreach(var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                string model=asset.spec.id;var p=fresh(model);var original=p.SelectedCar;string carId=original.instanceId,engineId=original.engineAssemblyId;
                original.fluids.oilLitres=2;original.fluids.coolantLitres=3;original.fluids.oilCondition=.5;original.fuel.litres=7;original.tune.tirePressurePsi=24;
                var piston=p.parts.Find(i=>i.instanceId==original.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);piston.condition.health=.62f;piston.condition.elements[0].health=.4f;piston.condition.elements[0].fault=ElementFault.CompressionLeak;piston.condition.elements[0].severity=.25f;
                string condition=JsonUtility.ToJson(piston.condition);int parts=p.parts.Count;
                var preview=AssemblyOperations.PreviewKeepEngine(p,carId);
                check(preview.retainedParts.Length+preview.remainingParts.Length==original.installed.slots.Count&&preview.retainedParts.Intersect(preview.remainingParts).Count()==0,"Keep/dispose preview partitions actual descendants: "+model);
                p=commit(p,q=>AssemblyOperations.Detach(q,carId));
                check(!MechanicsOwnership.Resolve(p,p.SelectedCar,catalog).Drivable&&p.SelectedCar.fuel.litres==7&&p.SelectedCar.fluids.oilLitres==0&&p.parts.Count==parts,"Detach preserves chassis/fuel and blocks missing engine: "+model);
                var stored=p.engines.Find(e=>e.instanceId==engineId);
                check(stored.fluids.oilLitres==2&&stored.fluids.coolantLitres==3&&stored.fluids.oilCondition==.5&&condition==JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==piston.instanceId).condition),"Stored engine retains liquids and individual faults: "+model);
                string detachedJson=JsonUtility.ToJson(p);MechanicsOwnership.Ensure(p,catalog);check(detachedJson==JsonUtility.ToJson(p),"Ensure never refills, reattaches or recreates detached descendants: "+model);
                p=commit(p,q=>AssemblyOperations.RemovePart(q,engineId,PartCategory.Pistons));
                check(AssemblyOperations.Missing(p,p.engines.Find(e=>e.instanceId==engineId)).Contains(PartSemantics.Friendly("Pistons")),"Partial stored engine reports actual missing pistons: "+model);
                p=commit(p,q=>AssemblyOperations.Attach(q,engineId,carId));check(!MechanicsOwnership.Resolve(p,p.SelectedCar,catalog).Drivable,"Incomplete engine can attach without fabricating missing parts: "+model);
                p=commit(p,q=>AssemblyOperations.Detach(q,carId));p=commit(p,q=>AssemblyOperations.InstallPart(q,engineId,piston.instanceId));
                check(condition==JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==piston.instanceId).condition),"Stored reassembly keeps exact used part: "+model);
                p=commit(p,q=>VehicleCollection.Buy(q,catalog,new VehicleOffer{model=model,price=asset.purchasePrice,reputation=asset.reputationRequired,stock=asset.CreateStockTune()}));string targetId=p.SelectedCar.instanceId;
                p.SelectedCar.tune.tirePressurePsi=35;p=commit(p,q=>AssemblyOperations.Detach(q,targetId));p=commit(p,q=>AssemblyOperations.Attach(q,engineId,targetId));
                check(p.SelectedCar.engineAssemblyId==engineId&&p.SelectedCar.tune.tirePressurePsi==35&&p.SelectedCar.fluids.oilLitres==2&&p.cars.Find(c=>c.instanceId==carId).fuel.litres==7,"Transfer changes mounts only, retains engine liquids and target wheel tune: "+model);
                check(MechanicsOwnership.Validate(p,catalog)==null,"Exclusive ownership validates after complete/partial transfer: "+model);
                var setup=fresh(model);carId=setup.SelectedCar.instanceId;string blueprint=SavedSetups.Save(setup,carId,"Stock engine",SetupScope.Engine);string tune=SavedSetups.Save(setup,carId,"Low tire pressure",SetupScope.TuneOnly);
                var binding=setup.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons");string sold=binding.partInstanceId;
                setup=commit(setup,q=>{PartOperations.Remove(q,carId,PartCategory.Pistons);PartOperations.Sell(q,catalog,sold);});
                string before=JsonUtility.ToJson(setup);var plan=SavedSetups.Preview(setup,carId,blueprint);
                check(plan.Missing==1&&plan.cost==PartsMarket.Quote(setup,catalog.Find(catalog.Id(model,PartCategory.Pistons)),InventoryUnits.Count(setup,setup.SelectedCar,catalog.Find(catalog.Id(model,PartCategory.Pistons)))).cost&&before==JsonUtility.ToJson(setup),"Sold setup item produces nonmutating missing purchase quote: "+model);
                rejects(()=>SavedSetups.Apply(setup,carId,blueprint,setup.revision,false));
                int cash=setup.cash;setup=commit(setup,q=>SavedSetups.Apply(q,carId,blueprint,q.revision,true));
                check(setup.cash==cash-plan.cost&&setup.parts.All(i=>i.instanceId!=sold)&&MechanicsOwnership.Resolve(setup,setup.SelectedCar,catalog).Drivable,"Missing-part apply buys new identity and restores working setup atomically: "+model);
                setup.SelectedCar.tune.tirePressurePsi=40;setup=commit(setup,q=>SavedSetups.Apply(q,carId,tune,q.revision,false));check(setup.SelectedCar.tune.tirePressurePsi==30,"Tune-only setup restores adjustments without hardware purchase: "+model);
                string restored=JsonUtility.ToJson(setup);var roundtrip=JsonUtility.FromJson<PlayerProfile>(restored);MechanicsOwnership.Ensure(roundtrip,catalog);check(restored==JsonUtility.ToJson(roundtrip),"Setup catalog survives JSON reload without identity changes: "+model);
            }
            var wear=fresh("starter-hatch");string wearCar=wear.SelectedCar.instanceId, saved=SavedSetups.Save(wear,wearCar,"Forged then sold",SetupScope.WholeCar);
            var used=wear.parts.Find(i=>i.instanceId==wear.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);used.condition.health=.55f;
            PartOperations.Remove(wear,wearCar,PartCategory.Pistons);var reuse=SavedSetups.Preview(wear,wearCar,saved);check(reuse.Missing==0&&reuse.lines.Any(l=>l.itemId==used.instanceId&&!l.purchase),"Preview chooses compatible usable worn loose item instead of purchasing.");
            wear=commit(wear,q=>SavedSetups.Apply(q,wearCar,saved,q.revision,false));check(wear.parts.Find(i=>i.instanceId==used.instanceId).condition.health==.55f,"Loading a blueprint never heals reused hardware.");
            rejects(()=>SavedSetups.Apply(wear,wearCar,saved,wear.revision-1,true));
            string originalJson=JsonUtility.ToJson(wear);PlayerProfile result;string error;
            check(!MechanicsTransactions.Try(wear,catalog,wear.revision,q=>AssemblyOperations.Detach(q,wearCar),q=>false,out result,out error)&&originalJson==JsonUtility.ToJson(wear),"Failed persistence rolls back engine, fluids and descendants.");
            PartOperations.Remove(wear,wearCar,PartCategory.Pistons);PartOperations.Sell(wear,catalog,used.instanceId);wear.cash=0;
            check(!MechanicsTransactions.Try(wear,catalog,wear.revision,q=>SavedSetups.Apply(q,wearCar,saved,q.revision,true),q=>true,out result,out error)&&wear.cash==0&&!wear.SelectedCar.installed.slots.Any(s=>s.slot=="Pistons"),"Unaffordable setup changes no cash/hardware/tune.");
            wear.cash=200000;wear.runCheckpoints.Add(new RunCheckpoint{runId="open",ownedVehicleId=wearCar});rejects(()=>AssemblyOperations.Detach(wear,wearCar));rejects(()=>SavedSetups.Apply(wear,wearCar,saved,wear.revision,true));wear.runCheckpoints.Clear();
            var reserved=fresh("starter-hatch");reserved.SelectedCar.engineRebuild=EngineRebuilds.Quote(reserved,reserved.SelectedCar,DateTime.UtcNow.Ticks);reserved.SelectedCar.engineRebuild.active=true;rejects(()=>AssemblyOperations.Detach(reserved,reserved.SelectedCar.instanceId));
            var mismatch=fresh("starter-hatch");string retained=AssemblyOperations.Detach(mismatch,mismatch.SelectedCar.instanceId);mismatch.cars.Add(new OwnedCar{vehicleId="v8-muscle"});MechanicsOwnership.Ensure(mismatch,catalog);var other=mismatch.cars.Last();AssemblyOperations.Detach(mismatch,other.instanceId);rejects(()=>AssemblyOperations.Attach(mismatch,retained,other.instanceId));
            var engineLess=fresh("starter-hatch");string empty=engineLess.SelectedCar.instanceId;string full=SavedSetups.Save(engineLess,empty,"Restore",SetupScope.WholeCar);AssemblyOperations.Detach(engineLess,empty);
            check(SavedSetups.Preview(engineLess,empty,full).errors.Any(e=>e.Contains("Attach a stored engine")),"Blueprint cannot grant a free engine core to an empty chassis.");
            string pendingTune=SavedSetups.Save(engineLess,empty,"Incomplete tune",SetupScope.TuneOnly);engineLess.SelectedCar.tune.tirePressurePsi=40;var tuningPreview=SavedSetups.Preview(engineLess,empty,pendingTune);
            check(tuningPreview.errors.Count==0&&tuningPreview.warnings.Any(w=>w.Contains("Build remains unavailable")),"Tune-only preview permits adjustments on retained incomplete chassis, with run warnings.");
            engineLess=commit(engineLess,q=>SavedSetups.Apply(q,empty,pendingTune,q.revision,false));check(engineLess.SelectedCar.tune.tirePressurePsi==30&&string.IsNullOrEmpty(engineLess.SelectedCar.engineAssemblyId),"Tune-only apply on missing engine never fabricates hardware/resources.");
            rejects(()=>ServiceOperations.TopUp(engineLess,empty,true,1));rejects(()=>ServiceOperations.ChangeOil(engineLess,empty));rejects(()=>ServiceOperations.FlushCoolant(engineLess,empty));
            rejects(()=>PartOperations.Buy(engineLess,catalog,empty,catalog.Id("starter-hatch",PartCategory.Pistons),true));
            var optional=fresh("starter-hatch");string optionalCar=optional.SelectedCar.instanceId;PartOperations.Remove(optional,optionalCar,PartCategory.Interior);string stripped=SavedSetups.Save(optional,optionalCar,"Stripped",SetupScope.WholeCar);
            var interior=optional.parts.First(i=>i.location==PartLocation.Inventory);PartOperations.Install(optional,catalog,optionalCar,interior.instanceId);optional=commit(optional,q=>SavedSetups.Apply(q,optionalCar,stripped,q.revision,false));
            check(!optional.SelectedCar.installed.slots.Any(s=>s.slot=="Interior")&&optional.parts.Any(i=>i.instanceId==interior.instanceId&&i.location==PartLocation.Inventory),"Optional omission restores stripped build while retaining interior in inventory.");
            var failed=fresh("starter-hatch");string failedCar=failed.SelectedCar.instanceId;string failedSetup=SavedSetups.Save(failed,failedCar,"Healthy target",SetupScope.Engine);
            var bad=failed.parts.First(i=>i.instanceId==failed.SelectedCar.installed.slots.First(s=>s.slot=="Pistons").partInstanceId);bad.condition.elements[0].fault=ElementFault.Disabled;
            check(SavedSetups.Preview(failed,failedCar,failedSetup).Missing==1,"Individually disabled piston pack is not silently selected as a usable match.");
            string failedBefore=JsonUtility.ToJson(failed);check(!MechanicsTransactions.Try(failed,catalog,failed.revision,q=>SavedSetups.Apply(q,failedCar,failedSetup,q.revision,true),q=>false,out result,out error)&&failedBefore==JsonUtility.ToJson(failed),"Failed setup persistence rolls back purchased cash, old failed originals and tuning.");
            var failedPlan=SavedSetups.Preview(failed,failedCar,failedSetup);var price=catalog.Find(catalog.Id("starter-hatch",PartCategory.Pistons));price.price++;rejects(()=>SavedSetups.Apply(failed,failedCar,failedSetup,failed.revision,true,failedPlan.cost));price.price--;
            failed=commit(failed,q=>SavedSetups.Apply(q,failedCar,failedSetup,q.revision,true));check(failed.parts.Any(i=>i.instanceId==bad.instanceId&&i.location==PartLocation.Inventory&&i.condition.elements[0].fault==ElementFault.Disabled),"Replacing failed matches retains the original damaged pack for service/inventory.");
            var efi=fresh("v8-muscle");string efiCar=efi.SelectedCar.instanceId;
            PartOperations.Buy(efi,catalog,efiCar,"v8-muscle/FuelSystem/EfiPortInjection",true);string incomplete=SavedSetups.Save(efi,efiCar,"Missing ECU",SetupScope.Engine);
            check(SavedSetups.Preview(efi,efiCar,incomplete).errors.Any(e=>e.Contains("injection-capable")),"Missing EFI controller dependency blocks blueprint apply rather than bypassing hardware.");
            PartOperations.Buy(efi,catalog,efiCar,"v8-muscle/Controls/EfiStreet",true);string injected=SavedSetups.Save(efi,efiCar,"EFI engine",SetupScope.Engine);
            var kit=efi.parts.Find(i=>i.instanceId==efi.SelectedCar.installed.slots.First(s=>s.slot=="FuelSystem").partInstanceId);var ecu=efi.parts.Find(i=>i.instanceId==efi.SelectedCar.installed.slots.First(s=>s.slot=="Controls").partInstanceId);
            PartOperations.Remove(efi,efiCar,PartCategory.FuelSystem);PartOperations.Sell(efi,catalog,kit.instanceId);PartOperations.Remove(efi,efiCar,PartCategory.Controls);PartOperations.Sell(efi,catalog,ecu.instanceId);
            check(SavedSetups.Preview(efi,efiCar,injected).Missing==2,"EFI engine blueprint includes its associated chassis ECU purchase.");
            efi=commit(efi,q=>SavedSetups.Apply(q,efiCar,injected,q.revision,true));check(MechanicsOwnership.Resolve(efi,efi.SelectedCar,catalog).spec.engineBehavior.fueling==FuelSystem.Injection&&MechanicsOwnership.Resolve(efi,efi.SelectedCar,catalog).Drivable,"All missing EFI dependencies apply together as a working engine.");
            var looseOwner=fresh("city-three");AssemblyOperations.Detach(looseOwner,looseOwner.SelectedCar.instanceId);looseOwner.cars.Add(new OwnedCar{vehicleId="v8-muscle"});MechanicsOwnership.Ensure(looseOwner,catalog);
            string v8Id=looseOwner.cars.Last().instanceId;string looseV8=PartOperations.Buy(looseOwner,catalog,v8Id,catalog.Id("v8-muscle",PartCategory.Pistons),false);MechanicsOwnership.Ensure(looseOwner,catalog);
            check((looseOwner.parts.Find(i=>i.instanceId==looseV8).condition.elements??new EngineElement[0]).Length==8,"New loose V8 kits contain the purchased eight members even when another owned chassis has no engine.");
            PartOperations.Install(looseOwner,catalog,v8Id,looseV8);MechanicsOwnership.Ensure(looseOwner,catalog);check(looseOwner.parts.Find(i=>i.instanceId==looseV8).condition.elements.Length==8,"The eight purchased V8 members remain fitted on their real owner.");
            check(catalog.Fits(catalog.Spec("sport-coupe"),catalog.Find(catalog.Id("starter-hatch",PartCategory.FuelSystem)))&&!catalog.Fits(catalog.Spec("touring-six"),catalog.Find(catalog.Id("starter-hatch",PartCategory.FuelSystem)))&&!catalog.Fits(catalog.Spec("city-three"),catalog.Find(catalog.Id("starter-hatch",PartCategory.FuelSystem))),"Four-cylinder injector packs remain shared, but cannot silently supply three/six-cylinder engines.");
            // Unsupported old contracts stay read-only; current assemblies still round-trip.
            string file=Path.Combine(folder,"contracts",Guid.NewGuid().ToString("N"),"profile-v1.json");Directory.CreateDirectory(Path.GetDirectoryName(file));var old=fresh("starter-hatch");old.saveVersion=3;string legacy=JsonUtility.ToJson(old);File.WriteAllText(file,legacy,System.Text.Encoding.UTF8);
            string previousPath=ProfileStore.VerificationPath;ProfileStore.VerificationPath=file;
            try {
                var rejected=ProfileStore.Load();check(rejected.SelectedCar==null&&!ProfileStore.TrySave(rejected)&&File.ReadAllText(file,System.Text.Encoding.UTF8)==legacy,"Unsupported schema-3 data is preserved and writes remain blocked.");
                old.saveVersion=MechanicsOwnership.SaveVersion;File.WriteAllText(file,JsonUtility.ToJson(old),System.Text.Encoding.UTF8);var loaded=ProfileStore.Load();check(loaded.SelectedCar.instanceId==old.SelectedCar.instanceId,"Current-contract assembly ownership reloads with exact original IDs.");
                string engine=AssemblyOperations.Detach(loaded,loaded.SelectedCar.instanceId);SavedSetups.Save(loaded,loaded.SelectedCar.instanceId,"Partial chassis",SetupScope.TuneOnly);ProfileStore.Save(loaded);var again=ProfileStore.Load();check(again.engines.Find(e=>e.instanceId==engine).slots.Count>0&&again.savedSetups.Count==1&&string.IsNullOrEmpty(again.SelectedCar.engineAssemblyId),"Disk reload retains stored engine, partial chassis and named tune.");
                var reset=again.RestartKeepingPreferences(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(reset,catalog);check(reset.savedSetups.Count==0&&reset.engines.Count==0&&reset.cars.Count==0&&reset.firstPurchasePending,"Progress reset clears configuration blueprints and stored assemblies.");
            } finally { ProfileStore.VerificationPath=previousPath; }
            int regressions=OwnershipVerification.Run(root)+InventoryVerification.Run(root);
            File.WriteAllText(Path.Combine(folder,"verification.txt"),"Model "+MechanicsSchema.Model+" / player schema "+MechanicsOwnership.SaveVersion+"\n"+log+"Checks: "+count+" / prior ownership/inventory: "+regressions+" / total "+(count+regressions)+"\n");Debug.Log("DRAGLAB ASSEMBLY SETUP PASS "+(count+regressions));
        }
    }
}
