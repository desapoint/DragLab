using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class NewSupplyVerification
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/new-parts-supply"));
        static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        static string Used(PlayerProfile p)=>string.Join("\n",p.inventory.dealer.Select(s=>JsonUtility.ToJson(s)))+"\nENGINES\n"+string.Join("\n",p.inventory.market.engines.Select(s=>JsonUtility.ToJson(s)));
        static PlayerProfile Create(MechanicalCatalog catalog,int seed,int minimum)
        {
            var p=PlayerProfile.New(new DateTime(2026,10,18,0,0,0,DateTimeKind.Utc).Ticks);p.cash=100000000;p.reputation=1000;p.driving.guaranteedFittingParts=minimum;
            VehicleListings.Initialize(p,catalog,seed);MechanicsOwnership.Ensure(p,catalog);return p;
        }
        public static void CaptureUsedBaseline()
        {
            Directory.CreateDirectory(Folder);var catalog=MechanicsTransactions.Catalog();var lines=new StringBuilder("seed,minimum,generation,used_rows,engines,sha256\n");
            foreach(int seed in new[]{1,41,981,-7})foreach(int minimum in new[]{0,5,20}){
                var p=Create(catalog,seed,minimum);
                for(int epoch=0;epoch<2;epoch++){
                    if(epoch>0){p.vehicleStock.generation++;PartsMarket.Refresh(p);}
                    lines.AppendLine(seed+","+minimum+","+epoch+","+p.inventory.dealer.Count+","+p.inventory.market.engines.Count+","+Hash(Used(p)));
                }
            }
            File.WriteAllText(Path.Combine(Folder,"used-baseline-matrix.csv"),lines.ToString(),new UTF8Encoding(false));
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);int checks=0;var log=new StringBuilder();var timer=System.Diagnostics.Stopwatch.StartNew();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS "+label);};
            NewSupplyChecks.Rules(check);log.AppendLine("Rule checks elapsed ms: "+timer.Elapsed.TotalMilliseconds.ToString("0"));var catalog=MechanicsTransactions.Catalog();
            NewSupplyChecks.LayoutRegression(catalog,check);
            // Compare entire deterministic market output with and without the
            // worker boundary, not just isolated category/quantity calculations.
            var deliveryOriginal=Create(catalog,209,5);
            // Exercise actual member wear plus a held new-stock reservation in
            // the full synchronous/worker parity test, not only empty defaults.
            var originalCar=deliveryOriginal.SelectedCar;
            var originalPiston=deliveryOriginal.parts.First(i=>originalCar.installed.slots.Any(slot=>slot.partInstanceId==i.instanceId)&&catalog.Find(i.definitionId).category==PartCategory.Pistons);
            originalPiston.condition.elements[0].health=.15f;
            EngineElementService.RefreshAggregate(originalPiston.condition);
            CartReservations.Capture(deliveryOriginal,originalCar.instanceId,
                new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=originalPiston.definitionId,quantity=2,role="Pistons"}});
            check(CartReservations.Active(deliveryOriginal),"Full worker parity fixture includes live member wear and a nine-race cart reservation");
            var deliveryWorker=EngineWorkshop.Copy(deliveryOriginal);deliveryWorker.catalog=catalog;
            deliveryOriginal.vehicleStock.generation++;
            deliveryWorker.vehicleStock.generation++;
            PartsMarket.Refresh(deliveryOriginal);
            double wholeSupplyCpu=-1;
            var priorObserver=PartsMarket.Measurement;
            PartsMarket.Measurement=(phase,ms)=>{
                if(phase=="new complete worker CPU")wholeSupplyCpu=ms;
                priorObserver?.Invoke(phase,ms);
            };
            try
            {
                using(var work=PartsMarket.RefreshSteps(deliveryWorker,true).GetEnumerator())
                    while(work.MoveNext())
                        if(work.Current is MarketWorkerWait waiting)waiting.task.GetAwaiter().GetResult();
            }
            finally {PartsMarket.Measurement=priorObserver;}
            check(wholeSupplyCpu>=0,"Detached new-stock worker processes wear, delivery and fitting guarantee");
            log.AppendLine("Measured detached new-stock worker CPU: "+wholeSupplyCpu.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" ms");
            check(ReferenceEquals(deliveryWorker.catalog,catalog),"Completed detached supply worker restores the authoritative catalog reference");
            check(JsonUtility.ToJson(deliveryOriginal.inventory.market)==JsonUtility.ToJson(deliveryWorker.inventory.market)&&
                string.Join("/",deliveryOriginal.inventory.dealer.Select(row=>JsonUtility.ToJson(row)))==
                string.Join("/",deliveryWorker.inventory.dealer.Select(row=>JsonUtility.ToJson(row))),
                "Entire detached new-supply worker preserves exact synchronous new/used/engine epoch");
            var baseline=File.ReadAllLines(Path.Combine(Folder,"used-baseline-matrix.csv"),Encoding.UTF8).Skip(1).Where(s=>s.Length>0).ToDictionary(s=>string.Join(",",s.Split(',').Take(3)),s=>s.Split(',')[5]);
            foreach(int seed in new[]{1,41,981,-7})foreach(int minimum in new[]{0,5,20}){
                var p=Create(catalog,seed,minimum);
                for(int epoch=0;epoch<2;epoch++){
                    if(epoch>0){p.vehicleStock.generation++;PartsMarket.Refresh(p);}
                    string key=seed+","+minimum+","+epoch;check(Hash(Used(p))==baseline[key],"Used parts, quantities, wear, identities and engines exactly match pre-change fingerprint "+key);
                }
            }
            log.AppendLine("Preserved supply checks elapsed ms: "+timer.Elapsed.TotalMilliseconds.ToString("0"));
            var source=Create(catalog,41,5);NewSupplyChecks.Coverage(EngineWorkshop.Copy(source),check);
            var six=EngineWorkshop.Copy(source);string sixModel=Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles").Where(v=>v.spec.sound.cylinders==6).OrderBy(v=>v.spec.id,StringComparer.Ordinal).First().spec.id;
            VehicleCollection.Buy(six,catalog,new VehicleOffer{model=sixModel,price=1,reputation=0,stock=new Tune(),paint="#FFFFFF"});NewSupplyChecks.Coverage(six,check);
            var identities=Create(catalog,77,0);VehicleCollection.Buy(identities,catalog,new VehicleOffer{model=sixModel,price=1,reputation=0,stock=new Tune(),paint="#FFFFFF"});identities.inventory.cart=null;
            foreach(var stock in identities.inventory.market.parts){stock.units=stock.initialUnits=0;stock.discovered=false;}
            foreach(var car in identities.cars){var pack=identities.parts.First(i=>car.installed.slots.Any(s=>s.partInstanceId==i.instanceId)&&catalog.Find(i.definitionId).category==PartCategory.Pistons);pack.condition.elements[0].health=.1f;EngineElementService.RefreshAggregate(pack.condition);}
            string renamedJson=JsonUtility.ToJson(identities);var ordered=identities.cars.OrderBy(c=>c.instanceId,StringComparer.Ordinal).ToArray();for(int n=0;n<ordered.Length;n++)renamedJson=renamedJson.Replace(ordered[n].instanceId,"supply-identity/"+(ordered.Length-n));
            var renamed=JsonUtility.FromJson<PlayerProfile>(renamedJson);renamed.catalog=catalog;MechanicsOwnership.Ensure(renamed,catalog);
            identities.vehicleStock.generation++;renamed.vehicleStock.generation++;PartsMarket.Refresh(identities);PartsMarket.Refresh(renamed);
            check(JsonUtility.ToJson(identities.inventory)==JsonUtility.ToJson(renamed.inventory),"Random owned GUIDs cannot change seeded supply choices for identical acquisition order, hardware and wear");
            var twin=EngineWorkshop.Copy(source);string before=JsonUtility.ToJson(source);
            source.vehicleStock.generation++;twin.vehicleStock.generation++;PartsMarket.Refresh(source);PartsMarket.Refresh(twin);
            check(JsonUtility.ToJson(source.inventory)==JsonUtility.ToJson(twin.inventory),"Identical persisted seed, history, wear and inventory replay exact new/used stock");
            check(source.inventory.market.version==2&&source.inventory.market.parts.Any(s=>s.discovered),"Discovery metadata is persisted with market version two");
            var loaded=EngineWorkshop.Copy(source);check(JsonUtility.ToJson(loaded.inventory)==JsonUtility.ToJson(source.inventory),"Serialization retains discovery and additive stock exactly");
            var row=source.inventory.market.parts.First(s=>s.definitions.Any(id=>catalog.Find(id).category==PartCategory.Pistons));row.units=row.initialUnits=42;row.discovered=true;
            var d=catalog.Find(row.definitions[0]);CartReservations.Capture(source,source.SelectedCar.instanceId,new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=d.id,quantity=2,role="Pistons"}});
            int oldGeneration=source.inventory.market.generation;source.vehicleStock.generation++;PartsMarket.Refresh(source);var next=source.inventory.market.parts.Single(s=>s.definitions.Contains(d.id));
            check(next.units>=40&&next.discovered&&source.inventory.cart.supply.Single().generation==oldGeneration,"Refresh carries unreserved stock and preserves old-generation holds without duplicating them");
            int free=next.units;next.units=0;source.inventory.usingCartReservation=true;var reserved=PartsMarket.Quote(source,d,4,true);source.inventory.usingCartReservation=false;next.units=free;
            check(reserved.localUnits==1&&CartReservations.Held(source,d)==2,"Prior cart supply stays available to its owner even with no free shelf stock");
            next.units=0;next.initialUnits=42;source.vehicleStock.generation++;PartsMarket.Refresh(source);check(source.inventory.market.parts.Single(s=>s.definitions.Contains(d.id)).discovered,"Sold-out products keep discovery history across refreshes");
            var legacy=Create(catalog,981,5);legacy.inventory.market.version=1;foreach(var s in legacy.inventory.market.parts)s.discovered=false;
            string assets=JsonUtility.ToJson(legacy.parts);string quantities=string.Join("/",legacy.inventory.market.parts.Select(s=>s.units+":"+s.initialUnits+":"+s.deliveryPercent));
            PartsMarket.Ensure(legacy);check(legacy.inventory.market.version==2&&assets==JsonUtility.ToJson(legacy.parts)&&quantities==string.Join("/",legacy.inventory.market.parts.Select(s=>s.units+":"+s.initialUnits+":"+s.deliveryPercent)),"Version-one metadata migration preserves exact assets, stock, fees and epoch");
            check(legacy.inventory.market.parts.All(s=>s.discovered==(s.initialUnits>0)),"Version-one history infers previously supplied products without restocking them");
            var changed=EngineWorkshop.Copy(source);foreach(var part in changed.parts){part.condition.health=.01f;foreach(var e in part.condition.elements)e.health=.01f;}foreach(var s in changed.inventory.market.parts){s.units=s.initialUnits=100;s.discovered=true;}
            source.vehicleStock.generation++;changed.vehicleStock.generation++;PartsMarket.Refresh(source);PartsMarket.Refresh(changed);check(Used(source)==Used(changed),"Changing player wear, discovery and accumulated new quantities cannot alter used/engine generation");
            var direct=EngineWorkshop.Copy(changed);var staged=EngineWorkshop.Copy(changed);direct.vehicleStock.generation++;staged.vehicleStock.generation++;PartsMarket.Refresh(direct);
            // Keep one report row per delivery stage rather than flooding the fixture log.
            // Values are CPU-only samples; the iterator's time between frames is excluded.
            var deliveryTimes=new SortedDictionary<string,double[]>(StringComparer.Ordinal);
            int wearSteps=0;var previousMeasurement=PartsMarket.Measurement;
            PartsMarket.Measurement=(phase,ms)=>{
                if(!phase.StartsWith("delivery/",StringComparison.Ordinal)){
                    log.AppendLine("Measured "+phase+": "+ms.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" ms");
                    return;
                }
                if(!deliveryTimes.TryGetValue(phase,out var values))deliveryTimes[phase]=values=new double[3]; // total, count, maximum
                values[0]+=ms;values[1]++;values[2]=Math.Max(values[2],ms);
            };
            try{foreach(var step in PartsMarket.RefreshSteps(staged))if((string)step=="parts/wear coverage")wearSteps++;}finally{PartsMarket.Measurement=previousMeasurement;}
            foreach(var pair in deliveryTimes){
                var values=pair.Value;
                log.AppendLine("Measured "+pair.Key+": "+values[0].ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" ms CPU total / "+((int)values[1])+" calls / "+values[2].ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" ms max");
            }
            check(deliveryTimes.ContainsKey("delivery/eligible pool")&&deliveryTimes.ContainsKey("delivery/wear probabilities"),"New supply profiling captures pool filtering and wear priority separately");
            check(wearSteps>0&&JsonUtility.ToJson(staged.inventory)==JsonUtility.ToJson(direct.inventory),"Staged wear/member preparation yields work and matches synchronous generation exactly");
            string priorPath=ProfileStore.VerificationPath;try{
                string path=Path.Combine(Folder,"sqlite",Guid.NewGuid().ToString("N")+".sqlite3");Directory.CreateDirectory(Path.GetDirectoryName(path));ProfileStore.VerificationPath=path;
                check(ProfileStore.TrySave(legacy),"SQLite commits migrated discovery metadata");var saved=ProfileStore.Load();
                check(JsonUtility.ToJson(saved.inventory.market)==JsonUtility.ToJson(legacy.inventory.market),"Real SQLite reload retains stock and discovery without rerolling");
                string oldFile=Path.Combine(Folder,"sqlite",Guid.NewGuid().ToString("N")+".sqlite3");var old=EngineWorkshop.Copy(legacy);old.storageCommit=0;NewSupplyChecks.WriteLegacyFixture(old,oldFile);ProfileStore.VerificationPath=oldFile;
                var upgraded=ProfileStore.Load();check(upgraded.inventory.market.version==2&&JsonUtility.ToJson(upgraded.parts)==JsonUtility.ToJson(old.parts)&&upgraded.cash==old.cash,"Real version-one-market SQLite save upgrades without replacing player resources");
                check(string.Join("/",upgraded.inventory.market.parts.Select(s=>s.units+":"+s.initialUnits+":"+s.deliveryPercent))==quantities&&upgraded.inventory.market.parts.All(s=>s.discovered==(s.initialUnits>0)),"Database upgrade infers history and retains exact stock and delivery rates");
                check(ProfileStore.TrySave(upgraded)&&ProfileStore.Load().inventory.market.version==2,"Upgraded database commits and reloads through the normal save boundary");
            }finally{ProfileStore.VerificationPath=priorPath;}
            log.AppendLine("Checks: "+checks+"; failures: 0; elapsed seconds: "+timer.Elapsed.TotalSeconds.ToString("0.0"));File.WriteAllText(Path.Combine(Folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("New supply verification passed: "+checks);
        }
    }
}
