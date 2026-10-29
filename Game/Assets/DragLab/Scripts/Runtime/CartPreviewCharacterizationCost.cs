#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Bounded, descriptive legacy cost attribution. No production route or save is changed.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint CostFixtureSeed=20261029u;
        const int CostWarmups=5,CostMeasuredSamples=31;

        sealed class CostFixture
        {
            public PlayerProfile Profile;
            public OwnedCar Car;
            public PartCartLine[] Rows;
            public PartDefinition Definition;
            public PartInstance Part;
            public PartsMarket.Ledger Ledger;
            public Func<string,bool,int> PriceView;
            public int LedgerUnits,LedgerBasePrice;
            public string AssetId,CarId,Json;
            public long Scalar;
            public object Result;
        }
        sealed class CostObservation
        {
            public string Label,Kind,Error,Result;
            public int Index,Gc0,Gc1,Gc2;
            public long Ticks,Allocated;
        }
        sealed class CostScenario
        {
            public PlayerProfile Profile;
            public string CarId,AssetId;
            public PartDefinition Definition;
            public PartCartLine[] Rows;
        }
        sealed class CostLog
        {
            readonly List<CostObservation> observations=new List<CostObservation>();
            readonly string folder;
            readonly bool allocationAvailable;
            public CostLog(string path,bool allocated){folder=path;allocationAvailable=allocated;}
            public void Add(CostObservation sample)=>observations.Add(sample);
            public void Write()
            {
                var raw=new StringBuilder("label,kind,index,ticks,milliseconds,gc0,gc1,gc2,allocatedBytes,result,error\n");
                foreach(var s in observations)
                    raw.Append(Csv(s.Label)).Append(',').Append(Csv(s.Kind)).Append(',').Append(s.Index).Append(',')
                        .Append(s.Ticks).Append(',').Append((s.Ticks*1000.0/Stopwatch.Frequency).ToString("F4",CultureInfo.InvariantCulture)).Append(',')
                        .Append(s.Gc0).Append(',').Append(s.Gc1).Append(',').Append(s.Gc2).Append(',')
                        .Append(allocationAvailable?s.Allocated.ToString(CultureInfo.InvariantCulture):"NA").Append(',')
                        .Append(Csv(s.Result)).Append(',').Append(Csv(s.Error)).Append('\n');
                File.WriteAllText(Path.Combine(folder,"cost-raw.csv"),raw.ToString(),new UTF8Encoding(false));
                var summary=new StringBuilder("S06d2b1g legacy operation samples; descriptive only; no pass threshold\n")
                    .Append("stopwatchFrequency=").Append(Stopwatch.Frequency).Append(";allocationAvailable=").Append(allocationAvailable)
                    .Append(";allocationSource=").Append(allocationAvailable?"same-run calibrated GC.GetAllocatedBytesForCurrentThread":"unavailable after 8192-byte calibration")
                    .Append(";sampleProtocol=first-cold+5-warmups+31-measured;maxPreparedCopies=1\n");
                foreach(var group in observations.Where(s=>s.Kind=="measured").GroupBy(s=>s.Label).OrderBy(g=>g.Key,StringComparer.Ordinal))
                {
                    var ordered=group.OrderBy(s=>s.Ticks).ToArray();
                    if(ordered.Length==0){summary.Append("OPEN|").Append(group.Key).Append("|no measured samples\n");continue;}
                    int p95Index=Math.Max(0,(int)Math.Ceiling(.95*ordered.Length)-1);
                    long allocated=ordered.Where(s=>s.Allocated>=0).Sum(s=>s.Allocated);
                    summary.Append("SERIES|").Append(group.Key).Append("|n=").Append(ordered.Length)
                        .Append("|medianTicks=").Append(ordered[ordered.Length/2].Ticks)
                        .Append("|p95Rank=").Append(p95Index+1).Append('/').Append(ordered.Length).Append("|p95Ticks=").Append(ordered[p95Index].Ticks)
                        .Append("|maxTicks=").Append(ordered[ordered.Length-1].Ticks)
                        .Append("|medianMs=").Append((ordered[ordered.Length/2].Ticks*1000.0/Stopwatch.Frequency).ToString("F4",CultureInfo.InvariantCulture))
                        .Append("|p95Ms=").Append((ordered[p95Index].Ticks*1000.0/Stopwatch.Frequency).ToString("F4",CultureInfo.InvariantCulture))
                        .Append("|maxMs=").Append((ordered[ordered.Length-1].Ticks*1000.0/Stopwatch.Frequency).ToString("F4",CultureInfo.InvariantCulture))
                        .Append("|gcSamples=").Append(ordered.Count(s=>s.Gc0+s.Gc1+s.Gc2>0))
                        .Append("|gc0/1/2=").Append(ordered.Sum(s=>s.Gc0)).Append('/').Append(ordered.Sum(s=>s.Gc1)).Append('/').Append(ordered.Sum(s=>s.Gc2))
                        .Append("|allocatedBytes=").Append(allocationAvailable?allocated.ToString(CultureInfo.InvariantCulture):"NA")
                        .Append("\n");
                }
                File.WriteAllText(Path.Combine(folder,"cost-summary.txt"),summary.ToString(),new UTF8Encoding(false));
            }
            static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";
        }

        /// <summary>Called by the thin Editor batch wrapper. subset is totals, stages, operations, or block.</summary>
        public static void RunCostAttribution(Action<bool,string> check,string reportFolder,string subset=null)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(reportFolder))throw new ArgumentException("An isolated Editor report folder is required.",nameof(reportFolder));
            if(subset!=null&&subset!="totals"&&subset!="stages"&&subset!="operations"&&subset!="block")throw new ArgumentException("Unknown cost subset: "+subset,nameof(subset));
            Directory.CreateDirectory(reportFolder);
            var trace=new Trace(check,reportFolder,true);
            bool allocationAvailable=CostAllocationCalibration();
            CostAllocationCalibrationCached=allocationAvailable;
            var log=new CostLog(reportFolder,allocationAvailable);
            trace.Add("COST_PROVENANCE|seed="+CostFixtureSeed+";nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks+
                ";warmups="+CostWarmups+";measured="+CostMeasuredSamples+";coldScope=first timed route call before harness warmups;fixture setup may pre-exercise route APIs;"+
                "p95=nearest-rank-30/31;allocationAvailable="+allocationAvailable+
                ";allocationProbe=known-8192-byte-same-thread;nativeFrameCounter=separate-whole-frame-only;subset="+(subset??"all"));
            if(subset==null||subset=="totals")CostRunSafely(trace,log,"totals",()=>RunCostTotals(trace,log));
            if(subset=="block")CostRunSafely(trace,log,"block",()=>RunCostPreparedBlockTotal(trace,log));
            if(subset==null||subset=="stages")CostRunSafely(trace,log,"stages",()=>RunCostStages(trace,log));
            if(subset==null||subset=="operations")CostRunSafely(trace,log,"operations",()=>RunCostOperations(trace,log));
            log.Write();
            File.WriteAllText(Path.Combine(reportFolder,"cost-trace.txt"),trace.Text.ToString(),new UTF8Encoding(false));
            check(trace.Failures==0,"Bounded cost measurements completed without unavailable required spans / subset="+(subset??"all")+
                " failures="+trace.Failures+" report="+reportFolder);
        }

        static void CostRunSafely(Trace trace,CostLog log,string group,Action action)
        {
            try{action();}
            catch(Exception error){CostUnavailable(trace,group,"fixture or route setup threw "+error.GetType().FullName+" | "+error.Message);}
        }
        static void CostUnavailable(Trace trace,string label,string reason)
        {
            string text="COST_OPEN|"+label+"|"+CostSafe(reason);
            trace.Add(text);trace.Assert(false,"Required measured route fixture exists / "+label+" / "+CostSafe(reason));
        }
        static string CostSafe(string value)=>string.IsNullOrEmpty(value)?"<empty>":new string(value.Select(c=>char.IsControl(c)?' ':c).Take(450).ToArray());

        static bool CostAllocationCalibration()
        {
            try
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                var known=new byte[8192];known[0]=17;GC.KeepAlive(known);
                long after=GC.GetAllocatedBytesForCurrentThread();
                return after-before>=8192;
            }
            catch{return false;}
        }

        static void CostMeasure(Trace trace,CostLog log,string label,Func<CostFixture> prepare,Action<CostFixture> operation)
        {
            var series=new List<CostObservation>(1+CostWarmups+CostMeasuredSamples);
            CostObserve(trace,log,label,"cold",-1,prepare,operation,series);
            for(int i=0;i<CostWarmups;i++)CostObserve(trace,log,label,"warmup",i,prepare,operation,series);
            for(int i=0;i<CostMeasuredSamples;i++)CostObserve(trace,log,label,"measured",i,prepare,operation,series);
            int measured=series.Count(s=>s.Kind=="measured"&&s.Error==null);
            bool ok=measured==CostMeasuredSamples&&series.All(s=>s.Error==null);
            if(!ok)CostUnavailable(trace,label,"completed="+measured+"/"+CostMeasuredSamples+";errors="+
                string.Join(" | ",series.Where(s=>s.Error!=null).Select(s=>s.Kind+":"+s.Error).Take(3)));
            else trace.Add("COST_READY|"+label+"|cold+"+CostWarmups+"warm+"+CostMeasuredSamples+"measured");
        }
        static void CostObserve(Trace trace,CostLog log,string label,string kind,int index,Func<CostFixture> prepare,
            Action<CostFixture> operation,List<CostObservation> series)
        {
            CostFixture fixture=null;Exception error=null;long ticks=0,allocated=-1;int g0=0,g1=0,g2=0;
            try{fixture=prepare();if(fixture==null)throw new InvalidOperationException("Fixture preparation returned null.");}
            catch(Exception e){error=e;}
            if(error==null)
            {
                if(kind=="cold")trace.Add("COST_INPUT|"+label+"|car="+CostSafe(fixture.CarId??fixture.Car?.instanceId)+
                    ";cash="+(fixture.Profile?.cash.ToString(CultureInfo.InvariantCulture)??"<none>")+";rep="+
                    (fixture.Profile?.reputation.ToString(CultureInfo.InvariantCulture)??"<none>")+";asset="+CostSafe(fixture.AssetId)+
                    ";definition="+CostSafe(fixture.Definition?.id)+";rows="+(fixture.Rows==null?"<none>":string.Join(";",fixture.Rows.Select(row=>
                        row==null?"<null>":row.action+"/"+CostSafe(row.definitionId)+"/q"+row.quantity+"/"+CostSafe(row.assetId)))));
                var watch=new Stopwatch();
                int before0=GC.CollectionCount(0),before1=GC.CollectionCount(1),before2=GC.CollectionCount(2);
                long beforeBytes=-1;
                if(CostAllocationCalibrationCached)beforeBytes=GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                try{operation(fixture);}
                catch(Exception e){error=e;}
                finally{watch.Stop();}
                ticks=watch.ElapsedTicks;
                g0=GC.CollectionCount(0)-before0;g1=GC.CollectionCount(1)-before1;g2=GC.CollectionCount(2)-before2;
                if(beforeBytes>=0)allocated=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
            }
            var sample=new CostObservation{Label=label,Kind=kind,Index=index,Ticks=ticks,Allocated=allocated,Gc0=g0,Gc1=g1,Gc2=g2,
                Error=error==null?null:error.GetType().FullName+" | "+error.Message,Result=fixture==null?"<none>":CostResult(fixture)};
            series.Add(sample);log.Add(sample);
            if(error!=null)trace.Add("COST_SAMPLE_ERROR|"+label+"|"+kind+"|"+CostSafe(sample.Error));
            fixture=null;
        }
        static bool CostAllocationCalibrationCached;
        static string CostResult(CostFixture fixture)=>"scalar="+fixture.Scalar+";result="+
            (fixture.Result==null?"<none>":fixture.Result.GetType().FullName);

        static CostFixture CostCopy(CostScenario scenario)
        {
            var copy=CartPreviewLiteralTestClone.Copy(scenario.Profile);
            return new CostFixture{Profile=copy,Car=copy.cars.Find(c=>c.instanceId==scenario.CarId),CarId=scenario.CarId,
                AssetId=scenario.AssetId,Part=string.IsNullOrEmpty(scenario.AssetId)?null:copy.parts.Find(item=>item.instanceId==scenario.AssetId),
                Definition=scenario.Definition,Rows=scenario.Rows==null?Array.Empty<PartCartLine>():scenario.Rows.Select(row=>row.Copy()).ToArray()};
        }
        static CostScenario CostBase(uint seed)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out string carId,seed);
            return new CostScenario{Profile=p,CarId=carId};
        }
        static CostFixture CostPrepare(CostScenario scenario)=>CostCopy(scenario);

        static void RunCostTotals(Trace trace,CostLog log)
        {
            var policy=ValuationPolicy.Defaults();long now=checked(CartPreviewCharacterizationFixtures.FixedNowTicks+1);
            var transaction=CostBuildSafely(trace,"total.sale-first/move-first",CostBuildSaleMoveScenario);
            if(transaction==null)CostUnavailable(trace,"total.sale-first/move-first","actual compatible sale and move offer fixture unavailable");
            else
            {
                foreach(var row in transaction.Rows)
                    if(row==null)throw new InvalidOperationException("Sale/move fixture has a null cart row.");
                var saleFirst=transaction.Rows;
                var moveFirst=saleFirst.OrderBy(row=>row.action==CartAction.Move?0:row.action==CartAction.SellPart?1:2).ToArray();
                CostTotalRoute(trace,log,"total.sale-first",transaction,saleFirst,policy,now);
                CostTotalRoute(trace,log,"total.move-first",transaction,moveFirst,policy,now);
            }

            var additional=CostBuildSafely(trace,"total.additional-move-two-rows",CostBuildTwoMoveScenario);
            if(additional==null)CostUnavailable(trace,"total.additional-move-two-rows","two real installed Move assets unavailable");
            else CostTotalRoute(trace,log,"total.additional-move-two-rows",additional,additional.Rows,policy,now);

            var install=CostBuildSafely(trace,"total.purchase-install",CostBuildInstallScenario);
            if(install==null)CostUnavailable(trace,"total.purchase-install","compatible authored market offer unavailable");
            else CostTotalRoute(trace,log,"total.purchase-install",install,install.Rows,policy,now);

            var mixed=CostBuildSafely(trace,"total.mixed-ledger-six-rows",CostBuildMixedLedgerScenario);
            if(mixed==null)CostUnavailable(trace,"total.mixed-ledger-six-rows","existing authored six-row mixed ledger fixture unavailable");
            else CostTotalRoute(trace,log,"total.mixed-ledger-six-rows",mixed,mixed.Rows,policy,CartPreviewCharacterizationFixtures.FixedNowTicks);

            RunCostPreparedBlockTotal(trace,log);
        }
        static void RunCostPreparedBlockTotal(Trace trace,CostLog log)
        {
            var block=CostBuildSafely(trace,"total.prepared-block-resolve",()=>CostBuildPreparedBlockScenario(trace));
            if(block==null)CostUnavailable(trace,"total.prepared-block-resolve","actual prepared block plan unavailable; see COST_BLOCK_PLAN setup evidence");
            else CostTotalRoute(trace,log,"total.prepared-block-resolve",block,block.Rows,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
        }
        static CostScenario CostBuildSafely(Trace trace,string label,Func<CostScenario> build)
        {
            try{return build();}
            catch(Exception error){trace.Add("COST_FIXTURE_SETUP_ERROR|"+label+"|"+CostSafe(error.GetType().FullName+" | "+error.Message));return null;}
        }
        static void CostTotalRoute(Trace trace,CostLog log,string label,CostScenario scenario,PartCartLine[] rows,ValuationPolicy policy,long now)
        {
            var local=new CostScenario{Profile=scenario.Profile,CarId=scenario.CarId,AssetId=scenario.AssetId,Definition=scenario.Definition,Rows=rows};
            trace.Add("COST_FIXTURE|"+label+"|seed="+CostFixtureSeed+";car="+CostSafe(scenario.CarId)+";cash="+scenario.Profile.cash+
                ";rep="+scenario.Profile.reputation+";catalogParts="+scenario.Profile.catalog.parts.Count+";marketGeneration="+
                scenario.Profile.inventory.market.generation+";rows="+string.Join(";",rows.Select(row=>row.action+"/"+CostSafe(row.definitionId)+
                    "/q"+row.quantity+"/asset="+CostSafe(row.assetId)+"/dest="+row.destination+":"+CostSafe(row.destinationId))));
            CostMeasure(trace,log,label,()=>CostCopy(local),f=>f.Scalar=PartCart.Total(f.Profile,f.CarId,f.Rows,policy,now));
        }

        static CostScenario CostBuildSaleMoveScenario()
        {
            var scenario=CostBase(CostFixtureSeed);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var offers=p.catalog.parts.Where(d=>d!=null&&d.price>0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    InventoryUnits.Count(p,car,d)==1&&PartsMarket.Stock(p,d)!=null)
                .Select(d=>new{Definition=d,Product=PartsMarket.ProductKey(d),Cost=PartsMarket.Quote(p,d,1,false,1).cost})
                .OrderBy(x=>x.Cost).ThenBy(x=>x.Definition.id,StringComparer.Ordinal).ToArray();
            var first=offers.FirstOrDefault();var second=first==null?null:offers.FirstOrDefault(x=>x.Product!=first.Product&&x.Cost<=p.cash-first.Cost);
            var purchase=p.catalog.parts.Where(d=>d!=null&&d.price>0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    InventoryUnits.Count(p,car,d)>1&&PartsMarket.Stock(p,d)!=null)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(first==null||second==null||purchase==null)return null;
            string sold=PartOperations.Buy(p,p.catalog,scenario.CarId,first.Definition.id,false);
            string moved=PartOperations.Buy(p,p.catalog,scenario.CarId,second.Definition.id,false);
            var soldPart=p.parts.Find(item=>item.instanceId==sold&&item.location==PartLocation.Inventory);
            var movedPart=p.parts.Find(item=>item.instanceId==moved&&item.location==PartLocation.Inventory);
            if(soldPart==null||movedPart==null)return null;
            var sale=new PartCartLine{action=CartAction.SellPart,definitionId=soldPart.definitionId,assetId=sold,quantity=1};
            var move=new PartCartLine{action=CartAction.Move,definitionId=movedPart.definitionId,assetId=moved,destination=InventoryKind.Storage,destinationId=SharedInventory.Storage,quantity=1};
            var add=new PartCartLine{action=CartAction.NewSet,definitionId=purchase.id,quantity=1};
            scenario.Rows=new[]{sale,add,move};scenario.AssetId=sold;scenario.Definition=purchase;
            return scenario;
        }
        static CostScenario CostBuildTwoMoveScenario()
        {
            var scenario=CostBase(CostFixtureSeed+1);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var parts=car.installed.slots.Select(binding=>p.parts.Find(item=>item.instanceId==binding.partInstanceId))
                .Where(item=>item!=null&&p.catalog.Find(item.definitionId)?.category!=PartCategory.CylinderBlock)
                .OrderBy(item=>p.catalog.Find(item.definitionId).category).ThenBy(item=>item.instanceId,StringComparer.Ordinal).Take(2).ToArray();
            if(parts.Length!=2)return null;
            scenario.Rows=parts.Select(item=>new PartCartLine{action=CartAction.Move,assetId=item.instanceId,definitionId=item.definitionId,
                destination=InventoryKind.Storage,destinationId=SharedInventory.Storage,quantity=1}).ToArray();
            return scenario;
        }
        static CostScenario CostBuildInstallScenario()
        {
            var scenario=CostBase(CostFixtureSeed+2);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var definition=p.catalog.parts.Where(d=>d!=null&&d.price>0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    d.category!=PartCategory.CylinderBlock&&PartsMarket.Available(p,d)>0)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(definition==null)return null;
            scenario.Definition=definition;scenario.Rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=definition.id,quantity=1,install=true}};
            return scenario;
        }
        static CostScenario CostBuildMixedLedgerScenario()
        {
            var scenario=CostBase(CostFixtureSeed+3);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var multi=p.catalog.parts.Where(d=>d!=null&&d.price>=0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    InventoryUnits.Count(p,car,d)>1&&InventoryUnits.Roles(p,car,d).Length>0&&PartsMarket.Stock(p,d)!=null&&
                    PartsMarket.Quote(p,d,InventoryUnits.Count(p,car,d),false,1).cost<=p.cash)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(multi==null)return null;
            string soldId=PartOperations.Buy(p,p.catalog,scenario.CarId,multi.id,false);
            uint partSeed=CostFixtureSeed^0x6c8e9cf5u;var usedPart=CartPreviewCharacterizationFixtures.AddUsedPart(p,"cost/mixed-used-part",ref partSeed);
            uint unitSeed=CostFixtureSeed^0x85ebca6bu;var usedUnit=CartPreviewCharacterizationFixtures.AddUsedUnit(p,"cost/mixed-used-unit",ref unitSeed);
            var single=p.catalog.parts.Where(d=>d!=null&&d.price>=0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    InventoryUnits.Count(p,car,d)==1&&PartsMarket.Stock(p,d)!=null&&PartsMarket.ProductKey(d)!=PartsMarket.ProductKey(multi)&&
                    PartsMarket.Quote(p,d,1,false,2).cost<=p.cash)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(usedPart?.IsPart!=true||usedUnit?.IsUnit!=true||single==null)return null;
            var stock=PartsMarket.Stock(p,multi);var singleStock=PartsMarket.Stock(p,single);int count=InventoryUnits.Count(p,car,multi);
            stock.units=count+1;stock.initialUnits=count+1;stock.deliveryPercent=5;singleStock.units=1;singleStock.initialUnits=1;singleStock.deliveryPercent=10;
            string valid=MechanicsOwnership.Validate(p,p.catalog);if(valid!=null)return null;
            string role=InventoryUnits.Roles(p,car,multi).FirstOrDefault();if(string.IsNullOrEmpty(role))return null;
            scenario.Rows=new[]{
                new PartCartLine{action=CartAction.NewSet,definitionId=multi.id,quantity=1},
                new PartCartLine{action=CartAction.NewUnits,definitionId=multi.id,role=role,quantity=2},
                new PartCartLine{action=CartAction.NewSet,definitionId=single.id,quantity=2},
                new PartCartLine{action=CartAction.BuyUsed,definitionId=usedPart.DefinitionId,assetId=usedPart.Id,quantity=1},
                new PartCartLine{action=CartAction.BuyUsed,definitionId=usedUnit.DefinitionId,assetId=usedUnit.Id,role=usedUnit.unit.element.role,quantity=1},
                new PartCartLine{action=CartAction.SellPart,definitionId=p.parts.Find(i=>i.instanceId==soldId).definitionId,assetId=soldId,quantity=1}};
            CartReservations.Capture(p,scenario.CarId,scenario.Rows);
            return scenario;
        }
        static CostScenario CostBuildPreparedBlockScenario(Trace trace)
        {
            var scenario=CostBase(CostFixtureSeed+4);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var current=MechanicsOwnership.Definitions(p,car).TryGetValue(PartCategory.CylinderBlock,out string currentId)?currentId:null;
            var block=p.catalog.parts.Where(d=>d!=null&&d.category==PartCategory.CylinderBlock&&d.id!=currentId&&d.price>=0&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&
                    PartsMarket.Stock(p,d)!=null).OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(block==null){trace.Add("COST_BLOCK_SETUP_OPEN|no actual distinct compatible authored block with a market row");return null;}
            var required=GraphRequiredFittedCandidates(p,car,block.id).FirstOrDefault();
            if(required==null){trace.Add("COST_BLOCK_SETUP_OPEN|replacement resolved spec has no actual fitted required companion");return null;}
            if(!GraphMoveActualCompanionToGarage(p,car,required,out string companionId))
            {trace.Add("COST_BLOCK_SETUP_OPEN|actual companion failed Garage staging;category="+required.Item1+";part="+CostSafe(required.Item2?.instanceId));return null;}
            var core=new PartCartLine{action=CartAction.NewSet,definitionId=block.id,quantity=1,install=true};
            var plan=BlockPreparation.Build(p,scenario.CarId,core,Array.Empty<PartCartLine>(),BlockPreparationMode.Reuse,null,ValuationPolicy.Defaults());
            if(plan==null){trace.Add("COST_BLOCK_PLAN|ready=false;error=<null plan>;block="+CostSafe(block.id));return null;}
            string[] rowFacts=(plan.Lines??new List<PartCartLine>()).Select(row=>row==null?"<null>":
                row.action+"/"+CostSafe(row.definitionId)+"/asset="+CostSafe(row.assetId)+"/install="+row.install+
                "/prepared="+row.preparedBlock+"/owner="+(row.preparationOwner==plan.Owner)+"/destination="+
                row.destination+":"+CostSafe(row.destinationId)).ToArray();
            var preparedCore=(plan.Lines??new List<PartCartLine>()).Where(row=>row!=null&&row.preparedBlock&&
                row.preparationOwner==plan.Owner&&row.action==core.action&&row.definitionId==block.id&&row.install).ToArray();
            var preparedRows=(plan.Lines??new List<PartCartLine>()).Where(row=>row!=null&&row.preparedBlock).ToArray();
            bool expectedOwner=plan.Owner==BlockPreparation.Owner(core);
            bool ownerScoped=(plan.Lines??new List<PartCartLine>()).All(row=>row!=null&&row.preparationOwner==plan.Owner);
            bool companionMarkers=(plan.Lines??new List<PartCartLine>()).Where(row=>row!=null&&!row.preparedBlock)
                .All(row=>row.preparationOwner==plan.Owner);
            trace.Add("COST_BLOCK_PLAN|ready="+plan.Ready+";error="+CostSafe(plan.Error)+";owner="+CostSafe(plan.Owner)+
                ";expectedOwner="+expectedOwner+";block="+CostSafe(block.id)+";preparedCoreCount="+preparedCore.Length+
                ";preparedRowCount="+preparedRows.Length+";ownerScoped="+ownerScoped+
                ";companionsOwnerMarked="+companionMarkers+";garageCompanion="+CostSafe(companionId)+";rows="+string.Join(";",rowFacts));
            if(!plan.Ready||plan.Lines==null||plan.Lines.Count<2||!expectedOwner||preparedCore.Length!=1||preparedRows.Length!=1||!ownerScoped||!companionMarkers)
            {
                string reason=!plan.Ready?"BlockPreparation.Build/Reuse not ready: "+(plan.Error??"no proposed inspection"):
                    plan.Lines==null?"BlockPreparation.Build/Reuse returned no rows":plan.Lines.Count<2?"Reuse plan has no supporting row beyond the prepared core":
                    !expectedOwner?"BlockPreparation plan owner differs from the canonical owner for the authored core":
                    preparedCore.Length!=1||preparedRows.Length!=1?"expected exactly one prepared row, and it must be the core":
                    !ownerScoped?"one or more rows are not scoped to the plan owner":
                    "one or more companion rows lack the plan owner marker";
                trace.Add("COST_BLOCK_SETUP_OPEN|"+CostSafe(reason));return null;
            }
            scenario.Rows=plan.Lines.Select(row=>row.Copy()).ToArray();scenario.Definition=block;
            trace.Add("COST_BLOCK_FIXTURE|block="+CostSafe(block.id)+";rows="+scenario.Rows.Length+";owner="+CostSafe(plan.Owner)+
                ";fixture=BlockPreparation.Build/Reuse;preparedCoreMarkers="+scenario.Rows.Count(row=>row.preparedBlock)+
                ";companionRows="+scenario.Rows.Count(row=>!row.preparedBlock));
            return scenario;
        }

        static void RunCostStages(Trace trace,CostLog log)
        {
            var scenario=CostBuildSafely(trace,"preview-stages",CostBuildTwoMoveScenario);
            var purchase=CostBuildSafely(trace,"preview.purchase",CostBuildInstallScenario);
            if(purchase==null)CostUnavailable(trace,"preview.purchase","authored install offer unavailable");
            else CostMeasure(trace,log,"preview.purchase",()=>CostCopy(purchase),f=>f.Result=PartCart.PreviewProfile(f.Profile,f.Car,f.Rows));
            if(scenario==null){CostUnavailable(trace,"preview.move/stages","actual two-row Move preview fixture unavailable");return;}
            CostMeasure(trace,log,"preview.move",()=>CostCopy(scenario),f=>f.Result=PartCart.PreviewProfile(f.Profile,f.Car,f.Rows));

            var source=scenario.Profile;var jsonSource=CartPreviewLiteralTestClone.Copy(source);string json=JsonUtility.ToJson(jsonSource);var policy=ValuationPolicy.Defaults();
            CostMeasure(trace,log,"preview.stage.ToJson",()=>CostCopy(scenario),f=>f.Result=JsonUtility.ToJson(f.Profile));
            CostMeasure(trace,log,"preview.stage.FromJson",()=>{var f=CostCopy(scenario);f.Json=json;return f;},f=>f.Result=JsonUtility.FromJson<PlayerProfile>(f.Json));
            CostMeasure(trace,log,"preview.stage.Ensure1",()=>CostPrepareParsed(source,scenario.Rows,json),f=>MechanicsOwnership.Ensure(f.Profile,f.Profile.catalog));
            CostMeasure(trace,log,"preview.stage.Apply",()=>CostPrepareApplyPrefix(source,scenario.CarId,scenario.Rows,json,policy),
                f=>PartCart.Apply(f.Profile,f.CarId,f.Rows,true,policy));
            CostMeasure(trace,log,"preview.stage.Ensure2",()=>CostPrepareEnsure2Prefix(source,scenario.CarId,scenario.Rows,json,policy),
                f=>MechanicsOwnership.Ensure(f.Profile,f.Profile.catalog));
        }
        static CostFixture CostPrepareParsed(PlayerProfile source,PartCartLine[] rows,string json)
        {
            var profile=JsonUtility.FromJson<PlayerProfile>(json);profile.catalog=source.catalog;
            return new CostFixture{Profile=profile,Car=profile.cars.Find(c=>c.instanceId==source.SelectedCar.instanceId),CarId=source.SelectedCar.instanceId,
                Rows=rows.Select(row=>row.Copy()).ToArray()};
        }
        static CostFixture CostPrepareApplyPrefix(PlayerProfile source,string carId,PartCartLine[] rows,string json,ValuationPolicy policy)
        {
            var f=CostPrepareParsed(source,rows,json);MechanicsOwnership.Ensure(f.Profile,f.Profile.catalog);f.Profile.cash=int.MaxValue/2;
            bool owns=CartReservations.SameCart(f.Profile,carId,f.Rows);f.Profile.inventory.usingCartReservation=CartReservations.Matches(f.Profile,carId,f.Rows);
            f.Result=owns;return f;
        }
        static CostFixture CostPrepareEnsure2Prefix(PlayerProfile source,string carId,PartCartLine[] rows,string json,ValuationPolicy policy)
        {
            var f=CostPrepareApplyPrefix(source,carId,rows,json,policy);
            try{PartCart.Apply(f.Profile,carId,f.Rows,true,policy);}finally{f.Profile.inventory.usingCartReservation=false;}
            if(f.Result is bool owns&&owns)CartReservations.Clear(f.Profile);
            return f;
        }

        static void RunCostOperations(Trace trace,CostLog log)
        {
            CostRunSafely(trace,log,"Ensure.normalized",()=>{
                var normalized=CostBase(CostFixtureSeed+5);
                CostMeasure(trace,log,"Ensure.normalized",()=>CostCopy(normalized),f=>MechanicsOwnership.Ensure(f.Profile,f.Profile.catalog));
            });
            CostRunSafely(trace,log,"Apply.ordered",()=>{
                var twoMove=CostBuildSafely(trace,"Apply.ordered",CostBuildTwoMoveScenario);
                if(twoMove==null)CostUnavailable(trace,"Apply.ordered","two actual Move rows unavailable");
                else CostMeasure(trace,log,"Apply.ordered",()=>CostCopy(twoMove),f=>PartCart.Apply(f.Profile,f.CarId,f.Rows,false,ValuationPolicy.Defaults()));
            });
            CostRunSafely(trace,log,"operations.moves",()=>CostMeasureMoves(trace,log));
            CostRunSafely(trace,log,"operations.buy-install-replace",()=>CostMeasureParts(trace,log));
            CostRunSafely(trace,log,"operations.ledger",()=>CostMeasureLedger(trace,log));
            CostRunSafely(trace,log,"operations.valuation-resolve-inspection",()=>CostMeasureValuationInspection(trace,log));
        }
        static void CostMeasureMoves(Trace trace,CostLog log)
        {
            var partScenario=CostBase(CostFixtureSeed+6);var p=partScenario.Profile;var car=p.cars.Find(c=>c.instanceId==partScenario.CarId);
            var part=car.installed.slots.Select(binding=>p.parts.Find(item=>item.instanceId==binding.partInstanceId)).FirstOrDefault(item=>item!=null&&
                p.catalog.Find(item.definitionId)?.category!=PartCategory.CylinderBlock);
            if(part==null)CostUnavailable(trace,"Move.sharedInventory.part","installed authored non-block part unavailable");
            else{partScenario.AssetId=part.instanceId;CostMeasure(trace,log,"Move.sharedInventory.part",()=>CostCopy(partScenario),f=>SharedInventory.Move(f.Profile,f.AssetId,InventoryKind.Storage,SharedInventory.Storage));}

            var unitScenario=CostBase(CostFixtureSeed+7);var up=unitScenario.Profile;var uc=up.cars.Find(c=>c.instanceId==unitScenario.CarId);
            var unitDefinition=up.catalog.parts.Where(d=>d!=null&&d.price>0&&up.reputation>=d.reputationRequired&&up.catalog.Fits(up.catalog.Spec(uc.vehicleId),d)&&
                InventoryUnits.Count(up,uc,d)>1&&InventoryUnits.Roles(up,uc,d).Any()&&PartsMarket.Stock(up,d)!=null&&
                PartsMarket.Quote(up,d,InventoryUnits.Count(up,uc,d),true,1).cost<=up.cash)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(unitDefinition==null)CostUnavailable(trace,"Move.sharedInventory.unit","actual compatible modeled unit offer unavailable");
            else
            {
                string role=InventoryUnits.Roles(up,uc,unitDefinition).First();var unit=InventoryUnits.Buy(up,unitScenario.CarId,unitDefinition.id,role);
                unitScenario.AssetId=unit.element.id;
                CostMeasure(trace,log,"Move.sharedInventory.unit",()=>CostCopy(unitScenario),f=>SharedInventory.Move(f.Profile,f.AssetId,InventoryKind.Storage,SharedInventory.Storage));
            }

            var engineScenario=CostBase(CostFixtureSeed+8);var ep=engineScenario.Profile;
            var engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(ep,engineScenario.CarId);
            engineScenario.AssetId=engine.instanceId;
            CostMeasure(trace,log,"Move.sharedInventory.engine",()=>CostCopy(engineScenario),f=>SharedInventory.Move(f.Profile,f.AssetId,InventoryKind.Car,f.CarId));
        }
        static void CostMeasureParts(Trace trace,CostLog log)
        {
            var scenario=CostBase(CostFixtureSeed+9);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var d=p.catalog.parts.Where(item=>item!=null&&item.price>0&&p.reputation>=item.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),item)&&
                    PartsMarket.Stock(p,item)!=null&&PartsMarket.Quote(p,item,InventoryUnits.Count(p,car,item),false,1).cost<=p.cash)
                .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
            if(d==null){CostUnavailable(trace,"Buy.part","authored compatible market stock unavailable");return;}
            scenario.Definition=d;
            CostMeasure(trace,log,"Buy.part",()=>CostCopy(scenario),f=>f.Result=PartOperations.Buy(f.Profile,f.Profile.catalog,f.CarId,d.id,false));

            var installedBase=CostBase(CostFixtureSeed+10);var ip=installedBase.Profile;var ic=ip.cars.Find(c=>c.instanceId==installedBase.CarId);
            var target=ip.catalog.parts.Where(item=>item!=null&&item.price>0&&ip.reputation>=item.reputationRequired&&ip.catalog.Fits(ip.catalog.Spec(ic.vehicleId),item)&&
                    item.category!=PartCategory.CylinderBlock&&PartsMarket.Available(ip,item)>0&&
                    PartsMarket.Quote(ip,item,InventoryUnits.Count(ip,ic,item),false,1).cost<=ip.cash)
                .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
            if(target==null){CostUnavailable(trace,"Install.part/Replace.mechanics","actual compatible replacement offer unavailable");return;}
            string installedId=PartOperations.Buy(ip,ip.catalog,installedBase.CarId,target.id,false);
            var part=ip.parts.Find(item=>item.instanceId==installedId&&item.location==PartLocation.Inventory);
            if(part==null){CostUnavailable(trace,"Install.part/Replace.mechanics","actual PartOperations.Buy did not create a loose physical part");return;}
            installedBase.AssetId=installedId;installedBase.Definition=target;
            CostMeasure(trace,log,"Install.part",()=>CostCopy(installedBase),f=>PartOperations.Install(f.Profile,f.Profile.catalog,f.CarId,f.AssetId));
            CostMeasure(trace,log,"Replace.mechanics",()=>CostCopy(installedBase),f=>MechanicsOwnership.Replace(f.Profile,f.Car,f.Part,f.Profile.catalog));
        }
        static void CostMeasureLedger(Trace trace,CostLog log)
        {
            var scenario=CostBase(CostFixtureSeed+11);var p=scenario.Profile;var car=p.cars.Find(c=>c.instanceId==scenario.CarId);
            var definition=p.catalog.parts.Where(d=>d!=null&&d.price>=0&&p.reputation>=d.reputationRequired&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)&&PartsMarket.Stock(p,d)!=null)
                .OrderBy(d=>d.id,StringComparer.Ordinal).FirstOrDefault();
            if(definition==null){CostUnavailable(trace,"Ledger.ctor/Take","authored compatible stocked definition unavailable");return;}
            scenario.Definition=definition;
            int units=InventoryUnits.Count(p,car,definition),basePrice=InventoryPricing.NewSet(definition);
            CostMeasure(trace,log,"Ledger.ctor",()=>CostCopy(scenario),f=>f.Result=new PartsMarket.Ledger(f.Profile));
            CostMeasure(trace,log,"Ledger.Take",()=>{var f=CostCopy(scenario);f.Ledger=new PartsMarket.Ledger(f.Profile);f.LedgerUnits=units;f.LedgerBasePrice=basePrice;return f;},
                f=>f.Result=f.Ledger.Take(f.Definition,f.LedgerUnits,f.LedgerBasePrice,1,false));
        }
        static void CostMeasureValuationInspection(Trace trace,CostLog log)
        {
            var scenario=CostBase(CostFixtureSeed+12);var p=scenario.Profile;var car=p.cars.Find(item=>item.instanceId==scenario.CarId);
            var definition=p.catalog.parts.Where(item=>item!=null&&item.price>=0&&p.reputation>=item.reputationRequired&&
                    p.catalog.Fits(p.catalog.Spec(car.vehicleId),item)&&PartsMarket.Stock(p,item)!=null&&
                    PartsMarket.Quote(p,item,InventoryUnits.Count(p,car,item),false,1).cost<=p.cash)
                .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
            if(definition==null){CostUnavailable(trace,"Valuation/Resolve/Inspection","no compatible authored purchase offer passes actual REP and quote/cash checks");return;}
            string looseId=PartOperations.Buy(p,p.catalog,scenario.CarId,definition.id,false);
            var loose=p.parts.FirstOrDefault(item=>item!=null&&item.instanceId==looseId&&item.location==PartLocation.Inventory);
            string validation=MechanicsOwnership.Validate(p,p.catalog);
            if(loose==null||validation!=null){CostUnavailable(trace,"Valuation/Resolve/Inspection","actual loose PartOperations.Buy or post-setup validation failed: "+(validation??"physical part missing"));return;}
            scenario.AssetId=loose.instanceId;scenario.Definition=p.catalog.Find(loose.definitionId);var policy=ValuationPolicy.Defaults();long now=CartPreviewCharacterizationFixtures.FixedNowTicks;
            CostMeasure(trace,log,"Valuation.LoosePriceView.construct",()=>CostCopy(scenario),f=>f.Result=Valuations.LoosePriceView(f.Profile,policy,"wholesale",now));
            CostMeasure(trace,log,"Valuation.LoosePriceView.lookup",()=>{var f=CostCopy(scenario);f.PriceView=Valuations.LoosePriceView(f.Profile,policy,"wholesale",now);return f;},
                f=>f.Scalar=f.PriceView(f.AssetId,false));
            CostMeasure(trace,log,"Valuation.Capture",()=>CostCopy(scenario),f=>f.Result=Valuations.Capture(f.Profile,f.Profile.catalog,policy,ValuationKind.LoosePart,f.AssetId,"wholesale",now));
            CostMeasure(trace,log,"Resolve.mechanics",()=>CostCopy(scenario),f=>f.Result=MechanicsOwnership.Resolve(f.Profile,f.Car,f.Profile.catalog));
            CostMeasure(trace,log,"InspectionBuild.Capture",()=>CostCopy(scenario),f=>f.Result=InspectionBuild.Capture(f.Profile,f.Car));
        }
    }
}
#endif
