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
    public static class CollectionVerification
    {
        public static void RunStandalone() { VehicleRoster.Prepare(); Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))); }
        public static int Run(string root)
        {
            string folder=Path.Combine(root,"reports/phases/3a");Directory.CreateDirectory(folder);int count=0;var log=new StringBuilder();
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
            var assets=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var catalog=MechanicsTransactions.Catalog();
            check(assets.Length==8&&assets.Select(a=>a.spec.id).Distinct().Count()==8,"Eight unique authored cars are available.");
            var calibration=new StringBuilder("model,cylinders,litres,drive,price,quarter_s,trap_kmh,lowest_health\n");
            foreach(var asset in assets){
                check(asset.spec.Validate()==null&&asset.purchasePrice>0,"Valid specification and paid repeat purchase: "+asset.spec.id);
                var recipe=catalog.Recipe(asset.spec.id);var build=catalog.Resolve(asset.spec.id,recipe,asset.CreateStockTune());
                check(build.Drivable,"Complete OEM recipe resolves: "+asset.spec.id);
                foreach(var part in build.spec.fittedParts)check(PartCatalogOverrides.Validate(part,part.Copy())==null,"Authored part validates: "+part.id);
                var pass=Benchmark.QuarterMile(build.spec,build.tune);check(pass.elapsed>8&&pass.elapsed<30&&pass.trapKmh>70,"Stock benchmark finishes: "+asset.spec.id);
                var car=new PhysicalDrivingSession(new RunSnapshot{vehicle=build.spec,tune=build.tune,driving=new DriverSettings{wearMode=WearMode.Full}});
                foreach(var input in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,13))car.Step(input);
                check(car.car.state.contact.Validate()==null&&!car.car.state.engineFailed&&car.car.mechanics.parts.All(p=>!p.condition.failed),"Stock pull/shift/coast/brake survives with full wear: "+asset.spec.id);
                calibration.AppendLine(string.Join(",",asset.spec.id,asset.spec.sound.cylinders,asset.spec.DisplacementLitres.ToString("R",System.Globalization.CultureInfo.InvariantCulture),asset.spec.drive,asset.purchasePrice,pass.elapsed.ToString("R",System.Globalization.CultureInfo.InvariantCulture),pass.trapKmh.ToString("R",System.Globalization.CultureInfo.InvariantCulture),car.car.mechanics.parts.Min(p=>p.condition.health).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            }
            var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.cash=100000;p.reputation=99;MechanicsOwnership.Ensure(p,catalog);string original=p.SelectedCar.instanceId;
            var starter=assets.Single(a=>a.spec.id=="starter-hatch");var offer=new VehicleOffer{model=starter.spec.id,stock=starter.CreateStockTune(),price=starter.purchasePrice,reputation=starter.reputationRequired};
            PlayerProfile next;string error;long rev=p.revision;
            check(MechanicsTransactions.Try(p,catalog,rev,q=>VehicleCollection.Buy(q,catalog,offer),q=>true,out next,out error),"Paid second copy commits atomically.");p=next;string copy=p.SelectedCar.instanceId;
            check(p.cars.Count==2&&copy!=original&&p.cash==100000-offer.price&&p.cars[0].engineAssemblyId!=p.cars[1].engineAssemblyId,"Same-model copies own separate vehicle/chassis/engine trees.");
            check(!MechanicsTransactions.Try(p,catalog,rev,q=>VehicleCollection.Buy(q,catalog,offer),q=>true,out next,out error),"Stale duplicate purchase cannot debit or create another copy.");
            string before=JsonUtility.ToJson(p);check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>VehicleCollection.Buy(q,catalog,offer),q=>false,out next,out error)&&before==JsonUtility.ToJson(p),"Failed disk write retains original cash/selection/ownership.");
            var first=p.cars.Find(c=>c.instanceId==original);first.fuel.litres=7;first.tune.tirePressurePsi=22;first.completedRuns=3;first.SavePreset(0);
            var binding=first.installed.slots.Find(b=>b.slot=="Pistons");var worn=p.parts.Find(i=>i.instanceId==binding.partInstanceId);worn.condition.health=.4f;
            check(p.SelectedCar.fuel.litres!=7&&p.SelectedCar.completedRuns==0&&p.SelectedCar.presets.Count==0,"Fuel, tune presets, records and condition remain independent.");
            PartOperations.Transfer(p,catalog,original,copy,PartCategory.Pistons);
            check(!first.installed.slots.Any(b=>b.slot=="Pistons")&&p.SelectedCar.installed.slots.Any(b=>b.partInstanceId==worn.instanceId)&&worn.condition.health==.4f,"Donor transfer moves the actual worn identity once and leaves only its donor incomplete.");
            string[] identities=p.parts.Select(i=>i.instanceId).ToArray();MechanicsOwnership.Ensure(p,catalog);
            check(identities.SequenceEqual(p.parts.Select(i=>i.instanceId))&&!first.installed.slots.Any(b=>b.slot=="Pistons"),"Repeated ownership initialization does not recreate sold/removed components.");
            string fixture=Path.Combine(folder,"save-fixture-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),"profile-v1.json");ProfileStore.VerificationPath=fixture;
            try{check(ProfileStore.TrySave(p),"Same-model collection saves.");var loaded=ProfileStore.Load();check(loaded.SelectedCar.instanceId==copy&&loaded.cars.Count==2&&loaded.cars.Find(c=>c.instanceId==original).fuel.litres==7,"Reload selects the exact instance and retains independent resources.");check(MechanicsOwnership.Validate(loaded,catalog)==null,"Loaded collection has exclusive valid ownership.");}finally{ProfileStore.VerificationPath=null;}
            var poor=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(poor,catalog);poor.cash=0;string unchanged=JsonUtility.ToJson(poor);
            check(!MechanicsTransactions.Try(poor,catalog,poor.revision,q=>VehicleCollection.Buy(q,catalog,offer),q=>true,out next,out error)&&unchanged==JsonUtility.ToJson(poor),"Insufficient cash rejects the complete purchase.");
            poor.cash=100000;var locked=assets.Single(a=>a.spec.id=="super-v8");check(!MechanicsTransactions.Try(poor,catalog,poor.revision,q=>VehicleCollection.Buy(q,catalog,new VehicleOffer{model=locked.spec.id,stock=locked.CreateStockTune(),price=locked.purchasePrice,reputation=locked.reputationRequired}),q=>true,out next,out error),"Reputation requirements remain enforced for new cars.");
            count+=OwnershipVerification.Run(root)+InventoryVerification.Run(root);
            File.WriteAllText(Path.Combine(folder,"calibration.csv"),calibration.ToString());File.WriteAllText(Path.Combine(folder,"verification.txt"),"Model: "+MechanicsSchema.Model+"\n"+log+"Checks including ownership/inventory: "+count+"\n");Debug.Log("DRAGLAB COLLECTION VERIFICATION PASS: "+count);return count;
        }
    }
}
