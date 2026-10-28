#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Input-row and quantity keyed identity checks for the legacy cart oracle.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint IdentityFixtureSeed=20261027u;

        static void RunIdentityMatrix(Trace trace)
        {
            IdentityBijectionNegativeProbes(trace);
            IdentityObservationLifecycleProbes(trace);
            IdentityGroup(trace,"mixed-new-physical-identities-forward",false);
            IdentityGroup(trace,"mixed-new-physical-identities-reversed",true);
            IdentityGroup(trace,"identical-loose-set-and-unit-quantity-three",()=>IdentityLooseQuantityThree(trace));
            IdentityGroup(trace,"installed-two-positions-two-extras",()=>IdentityInstalledExtras(trace));
            IdentityGroup(trace,"same-definition-two-set-rows-forward",()=>IdentitySameDefinitionRows(trace,false));
            IdentityGroup(trace,"same-definition-two-set-rows-reversed",()=>IdentitySameDefinitionRows(trace,true));
            IdentityGroup(trace,"same-definition-loose-set-installed-used-unit",()=>IdentitySameDefinitionUsedUnit(trace));
            IdentityGroup(trace,"tire-set-derived-member-identities",()=>IdentityTireSet(trace));
            IdentityGroup(trace,"wheel-units-corners-and-loose-extras",()=>IdentityCornerUnits(trace,PartCategory.Wheels,IdentityFixtureSeed+7));
            IdentityGroup(trace,"tire-units-corners-and-loose-extras",()=>IdentityCornerUnits(trace,PartCategory.Tires,IdentityFixtureSeed+8));
            IdentityGroup(trace,"separately-issued-tire-id",()=>IdentityMissingTireId(trace));
            trace.Add("IDENTITY_MATRIX|status=step1-subset;key=row/action/quantity/kind/role-ordinal-or-position;existing=literal;guidObservation=actual-emissions");
        }

        static void IdentityGroup(Trace trace,string name,bool reverse)
        {
            try{IdentityMixedRows(trace,name,reverse);}
            catch(Exception error)
            {
                trace.Assert(false,"Identity scenario completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                trace.Add("IDENTITY_SKIP|"+name+"|remaining identity scenarios continued");
            }
        }

        static void IdentityGroup(Trace trace,string name,Action body)
        {
            try{body();}
            catch(Exception error)
            {
                trace.Assert(false,"Identity scenario completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                trace.Add("IDENTITY_SKIP|"+name+"|remaining identity scenarios continued");
            }
        }

        static void IdentityMixedRows(Trace trace,string name,bool reverse)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed);
            var sourceCar=source.cars.Find(x=>x.instanceId==carId);
            var set=sourceCar==null?null:FindOffer(source,sourceCar,PartCategory.Pistons,false);
            if(set==null||EngineElements.Count(InventoryUnits.Spec(source,sourceCar,set),set)<2)
            {trace.Add("IDENTITY_OPEN|"+name+"|no actual stocked multi-member piston set");return;}
            var unitTarget=IdentityFindInstalledUnitTarget(source,sourceCar,PartCategory.Pistons);
            if(unitTarget==null||unitTarget.Positions.Length<2)
            {trace.Add("IDENTITY_OPEN|"+name+"|no actual installed unit target outside the set category with at least two real positions");return;}
            var used=source.inventory.dealer.FirstOrDefault(stock=>stock!=null&&stock.IsPart);
            if(used==null){trace.Add("IDENTITY_OPEN|"+name+"|normalized fixture has no actual used part row");return;}
            string usedAssetId=used.Id,usedPhysicalId=used.part.instanceId;
            string[] selected=unitTarget.Positions.Take(Math.Min(2,unitTarget.Positions.Length)).ToArray();
            var setLine=new PartCartLine{action=CartAction.NewSet,definitionId=set.id,quantity=2,install=true};
            var unitLine=new PartCartLine{action=CartAction.NewUnits,definitionId=unitTarget.Definition.id,role=unitTarget.Role,
                quantity=selected.Length+1,install=true,positions=selected.ToList()};
            var usedLine=new PartCartLine{action=CartAction.BuyUsed,definitionId=used.DefinitionId,assetId=usedAssetId,quantity=1};
            var rows=reverse?new[]{usedLine,unitLine,setLine}:new[]{setLine,unitLine,usedLine};
            IdentityPreviewCase(trace,name,source,carId,rows,usedAssetId,usedPhysicalId,false);
        }

        static void IdentityPreviewCase(Trace trace,string name,PlayerProfile source,string carId,PartCartLine[] rows,
            string usedAssetId=null,string usedPhysicalId=null,bool duplicateNewOffer=false)
        {
            var total=CompareTotal(trace,name+"/total",source,carId,rows,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,
                duplicateNewOffer?typeof(InvalidOperationException).FullName:null,duplicateNewOffer?"Duplicate new offer.":null);
            trace.Assert(duplicateNewOffer?total.Threw:!total.Threw,"Identity fixture Total acceptance matches its declared cart shape / "+name+" / "+total.Text);
            if(!TryPreparePair(trace,name,source,rows,out var pair))return;
            var random=UnityEngine.Random.state;
            RuntimeEntropy.IdentityObservation actualObservation=null,legacyObservation=null;
            PlayerProfile actualOutput=null,legacyOutput=null;
            var actual=Measure(()=>{using(var observation=RuntimeEntropy.BeginIdentityObservation())
                {actualObservation=observation;var car=pair.Actual.cars.Find(item=>item.instanceId==carId);actualOutput=PartCart.PreviewProfile(pair.Actual,car,pair.ActualRows);}return "PreviewProfile";});
            bool actualUnchanged=pair.ProfileBefore==SourceSnapshot(pair.Actual)&&pair.CatalogBefore==CatalogSnapshot(pair.Actual)&&
                pair.RowsBefore==RowsSnapshot(pair.ActualRows)&&pair.ProfileBefore==SourceSnapshot(source)&&random.Equals(UnityEngine.Random.state);
            trace.Assert(actualUnchanged,"Production PreviewProfile source/catalog/rows/random remain unchanged / "+name);
            UnityEngine.Random.state=random;
            if(pair.CatalogBefore!=CatalogSnapshot(pair.Actual))
            {trace.Add("IDENTITY_SKIP|"+name+"|oracle skipped after shared catalog mutation");return;}
            var legacy=Measure(()=>{using(var observation=RuntimeEntropy.BeginIdentityObservation())
                {legacyObservation=observation;var car=pair.Legacy.cars.Find(item=>item.instanceId==carId);legacyOutput=LegacyCartPreviewOracle.PreviewProfile(pair.Legacy,car,pair.LegacyRows);}return "PreviewProfile";});
            bool legacyUnchanged=pair.ProfileBefore==SourceSnapshot(pair.Legacy)&&pair.CatalogBefore==CatalogSnapshot(pair.Legacy)&&
                pair.RowsBefore==RowsSnapshot(pair.LegacyRows)&&pair.ProfileBefore==SourceSnapshot(source)&&random.Equals(UnityEngine.Random.state);
            trace.Assert(legacyUnchanged,"Frozen PreviewProfile source/catalog/rows/random remain unchanged / "+name);
            UnityEngine.Random.state=random;
            trace.Assert(!actual.Threw&&!legacy.Threw&&actualOutput!=null&&legacyOutput!=null,
                "Production and frozen PreviewProfile finish / "+name+" actual="+actual.Text+" oracle="+legacy.Text);
            if(actual.Threw||legacy.Threw||actualOutput==null||legacyOutput==null)return;

            var existingIds=PhysicalIds(source);
            bool leftMapped=IdentityBuildMap(trace,name+"/production",source,actualOutput,pair.ActualRows,actualObservation,usedAssetId,usedPhysicalId,existingIds,out var leftMap,out var leftNew);
            bool rightMapped=IdentityBuildMap(trace,name+"/oracle",source,legacyOutput,pair.LegacyRows,legacyObservation,usedAssetId,usedPhysicalId,existingIds,out var rightMap,out var rightNew);
            trace.Assert(leftMapped&&rightMapped,"Preview outputs have complete row-keyed identity bijections / "+name+" production="+leftMap.Count+" oracle="+rightMap.Count);
            if(!leftMapped||!rightMapped)return;
            bool sameKeys=leftMap.Keys.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(rightMap.Keys.OrderBy(x=>x,StringComparer.Ordinal),StringComparer.Ordinal);
            var leftAliases=IdentityAliases(leftMap);var rightAliases=IdentityAliases(rightMap);
            bool sameGraph=sameKeys&&TypedCanonical(actualOutput,leftAliases)==TypedCanonical(legacyOutput,rightAliases);
            trace.Assert(sameGraph,"Typed PreviewProfile outputs match after observed input-row ID bijection; existing IDs remain literal / "+name);
            trace.Add("IDENTITY_CASE|"+name+"|rows="+string.Join(",",rows.Select((row,index)=>index+":"+row.action+"/"+row.definitionId+"/q"+row.quantity+"/install="+row.install))+
                "|newKeys="+string.Join(",",leftMap.Keys.OrderBy(x=>x,StringComparer.Ordinal))+"|typedEqual="+sameGraph+
                "|usedAssetLiteral="+(string.IsNullOrEmpty(usedAssetId)||!actualOutput.inventory.dealer.Any(stock=>stock.Id==usedAssetId)));
        }

        static void IdentityLooseQuantityThree(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+1);
            var car=source.cars.Find(item=>item.instanceId==carId);var set=car==null?null:FindOffer(source,car,PartCategory.Pistons,false);
            var unit=IdentityFindInstalledUnitTarget(source,car,PartCategory.Pistons);
            if(set==null||unit==null){trace.Add("IDENTITY_OPEN|identical-loose-set-and-unit-quantity-three|authored set/unit shape unavailable");return;}
            var rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=set.id,quantity=3},
                new PartCartLine{action=CartAction.NewUnits,definitionId=unit.Definition.id,role=unit.Role,quantity=3,install=false,
                    positions=unit.Positions.Take(2).ToList()}};
            trace.Assert(!rows[1].install&&rows[1].positions.Count>0,
                "Loose NewUnits regression retains nonempty draft positions while identity keys ignore them");
            IdentityPreviewCase(trace,"identical-loose-set-and-unit-quantity-three",source,carId,rows);
        }

        static void IdentityInstalledExtras(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+2);
            var car=source.cars.Find(item=>item.instanceId==carId);var target=IdentityFindInstalledUnitTarget(source,car,PartCategory.CylinderBlock);
            if(target==null||target.Positions.Length<2){trace.Add("IDENTITY_OPEN|installed-two-positions-two-extras|authored unit lacks two fitted positions");return;}
            var selected=target.Positions.Take(2).ToList();
            var rows=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=target.Definition.id,role=target.Role,quantity=4,install=true,positions=selected}};
            IdentityPreviewCase(trace,"installed-two-positions-two-extras",source,carId,rows);
        }

        static void IdentitySameDefinitionRows(Trace trace,bool reverse)
        {
            string label=reverse?"same-definition-two-set-rows-reversed":"same-definition-two-set-rows-forward";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+3);
            var car=source.cars.Find(item=>item.instanceId==carId);var set=car==null?null:FindOffer(source,car,PartCategory.Pistons,false);
            if(set==null||EngineElements.Count(InventoryUnits.Spec(source,car,set),set)<2){trace.Add("IDENTITY_OPEN|"+label+"|authored multi-member set unavailable");return;}
            var loose=new PartCartLine{action=CartAction.NewSet,definitionId=set.id,quantity=2,install=false};
            var installed=new PartCartLine{action=CartAction.NewSet,definitionId=set.id,quantity=3,install=true};
            var rows=reverse?new[]{installed,loose}:new[]{loose,installed};
            IdentityPreviewCase(trace,label,source,carId,rows,duplicateNewOffer:true);
        }

        static DealerStock IdentityAddUsedUnit(PlayerProfile source,OwnedCar car,PartDefinition definition)
        {
            uint seed=IdentityFixtureSeed^0x9e3779b9u;
            for(int attempt=0;attempt<128;attempt++)
            {
                uint random=unchecked(seed+(uint)attempt);if(random==0)random=1;int first=source.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(source,InventoryUnits.Spec(source,car,definition),definition,
                    "identity/same-definition-used-unit/"+attempt.ToString(System.Globalization.CultureInfo.InvariantCulture),ref random);
                var row=source.inventory.dealer.Skip(first).FirstOrDefault(stock=>stock!=null&&stock.IsUnit&&stock.DefinitionId==definition.id&&
                    stock.unit?.element!=null&&!stock.unit.element.Failed&&InventoryUnits.Positions(source,car,definition.id,stock.unit.element.role).Length>0);
                if(row!=null)return row;
                source.inventory.dealer.RemoveRange(first,source.inventory.dealer.Count-first);
            }
            return null;
        }

        static void IdentitySameDefinitionUsedUnit(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+4);
            var car=source.cars.Find(item=>item.instanceId==carId);var selectedOffer=car==null?null:FindOffer(source,car,PartCategory.Pistons,false);
            if(selectedOffer==null){trace.Add("IDENTITY_OPEN|same-definition-loose-set-installed-used-unit|authored piston offer unavailable");return;}
            int cashBefore=source.cash;string stockBefore=StockShape(source,selectedOffer);int dealerBefore=source.inventory.dealer.Count;
            string installedPartId=PartOperations.Buy(source,source.catalog,carId,selectedOffer.id,true);
            car=source.cars.Find(item=>item.instanceId==carId);
            var installed=source.parts.FirstOrDefault(part=>part?.instanceId==installedPartId);
            string actualInstalledId=car?.installed?.slots?.FirstOrDefault(binding=>binding.slot==PartCategory.Pistons.ToString())?.partInstanceId;
            string stockAfterBuy=StockShape(source,selectedOffer);
            if(installed==null||installed.definitionId!=selectedOffer.id||actualInstalledId!=installedPartId)
            {trace.Add("IDENTITY_OPEN|same-definition-loose-set-installed-used-unit|real Buy did not install selected offered piston definition");return;}
            var used=IdentityAddUsedUnit(source,car,selectedOffer);
            if(used==null){trace.Add("IDENTITY_OPEN|same-definition-loose-set-installed-used-unit|bounded actual used supply did not yield a matching unit");return;}
            string[] positions=InventoryUnits.Positions(source,car,selectedOffer.id,used.unit.element.role);
            string selectedPosition=positions.FirstOrDefault();
            string stockAfterUsed=StockShape(source,selectedOffer);
            trace.Add("IDENTITY_SETUP|same-definition-loose-set-installed-used-unit|offer="+selectedOffer.id+
                ";stock="+stockBefore+"->"+stockAfterBuy+"->"+stockAfterUsed+";cash="+cashBefore+"->"+source.cash+
                ";dealerRows="+dealerBefore+"->"+source.inventory.dealer.Count+";installedPartId="+installedPartId+
                ";installedDefinition="+installed.definitionId+";installedSelection="+actualInstalledId+
                ";actualPositions="+string.Join(",",positions)+";usedDealerId="+used.Id+";usedPhysicalId="+used.unit.element.id+
                ";usedRole="+used.unit.element.role+";selectedPosition="+(selectedPosition??"<none>"));
            if(string.IsNullOrEmpty(selectedPosition)){trace.Add("IDENTITY_OPEN|same-definition-loose-set-installed-used-unit|real installed piston has no position for actual used role");return;}
            var rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=selectedOffer.id,quantity=3},
                new PartCartLine{action=CartAction.BuyUsed,definitionId=selectedOffer.id,assetId=used.Id,role=used.unit.element.role,quantity=1,install=true,positions=new List<string>{selectedPosition}}};
            IdentityPreviewCase(trace,"same-definition-loose-set-installed-used-unit",source,carId,rows,used.Id,used.unit.element.id);
        }

        static void IdentityTireSet(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+5);
            var car=source.cars.Find(item=>item.instanceId==carId);var tires=car==null?null:FindOffer(source,car,PartCategory.Tires,false);
            if(tires==null){trace.Add("IDENTITY_OPEN|tire-set-derived-member-identities|no compatible tire offer row");return;}
            var rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=tires.id,quantity=2}};
            IdentityPreviewCase(trace,"tire-set-derived-member-identities",source,carId,rows);
        }

        static void IdentityCornerUnits(Trace trace,PartCategory category,uint seed)
        {
            string label=(category==PartCategory.Wheels?"wheel":"tire")+"-units-corners-and-loose-extras";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,seed);
            var car=source.cars.Find(item=>item.instanceId==carId);
            var target=IdentityFindCornerUnitTarget(source,car,category);
            if(target==null||target.Positions.Length<2)
            {trace.Add("IDENTITY_OPEN|"+label+"|no actual installed four-corner compatible pack");return;}
            var rows=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=target.Definition.id,role=target.Role,
                quantity=4,install=true,positions=target.Positions.Take(2).ToList()}};
            IdentityPreviewCase(trace,label,source,carId,rows);
        }

        static CoreRowsTarget IdentityFindCornerUnitTarget(PlayerProfile profile,OwnedCar car,PartCategory category)
        {
            if(car?.installed?.slots==null||(category!=PartCategory.Wheels&&category!=PartCategory.Tires))return null;
            var binding=car.installed.slots.FirstOrDefault(item=>item.slot==category.ToString());
            var pack=binding==null?null:profile.parts.FirstOrDefault(item=>item?.instanceId==binding.partInstanceId);
            var definition=pack==null?null:profile.catalog.Find(pack.definitionId);
            string role=category==PartCategory.Wheels?"wheel":"tire";
            var positions=definition==null?Array.Empty<string>():InventoryUnits.Positions(profile,car,definition.id,role);
            if(definition==null||positions.Length<2)return null;
            return new CoreRowsTarget{Definition=definition,PartId=pack.instanceId,Role=role,Positions=positions,
                ByPosition=positions.ToDictionary(position=>position,position=>position,StringComparer.Ordinal)};
        }

        static void IdentityMissingTireId(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,IdentityFixtureSeed+6);
            var car=source.cars.Find(item=>item.instanceId==carId);var definition=car==null?null:FindOffer(source,car,PartCategory.Tires,false);
            if(definition==null){trace.Add("IDENTITY_OPEN|separately-issued-tire-id|no actual stocked compatible tire definition");return;}
            DealerStock used=null;uint seed=IdentityFixtureSeed^0x7f4a7c15u;
            for(int attempt=0;attempt<128&&used==null;attempt++)
            {
                uint random=unchecked(seed+(uint)attempt);if(random==0)random=1;int first=source.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(source,InventoryUnits.Spec(source,car,definition),definition,
                    "identity/used-tire/"+attempt.ToString(System.Globalization.CultureInfo.InvariantCulture),ref random);
                used=source.inventory.dealer.Skip(first).FirstOrDefault(stock=>stock!=null&&stock.IsUnit&&stock.DefinitionId==definition.id&&stock.unit?.tire!=null);
                if(used==null)source.inventory.dealer.RemoveRange(first,source.inventory.dealer.Count-first);
            }
            if(used==null){trace.Add("IDENTITY_OPEN|separately-issued-tire-id|bounded actual used supply did not produce a tire unit");return;}
            var installed=car.installed.slots.Select(binding=>source.parts.FirstOrDefault(part=>part?.instanceId==binding.partInstanceId))
                .FirstOrDefault(part=>part!=null&&part.slot==PartCategory.Tires.ToString());
            if(installed?.condition?.tires==null||installed.condition.tires.Length!=4){trace.Add("IDENTITY_OPEN|separately-issued-tire-id|selected car lacks an actual four-corner tire pack");return;}
            installed.condition.tires[0].id=null;
            string position="tire:0",assetId=used.Id,usedPhysicalId=used.unit.element.id;
            var rows=new[]{new PartCartLine{action=CartAction.BuyUsed,definitionId=definition.id,assetId=assetId,role="tire",quantity=1,install=true,positions=new List<string>{position}}};
            if(!TryPreparePair(trace,"separately-issued-tire-id",source,rows,out var pair))return;
            var randomState=UnityEngine.Random.state;RuntimeEntropy.IdentityObservation actualObservation=null,legacyObservation=null;
            var actual=Measure(()=>{using(var observation=RuntimeEntropy.BeginIdentityObservation())
                {actualObservation=observation;PartCart.Apply(pair.Actual,carId,pair.ActualRows,true,ValuationPolicy.Defaults());return "applied";}});
            bool actualRandom=randomState.Equals(UnityEngine.Random.state);UnityEngine.Random.state=randomState;
            var legacy=Measure(()=>{using(var observation=RuntimeEntropy.BeginIdentityObservation())
                {legacyObservation=observation;LegacyCartPreviewOracle.Apply(pair.Legacy,carId,pair.LegacyRows,true,ValuationPolicy.Defaults());return "applied";}});
            bool legacyRandom=randomState.Equals(UnityEngine.Random.state);UnityEngine.Random.state=randomState;
            trace.Assert(!actual.Threw&&!legacy.Threw&&actualRandom&&legacyRandom,
                "Malformed missing tire-ID replacement reaches the same actual Apply path in both copies / actual="+actual.Text+";oracle="+legacy.Text);
            if(actual.Threw||legacy.Threw)return;
            var key=IdentityKey(0,CartAction.BuyUsed,0,"displaced-tire","tire",0,position);
            bool left=IdentityTireBirthMap(trace,"separately-issued-tire-id/production",source,pair.Actual,actualObservation,key,assetId,usedPhysicalId,out var leftMap,out var leftIds);
            bool right=IdentityTireBirthMap(trace,"separately-issued-tire-id/oracle",source,pair.Legacy,legacyObservation,key,assetId,usedPhysicalId,out var rightMap,out var rightIds);
            bool same=left&&right&&TypedCanonical(pair.Actual,IdentityAliases(leftMap))==TypedCanonical(pair.Legacy,IdentityAliases(rightMap));
            trace.Assert(same,"Separately allocated displaced-tire GUID is row/position keyed and typed output matches / existing incoming tire ID remains literal");
            trace.Assert(pair.ProfileBefore==SourceSnapshot(source)&&pair.CatalogBefore==CatalogSnapshot(source)&&
                pair.RowsBefore==RowsSnapshot(pair.ActualRows)&&pair.RowsBefore==RowsSnapshot(pair.LegacyRows),
                "Tire identity Apply leaves the malformed source fixture, shared catalog and input rows untouched");
        }

        static bool IdentityTireBirthMap(Trace trace,string label,PlayerProfile source,PlayerProfile output,
            RuntimeEntropy.IdentityObservation observation,string key,string assetId,string usedPhysicalId,
            out Dictionary<string,string> map,out HashSet<string> generated)
        {
            map=new Dictionary<string,string>(StringComparer.Ordinal);var existing=PhysicalIds(source);
            var eventsValid=observation!=null&&observation.Closed&&!observation.Overflowed&&observation.Count==1&&observation.EventAt(0).Scope=="tire";
            if(eventsValid)IdentityAdd(map,key,"tire-"+observation.EventAt(0).Value.ToString("N"));
            var all=IdentityPhysicalIds(output);var newIds=all.Where(id=>!existing.Contains(id)).ToArray();
            generated=new HashSet<string>(newIds.Where(id=>!string.IsNullOrEmpty(id)),StringComparer.Ordinal);
            var displaced=map.TryGetValue(key,out string id)&&output.looseEngineElements.Any(unit=>unit?.element?.id==id&&unit.tireUnit);
            var incoming=output.parts.SelectMany(part=>part?.condition?.tires??Array.Empty<TireCondition>()).Any(tire=>tire?.id==usedPhysicalId);
            bool dealerRemoved=!output.inventory.dealer.Any(stock=>stock!=null&&stock.Id==assetId);
            bool identityValid=IdentityValidateBijection(new HashSet<string>(new[]{key},StringComparer.Ordinal),existing,newIds,map,out string reason);
            bool valid=eventsValid&&displaced&&incoming&&dealerRemoved&&!newIds.Any(string.IsNullOrEmpty)&&identityValid;
            trace.Assert(valid,"Separately allocated tire emission is complete, unique, input-keyed and present in the displaced physical record / "+label+
                " observed="+(observation?.Count??-1)+";generated="+generated.Count+";reason="+(eventsValid?reason:"unexpected-scope/count/overflow"));
            return valid;
        }

        static bool IdentityBuildMap(Trace trace,string label,PlayerProfile source,PlayerProfile output,PartCartLine[] rows,
            RuntimeEntropy.IdentityObservation observation,string usedAssetId,string usedPhysicalId,HashSet<string> existingIds,
            out Dictionary<string,string> map,out HashSet<string> generatedIds)
        {
            map=new Dictionary<string,string>(StringComparer.Ordinal);
            var rawPhysicalIds=IdentityPhysicalIds(output);
            var resultNewIds=rawPhysicalIds.Where(id=>!existingIds.Contains(id)).ToArray();
            generatedIds=new HashSet<string>(resultNewIds.Where(id=>!string.IsNullOrEmpty(id)),StringComparer.Ordinal);
            bool ok=IdentityCaptureEvents(trace,label,source,rows,observation,map);
            var outputCar=output.cars.Find(car=>car.instanceId==source.SelectedCar.instanceId);
            foreach(var pair in rows.Select((line,index)=>new{line,index}))
            {
                var line=pair.line;if(line==null)continue;
                if(line.action==CartAction.NewSet)
                {
                    var definition=source.catalog.Find(line.definitionId);
                    var expected=InventoryUnits.Template(source,source.cars.Find(car=>car.instanceId==outputCar?.instanceId),definition).condition?.elements??Array.Empty<EngineElement>();
                    for(int quantity=0;quantity<line.quantity;quantity++)
                    {
                        string rootKey=IdentityKey(pair.index,line.action,quantity,"part-root",null,-1,null);
                        if(!map.TryGetValue(rootKey,out string rootId)){ok=false;continue;}
                        var root=output.parts.FirstOrDefault(part=>part?.instanceId==rootId);
                        if(root==null||root.definitionId!=line.definitionId){ok=false;continue;}
                        var members=root.condition?.elements;
                        if(members==null||members.Length!=expected.Length){ok=false;continue;}
                        for(int ordinal=0;ordinal<expected.Length;ordinal++)
                        {
                            string role=expected[ordinal]?.role??"<null>";
                            if(members[ordinal]==null||members[ordinal].role!=expected[ordinal]?.role||
                                !EngineElements.MatchesCategory(definition.category,members[ordinal])){ok=false;continue;}
                            string memberKey=IdentityMemberKey(pair.index,line,quantity,definition.category,role,ordinal);
                            if(definition.category==PartCategory.Tires)
                            {
                                string mappedMember;
                                ok&=map.TryGetValue(memberKey,out mappedMember)&&mappedMember==members[ordinal].id;
                            }
                            else
                            {
                                string derivedId=rootId+"/element/"+ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                ok&=IdentityAdd(map,memberKey,derivedId)&&members[ordinal].id==derivedId;
                            }
                        }
                    }
                }
                else if(line.action==CartAction.NewUnits)
                {
                    var packId=unitTargetPartId(source,source.cars.Find(car=>car.instanceId==outputCar?.instanceId),line.definitionId,line.role);
                    var beforePack=source.parts.FirstOrDefault(part=>part?.instanceId==packId);
                    var afterPack=beforePack==null?null:output.parts.FirstOrDefault(part=>part?.instanceId==beforePack.instanceId);
                    var category=source.catalog.Find(line.definitionId)?.category??PartCategory.Body;
                    var positions=IdentityEffectivePositions(line);
                    for(int quantity=0;quantity<line.quantity;quantity++)
                    {
                        string position=quantity<positions.Count?positions[quantity]:null;
                        string kind=position==null?"loose-unit":"engine-member";
                        string key=IdentityKey(pair.index,line.action,quantity,kind,line.role,quantity,position);
                        if(!map.TryGetValue(key,out string id)){ok=false;continue;}
                        if(position!=null)
                        {
                            if(category==PartCategory.Wheels||category==PartCategory.Tires)
                            {
                                int corner=IdentityCornerIndex(position,category);
                                var old=corner<0?null:beforePack?.condition?.elements?.ElementAtOrDefault(corner);
                                var placed=corner<0?null:afterPack?.condition?.elements?.ElementAtOrDefault(corner);
                                int expectedCylinder=category==PartCategory.Tires?-1:corner;
                                bool cornerValid=old!=null&&placed!=null&&placed.id==id&&placed.role==line.role&&placed.definitionId==line.definitionId&&
                                    placed.cylinder==expectedCylinder&&placed.valve==-1&&EngineElements.MatchesCategory(category,placed);
                                if(category==PartCategory.Tires)cornerValid&=placed is TireCondition tire&&tire.lastContactWorkJ==0;
                                trace.Assert(cornerValid,"Installed corner member retains actual destination metadata / "+label+
                                    " category="+category+";position="+position+";expectedCylinder="+expectedCylinder+";actualCylinder="+(placed?.cylinder??int.MinValue)+
                                    ";expectedValve=-1;actualValve="+(placed?.valve??int.MinValue)+";corner="+corner);
                                ok&=cornerValid;
                            }
                            else
                            {
                                var old=beforePack?.condition?.elements?.FirstOrDefault(element=>element?.id==position);
                                var placed=afterPack?.condition?.elements?.FirstOrDefault(element=>element?.id==id);
                                if(old==null||placed==null||placed.role!=line.role||placed.cylinder!=old.cylinder||placed.valve!=old.valve)ok=false;
                            }
                        }
                        else
                        {
                            var loose=output.looseEngineElements.FirstOrDefault(unit=>unit?.element?.id==id);
                            if(loose==null||loose.definitionId!=line.definitionId||loose.element.role!=line.role)ok=false;
                            if(category==PartCategory.Tires&&(loose?.tire==null||!loose.tireUnit||loose.tire.lastContactWorkJ!=0))ok=false;
                            if(category==PartCategory.Wheels&&!(loose?.element is WheelElement))ok=false;
                        }
                    }
                }
            }

            if(!string.IsNullOrEmpty(usedAssetId))
            {
                var usedSource=source.inventory.dealer.FirstOrDefault(stock=>stock!=null&&stock.Id==usedAssetId);
                bool retained=false;
                if(usedSource?.part!=null)
                {
                    var usedOutput=output.parts.FirstOrDefault(part=>part?.instanceId==usedPhysicalId);
                    retained=usedOutput!=null&&TypedCanonical(usedSource.part.condition,new Dictionary<string,string>())==
                        TypedCanonical(usedOutput.condition,new Dictionary<string,string>());
                }
                else if(usedSource?.unit?.element!=null)
                    retained=output.looseEngineElements.Any(unit=>unit?.element?.id==usedPhysicalId)||
                        output.parts.Any(part=>part?.condition?.elements?.Any(member=>member?.id==usedPhysicalId)==true);
                ok&=retained&&!output.inventory.dealer.Any(stock=>stock!=null&&stock.Id==usedAssetId)&&
                    rows.Any(line=>line?.action==CartAction.BuyUsed&&line.assetId==usedAssetId);
            }
            if(resultNewIds.Any(string.IsNullOrEmpty))ok=false;
            var expectedKeys=IdentityExpectedKeys(source,rows);
            ok&=IdentityValidateBijection(expectedKeys,existingIds,resultNewIds,map,out string reason);
            trace.Assert(ok,"Observed birth IDs map to input rows/quantities and every descendant/member; existing used-part ID stays literal / "+label+
                " emissions="+(observation?.Count??-1)+";map="+map.Count+";generated="+generatedIds.Count+";reason="+reason);
            return ok;
        }

        sealed class IdentityExpectedEmission
        {public string Key,Scope;}

        static bool IdentityCaptureEvents(Trace trace,string label,PlayerProfile source,PartCartLine[] rows,RuntimeEntropy.IdentityObservation observation,
            Dictionary<string,string> map)
        {
            var expected=IdentityExpectedEmissions(source,rows);bool valid=observation!=null&&observation.Closed&&!observation.Overflowed&&observation.Count==expected.Count;
            if(observation==null||observation.Overflowed||!observation.Closed)
                trace.Assert(false,"Identity observer completed without overflow / "+label+" absent="+(observation==null)+";closed="+(observation?.Closed??false)+";overflow="+(observation?.Overflowed??false));
            if(observation==null)return false;
            if(observation.Count!=expected.Count)valid=false;
            int count=Math.Min(observation.Count,expected.Count);
            var observedScopes=new List<string>();
            for(int i=0;i<count;i++)
            {
                var emission=observation.EventAt(i);var expectedEvent=expected[i];
                if(emission.Scope!=expectedEvent.Scope)valid=false;
                observedScopes.Add(emission.Scope??"<null>");
                string id=emission.Scope+"-"+emission.Value.ToString("N");
                if(!IdentityAdd(map,expectedEvent.Key,id))valid=false;
            }
            trace.Assert(valid,"Actual GUID emissions exactly match known physical row/quantity births in execution order / "+label+
                " expectedScopes="+string.Join(",",expected.Select(item=>item.Scope))+";observedScopes="+string.Join(",",observedScopes)+
                ";expected="+expected.Count+";observed="+observation.Count);
            return valid;
        }

        static List<IdentityExpectedEmission> IdentityExpectedEmissions(PlayerProfile source,PartCartLine[] rows)
        {
            var expected=new List<IdentityExpectedEmission>();
            for(int row=0;row<rows.Length;row++)
            {
                var line=rows[row];if(line==null)continue;
                if(line.action==CartAction.NewSet)
                    for(int quantity=0;quantity<line.quantity;quantity++)
                        expected.Add(new IdentityExpectedEmission{Key=IdentityKey(row,line.action,quantity,"part-root",null,-1,null),Scope="part"});
                else if(line.action==CartAction.NewUnits)
                {
                    var positions=IdentityEffectivePositions(line);
                    for(int quantity=0;quantity<line.quantity;quantity++)
                    {
                        string position=quantity<positions.Count?positions[quantity]:null;
                        string kind=position==null?"loose-unit":"engine-member";
                        expected.Add(new IdentityExpectedEmission{Key=IdentityKey(row,line.action,quantity,kind,line.role,quantity,position),Scope="element"});
                    }
                }
            }
            // MechanicsOwnership.Ensure allocates a real tire ID for each fresh
            // NewSet member after row actions have completed; these are emitted,
            // not parent-derived IDs, and follow profile part insertion order.
            foreach(var pair in rows.Select((line,index)=>new{line,index}))
            {
                var line=pair.line;
                if(line?.action!=CartAction.NewSet||source?.catalog?.Find(line.definitionId)?.category!=PartCategory.Tires)continue;
                for(int quantity=0;quantity<line.quantity;quantity++)
                    for(int corner=0;corner<4;corner++)
                        expected.Add(new IdentityExpectedEmission{Key=IdentityMemberKey(pair.index,line,quantity,PartCategory.Tires,"tire",corner),Scope="tire"});
            }
            return expected;
        }

        static List<string> IdentityEffectivePositions(PartCartLine line)
        {return line!=null&&line.install&&line.positions!=null?line.positions:new List<string>();}

        static HashSet<string> IdentityExpectedKeys(PlayerProfile source,PartCartLine[] rows)
        {
            var expected=new HashSet<string>(IdentityExpectedEmissions(source,rows).Select(item=>item.Key),StringComparer.Ordinal);
            var car=source.SelectedCar;
            for(int row=0;row<rows.Length;row++)
            {
                var line=rows[row];if(line?.action!=CartAction.NewSet)continue;
                var definition=source.catalog.Find(line.definitionId);var template=InventoryUnits.Template(source,car,definition);
                var members=template.condition?.elements??Array.Empty<EngineElement>();
                var category=definition?.category??PartCategory.Body;
                for(int quantity=0;quantity<line.quantity;quantity++)
                    for(int ordinal=0;ordinal<members.Length;ordinal++)
                        expected.Add(IdentityMemberKey(row,line,quantity,category,members[ordinal]?.role??"<null>",ordinal));
            }
            return expected;
        }

        static string unitTargetPartId(PlayerProfile profile,OwnedCar car,string definitionId,string role)
        {
            if(car==null)return null;
            var category=profile.catalog.Find(definitionId)?.category;
            foreach(var binding in car.installed.slots)
            {
                var part=profile.parts.FirstOrDefault(item=>item?.instanceId==binding.partInstanceId);
                if(category==PartCategory.Wheels||category==PartCategory.Tires)
                {if(binding.slot==category.ToString()&&part!=null)return part.instanceId;}
                else if(part?.definitionId==definitionId&&part.condition?.elements?.Any(element=>element?.role==role)==true)return part.instanceId;
            }
            return null;
        }

        static string IdentityMemberKey(int row,PartCartLine line,int quantity,PartCategory category,string role,int ordinal)
        {
            if(category==PartCategory.Tires)
                return IdentityKey(row,line.action,quantity,"tire-member",role,ordinal,"tire:"+ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return IdentityKey(row,line.action,quantity,"member",role,ordinal,null);
        }

        static int IdentityCornerIndex(string position,PartCategory category)
        {
            string prefix=category==PartCategory.Wheels?"wheel:":"tire:";
            if(string.IsNullOrEmpty(position)||!position.StartsWith(prefix,StringComparison.Ordinal)||
                !int.TryParse(position.Substring(prefix.Length),out int corner)||corner<0||corner>3)return -1;
            return corner;
        }

        static CoreRowsTarget IdentityFindInstalledUnitTarget(PlayerProfile profile,OwnedCar car,PartCategory excludedCategory)
        {
            if(car?.installed?.slots==null)return null;
            foreach(var binding in car.installed.slots)
            {
                var part=profile.parts.FirstOrDefault(item=>item?.instanceId==binding.partInstanceId);var definition=part==null?null:profile.catalog.Find(part.definitionId);
                if(definition==null||definition.category==excludedCategory||part.condition?.elements==null)continue;
                foreach(string role in InventoryUnits.Roles(profile,car,definition))
                {
                    var positions=InventoryUnits.Positions(profile,car,definition.id,role);
                if(positions.Length<2)continue;
                    return new CoreRowsTarget{Definition=definition,PartId=part.instanceId,Role=role,Positions=positions,
                        ByPosition=positions.ToDictionary(position=>position,position=>position,StringComparer.Ordinal)};
                }
            }
            return null;
        }

        static Dictionary<string,string> IdentityAliases(Dictionary<string,string> mapping)
        {var aliases=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var entry in mapping)aliases.Add(entry.Value,"<new:"+entry.Key+">");return aliases;}

        static List<string> IdentityPhysicalIds(PlayerProfile profile)
        {
            var ids=new List<string>();
            if(profile.parts!=null)foreach(var part in profile.parts)if(part!=null)
            {ids.Add(part.instanceId);foreach(var member in part.condition?.elements??Array.Empty<EngineElement>())if(member!=null)ids.Add(member.id);}
            if(profile.looseEngineElements!=null)foreach(var unit in profile.looseEngineElements)if(unit!=null)ids.Add(unit.element?.id);
            if(profile.engines!=null)foreach(var engine in profile.engines)if(engine!=null)ids.Add(engine.instanceId);
            if(profile.inventory?.dealer!=null)foreach(var stock in profile.inventory.dealer)if(stock!=null)
            {
                ids.Add(stock.Id);
                if(stock.part!=null){ids.Add(stock.part.instanceId);foreach(var member in stock.part.condition?.elements??Array.Empty<EngineElement>())if(member!=null)ids.Add(member.id);}
                if(stock.unit!=null)ids.Add(stock.unit.element?.id);
            }
            return ids;
        }

        static string IdentityKey(int row,CartAction action,int quantity,string kind,string role,int ordinal,string position)
        {
            role=role??string.Empty;position=position??string.Empty;
            return "row="+row.ToString(System.Globalization.CultureInfo.InvariantCulture)+";action="+action+
                ";quantity="+quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)+";kind="+kind+
                ";role="+role.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+role+
                ";ordinal="+ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)+
                ";position="+position.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+position;
        }

        static bool IdentityAdd(Dictionary<string,string> map,string key,string id)
        {if(string.IsNullOrEmpty(id)||map.ContainsKey(key))return false;map.Add(key,id);return true;}

        static bool IdentityValidateBijection(HashSet<string> expectedKeys,HashSet<string> existingIds,IEnumerable<string> resultNewIds,
            Dictionary<string,string> map,out string reason)
        {
            if(map==null||!expectedKeys.SetEquals(map.Keys)){reason="missing-or-extra-key";return false;}
            var values=map.Values.ToArray();
            if(values.Any(string.IsNullOrEmpty)){reason="empty-id";return false;}
            if(values.Distinct(StringComparer.Ordinal).Count()!=values.Length){reason="aliased-id";return false;}
            if(values.Any(existingIds.Contains)){reason="existing-id-collision";return false;}
            var actualNew=resultNewIds.ToArray();
            if(actualNew.Any(string.IsNullOrEmpty)){reason="empty-result-id";return false;}
            if(actualNew.Distinct(StringComparer.Ordinal).Count()!=actualNew.Length){reason="duplicate-result-id";return false;}
            if(values.Length!=actualNew.Length||!new HashSet<string>(values,StringComparer.Ordinal).SetEquals(actualNew))
            {reason="missing-or-extra-generated-record";return false;}
            reason="complete-bijection";return true;
        }

        static void IdentityBijectionNegativeProbes(Trace trace)
        {
            var keys=new HashSet<string>(new[]{"row0/root","row1/member"},StringComparer.Ordinal);
            var existing=new HashSet<string>(new[]{"old-id"},StringComparer.Ordinal);
            var generated=new HashSet<string>(new[]{"new-a","new-b"},StringComparer.Ordinal);
            var good=new Dictionary<string,string>{{"row0/root","new-a"},{"row1/member","new-b"}};
            trace.Assert(IdentityValidateBijection(keys,existing,generated,good,out _),"Identity mapper accepts a complete two-record bijection");
            var missing=new Dictionary<string,string>{{"row0/root","new-a"}};
            trace.Assert(!IdentityValidateBijection(keys,existing,generated,missing,out _),"Identity mapper rejects a missing expected key");
            var alias=new Dictionary<string,string>{{"row0/root","new-a"},{"row1/member","new-a"}};
            trace.Assert(!IdentityValidateBijection(keys,existing,generated,alias,out _),"Identity mapper rejects aliases");
            var collision=new Dictionary<string,string>{{"row0/root","new-a"},{"row1/member","old-id"}};
            trace.Assert(!IdentityValidateBijection(keys,existing,generated,collision,out _),"Identity mapper rejects collision with an existing physical ID");
            var extra=new HashSet<string>(generated,StringComparer.Ordinal){"new-extra"};
            trace.Assert(!IdentityValidateBijection(keys,existing,extra,good,out _),"Identity mapper rejects an unkeyed extra generated record");
            var missingOutput=new HashSet<string>(new[]{"new-a"},StringComparer.Ordinal);
            trace.Assert(!IdentityValidateBijection(keys,existing,missingOutput,good,out _),"Identity mapper rejects a missing generated record");
        }

        static void IdentityObservationLifecycleProbes(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,IdentityFixtureSeed);
            var car=profile.cars.Find(item=>item.instanceId==carId);var offer=car==null?null:IdentityFindSetOffer(profile,car);
            if(offer==null){trace.Add("IDENTITY_OPEN|observer-lifecycle|no actual set offer for domain emission probes");return;}

            var successRows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=offer.id,quantity=1}};
            RuntimeEntropy.IdentityObservation completed;
            using(completed=RuntimeEntropy.BeginIdentityObservation())PartCart.Apply(profile,carId,successRows,true,ValuationPolicy.Defaults());
            string purchased=profile.parts.Last(part=>part.definitionId==offer.id).instanceId;
            trace.Assert(completed.Closed&&!completed.Overflowed&&completed.Count==1&&completed.EventAt(0).Scope=="part"&&
                purchased=="part-"+completed.EventAt(0).Value.ToString("N"),"Observer records the actual domain-issued part ID and remains readable after close");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var nestedProfile,out var nestedCarId,IdentityFixtureSeed);
            var nestedCar=nestedProfile.cars.Find(item=>item.instanceId==nestedCarId);var nestedOffer=IdentityFindSetOffer(nestedProfile,nestedCar);
            if(nestedOffer==null){trace.Add("IDENTITY_OPEN|observer-nested|no actual set offer");return;}
            bool nestedRejected=false;RuntimeEntropy.IdentityObservation outer;
            using(outer=RuntimeEntropy.BeginIdentityObservation())
            {
                try{RuntimeEntropy.BeginIdentityObservation();}
                catch(InvalidOperationException){nestedRejected=true;}
                PartCart.Apply(nestedProfile,nestedCarId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=nestedOffer.id,quantity=1}},true,ValuationPolicy.Defaults());
            }
            trace.Assert(nestedRejected&&outer.Closed&&outer.Count==1&&outer.EventAt(0).Scope=="part",
                "Nested observer rejection leaves the owner lease active for actual domain IDs");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var foreignProfile,out var foreignCarId,IdentityFixtureSeed);
            var foreignCar=foreignProfile.cars.Find(item=>item.instanceId==foreignCarId);var foreignOffer=IdentityFindSetOffer(foreignProfile,foreignCar);
            if(foreignOffer==null){trace.Add("IDENTITY_OPEN|observer-foreign-dispose|no actual set offer");return;}
            var foreign=RuntimeEntropy.BeginIdentityObservation();Exception foreignError=null;
            var worker=new Thread(()=>{try{foreign.Dispose();}catch(Exception error){foreignError=error;}});
            worker.Start();worker.Join();bool remainedOpen=!foreign.Closed;
            PartCart.Apply(foreignProfile,foreignCarId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=foreignOffer.id,quantity=1}},true,ValuationPolicy.Defaults());
            foreign.Dispose();
            trace.Assert(foreignError is InvalidOperationException&&remainedOpen&&foreign.Closed&&foreign.Count==1&&foreign.EventAt(0).Scope=="part",
                "Foreign-thread disposal is rejected before lease mutation and owner can continue capture");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var overflowProfile,out var overflowCarId,IdentityFixtureSeed);
            var overflowCar=overflowProfile.cars.Find(item=>item.instanceId==overflowCarId);var overflowOffer=IdentityFindSetOffer(overflowProfile,overflowCar);
            if(overflowOffer==null){trace.Add("IDENTITY_OPEN|observer-overflow|no actual set offer");return;}
            int overflowBefore=overflowProfile.parts.Count(part=>part.definitionId==overflowOffer.id);
            RuntimeEntropy.IdentityObservation overflow;
            using(overflow=RuntimeEntropy.BeginIdentityObservation(1))
                PartCart.Apply(overflowProfile,overflowCarId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=overflowOffer.id,quantity=2}},true,ValuationPolicy.Defaults());
            int overflowRoots=overflowProfile.parts.Count(part=>part.definitionId==overflowOffer.id)-overflowBefore;
            trace.Assert(overflow.Closed&&overflow.Overflowed&&overflow.Count==1&&overflowRoots==2,
                "Overflow flags and drops diagnostics without interrupting real quantity-two Apply");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var errorProfile,out var errorCarId,IdentityFixtureSeed);
            var errorCar=errorProfile.cars.Find(item=>item.instanceId==errorCarId);var errorOffer=IdentityFindSetOffer(errorProfile,errorCar);
            if(errorOffer==null){trace.Add("IDENTITY_OPEN|observer-error-cleanup|no actual set offer");return;}
            RuntimeEntropy.IdentityObservation errorLease;bool applyThrew=false;
            using(errorLease=RuntimeEntropy.BeginIdentityObservation())
            {
                try{PartCart.Apply(errorProfile,errorCarId,new[]{
                    new PartCartLine{action=CartAction.NewSet,definitionId=errorOffer.id,quantity=1},
                    new PartCartLine{action=CartAction.NewSet,definitionId="missing-identity-probe-definition",quantity=1}},true,ValuationPolicy.Defaults());}
                catch(InvalidOperationException){applyThrew=true;}
            }
            bool leaseClosedOnError=errorLease.Closed&&errorLease.Count==1&&!errorLease.Overflowed;
            var reopened=RuntimeEntropy.BeginIdentityObservation();reopened.Dispose();
            trace.Assert(applyThrew&&leaseClosedOnError&&reopened.Closed,
                "Exceptional real Apply closes its observation and permits a later lease");
        }

        static PartDefinition IdentityFindSetOffer(PlayerProfile profile,OwnedCar car)
        {
            if(profile?.catalog?.parts==null||car==null)return null;
            foreach(var category in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>())
            {var offer=FindOffer(profile,car,category,false);if(offer!=null)return offer;}
            return null;
        }
    }
}
#endif
