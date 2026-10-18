using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DriveType = DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    // Shared simulation, isolated fixtures. Never writes the real player save.
    public static class TireProgressionVerification
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        static string Folder => Path.Combine(Root,"reports/tires-wear-20261018");
        static string Fixture => Path.Combine(Root,"reports/player-fixtures/tires-wear-20261018");
        static string N(double value) => value.ToString("G9",CultureInfo.InvariantCulture);
        [Serializable] sealed class JournalData { public int version=1; public long baseRevision; public RunCheckpoint checkpoint; }
        [Serializable] sealed class JournalEnvelope { public string payload,sha256; }
        sealed class Pass
        {
            public CarSimulation car;public double seconds,peakSlip,peakBoost,peakHp,heat,used;
            public double shaftWork,engineWork,clutchHeat;
            public readonly double[] spin=new double[11];public bool finished;
        }
        static DriverSettings Driver(WearMode wear,bool assist=false) => new DriverSettings{
            shifting=ShiftMode.Automatic,launch=LaunchMode.Automatic,tractionAssist=assist,throttleAssist=false,
            wearMode=wear,fuelUsage=FuelUsageMode.None,fluidAging=ConsumptionMode.None,fluidDepletion=ConsumptionMode.None,
            raceTemperatures=RaceTemperatureMode.FullyCooled};
        static Pass Drive(BuildResolution b,DriverSettings driver,IEnumerable<PartInstance> parts=null,bool shutdown=true,bool stopOnTireFailure=false,double maxSeconds=120)
        {
            var result=new Pass();var car=result.car=new CarSimulation(b.spec,b.tune,driver,parts);
            car.ApplyRaceTemperatures(driver.raceTemperatures,0,DateTime.UtcNow.Ticks);car.Launch(0);
            for(int tick=0;tick<maxSeconds/DragMath.FixedStep&&car.state.distance<402.336&&!car.state.engineFailed&&!car.state.drivetrainFailed;tick++){
                car.Step(DragMath.FixedStep,new DriverInput{throttle=1});result.seconds+=DragMath.FixedStep;
                if(result.seconds<=8){result.shaftWork+=Math.Max(0,car.state.transmittedTorqueNm*car.GearboxInputRpm/FlywheelDynamics.RpmPerRadian)*DragMath.FixedStep;
                    result.engineWork+=Math.Max(0,car.state.engineHorsepower*745.7)*DragMath.FixedStep;result.clutchHeat+=car.state.clutchFrictionPowerW*DragMath.FixedStep;}
                result.peakBoost=Math.Max(result.peakBoost,car.state.boostBar);result.peakHp=Math.Max(result.peakHp,car.state.engineHorsepower);
                double slip=car.spec.drive==DriveType.FWD?Math.Max(Math.Abs(car.state.frontLeftSlip),Math.Abs(car.state.frontRightSlip)):car.spec.drive==DriveType.RWD?Math.Max(Math.Abs(car.state.rearLeftSlip),Math.Abs(car.state.rearRightSlip)):car.state.slip;
                result.peakSlip=Math.Max(result.peakSlip,slip);
                if(car.state.speed>5&&slip>.15)result.spin[Math.Min(10,Math.Max(0,car.state.gear))]+=DragMath.FixedStep;
                if(stopOnTireFailure&&TireDynamics.AnyFailed(car.mechanics.Part(PartCategory.Tires).condition))break;
            }
            result.finished=car.state.distance>=402.336;
            if(shutdown&&!(stopOnTireFailure&&TireDynamics.AnyFailed(car.mechanics.Part(PartCategory.Tires).condition)))for(int tick=0;tick<12000&&car.state.speed>.1;tick++)car.IndependentStep(DragMath.FixedStep,new DriverInput{brake=.8},true,false);
            var pack=car.mechanics.Part(PartCategory.Tires);result.used=pack.condition.tires.Max(t=>t.consumedLife);result.heat=pack.condition.tires.Max(t=>t.temperatureC);
            return result;
        }
        static BuildResolution Build(MechanicalCatalog cat,VehicleDefinition v,string tire,bool tuned)
        {
            var t=v.CreateStockTune();var recipe=cat.Recipe(v.spec.id);
            if(tuned){
                foreach(var c in new[]{PartCategory.HeadIntake,PartCategory.Body,PartCategory.Gearbox})recipe[c]=cat.Id(v.spec.id,c,3);
                foreach(var c in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.Clutch,PartCategory.FuelSystem,PartCategory.Ignition,PartCategory.Cooling,PartCategory.Controls})if(recipe.ContainsKey(c))recipe[c]=cat.Id(v.spec.id,c,1);
                t.boostSetting=1.2f;t.launchRpm=4500;
            }
            recipe[PartCategory.Tires]=tire;return cat.Resolve(v.spec.id,recipe,t);
        }
        public static void Baseline(){Matrix("before");}
        static void Matrix(string label)
        {
            Directory.CreateDirectory(Folder);var cat=MechanicsTransactions.Catalog();var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var csv=new StringBuilder("source,model,tire,grip,rolling_km,sliding_mj,quarter_s,finished,peak_hp,peak_boost,spin_1_s,spin_2_s,spin_3plus_s,peak_slip,end_temp,consumed_life\n");
            void Row(string source,string model,string tire,Pass pass){var d=cat.Find(tire);csv.AppendLine(string.Join(",",source,model,tire,N(d.grip),N(d.tireLifeDistanceM/1000),N(d.wearEnergyJ/1e6),N(pass.seconds),pass.finished,N(pass.peakHp),N(pass.peakBoost),N(pass.spin[1]),N(pass.spin[2]),N(pass.spin.Skip(3).Sum()),N(pass.peakSlip),N(pass.heat),N(pass.used)));}
            foreach(string model in new[]{"starter-hatch","v8-muscle","rally-sedan","super-v8","timber-diesel"}){
                var v=vehicles.Single(x=>x.spec.id==model);
                foreach(var d in cat.parts.Where(p=>p.id.StartsWith(model+"/Tires/",StringComparison.Ordinal)&&p.category==PartCategory.Tires))
                    Row("tuned_reference",model,d.id,Drive(Build(cat,v,d.id,true),Driver(WearMode.Full)));
                var stock=Drive(Build(cat,v,cat.Id(model,PartCategory.Tires),false),Driver(WearMode.Full,true));Row("stock_clean",model,cat.Id(model,PartCategory.Tires),stock);
            }
            string path=Path.Combine(Fixture,"live-save.sqlite3");
            if(File.Exists(path)){
                var previous=ProfileStore.VerificationPath;PlayerProfile p;
                try{ProfileStore.VerificationPath=path;p=ProfileStore.Load();}finally{ProfileStore.VerificationPath=previous;}
                foreach(var car in p.cars.Take(3)){
                    var b=MechanicsOwnership.Resolve(p,car,cat);if(!b.Drivable)continue;
                    var owned=p.parts.Where(i=>i.ownerId==car.instanceId||i.ownerId==car.engineAssemblyId).ToArray();
                    Row("owned_snapshot",car.vehicleId,TireFitments.DefinitionId(owned.First(i=>i.slot=="Tires"),car.vehicleId=="v8-muscle"?2:0),Drive(b,Driver(WearMode.Full),owned));
                    if(car.vehicleId=="v8-muscle")foreach(string key in new[]{"3","ProRadial","SlickMedium","SlickSoft"}){
                        string id=car.vehicleId+"/Tires/"+key;if(cat.Find(id)==null)continue;
                        var preview=EngineWorkshop.Copy(p);preview.cash=100000; // Isolated paid-fit diagnostic, never the original save.
                        PartOperations.Buy(preview,cat,car.instanceId,id,true);var target=preview.cars.Find(c=>c.instanceId==car.instanceId);
                        Row("owned_new_tires",car.vehicleId,id,Drive(MechanicsOwnership.Resolve(preview,target,cat),Driver(WearMode.Full),preview.parts.Where(i=>i.ownerId==target.instanceId||i.ownerId==target.engineAssemblyId)));
                    }
                }
            }
            File.WriteAllText(Path.Combine(Folder,label+"-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
        }
        public static void Run()
        {
            Matrix("after");var cat=MechanicsTransactions.Catalog();var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            int checks=0;var log=new StringBuilder();void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;log.AppendLine("PASS: "+message);}
            foreach(var v in vehicles){var tires=cat.parts.Where(p=>p.category==PartCategory.Tires&&p.id.StartsWith(v.spec.id+"/",StringComparison.Ordinal)).ToArray();Check(tires.Length>=10,v.spec.id+": distinct compound choices");Check(tires.Max(p=>p.grip)>2.5,v.spec.id+": high-output grip headroom");foreach(var t in tires)Check(PartCatalogOverrides.Validate(t,t)==null,t.id+": valid authoring and no tuning unlocked");}
            foreach(var mode in new[]{WearMode.Full,WearMode.Partial,WearMode.None}){
                var v=vehicles.Single(x=>x.spec.id=="v8-muscle");var b=Build(cat,v,cat.Id(v.spec.id,PartCategory.Tires,3),false);var car=new CarSimulation(b.spec,b.tune,Driver(mode));
                for(int pass=0;pass<50;pass++){car.state.distance+=600;for(int i=0;i<4;i++)car.state.contact.slipWorkJ[i]+=1500;car.state.frontLoadN=car.state.rearLoadN=8000;TireDynamics.Tick(car,DragMath.FixedStep,car.state.distance-600);}
                double used=car.mechanics.Part(PartCategory.Tires).condition.tires[0].consumedLife;
                Check(mode==WearMode.None?used==0:mode==WearMode.Partial?used>.15&&used<.35:used>.8&&used<1.3,"50 reference clean passes / "+mode+": appropriate persistent wear");
            }
            foreach(string key in new[]{"StreetSemiSlick","EnduranceRadial","ProRadial","SlickMedium","SlickSoft","QualifyingSlick"}){
                var v=vehicles.Single(x=>x.spec.id=="v8-muscle");var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,cat);p.cash=100000;p.reputation=1000;
                VehicleCollection.Buy(p,cat,new VehicleOffer{model=v.spec.id,price=1,reputation=0,stock=v.CreateStockTune(),paint="#FFFFFF"});var owned=p.SelectedCar;
                var pack=p.parts.Find(i=>i.instanceId==owned.installed.slots.Find(b=>b.slot=="Tires").partInstanceId);TireDynamics.Ensure(pack.condition);
                pack.condition.tires[2].health=.4f;pack.condition.tires[2].consumedLife=.6;string prior=JsonUtility.ToJson(pack.condition.tires[0]);string id=v.spec.id+"/Tires/"+key;
                var lines=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=id,role="tire",quantity=2,install=true,positions=new List<string>{"tire:2","tire:3"}}};
                long now=DateTime.UtcNow.Ticks;var quote=PartCart.Quote(p,owned.instanceId,lines,now,60);int cash=p.cash;string packId=pack.instanceId;
                bool accepted=MechanicsTransactions.Try(p,cat,p.revision,a=>PartCart.Checkout(a,quote,now),a=>true,out var next,out string error);
                Check(accepted,key+": checkout publishes through the real ledger transaction / "+error);p=next;pack=p.parts.Find(i=>i.instanceId==packId);
                Check(p.cash==cash-quote.total&&prior==JsonUtility.ToJson(pack.condition.tires[0])&&TireFitments.DefinitionId(pack,2)==id,key+": two rear units charge the reviewed stock/express total and keep fronts");
                Check(p.looseEngineElements.Any(u=>u.tireUnit&&u.tire.health==.4f&&u.tire.consumedLife==.6),key+": removed worn tire remains in inventory");
                var restored=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(restored,cat);var actual=MechanicsOwnership.Resolve(restored,restored.SelectedCar,cat);
                Check(actual.Drivable&&actual.spec.cornerTireDefinitions[2].id==id&&MechanicsOwnership.Validate(restored,cat)==null,key+": actual per-corner compound and ownership survive reload");
            }
            var health=new ComponentCondition();for(int i=0;i<120000;i++)ComponentWear.Apply(health,1e-9);
            Check(Math.Abs((1-health.health)+health.serviceWearRemainder-.00012)<1e-10,"Tiny mechanical losses accumulate instead of rounding away");
            var restoredHealth=JsonUtility.FromJson<ComponentCondition>(JsonUtility.ToJson(health));Check(restoredHealth.health==health.health&&restoredHealth.serviceWearRemainder==health.serviceWearRemainder,"Sub-tick wear remainder survives serialization");
            foreach(string model in new[]{"draglab-variety-v18","draglab-variety-v19","draglab-variety-v20","unknown-future-model"}){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,cat);var build=MechanicsOwnership.Resolve(p,p.SelectedCar,cat);
                var sim=new CarSimulation(build.spec,build.tune,new DriverSettings(),p.parts);string run=MechanicsOwnership.NewId("tire-upgrade");
                MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,0,sim));
                var cp=RunPersistence.Capture(run,p.SelectedCar.instanceId,120,sim);cp.model=model;cp.components[0].condition.health=.9f;
                string path=Path.Combine(Fixture,Guid.NewGuid().ToString("N")+".json");var data=new JournalData{baseRevision=p.revision,checkpoint=cp};string payload=JsonUtility.ToJson(data),hash;
                using(var sha=System.Security.Cryptography.SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
                File.WriteAllText(RaceCheckpointStore.JournalPath(path),JsonUtility.ToJson(new JournalEnvelope{payload=payload,sha256=hash}),new UTF8Encoding(false));
                bool merged=RaceCheckpointStore.Merge(path,p,true,out string warning);
                Check(model=="unknown-future-model"?!merged:merged&&p.parts[0].condition.health==.9f&&p.runCheckpoints.Single(r=>r.runId==run).tick==120,model+": recover only known monotonic resource journals after physics update");
            }
            File.WriteAllText(Path.Combine(Folder,"verification.txt"),log+"Checks: "+checks+" / failures: 0\n",new UTF8Encoding(false));
        }
        public static void Endurance()
        {
            Directory.CreateDirectory(Folder);var cat=MechanicsTransactions.Catalog();var v=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(x=>x.spec.id=="v8-muscle");
            int checks=0;var log=new StringBuilder();void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;log.AppendLine("PASS: "+message);}
            var csv=new StringBuilder("compound,pass,remaining,consumed,distance,engine_failed,drive_failed\n");
            foreach(string key in new[]{"3","SlickMedium"}){
                string id=v.spec.id+"/Tires/"+key;var build=Build(cat,v,id,false);var template=new CarSimulation(build.spec,build.tune,Driver(WearMode.Full,true)).mechanics.parts;
                var retained=template.Find(p=>p.slot=="Tires").Copy();int passes=0;
                while(passes<80&&!TireDynamics.AnyFailed(retained.condition)){
                    // Isolate tire life: identical healthy non-tire fixture hardware
                    // each pass; all four actual tire identities/history are retained.
                    var parts=template.Select(p=>p.slot=="Tires"?retained:p.Copy()).ToArray();
                    var result=Drive(build,Driver(WearMode.Full,true),parts,stopOnTireFailure:true);retained=result.car.mechanics.Part(PartCategory.Tires).Copy();passes++;
                    csv.AppendLine(string.Join(",",key,passes,N(TireDynamics.LowestHealth(retained.condition)),N(retained.condition.tires.Max(t=>t.consumedLife)),N(retained.condition.tires.Max(t=>t.distanceM)),result.car.state.engineFailed,result.car.state.drivetrainFailed));
                    Check(!result.car.state.engineFailed&&!result.car.state.drivetrainFailed,key+": tire life does not invent an engine/drivetrain failure at pass "+passes);
                }
                File.WriteAllText(Path.Combine(Folder,"endurance-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
                Check(passes>=40&&passes<=60,key+": real clean-pass life reaches exhaustion in 40–60 passes (actual "+passes+")");
                var loaded=JsonUtility.FromJson<PartInstance>(JsonUtility.ToJson(retained));Check(TireDynamics.LowestHealth(loaded.condition)==TireDynamics.LowestHealth(retained.condition)&&TireDynamics.AnyFailed(loaded.condition),key+": exhausted individual tire condition survives reload");
            }
            File.WriteAllText(Path.Combine(Folder,"endurance-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
            var super=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(x=>x.spec.id=="super-v8");
            var old=Drive(Build(cat,super,cat.Id(super.spec.id,PartCategory.Tires,3),true),Driver(WearMode.None));
            var stronger=Drive(Build(cat,super,super.spec.id+"/Tires/SlickMedium",true),Driver(WearMode.None));
            Check(stronger.spin.Skip(3).Sum()<old.spin.Skip(3).Sum()*.5,"Higher compound grip reduces high-gear wheelspin at the same engine output");
            Check(Math.Abs(stronger.peakHp-old.peakHp)/old.peakHp<.01,"Grip change does not secretly cut engine output");
            Check(v.spec.transmission.type==TransmissionType.Manual,"Clutch-loss fixture uses an actual friction-clutch manual, not a torque converter");
            var strongBuild=Build(cat,v,cat.Id(v.spec.id,PartCategory.Tires,3),true);
            var clutchStrong=Drive(strongBuild,Driver(WearMode.None),shutdown:false,maxSeconds:8);
            var recipe=strongBuild.spec.fittedParts.ToDictionary(p=>p.category,p=>p.id);recipe[PartCategory.Clutch]=cat.Id(v.spec.id,PartCategory.Clutch);
            var wornBuild=cat.Resolve(v.spec.id,recipe,strongBuild.tune);var wornParts=new CarSimulation(wornBuild.spec,wornBuild.tune,Driver(WearMode.None)).mechanics.parts;
            var wornClutch=wornParts.Find(p=>p.slot=="Clutch");wornClutch.condition.health=.05f;foreach(var member in wornClutch.condition.elements)member.health=.05f;
            var weak=Drive(wornBuild,Driver(WearMode.None),wornParts,shutdown:false,maxSeconds:8);
            File.WriteAllText(Path.Combine(Folder,"clutch-power-matrix.csv"),"clutch,window_s,engine_j,shaft_j,slip_heat_j\nstrong,8,"+N(clutchStrong.engineWork)+","+N(clutchStrong.shaftWork)+","+N(clutchStrong.clutchHeat)+"\nweak,8,"+N(weak.engineWork)+","+N(weak.shaftWork)+","+N(weak.clutchHeat)+"\n",new UTF8Encoding(false));
            Check(weak.shaftWork<clutchStrong.shaftWork*.9&&weak.clutchHeat>clutchStrong.clutchHeat,"A worn stock clutch that cannot hold the tune transmits less work and dissipates more heat");
            Check(weak.shaftWork<=weak.engineWork+100000,"Clutch output respects the engine plus initial rotational-energy budget");
            var c=new ComponentCondition();ComponentWear.Apply(c,1e-9);var unchanged=c.health;ComponentWear.Apply(c,0);Check(c.health==unchanged&&c.serviceWearRemainder==1e-9,"None wear consumes neither health nor the saved sub-tick remainder");
            foreach(double loss in new[]{1e-12,1e-9,1e-6,.1}){var full=new ComponentCondition();var partial=new ComponentCondition();for(int i=0;i<100;i++){ComponentWear.Apply(full,loss);ComponentWear.Apply(partial,loss*.25);}if(loss<.01)Check(Math.Abs(((1-partial.health)+partial.serviceWearRemainder)*4-((1-full.health)+full.serviceWearRemainder))<1e-10,"Mechanical partial wear is exactly 25%, including sub-float remainder / "+N(loss));else Check(full.health==0&&partial.health==0,"Accumulated wear reaches a real failed health threshold");}
            File.WriteAllText(Path.Combine(Folder,"endurance-verification.txt"),log+"Checks: "+checks+" / failures: 0\n",new UTF8Encoding(false));
        }
    }
}
