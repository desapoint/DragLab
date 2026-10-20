#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public static class NewSupplyChecks
    {
        // Editor assembly invokes the public verifier facade; internal catalog
        // caching APIs stay private to the runtime assembly.
        public static bool PrepareSupplyCategoryCache(MechanicalCatalog catalog)
        {
            NewSupplyCatalogIndex.Request(catalog);
            int advances=0;
            while(!NewSupplyCatalogIndex.Ready(catalog)&&advances++<catalog.parts.Count+2)
                NewSupplyCatalogIndex.Advance(catalog,8);
            return NewSupplyCatalogIndex.ForDetached(catalog)!=null;
        }
        public static bool PrepareProductKeyCache(MechanicalCatalog catalog)
        {
            var definitions=catalog.parts.ToArray();
            var keys=definitions.ToDictionary(d=>d.id,d=>PartsMarket.ProductKey(d),StringComparer.Ordinal);
            return MarketCatalogLayout.PublishProductKeys(catalog,definitions,
                catalog.AuthoringRevision,keys);
        }
        public static void WriteLegacyFixture(PlayerProfile p,string path)
        {
            p.inventory.market.version=1;foreach(var stock in p.inventory.market.parts)stock.discovered=false;
            DragLab.Infrastructure.SqliteStateStore.Write(path,DragLab.Infrastructure.SqliteStateStore.Capture(p));
        }
        static NewPartSupply.Product Product(PartCategory category,int maximum=1,bool individual=false)=>new NewPartSupply.Product{definition=new PartDefinition{id="fixture/"+category,category=category},stock=new NewPartStock(),maximum=maximum,individual=individual};
        // Frozen pre-optimization selector: compare both the exact chosen identity and
        // final RNG state, not just category distribution or statistical frequencies.
        static NewPartSupply.Product LegacyPickCategory(List<NewPartSupply.Product> pool,NewPartSupply.Branch branch,ref uint random)
        {
            if(pool.Count==0)return null;if(pool.Count==1)return pool[0];
            if(branch.children==null)return pool[(int)(NewPartSupply.Next(ref random)*pool.Count)];
            var filters=branch.children.Select<NewPartSupply.Branch,Func<NewPartSupply.Product,bool>>(b=>b.Accepts).ToArray();
            var weights=branch.children.Select(b=>b.weight).ToArray();
            var selected=NewPartSupply.Filter(pool,filters,weights,ref random);
            if(selected.Count==0)return null;
            var child=branch.children.First(b=>b.weight>0&&b.Accepts(selected[0]));
            return LegacyPickCategory(selected,child,ref random);
        }
        public static void Rules(Action<bool,string> check)
        {
            foreach(var point in new[]{(0.0,.4),(.1f,.4),(.10001,.3),(.25,.3),(.25001,.2),(.5,.2),(.50001,.1),(.6f,.1),(.60001,0.0),(1.0,0.0)})check(SupplyWear.Base(point.Item1,false)==point.Item2,"Exact wear base boundary "+point.Item1);
            check(SupplyWear.Base(1,true)==.4,"Failed component uses maximum base despite healthy numeric condition");
            check(SupplyWear.NewMultiplier(0)==1&&SupplyWear.NewMultiplier(1)==.5&&SupplyWear.NewMultiplier(2)==0,"New complete-repair multipliers");
            check(SupplyWear.UsedMultiplier(0,false)==1&&SupplyWear.UsedMultiplier(0,true)==.75&&SupplyWear.UsedMultiplier(1,true)==.5&&SupplyWear.UsedMultiplier(2,true)==.25,"Used complete/partial multipliers");
            check(NewPartSupply.DiscoveryProbability(0,10)==0&&NewPartSupply.DiscoveryProbability(1,100)==.1&&NewPartSupply.DiscoveryProbability(3,4)==.75&&NewPartSupply.DiscoveryProbability(4,4)==1,"Discovery probability floor, fraction and empty branches");
            check(NewPartSupply.Roll(new[]{0.0,2,1},0)==1&&NewPartSupply.Roll(new[]{0.0,2,1},.6666666666666667)==2&&NewPartSupply.Roll(new[]{0.0,0},.4)==-1,"Weighted roll preserves zero branches and proportional boundaries");
            var single=Product(PartCategory.Exhaust);var multiple=Product(PartCategory.Pistons,6,true);var kit=Product(PartCategory.Brakes,4,false);
            foreach(var pair in new[]{(0.0,1),(.79999,1),(.8,2),(.96999,2),(.97,3)})check(NewPartSupply.Quantity(single,true,0,pair.Item1)==pair.Item2,"First single delivery boundary "+pair.Item1);
            foreach(var pair in new[]{(0.0,6),(.49999,6),(.5,12),(.84999,12),(.85,18)})check(NewPartSupply.Quantity(multiple,true,.59999,pair.Item1)==pair.Item2,"First full-set delivery boundary "+pair.Item1);
            check(NewPartSupply.Quantity(multiple,true,.6,.99)==1&&NewPartSupply.Quantity(multiple,false,.99,.99)==1&&NewPartSupply.Quantity(multiple,false,.59,.99)==6&&NewPartSupply.Quantity(kit,false,.99,.99)==4&&NewPartSupply.Quantity(single,false,0,.99)==1,"60/40 mode, unsupported singles, and restock quantity rules");
            uint random=71,quantity=92;uint before=random;
            check(NewPartSupply.Deliver(new List<NewPartSupply.Product>(),Array.Empty<SupplyWear.Need>(),ref random,ref quantity)==0&&random==before,"Empty initial pool exits without rolling");
            var unavailable=Product(PartCategory.Exhaust,0);check(NewPartSupply.Deliver(new List<NewPartSupply.Product>{unavailable},Array.Empty<SupplyWear.Need>(),ref random,ref quantity)==0&&random==before,"Undeliverable product is pruned before the single-product exit");
            check(NewPartSupply.Deliver(new List<NewPartSupply.Product>{multiple},Array.Empty<SupplyWear.Need>(),ref random,ref quantity)>0&&random==before&&multiple.stock.discovered,"Sole product skips all product-filtering rolls, still delivers and discovers");
            int stock=multiple.stock.units;NewPartSupply.Deliver(new List<NewPartSupply.Product>{multiple},Array.Empty<SupplyWear.Need>(),ref random,ref quantity);check(multiple.stock.units>stock,"Repeated opportunity adds stock rather than replacing it");
            var slot=new RuntimeIndexSlot<object,string>();var owner=new object();
            check(!slot.TryGet(owner,1,out _),"Cold runtime index has no published state");
            slot.Publish(owner,1,"epoch-one");
            check(slot.TryGet(owner,1,out var prior)&&prior=="epoch-one",
                "Runtime index publishes a complete source/version snapshot");
            slot.Publish(owner,2,"epoch-two");
            check(!slot.TryGet(owner,1,out _)&&slot.TryGet(owner,2,out var next)&&next=="epoch-two",
                "Runtime index rejects stale generation after replacement");
            slot.Invalidate(owner);
            check(!slot.TryGet(owner,2,out _),"Runtime index invalidation discards derived state");
            var all=Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().Select(c=>Product(c)).ToList();
            // Cold/preparing/ready paths must agree on every category branch. Warmup
            // never publishes half-built state and does not consume a random draw.
            var indexCatalog=new MechanicalCatalog(Array.Empty<VehicleSpec>(),false);
            indexCatalog.parts.AddRange(all.Select(p=>p.definition));
            NewSupplyCatalogIndex.Request(indexCatalog);
            check(!NewSupplyCatalogIndex.Ready(indexCatalog)&&NewSupplyCatalogIndex.For(indexCatalog,all[0].definition)==null&&
                NewSupplyCatalogIndex.ForDetached(indexCatalog)==null,
                "Cold startup index leaves original eligibility available while unready");
            NewSupplyCatalogIndex.Advance(indexCatalog,.00001);
            check(!NewSupplyCatalogIndex.Ready(indexCatalog)&&NewSupplyCatalogIndex.For(indexCatalog,all[0].definition)==null&&
                NewSupplyCatalogIndex.ForDetached(indexCatalog)==null,
                "Partial startup index is never published");
            int preparedAdvances=0;
            while(!NewSupplyCatalogIndex.Ready(indexCatalog)&&preparedAdvances++<all.Count+2)
                NewSupplyCatalogIndex.Advance(indexCatalog,5);
            check(NewSupplyCatalogIndex.Ready(indexCatalog),"Incremental startup index publishes once fully ready");
            bool exactMembership=true;
            void CompareBranches(NewPartSupply.Branch branch,NewPartSupply.Product plain,NewPartSupply.Product cached){
                if(branch.Accepts(plain)!=branch.Accepts(cached))exactMembership=false;
                if(branch.children!=null)foreach(var child in branch.children)CompareBranches(child,plain,cached);
            }
            var indexedAll=all.Select(p=>new NewPartSupply.Product{definition=p.definition,
                stock=new NewPartStock(),maximum=p.maximum,individual=p.individual,owned=p.owned,
                preclassified=NewSupplyCatalogIndex.For(indexCatalog,p.definition)}).ToList();
            for(int i=0;i<all.Count;i++)CompareBranches(NewPartSupply.Categories,all[i],indexedAll[i]);
            check(exactMembership&&indexedAll.All(p=>p.preclassified!=null),"Published index exactly matches cold category tree for every authored category");
            var snapshot=NewSupplyCatalogIndex.ForDetached(indexCatalog);
            check(snapshot!=null&&ReferenceEquals(snapshot,NewSupplyCatalogIndex.ForDetached(indexCatalog)),
                "Detached category snapshot is fully published once and reused");
            var snapshotAll=all.Select(p=>new NewPartSupply.Product{definition=p.definition,
                stock=new NewPartStock(),maximum=p.maximum,individual=p.individual,owned=p.owned,
                preindexed=snapshot.Find(p.definition.id)}).ToList();
            for(int i=0;i<all.Count;i++)CompareBranches(NewPartSupply.Categories,all[i],snapshotAll[i]);
            check(exactMembership&&snapshotAll.All(p=>p.preindexed!=null),
                "Worker category membership exactly matches the original tree");
            uint bareSeed=0x8f24173du,cachedSeed=bareSeed;bool equivalent=true;
            for(int i=0;i<256;i++)
                if(!ReferenceEquals(NewPartSupply.PickCategory(all,NewPartSupply.Categories,ref bareSeed).definition,
                    NewPartSupply.PickCategory(indexedAll,NewPartSupply.Categories,ref cachedSeed).definition)||bareSeed!=cachedSeed){equivalent=false;break;}
            check(equivalent,"Cached product selection preserves 256 exact RNG positions and definition identities");
            uint snapshotSeed=0x8f24173du,plainSeed=snapshotSeed;bool snapshotParity=true;
            for(int i=0;i<256;i++)
                if(!ReferenceEquals(NewPartSupply.PickCategory(all,NewPartSupply.Categories,ref plainSeed).definition,
                    NewPartSupply.PickCategory(snapshotAll,NewPartSupply.Categories,ref snapshotSeed).definition)||
                    plainSeed!=snapshotSeed){snapshotParity=false;break;}
            check(snapshotParity,"Detached runtime index preserves 256 exact selections and RNG positions");
            NewSupplyCatalogIndex.Request(indexCatalog);
            check(NewSupplyCatalogIndex.Ready(indexCatalog),"Reset/reentry reuses the completed catalog index");
            var mutated=all.First(p=>p.definition.category==PartCategory.Tires).definition;
            var originalStage=mutated.stage;mutated.stage=originalStage+1;
            check(NewSupplyCatalogIndex.For(indexCatalog,mutated)==null&&
                NewSupplyCatalogIndex.ForDetached(indexCatalog)==null,
                "Authored classification changes reject stale cached membership and worker snapshots");
            mutated.stage=originalStage;
            foreach(var p in all)check(NewPartSupply.Categories.Accepts(p),"Category tree covers "+p.definition.category);
            double[] rootWeights={3,3,2.5,2,2,1.75,1.5,1};
            check(NewPartSupply.Categories.children.Select(b=>b.weight).SequenceEqual(rootWeights),"Root weights equal the approved 16.75-total contract");
            double[][] groupWeights={new double[]{65,25,10},new double[]{20,30,50},new double[]{40,30,30},new double[]{25,20,20,15,20},new double[]{35,30,20,15},new double[]{35,40,25},null,new double[]{50,25,25}};
            for(int i=0;i<groupWeights.Length;i++)if(groupWeights[i]!=null)check(NewPartSupply.Categories.children[i].children.Select(b=>b.weight).SequenceEqual(groupWeights[i]),"Approved family weights: "+NewPartSupply.Categories.children[i].name);
            single.owned=true;kit.owned=false;int fitting=0;const int rolls=12000;
            for(int n=0;n<rolls;n++)if(NewPartSupply.Filter(new List<NewPartSupply.Product>{single,kit},new Func<NewPartSupply.Product,bool>[] {p=>p.owned,p=>!p.owned},new[]{.76,.24},ref random)[0].owned)fitting++;
            check(Math.Abs(fitting/(double)rolls-.76)<.02,"Owned-car preference follows 76/24 rather than product counts");
            before=random;check(NewPartSupply.Filter(new List<NewPartSupply.Product>{single},new Func<NewPartSupply.Product,bool>[] {p=>!p.owned,p=>p.owned},new[]{.99,.01},ref random).Single()==single&&random==before,"Empty high-weight branch renormalizes to valid low-weight branch without retries");
            var only=NewPartSupply.PickCategory(new List<NewPartSupply.Product>{single},NewPartSupply.Categories,ref random);check(only==single,"Deep empty family/category branches prune to sole valid product");
            var custom=new NewPartSupply.Branch{name="fixture",weight=1,children=new[]{new NewPartSupply.Branch{name="dead",weight=99,children=new[]{new NewPartSupply.Branch{name="disabled",weight=0,filter=p=>true}}},new NewPartSupply.Branch{name="live",weight=1,filter=p=>true}}};
            check(!custom.children[0].Accepts(single)&&NewPartSupply.PickCategory(new List<NewPartSupply.Product>{single,kit},custom,ref random)!=null,"Downstream-disabled branches cannot receive original parent weight");
            void CompareCategoryRng(List<NewPartSupply.Product> pool,string label){
                uint optimized=0x74bc319du,legacy=optimized;bool identical=true;
                for(int i=0;i<256;i++){
                    var chosen=NewPartSupply.PickCategory(pool,NewPartSupply.Categories,ref optimized);
                    var expected=LegacyPickCategory(pool,NewPartSupply.Categories,ref legacy);
                    if(!ReferenceEquals(chosen,expected)||optimized!=legacy){identical=false;break;}
                }
                check(identical,"Optimized category selection preserves 256 seeded choices/RNG positions: "+label);
            }
            CompareCategoryRng(all,"all categories");
            CompareCategoryRng(all.Where(p=>p.definition.category!=PartCategory.Tires&&p.definition.category!=PartCategory.Induction).ToList(),"filtered families");
            // Compare a frozen eligible pool against the historical scan on every
            // opportunity, including stock discovery changes between opportunities.
            var originalPool=all.Select((p,i)=>new NewPartSupply.Product{
                definition=p.definition,maximum=i%9==0?0:2,owned=i%2==0,
                stock=new NewPartStock{discovered=i%5==0,units=i%5==0?2:0,initialUnits=i%5==0?2:0}
            }).ToList();
            var preparedPool=originalPool.Select(p=>new NewPartSupply.Product{
                definition=p.definition,maximum=p.maximum,owned=p.owned,
                stock=new NewPartStock{discovered=p.stock.discovered,units=p.stock.units,initialUnits=p.stock.initialUnits}
            }).ToList();
            var prepared=NewPartSupply.PrepareDeliveryPool(preparedPool);
            uint oldSelection=0xa148ed3fu,fastSelection=oldSelection,oldQuantity=0xc7124baeu,fastQuantity=oldQuantity;
            bool sameSupply=true;
            for(int i=0;i<44;i++){
                int a=NewPartSupply.Deliver(originalPool,Array.Empty<SupplyWear.Need>(),ref oldSelection,ref oldQuantity);
                int b=NewPartSupply.Deliver(prepared,Array.Empty<SupplyWear.Need>(),ref fastSelection,ref fastQuantity);
                if(a!=b||oldSelection!=fastSelection||oldQuantity!=fastQuantity||
                   originalPool.Where((p,j)=>p.stock.units!=preparedPool[j].stock.units||
                       p.stock.initialUnits!=preparedPool[j].stock.initialUnits||
                       p.stock.discovered!=preparedPool[j].stock.discovered).Any()){
                    sameSupply=false;break;
                }
            }
            check(sameSupply,"Prepared eligible pool preserves all 44 seeded deliveries and evolving stock");
            // Detached worker batch must retain the exact per-opportunity choices,
            // quantity draws and final stock, without calling Unity from its loop.
            var sequential=originalPool.Select(p=>new NewPartSupply.Product{
                definition=p.definition,maximum=p.maximum,owned=p.owned,
                stock=new NewPartStock{definitions=new List<string>{p.definition.id},
                    discovered=p.stock.discovered,units=p.stock.units,initialUnits=p.stock.initialUnits}
            }).ToList();
            var batched=sequential.Select(p=>new NewPartSupply.Product{
                definition=p.definition,maximum=p.maximum,owned=p.owned,
                stock=new NewPartStock{definitions=new List<string>{p.definition.id},
                    discovered=p.stock.discovered,units=p.stock.units,initialUnits=p.stock.initialUnits}
            }).ToList();
            uint sequentialSeed=991u,batchSeed=991u,sequentialQuantity=992u,batchQuantity=992u;
            var directPool=NewPartSupply.PrepareDeliveryPool(sequential);
            var workerPool=NewPartSupply.PrepareDeliveryPool(batched,(stage,milliseconds)=>{});
            for(int i=0;i<44;i++)NewPartSupply.Deliver(directPool,Array.Empty<SupplyWear.Need>(),ref sequentialSeed,ref sequentialQuantity);
            NewPartSupply.DeliverBatch(workerPool,Array.Empty<SupplyWear.Need>(),44,ref batchSeed,ref batchQuantity,null);
            check(sequentialSeed==batchSeed&&sequentialQuantity==batchQuantity&&
                sequential.Where((row,i)=>row.stock.units!=batched[i].stock.units||
                    row.stock.initialUnits!=batched[i].stock.initialUnits||
                    row.stock.discovered!=batched[i].stock.discovered).Count()==0,
                "Detached worker batch preserves all 44 seeded new-part deliveries");
            var categories=new int[8];const int samples=12000;random=1901;
            for(int n=0;n<samples;n++){var selected=NewPartSupply.PickCategory(all,NewPartSupply.Categories,ref random);categories[Array.FindIndex(NewPartSupply.Categories.children,b=>b.Accepts(selected))]++;}
            double total=rootWeights.Sum();
            for(int i=0;i<8;i++)check(Math.Abs(categories[i]/(double)samples-rootWeights[i]/total)<.025,"Seeded root distribution follows category weight, not catalogue size: "+NewPartSupply.Categories.children[i].name);
            var tireKeys=new[]{"OEM","Touring","StreetSemiSlick","EnduranceRadial","ProRadial","SlickMedium","SlickSoft","QualifyingSlick"};var tires=tireKeys.Select(key=>{var p=Product(PartCategory.Tires);p.definition.id="fixture/Tires/"+key;p.definition.stage=key=="OEM"?0:1;return p;}).ToList();
            CompareCategoryRng(tires,"tire families");
            int street=0,radial=0,slick=0;for(int n=0;n<samples;n++){string family=NewPartSupply.TireFamily(NewPartSupply.PickCategory(tires,NewPartSupply.Categories,ref random).definition);if(Array.IndexOf(tireKeys,family)<3)street++;else if(Array.IndexOf(tireKeys,family)<5)radial++;else slick++;}
            check(Math.Abs(street/(double)samples-.2)<.025&&Math.Abs(radial/(double)samples-.3)<.025&&Math.Abs(slick/(double)samples-.5)<.025,"Pruned root retains nested tire-family 20/30/50 weights");
            var induction=Enum.GetValues(typeof(EngineUpgradeInduction)).Cast<EngineUpgradeInduction>().Select(kind=>{var p=Product(PartCategory.Induction);p.definition.induction=kind;return p;}).ToList();var counts=new int[6];
            CompareCategoryRng(induction,"induction branches");
            for(int n=0;n<samples;n++)counts[(int)NewPartSupply.PickCategory(induction,NewPartSupply.Categories,ref random).definition.induction]++;
            check(Math.Abs(counts[(int)EngineUpgradeInduction.Factory]/(double)samples-.1)<.02&&Math.Abs((counts[(int)EngineUpgradeInduction.Turbo]+counts[(int)EngineUpgradeInduction.TwinTurbo]+counts[(int)EngineUpgradeInduction.Twincharged])/(double)samples-.65)<.025,"Nested induction families retain 65/25/10 weights");
            foreach(var kind in new[]{EngineUpgradeInduction.Turbo,EngineUpgradeInduction.TwinTurbo,EngineUpgradeInduction.Twincharged})check(Math.Abs(counts[(int)kind]/(double)samples-.65/3)<.025,"Turbo subtypes are equally weighted: "+kind);
        }
        public static void LayoutRegression(MechanicalCatalog catalog,Action<bool,string> check)
        {
            // Full authored catalog comparison lives in the game assembly because
            // MarketCatalogLayout is deliberately internal.
            var layoutKeys=catalog.parts.ToDictionary(d=>d.id,d=>PartsMarket.ProductKey(d),StringComparer.Ordinal);
            var layoutModels=catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock)
                .Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            var layoutSpecs=layoutModels.Select(catalog.Spec).ToArray();
            MarketCatalogLayout.Layout staged=null;int layoutYields=0,groupingYields=0;
            using(var layoutIterator=MarketCatalogLayout.GetSteps(catalog,layoutKeys,layoutSpecs,l=>staged=l).GetEnumerator())
                while(layoutIterator.MoveNext()){
                    layoutYields++;
                    if(string.Equals(layoutIterator.Current as string,"parts/layout grouping",StringComparison.Ordinal))
                        groupingYields++;
                    if(staged!=null)throw new InvalidOperationException("Partially constructed market layout escaped before commit.");
                }
            check(staged!=null&&staged.groups.Length>0&&staged.ids.Length==catalog.parts.Count,
                "Cold layout publishes all authored product identities");
            var direct=MarketCatalogLayout.Get(catalog,layoutKeys,layoutSpecs);
            check(ReferenceEquals(staged,direct)&&direct.groups.All(g=>g.ids.Count>0&&g.maximum>0),
                "Direct callers reuse the exact completed staged layout");
            var expectedGroups=catalog.parts.GroupBy(d=>layoutKeys[d.id],StringComparer.Ordinal)
                .OrderBy(g=>g.Min(d=>d.id),StringComparer.Ordinal).ToArray();
            bool sameGroups=expectedGroups.Length==direct.groups.Length;
            for(int i=0;sameGroups&&i<expectedGroups.Length;i++){
                var group=expectedGroups[i];
                var d=group.OrderBy(v=>v.id,StringComparer.Ordinal).First();
                var fitting=layoutSpecs.Where(spec=>catalog.Fits(spec,d)).ToArray();
                sameGroups=ReferenceEquals(d,direct.groups[i].definition)&&
                    group.Select(v=>v.id).OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(direct.groups[i].ids)&&
                    direct.groups[i].deliverable==(fitting.Length>0)&&
                    direct.groups[i].maximum==fitting.Select(spec=>PartsMarket.Count(spec,d)).DefaultIfEmpty(1).Max();
            }
            check(sameGroups,"Staged layout exactly matches the former fitted grouping/max-set algorithm");
            check(ReferenceEquals(direct,MarketCatalogLayout.Get(catalog,layoutKeys,layoutSpecs)),
                "Warm catalog layout remains reusable without another fitting build");
            check(layoutYields>0,"Cold authored market layout yields between fitting batches");
            check(groupingYields>1,"Cold authored product grouping is now incrementally yieldable");
            // Test the independent off-thread cold path, not the already-cached
            // layout. The existing staged/synchronous path above remains exact.
            MarketCatalogLayout.ClearForVerification(catalog);
            MarketCatalogLayout.Layout workerLayout=null;
            int workerWaits=0;
            using(var iterator=MarketCatalogLayout.GetSteps(catalog,layoutKeys,layoutSpecs,value=>workerLayout=value,true).GetEnumerator())
                while(iterator.MoveNext()){
                    if(iterator.Current is MarketWorkerWait wait){workerWaits++;wait.task.GetAwaiter().GetResult();}
                    if(workerLayout!=null)throw new InvalidOperationException("Worker exposed an unfinished market layout.");
                }
            bool workerMatches=workerLayout!=null&&workerLayout.groups.Length==direct.groups.Length;
            for(int i=0;workerMatches&&i<direct.groups.Length;i++){
                var a=direct.groups[i];var w=workerLayout.groups[i];
                workerMatches=ReferenceEquals(a.definition,w.definition)&&
                    a.ids.SequenceEqual(w.ids)&&a.maximum==w.maximum&&a.deliverable==w.deliverable;
            }
            check(workerWaits>0&&workerMatches,"Detached worker computes the same cold fitted catalog without publishing partial groups");
            // Author metadata keys belong to a catalog revision, not an epoch.
            // Verify that the ready dictionary is reused as-is and explicit
            // authored updates invalidate it while wear never changes it.
            var definitions=catalog.parts.ToArray();
            var revision=catalog.AuthoringRevision;
            check(MarketCatalogLayout.PublishProductKeys(catalog,definitions,revision,layoutKeys),
                "Completed authored product keys publish as a single catalog snapshot");
            Dictionary<string,string> ready;
            check(MarketCatalogLayout.TryReadyProductKeys(catalog,out ready)&&ReferenceEquals(ready,layoutKeys),
                "Unchanged catalog retrieves the exact cached key dictionary");
            var sample=definitions[0];string originalName=sample.name;
            try
            {
                sample.name=originalName+" / invalidation regression";
                catalog.RebuildIndexes();
                check(!MarketCatalogLayout.TryReadyProductKeys(catalog,out ready),
                    "Authoring revision invalidates completed product-key snapshots");
                check(PartsMarket.ProductKey(sample)!=layoutKeys[sample.id],
                    "An authored field change produces a different canonical product key");
            }
            finally
            {
                sample.name=originalName;
                catalog.RebuildIndexes();
            }
            check(!MarketCatalogLayout.TryReadyProductKeys(catalog,out ready),
                "Restoring the authored field also requires a new complete key snapshot");
            check(PartsMarket.ProductKey(sample)==layoutKeys[sample.id],
                "Restored authored part reproduces the original canonical key");
        }
        public static void Coverage(PlayerProfile p,Action<bool,string> check)
        {
            var car=p.SelectedCar;var catalog=p.catalog;var spec=catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(p,car),car.tune).spec;
            var installed=p.parts.First(i=>car.installed.slots.Any(s=>s.partInstanceId==i.instanceId)&&catalog.Find(i.definitionId).category==PartCategory.Pistons);
            foreach(var part in p.parts){part.condition.failed=false;part.condition.health=1;foreach(var e in part.condition.elements){e.health=1;e.fault=ElementFault.None;if(e is TireCondition t){t.failed=false;t.consumedLife=0;}}}
            installed.condition.elements[0].health=.3f;p.inventory.dealer.Clear();p.inventory.cart=null;
            foreach(var row in p.inventory.market.parts){row.units=0;row.initialUnits=Math.Max(row.initialUnits,100);}
            var products=p.inventory.market.parts.Select(row=>{var d=catalog.Find(row.definitions[0]);return new NewPartSupply.Product{definition=d,stock=row,maximum=4,individual=EngineElements.Supported(d.category)||d.category==PartCategory.Tires};}).ToArray();
            var product=products.Single(v=>v.stock.definitions.Contains(installed.definitionId));var definition=catalog.Find(installed.definitionId);string role=installed.condition.elements[0].role;
            SupplyWear.Need Need()=>SupplyWear.Build(p,products).First(n=>n.component.StartsWith(installed.instanceId+"/",StringComparison.Ordinal));
            check(Math.Abs(Need().Probability()-.2)<1e-12,"Worn individual uses exact 30% condition, not aggregate pack average");
            product.stock.units=1;check(Need().Probability()==.1,"One matching new individual is one complete repair");
            check(Need().Probability(true)==Need().Probability(),"Detached worker availability matches first complete new repair");
            product.stock.units=2;check(Need().Probability()==0,"Two matching new individuals suppress further priority");product.stock.units=0;
            var alternate=Need().candidates.First(c=>c.product!=product&&c.required>1);alternate.product.stock.units=alternate.required-1;check(Need().Probability()==.2,"Partial differently specified engine stock cannot count as a matching complete replacement");alternate.product.stock.units=alternate.required;check(Need().Probability()==.1,"Complete alternative matched set provides one repair");alternate.product.stock.units=0;
            DealerStock Unit(string id,string def,float health){var e=EngineElements.Create(PartCategory.Pistons);e.id=id;e.role=role;e.cylinder=e.valve=-1;e.health=health;return new DealerStock{isUnit=true,unit=new LooseEngineElement{definitionId=def,unitPrice=10,element=e},paidPrice=1};}
            p.inventory.dealer.Add(Unit("coverage/one",definition.id,.6f));check(Need().usedMultiplier==1,"Used member at exactly 60% is excluded");
            p.inventory.dealer[0].unit.element.health=.75f;check(Need().usedMultiplier==.5,"One suitable 75% used individual fully covers one worn member");
            p.inventory.dealer[0].unit.element.fault=ElementFault.CompressionLeak;check(Need().usedMultiplier==1,"Faulted otherwise high-condition member is unusable coverage");p.inventory.dealer[0].unit.element.fault=ElementFault.None;
            CartReservations.Capture(p,car.instanceId,new[]{new PartCartLine{action=CartAction.BuyUsed,definitionId=definition.id,assetId="coverage/one"}});check(Need().usedMultiplier==1,"Used member reserved in an active cart is excluded");p.inventory.cart=null;
            p.inventory.dealer.Clear();for(int n=0;n<alternate.required-1;n++)p.inventory.dealer.Add(Unit("coverage/partial/"+n,alternate.product.definition.id,.8f));
            check(Need().usedMultiplier==.75,"Partial alternative-spec used units are not a complete matching repair");
            p.inventory.dealer.Add(Unit("coverage/complete",alternate.product.definition.id,.8f));check(Need().usedMultiplier==.5,"All alternative-spec members together cover one matching repair");
            for(int n=0;n<alternate.required;n++)p.inventory.dealer.Add(Unit("coverage/second/"+n,alternate.product.definition.id,.8f));check(Need().usedMultiplier==.25,"Two complete qualifying used sets retain quarter priority");p.inventory.dealer.Clear();
            int members=installed.condition.elements.Length;foreach(var e in installed.condition.elements)e.health=.3f;
            product.stock.units=members/2;check(Need().Probability()==.2,"Partial new units cannot cover all "+members+" worn matching pistons");product.stock.units=members;check(Need().Probability()==.1,"One complete new repair covers all "+members+" worn matching pistons");product.stock.units=0;
            for(int n=0;n<members/2;n++)p.inventory.dealer.Add(Unit("coverage/matching/"+n,definition.id,.8f));check(Need().usedMultiplier==.75,"Matching used half-set is partial coverage for "+members+" worn pistons");
            for(int n=members/2;n<members;n++)p.inventory.dealer.Add(Unit("coverage/matching/"+n,definition.id,.8f));check(Need().usedMultiplier==.5,"Matching full used set covers the whole worn-piston repair");
            p.inventory.dealer.Clear();foreach(var e in installed.condition.elements)e.health=1;installed.condition.elements[0].health=.3f;
            product.stock.units=2;CartReservations.Capture(p,car.instanceId,new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=definition.id,role=role,quantity=2}});check(Need().Probability()==.2,"Reserved new units do not suppress wear priority");
            check(Need().Probability(true)==Need().Probability(),"Detached worker availability preserves held cart reservations");
            p.inventory.cart=null;product.stock.units=0;
            installed.condition.elements[0].health=.9f;installed.condition.elements[0].fault=ElementFault.Seized;check(Need().Probability()==.4&&Need().Severity==1,"Failed member has 40% base and severity one");
            check(p.parts.Contains(installed)&&installed.condition.elements[0].health==.9f,"Coverage inspection preserves owned condition and identity");
            // Exercise the actual decision path: max(Pi)=40%, not summed probabilities;
            // conditional need choice must use Pi * severity, not uniform cylinders.
            var three=new[]{PartCategory.Exhaust,PartCategory.HeadIntake,PartCategory.FuelSystem}.Select(category=>{
                var d=catalog.Find(catalog.Id(car.vehicleId,category));var row=p.inventory.market.parts.Single(s=>s.definitions.Contains(d.id));
                return new NewPartSupply.Product{definition=d,stock=row,maximum=1,owned=true};
            }).ToList();
            var needs=new[]{new SupplyWear.Need{owner=p,health=.1,usedMultiplier=1,candidates=new[]{new SupplyWear.Candidate{product=three[0],required=1}}},new SupplyWear.Need{owner=p,health=.5,usedMultiplier=1,candidates=new[]{new SupplyWear.Candidate{product=three[1],required=1}}}};
            uint selection=9181,quantity=8173;int[] selected=new int[3];const int samples=12000;
            for(int n=0;n<samples;n++){
                foreach(var candidate in three){candidate.stock.units=0;candidate.stock.discovered=false;}
                NewPartSupply.Deliver(three,needs,ref selection,ref quantity);selected[three.FindIndex(product=>product.stock.units>0)]++;
            }
            check(Math.Abs(selected[2]/(double)samples-.18)<.02,"Actual wear selection uses maximum Pi, leaving 60% for normal categories");
            check(Math.Abs(selected[0]/(double)samples-(.6*.3+.4*.36/.46))<.02&&Math.Abs(selected[1]/(double)samples-(.6*.4+.4*.1/.46))<.02,"Actual wear target distribution follows probability times severity");
            var tirePack=p.parts.First(i=>car.installed.slots.Any(s=>s.partInstanceId==i.instanceId)&&catalog.Find(i.definitionId).category==PartCategory.Tires);
            foreach(var tire in tirePack.condition.tires){tire.health=.3f;tire.consumedLife=.7;}
            foreach(var row in p.inventory.market.parts)row.units=0;
            SupplyWear.Need Tires()=>SupplyWear.Build(p,products).First(n=>n.component.StartsWith(tirePack.instanceId+"/",StringComparison.Ordinal));
            var tireProduct=products.Single(product=>product.stock.definitions.Contains(tirePack.definitionId));tireProduct.stock.units=2;
            check(Tires().Probability()==.2,"Two new tires are partial coverage for a four-tire repair");tireProduct.stock.units=0;
            for(int n=0;n<4;n++){
                var tire=new TireCondition{id="coverage/tire/"+n,definitionId=tirePack.definitionId,role="tire",cylinder=-1,valve=-1,health=.75f,consumedLife=.25};
                p.inventory.dealer.Add(new DealerStock{isUnit=true,unit=new LooseEngineElement{definitionId=tirePack.definitionId,unitPrice=10,tireUnit=true,element=tire},paidPrice=1});
                if(n==1)check(Tires().usedMultiplier==.75,"Two qualifying used tires are partial coverage for four worn corners");
            }
            check(Tires().usedMultiplier==.5,"Four qualifying used tires cover the four-corner repair");
        }
    }
}
#endif
