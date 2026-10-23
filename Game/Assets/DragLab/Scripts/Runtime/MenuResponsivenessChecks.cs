using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class MenuResponsivenessChecks
    {
        [Serializable] sealed class PhysicsInput { public VehicleSpec spec;public Tune tune;public List<PartInstance> parts;public FuelState fuel;public FluidState fluids; }
        [Serializable] sealed class PhysicsInputs { public List<PhysicsInput> cases=new List<PhysicsInput>(); }
        static void PhysicsParity(string label,VehicleDefinition[] vehicles,MechanicalCatalog catalog,StringBuilder text)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/commerce-performance-20261022/physics"));Directory.CreateDirectory(folder);
            string inputPath=Path.Combine(folder,"inputs.json");PhysicsInputs inputs;
            if(label=="before"){
                inputs=new PhysicsInputs();
                var cases=vehicles.GroupBy(v=>v.spec.sound.cylinders+"/"+v.spec.drive+"/"+v.spec.combustion.fuelKind).Select(g=>g.First()).Take(8).ToList();
                var diesel=vehicles.FirstOrDefault(v=>CombustionHardware.Diesel(v.spec));if(diesel!=null&&!cases.Contains(diesel))cases.Add(diesel);
                foreach(var vehicle in cases){
                    var build=catalog.Resolve(vehicle.spec.id,catalog.Recipe(vehicle.spec.id,vehicle.CreateStockTune()),vehicle.CreateStockTune());
                    var initialized=new CarSimulation(build.spec,build.tune,new DriverSettings{wearMode=WearMode.Full});
                    var parts=initialized.mechanics.parts.Select(p=>p.Copy()).ToList();
                    foreach(var part in parts){part.condition.health=.78f;foreach(var member in part.condition.elements)member.health=.78f;foreach(var tire in part.condition.tires)tire.health=.78f;}
                    inputs.cases.Add(new PhysicsInput{spec=build.spec,tune=build.tune,parts=parts,fuel=new FuelState{litres=build.spec.fuel.capacityLitres*.6},fluids=FluidState.Full(build.spec.fluids)});
                }
                File.WriteAllText(inputPath,JsonUtility.ToJson(inputs),new UTF8Encoding(false));
            }else{
                string fixture=Path.GetFullPath(Path.Combine(Application.dataPath,"../../tools/fixtures/menu-physics-v22.json.gz"));
                using(var file=File.OpenRead(fixture))using(var compressed=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(compressed,Encoding.UTF8))inputs=JsonUtility.FromJson<PhysicsInputs>(reader.ReadToEnd());
            }
            var golden=label=="before"?null:File.ReadAllLines(Path.GetFullPath(Path.Combine(Application.dataPath,"../../tools/fixtures/menu-physics-v22.csv")),Encoding.UTF8).Skip(1).Select(row=>row.Split(',')).ToDictionary(row=>row[0],row=>row[1]);
            for(int i=0;i<inputs.cases.Count;i++){
                var item=inputs.cases[i];var source=new InspectionBuild{build=new BuildResolution{spec=item.spec,tune=item.tune},components=item.parts,startingFuel=item.fuel,startingFluids=item.fluids};
                var car=CarInspection.PrepareCar(source);var timer=Stopwatch.StartNew();var result=CarInspection.CalculatePrepared(source,car,true);timer.Stop();
                string snapshot=JsonUtility.ToJson(car.state)+"\n"+string.Join("\n",car.mechanics.parts.Select(p=>JsonUtility.ToJson(p)))+"\n"+JsonUtility.ToJson(result);
                string expected=Path.Combine(folder,i+"-before.json");
                if(label=="before")File.WriteAllText(expected,snapshot,new UTF8Encoding(false));
                else {using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(snapshot))).Replace("-","").ToLowerInvariant();if(!golden.TryGetValue(item.spec.id,out string approved)||hash!=approved)throw new InvalidOperationException("Physics changed during performance optimization: "+item.spec.id);}}
                text.AppendLine("PHYSICS "+item.spec.id+" / "+item.spec.sound.cylinders+" cylinders / "+item.spec.drive+" / "+item.spec.combustion.fuelKind+" / "+timer.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms / "+(label=="before"?"baseline captured":"exact state, wear, fluids and inspection parity"));
            }
        }
        public static void Baseline()=>Run("before");
        public static void Optimized()=>Run("after");
        static void Run(string label)
        {
            var text=new StringBuilder("Menu workload timings / "+label+" / milliseconds\n");
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");MechanicalCatalog catalog=null;PlayerProfile p=null;NativePartBrowser browser=null;
            void Time(string name,Action work){var timer=Stopwatch.StartNew();work();timer.Stop();text.AppendLine(name+": "+timer.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));}
            Time("catalogue",()=>catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec)));
            PhysicsParity(label,vehicles,catalog,text);
            Time("new profile / normalization",()=>{p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;});
            VehicleListings.Initialize(p,catalog,12345);
            text.AppendLine("definitions: "+catalog.parts.Count+" / new stock products: "+p.inventory.market.parts.Count+" / used stock: "+p.inventory.dealer.Count);
            Time("current profile normalization",()=>MechanicsOwnership.Ensure(p,catalog));
            Time("current profile validation",()=>{var error=MechanicsOwnership.Validate(p,catalog);if(error!=null)throw new InvalidOperationException(error);});
            var source=InspectionBuild.Capture(p,p.SelectedCar);CarSimulation simulation=null;
            Time("class simulation preparation",()=>simulation=CarInspection.PrepareCar(source));
            Time("class static inspection",()=>CarInspection.CalculatePrepared(source,simulation,false));
            simulation=CarInspection.PrepareCar(source);
            Time("class full quarter-mile probe",()=>CarInspection.CalculatePrepared(source,simulation,true));
            simulation=CarInspection.PrepareCar(source);
            Time("1000 simulation steps",()=>{simulation.Launch(0);for(int n=0;n<1000;n++)simulation.Step(DragMath.FixedStep,new DriverInput{throttle=1});});
            void Ticks(string name,Action<CarSimulation> work){var car=CarInspection.PrepareCar(source);Time(name,()=>{for(int n=0;n<1000;n++)work(car);});}
            Ticks("1000 mechanical prepare",car=>car.mechanics.Prepare(car.state));
            Ticks("1000 service prepare",car=>ServiceDynamics.Prepare(car));
            Ticks("1000 tire wear ticks",car=>TireDynamics.Tick(car,DragMath.FixedStep,car.state.distance));
            Ticks("1000 mechanical wear ticks",car=>car.mechanics.Tick(car,DragMath.FixedStep,0,car.state.distance));
            Ticks("1000 fluid ticks",car=>FluidDynamics.Tick(car,DragMath.FixedStep));
            Time("transaction validation / clone",()=>{if(!MechanicsTransactions.Try(p,catalog,p.revision,q=>{},q=>true,out _,out var error))throw new InvalidOperationException(error);});
            Time("three dealership static previews",()=>{foreach(var offer in p.vehicleStock.listings.Take(3))VehicleCollection.Preview(catalog,offer);});
            Time("stock refresh",()=>{for(int i=0;i<3;i++)VehicleListings.AfterRace(p,catalog);});
            var cache=new NativePartPreviewCache();
            Time("shop cold",()=>browser=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache));
            Time("shop revisit",()=>browser=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache));
            Time("10000 catalogue lookups",()=>{for(int i=0;i<10000;i++)catalog.Find(catalog.parts[i%catalog.parts.Count].id);});
            Time("100 stock quotes",()=>{foreach(var d in catalog.parts.Take(100))PartsMarket.Quote(p,d,1);});
            var head=browser.Entries.First(e=>e.Compatibility=="Compatible"&&e.Definition.category==PartCategory.HeadIntake);
            Time("part detail comparison",()=>browser.Evaluate(head));
            Time("100 fitting part comparisons",()=>{foreach(var item in browser.Entries.Where(e=>e.Compatibility=="Compatible").Take(100))browser.Evaluate(item);});
            var core=p.componentsPart(PartCategory.CylinderBlock);
            foreach(var mode in new[]{BlockPreparationMode.Reuse,BlockPreparationMode.OEM,BlockPreparationMode.Manual})Time("block preparation "+mode,()=>BlockPreparation.Build(p,p.SelectedCar.instanceId,new PartCartLine{definitionId=core.definitionId,action=CartAction.NewSet},Array.Empty<PartCartLine>(),mode,null,ValuationPolicy.Defaults()));
            var line=new PartCartLine{definitionId=head.Definition.id,quantity=1,install=true};
            Time("planned shop",()=>browser=new NativePartBrowser(p,new[]{line},true,"Shop",_=>100,(_,u)=>100,cache));
            Time("planned shop revisit",()=>browser=new NativePartBrowser(p,new[]{line},true,"Shop",_=>100,(_,u)=>100,cache));
            Time("10 full profile serializations",()=>{for(int i=0;i<10;i++)JsonUtility.ToJson(p);});
            var stocked=p.componentsPart(PartCategory.HeadIntake).definitionId;
            var spares=new List<string>();for(int i=0;i<24;i++){string id=PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,stocked,false);var part=p.parts.Find(v=>v.instanceId==id);part.condition.health=(i%3+1)*.25f;spares.Add(id);}
            var policy=ValuationPolicy.Defaults();long now=DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;var quoted=new Dictionary<string,int>();string original=JsonUtility.ToJson(p);
            Time("24 full binding sale quotes",()=>{foreach(string id in spares)quoted[id]=InventoryPricing.Sell(p,id,false,policy,"wholesale",now).Total;});
            Time("24 shared display sale prices",()=>{var prices=Valuations.LoosePriceView(p,policy,"wholesale",now);foreach(string id in spares)if(prices(id,false)!=quoted[id])throw new InvalidOperationException("Display sale value differs from binding quote.");});
            Time("inventory browser with 24 spares",()=>{var prices=Valuations.LoosePriceView(p,policy,"wholesale",now);new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Inventory",_=>100,prices,cache);});
            if(original!=JsonUtility.ToJson(p))throw new InvalidOperationException("Read-only prices changed player state.");
            text.AppendLine("PRICE PARITY: 24 varied-condition owned parts match full quotes without editing state");
#if UNITY_EDITOR
            string prior=ProfileStore.VerificationPath;try{ProfileStore.VerificationPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/player-fixtures/responsiveness-cpu/profile-v1.json"));ProfileStore.SaveMeasurement=(phase,ms)=>text.AppendLine(phase+": "+ms.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));Time("durable profile save",()=>{if(!ProfileStore.TrySave(p))throw new InvalidOperationException(ProfileStore.Status);});}finally{ProfileStore.VerificationPath=prior;ProfileStore.SaveMeasurement=null;}
#endif
            text.AppendLine("profile bytes: "+Encoding.UTF8.GetByteCount(JsonUtility.ToJson(p)));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/responsiveness"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"timings-"+label+".txt"),text.ToString(),new UTF8Encoding(false));UnityEngine.Debug.Log(text.ToString());
        }
    }
#endif
}
