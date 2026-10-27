#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Concrete Editor-only core cart row characterization.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint CoreRowsSeed=20261027u;

        static void RunCoreRowMatrix(Trace trace,string subset=null)
        {
            if(subset!=null&&subset!="purchases"&&subset!="sales-stock"&&subset!="stock"&&subset!="new-sets")throw new ArgumentException("Unknown core row subset: "+subset,nameof(subset));
            bool purchases=subset==null||subset=="purchases",salesStock=subset==null||subset=="sales-stock"||subset=="stock";
            if(purchases||subset=="new-sets")CoreRowsGroup(trace,"new-sets",CoreRowsNewSets);
            if(purchases)
            {
                CoreRowsGroup(trace,"new-units",CoreRowsNewUnits);
                CoreRowsGroup(trace,"used-part-loose",CoreRowsUsedPartLoose);
                CoreRowsGroup(trace,"used-unit-loose",CoreRowsUsedUnitLoose);
                CoreRowsGroup(trace,"used-part-install",CoreRowsUsedPartInstall);
                CoreRowsGroup(trace,"used-unit-install",CoreRowsUsedUnitInstall);
            }
            if(salesStock&&subset!="stock")
            {
                CoreRowsGroup(trace,"sell-loose-part",CoreRowsSellPart);
                CoreRowsGroup(trace,"sell-loose-unit",CoreRowsSellUnit);
                CoreRowsGroup(trace,"sale-errors-duplicates-order",CoreRowsSaleChecks);
            }
            if(salesStock)CoreRowsGroup(trace,"stock",CoreRowsStock);
            trace.Add("CORE_ROWS|scope="+(subset??"all")+";status=step1-subset");
        }

        static void CoreRowsGroup(Trace trace,string name,Action<Trace> body)
        {
            try{body(trace);}
            catch(Exception error){trace.Assert(false,"Core row group completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);trace.Add("CORE_ROWS_SKIP|"+name+"|remaining groups continued");}
        }

        static void CoreRowsNewSets(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed);
            var car=p.cars.Find(x=>x.instanceId==carId);var d=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);
            if(d==null){trace.Add("CORE_OPEN|new-set|no actual compatible HeadIntake offer");return;}
            var row=new PartCartLine{action=CartAction.NewSet,definitionId=d.id,quantity=2};
            var price=CoreRowsPrice(p,d,InventoryUnits.Count(p,car,d),InventoryPricing.NewSet(d),2);
            var total=CompareTotal(trace,"core-new-set-loose-q2",p,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!total.Threw&&Convert.ToInt32(total.Value,CultureInfo.InvariantCulture)==price.Cost,"NewSet loose q2 exact sequential purchase price / "+total.Text+" expected="+price.Cost);
            var fee=CompareFee(trace,"core-new-set-loose-q2",p,carId,new[]{row});
            trace.Assert(!fee.Threw&&Convert.ToInt32(fee.Value,CultureInfo.InvariantCulture)==price.Fee,"NewSet fee equals per-quantity express fees / "+fee.Text+" expected="+price.Fee);
            ComparePreview(trace,"core-new-set-loose-q2",p,car,new[]{row});
            var applied=CartPreviewLiteralTestClone.Copy(p);int cash=applied.cash;var stock=CoreRowsStockFor(applied,d.id);int stockUnits=stock?.units??0;
            PartCart.Apply(applied,carId,new[]{row},true,ValuationPolicy.Defaults());
            var roots=applied.parts.Where(x=>x.definitionId==d.id&&!p.parts.Any(old=>old.instanceId==x.instanceId)).ToArray();
            var looseOutputCar=applied.cars.Find(x=>x.instanceId==carId);
            var looseDefinition=applied.catalog.Find(d.id);int modeledMembers=EngineElements.Count(InventoryUnits.Spec(applied,looseOutputCar,looseDefinition),looseDefinition);
            trace.Assert(roots.Length==2&&roots.All(x=>!string.IsNullOrEmpty(x.instanceId)&&x.location==PartLocation.Inventory&&string.IsNullOrEmpty(x.ownerId)&&string.IsNullOrEmpty(x.slot)&&
                x.condition!=null&&x.condition.Validate()==null&&x.condition.elements!=null&&x.condition.elements.Length==modeledMembers),"NewSet loose Apply produces two complete distinct roots / count="+roots.Length+";modeledMembers="+modeledMembers);
            trace.Assert(roots.Select(x=>x.instanceId).Distinct(StringComparer.Ordinal).Count()==2,"NewSet q2 roots have distinct nonempty actual IDs");
            trace.Assert(applied.cash==cash-price.Cost,"NewSet loose Apply cash delta equals expected / actual="+(cash-applied.cash)+" expected="+price.Cost);
            trace.Assert((CoreRowsStockFor(applied,d.id)?.units??0)==Math.Max(0,stockUnits-price.Local),"NewSet consumes local stock only");
            trace.Assert(MechanicsOwnership.Validate(applied,applied.catalog)==null,"NewSet loose Apply leaves a globally valid ownership graph");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var ip,out var iid,CoreRowsSeed+1);
            var ic=ip.cars.Find(x=>x.instanceId==iid);var id=ic==null?null:FindOffer(ip,ic,PartCategory.HeadIntake,false);
            var binding=ic?.installed.slots.FirstOrDefault(x=>x.slot==PartCategory.HeadIntake.ToString());
            var displaced=binding==null?null:ip.parts.Find(x=>x.instanceId==binding.partInstanceId);
            if(id==null||displaced==null){trace.Add("CORE_OPEN|new-set-install|selected car/market lacks actual HeadIntake offer or installed root");return;}
            string oldId=displaced.instanceId,oldCondition=TypedCanonical(displaced.condition,new Dictionary<string,string>());
            var install=new PartCartLine{action=CartAction.NewSet,definitionId=id.id,quantity=2,install=true};
            int expected=CoreRowsPrice(ip,id,InventoryUnits.Count(ip,ic,id),InventoryPricing.NewSet(id),2).Cost;
            var installTotal=CompareTotal(trace,"core-new-set-install-q2",ip,iid,new[]{install},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!installTotal.Threw&&Convert.ToInt32(installTotal.Value,CultureInfo.InvariantCulture)==expected,"Installed NewSet q2 charges complete sets / "+installTotal.Text);
            ComparePreview(trace,"core-new-set-install-q2",ip,ic,new[]{install});
            var output=CartPreviewLiteralTestClone.Copy(ip);PartCart.Apply(output,iid,new[]{install},true,ValuationPolicy.Defaults());
            var outputCar=output.cars.Find(x=>x.instanceId==iid);var newBinding=outputCar?.installed?.slots?.Find(x=>x.slot==PartCategory.HeadIntake.ToString());
            var installed=newBinding==null?null:output.parts.Find(x=>x.instanceId==newBinding.partInstanceId);var oldAfter=output.parts.Find(x=>x.instanceId==oldId);
            var spare=output.parts.Where(x=>x.definitionId==id.id&&x.instanceId!=installed?.instanceId&&!ip.parts.Any(old=>old.instanceId==x.instanceId)).ToArray();
            int installedMembers=outputCar==null?0:EngineElements.Count(InventoryUnits.Spec(output,outputCar,output.catalog.Find(id.id)),output.catalog.Find(id.id));
            trace.Assert(newBinding?.partInstanceId==installed?.instanceId&&installed!=null&&installed.instanceId!=oldId&&installed.definitionId==id.id&&installed.slot==PartCategory.HeadIntake.ToString()&&
                installed.location==PartLocation.Assembly&&installed.ownerId==outputCar?.engineAssemblyId&&installed.condition!=null&&installed.condition.Validate()==null&&
                installed.condition.elements!=null&&installed.condition.elements.Length==installedMembers,"NewSet first quantity binds the selected slot to a complete assembly-owned root / modeledMembers="+installedMembers);
            trace.Assert(oldAfter?.location==PartLocation.Inventory&&TypedCanonical(oldAfter.condition,new Dictionary<string,string>())==oldCondition,"NewSet displacement retains old root identity and exact condition");
            trace.Assert(spare.Length==1&&spare[0].location==PartLocation.Inventory&&string.IsNullOrEmpty(spare[0].ownerId)&&string.IsNullOrEmpty(spare[0].slot)&&
                spare[0].condition!=null&&spare[0].condition.Validate()==null&&spare[0].condition.elements!=null&&spare[0].condition.elements.Length==installedMembers,"NewSet install q2 leaves the complete second purchased root loose / count="+spare.Length);
            trace.Assert(output.cash==ip.cash-expected,"Installed NewSet charges both quantities");
            trace.Assert(MechanicsOwnership.Validate(output,output.catalog)==null,"NewSet installed Apply leaves a globally valid ownership graph");
        }

        static void CoreRowsNewUnits(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+2);
            var car=p.cars.Find(x=>x.instanceId==carId);
            var d=car==null?null:p.catalog.parts.Where(x=>x!=null&&x.price>=0&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),x)&&InventoryUnits.Count(p,car,x)>1&&InventoryUnits.Roles(p,car,x).Length>0&&CoreRowsStockFor(p,x.id)!=null)
                .OrderBy(x=>x.price).ThenBy(x=>x.id,StringComparer.Ordinal).FirstOrDefault();
            if(d==null){trace.Add("CORE_OPEN|new-unit-all-roles|no compatible multi-member modeled-unit definition");return;}
            var roles=InventoryUnits.Roles(p,car,d);trace.Add("CORE_ROLES|new-unit-definition="+d.id+";roles="+string.Join(",",roles));
            int ordinal=0;
            foreach(string role in roles)
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var rp,out var rcid,CoreRowsSeed+10u+(uint)ordinal);
                var rc=rp.cars.Find(x=>x.instanceId==rcid);var rd=rp.catalog.Find(d.id);
                var row=new PartCartLine{action=CartAction.NewUnits,definitionId=rd.id,role=role,quantity=2};
                var expected=CoreRowsPrice(rp,rd,1,InventoryPricing.NewUnit(rd,InventoryUnits.Count(rp,rc,rd)),2);
                var startingStock=CoreRowsStockFor(rp,rd.id);int startingUnits=startingStock?.units??0;
                var total=CompareTotal(trace,"core-new-unit-loose-"+CoreRowsSafe(role),rp,rcid,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                trace.Assert(!total.Threw&&Convert.ToInt32(total.Value,CultureInfo.InvariantCulture)==expected.Cost,"NewUnits role q2 exact price / role="+role+" actual="+total.Text+" expected="+expected.Cost);
                ComparePreview(trace,"core-new-unit-loose-"+CoreRowsSafe(role),rp,rc,new[]{row});
                var output=CartPreviewLiteralTestClone.Copy(rp);int n=output.looseEngineElements.Count;int cash=output.cash;
                PartCart.Apply(output,rcid,new[]{row},true,ValuationPolicy.Defaults());
                var added=output.looseEngineElements.Skip(n).ToArray();
                trace.Assert(added.Length==2&&added.All(x=>x.definitionId==rd.id&&x.element?.role==role),"NewUnits loose role adds two actual members / "+role+" count="+added.Length);
                trace.Assert(added.Select(x=>x.element.id).Distinct(StringComparer.Ordinal).Count()==2&&output.cash==cash-expected.Cost,"NewUnits IDs unique and cash exact / "+role);
                trace.Assert((CoreRowsStockFor(output,rd.id)?.units??0)==Math.Max(0,startingUnits-expected.Local),"NewUnits consumes only actual local units / role="+role);
                ordinal++;
            }

            CartPreviewCharacterizationFixtures.CreateNormalized(out var ip,out var iid,CoreRowsSeed+40);
            var ic=ip.cars.Find(x=>x.instanceId==iid);var target=CoreRowsInstalledUnitTarget(ip,ic);
            if(target==null){trace.Add("CORE_OPEN|new-unit-install|no real installed modeled role with addressable positions");return;}
            string[] selected=target.Positions.Length>1?new[]{target.Positions[1],target.Positions[0]}:new[]{target.Positions[0]};
            var rowInstall=new PartCartLine{action=CartAction.NewUnits,definitionId=target.Definition.id,role=target.Role,quantity=selected.Length+1,install=true,positions=selected.ToList()};
            int cost=CoreRowsPrice(ip,target.Definition,1,InventoryPricing.NewUnit(target.Definition,InventoryUnits.Count(ip,ic,target.Definition)),rowInstall.quantity).Cost;
            var installStock=CoreRowsStockFor(ip,target.Definition.id);int installStockUnits=installStock?.units??0;
            int installLocal=CoreRowsPrice(ip,target.Definition,1,InventoryPricing.NewUnit(target.Definition,InventoryUnits.Count(ip,ic,target.Definition)),rowInstall.quantity).Local;
            var totalInstall=CompareTotal(trace,"core-new-unit-install-positions",ip,iid,new[]{rowInstall},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!totalInstall.Threw&&Convert.ToInt32(totalInstall.Value,CultureInfo.InvariantCulture)==cost,"NewUnits installed row accepts actual ordered positions and charges extras / "+totalInstall.Text);
            ComparePreview(trace,"core-new-unit-install-positions",ip,ic,new[]{rowInstall});
            var outputInstall=CartPreviewLiteralTestClone.Copy(ip);var oldRoot=outputInstall.parts.Find(x=>x.instanceId==target.PartId);
            if(oldRoot?.condition?.elements==null){trace.Assert(false,"installed NewUnits target root remains available in literal Apply input");return;}
            var before=oldRoot.condition.elements.ToDictionary(x=>x.id,x=>TypedCanonical(x,new Dictionary<string,string>()),StringComparer.Ordinal);
            string[] oldIds=selected.Select(x=>target.ByPosition[x]).ToArray();
            PartCart.Apply(outputInstall,iid,new[]{rowInstall},true,ValuationPolicy.Defaults());
            var newRoot=outputInstall.parts.Find(x=>x.instanceId==target.PartId);
            var returned=outputInstall.looseEngineElements.Where(x=>oldIds.Contains(x.element?.id,StringComparer.Ordinal)).ToArray();
            bool returnedExact=returned.Length==oldIds.Length&&returned.All(x=>before[x.element.id]==TypedCanonical(x.element,new Dictionary<string,string>()));
            bool neighborExact=newRoot?.condition?.elements!=null&&newRoot.condition.elements.Count(x=>before.ContainsKey(x.id))==before.Count-oldIds.Length&&
                newRoot.condition.elements.Where(x=>before.ContainsKey(x.id)).All(x=>before[x.id]==TypedCanonical(x,new Dictionary<string,string>()));
            int created=CoreRowsCreatedUnitCount(outputInstall,ip,target.Definition.id,target.Role);
            trace.Assert(returnedExact&&neighborExact,"NewUnits preserve replaced condition and all unselected member facts / positions="+string.Join(",",selected));
            trace.Assert(created==rowInstall.quantity,"NewUnits apply maps selected positions in input order and retains purchased extras / created="+created);
            trace.Assert((CoreRowsStockFor(outputInstall,target.Definition.id)?.units??0)==Math.Max(0,installStockUnits-installLocal),"installed NewUnits consume local stock only");
        }

        sealed class CoreRowsTarget
        {public PartDefinition Definition;public string PartId,Role;public string[] Positions;public Dictionary<string,string> ByPosition;}
        static CoreRowsTarget CoreRowsInstalledUnitTarget(PlayerProfile p,OwnedCar car)
        {
            foreach(var binding in car.installed.slots)
            {
                var root=p.parts.Find(x=>x.instanceId==binding.partInstanceId);var d=root==null?null:p.catalog.Find(root.definitionId);
                if(root?.condition?.elements==null||d==null)continue;
                // This fixture exercises generic engine-member ReplacePack.
                // Tire/wheel destination transforms have separate corner cases.
                if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels)continue;
                foreach(string role in InventoryUnits.Roles(p,car,d))
                {
                    var positions=InventoryUnits.Positions(p,car,d.id,role);if(positions.Length==0)continue;
                    var map=new Dictionary<string,string>(StringComparer.Ordinal);
                    if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels)
                    {for(int i=0;i<Math.Min(4,positions.Length);i++)map[positions[i]]=root.condition.elements[i].id;}
                    else foreach(var element in root.condition.elements.Where(x=>x.role==role))map[element.id]=element.id;
                    if(positions.All(map.ContainsKey))return new CoreRowsTarget{Definition=d,PartId=root.instanceId,Role=role,Positions=positions,ByPosition=map};
                }
            }
            return null;
        }

        static void CoreRowsUsedPartLoose(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+50);uint seed=CoreRowsSeed^0x6c8e9cf5u;
            var stock=CartPreviewCharacterizationFixtures.AddUsedPart(p,"core-rows/used-part-loose",ref seed);
            if(stock?.IsPart!=true){trace.Assert(false,"actual used-part helper returns DealerStock.IsPart");return;}
            var part=stock.part;string id=part.instanceId;string facts=TypedCanonical(part.condition,new Dictionary<string,string>());
            var row=new PartCartLine{action=CartAction.BuyUsed,definitionId=stock.DefinitionId,assetId=stock.Id,quantity=1};
            int price=InventoryPricing.BuyUsed(stock,ValuationPolicy.Defaults());var total=CompareTotal(trace,"core-used-part-loose",p,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!total.Threw&&Convert.ToInt32(total.Value,CultureInfo.InvariantCulture)==price,"BuyUsed part exact dealer amount / "+total.Text);
            ComparePreview(trace,"core-used-part-loose",p,p.cars.Find(x=>x.instanceId==carId),new[]{row});
            var output=CartPreviewLiteralTestClone.Copy(p);int cash=output.cash;int marketCount=output.inventory.market.parts.Count;
            PartCart.Apply(output,carId,new[]{row},true,ValuationPolicy.Defaults());var moved=output.parts.Find(x=>x.instanceId==id);
            trace.Assert(moved!=null&&moved.storageId==SharedInventory.Garage&&TypedCanonical(moved.condition,new Dictionary<string,string>())==facts,
                "BuyUsed part preserves root/member identity and exact wear/fault/condition");
            trace.Assert(!output.inventory.dealer.Any(x=>x.Id==stock.Id)&&output.cash==cash-price,"BuyUsed part removes dealer record and charges exact price");
            trace.Assert(output.inventory.market.parts.Count==marketCount&&output.inventory.market.parts.Select((x,i)=>x.units==p.inventory.market.parts[i].units).All(x=>x),"BuyUsed part leaves every new-market stock amount unchanged");

        }

        static void CoreRowsUsedUnitLoose(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var up,out var uid,CoreRowsSeed+51);uint unitSeed=CoreRowsSeed^0x85ebca6bu;
            var unitStock=CartPreviewCharacterizationFixtures.AddUsedUnit(up,"core-rows/used-unit-loose",ref unitSeed);
            if(unitStock?.IsUnit!=true){trace.Assert(false,"actual used-unit helper returns DealerStock.IsUnit");return;}
            string unitId=unitStock.unit.element.id;string unitFacts=TypedCanonical(unitStock.unit.element,new Dictionary<string,string>());
            var unitRow=new PartCartLine{action=CartAction.BuyUsed,definitionId=unitStock.DefinitionId,assetId=unitStock.Id,role=unitStock.unit.element.role,quantity=1};
            int unitPrice=InventoryPricing.BuyUsed(unitStock,ValuationPolicy.Defaults());var unitTotal=CompareTotal(trace,"core-used-unit-loose",up,uid,new[]{unitRow},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!unitTotal.Threw&&Convert.ToInt32(unitTotal.Value,CultureInfo.InvariantCulture)==unitPrice,"BuyUsed unit exact dealer amount / "+unitTotal.Text);
            ComparePreview(trace,"core-used-unit-loose",up,up.cars.Find(x=>x.instanceId==uid),new[]{unitRow});
            var unitOutput=CartPreviewLiteralTestClone.Copy(up);PartCart.Apply(unitOutput,uid,new[]{unitRow},true,ValuationPolicy.Defaults());
            var owned=unitOutput.looseEngineElements.Find(x=>x.element.id==unitId);
            trace.Assert(owned!=null&&owned.storageId==SharedInventory.Garage&&owned.definitionId==unitStock.DefinitionId&&TypedCanonical(owned.element,new Dictionary<string,string>())==unitFacts,
                "BuyUsed unit keeps its real ID, subtype, condition and wear while moving to owned loose stock");
            trace.Assert(!unitOutput.inventory.dealer.Any(x=>x.Id==unitStock.Id),"BuyUsed unit removes dealer wrapper by real identity");

        }

        static void CoreRowsUsedPartInstall(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+52);uint seed=CoreRowsSeed^0x5f3759dfu;
            var dealer=CartPreviewCharacterizationFixtures.AddUsedPart(p,"core-rows/used-part-install",ref seed);var car=p.cars.Find(x=>x.instanceId==carId);
            if(dealer?.IsPart!=true||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),p.catalog.Find(dealer.DefinitionId)))
            {trace.Add("CORE_OPEN|used-part-install|actual helper produced no fitting used part");return;}
            var definition=p.catalog.Find(dealer.DefinitionId);var existingBinding=car.installed.slots.FirstOrDefault(x=>x.slot==definition.category.ToString());
            var previous=existingBinding==null?null:p.parts.Find(x=>x.instanceId==existingBinding.partInstanceId);
            string oldId=previous?.instanceId,oldFacts=previous==null?null:TypedCanonical(previous.condition,new Dictionary<string,string>());
            string incomingId=dealer.part.instanceId,incomingFacts=TypedCanonical(dealer.part.condition,new Dictionary<string,string>());
            var row=new PartCartLine{action=CartAction.BuyUsed,definitionId=dealer.DefinitionId,assetId=dealer.Id,quantity=1,install=true};
            var total=CompareTotal(trace,"core-used-part-install",p,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!total.Threw,"compatible used part installation quotes successfully / "+total.Text);
            ComparePreview(trace,"core-used-part-install",p,car,new[]{row});
            var output=CartPreviewLiteralTestClone.Copy(p);PartCart.Apply(output,carId,new[]{row},true,ValuationPolicy.Defaults());
            var target=output.cars.Find(x=>x.instanceId==carId);var slot=target?.installed?.slots?.Find(x=>x.slot==definition.category.ToString());
            var installed=slot==null?null:output.parts.Find(x=>x.instanceId==slot.partInstanceId);var old=oldId==null?null:output.parts.Find(x=>x.instanceId==oldId);
            trace.Assert(installed?.instanceId==incomingId&&installed.ownerId==carId&&TypedCanonical(installed.condition,new Dictionary<string,string>())==incomingFacts,
                "BuyUsed install retains incoming actual ID and condition");
            trace.Assert(oldId==null||old!=null&&old.location==PartLocation.Inventory&&TypedCanonical(old.condition,new Dictionary<string,string>())==oldFacts,
                "BuyUsed install displaces prior part without ID or condition loss / old="+(oldId??"empty slot"));
            trace.Assert(!output.inventory.dealer.Any(x=>x.Id==dealer.Id),"BuyUsed installed part removes its exact dealer record");
        }

        static void CoreRowsUsedUnitInstall(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+53);var car=p.cars.Find(x=>x.instanceId==carId);
            var target=CoreRowsInstalledUnitTarget(p,car);
            if(target==null){trace.Add("CORE_OPEN|used-unit-install|no fitted actual unit positions");return;}
            uint seed=CoreRowsSeed^0xc2b2ae35u;DealerStock found=null;
            for(int attempt=0;attempt<96&&found==null;attempt++)
            {
                uint random=unchecked(seed+(uint)attempt);if(random==0)random=1;int first=p.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(p,InventoryUnits.Spec(p,car,target.Definition),target.Definition,"core-rows/used-unit-install/"+attempt.ToString(CultureInfo.InvariantCulture),ref random);
                found=p.inventory.dealer.Skip(first).FirstOrDefault(x=>x!=null&&x.IsUnit&&x.DefinitionId==target.Definition.id&&x.unit.element.role==target.Role&&!x.unit.element.Failed);
                if(found==null)p.inventory.dealer.RemoveRange(first,p.inventory.dealer.Count-first);
            }
            if(found==null){trace.Add("CORE_OPEN|used-unit-install|no actual used row matched installed definition and role after 96 real AddUsedSupply attempts / definition="+target.Definition.id+";role="+target.Role);return;}
            string validation=MechanicsOwnership.Validate(p,p.catalog);
            if(validation!=null){trace.Add("CORE_OPEN|used-unit-install|actual appended stock failed global validation: "+validation);return;}
            string position=target.Positions[0],replacedId=target.ByPosition[position];var old= p.parts.Find(x=>x.instanceId==target.PartId).condition.elements.First(x=>x.id==replacedId);
            string oldFacts=TypedCanonical(old,new Dictionary<string,string>()),newId=found.unit.element.id;
            var expectedPlaced=found.unit.element.Copy();expectedPlaced.cylinder=old.cylinder;expectedPlaced.valve=old.valve;
            string newFacts=TypedCanonical(expectedPlaced,new Dictionary<string,string>());
            var beforeNeighbors=p.parts.Find(x=>x.instanceId==target.PartId).condition.elements.Where(x=>x.id!=replacedId)
                .ToDictionary(x=>x.id,x=>TypedCanonical(x,new Dictionary<string,string>()),StringComparer.Ordinal);
            var row=new PartCartLine{action=CartAction.BuyUsed,definitionId=found.DefinitionId,assetId=found.Id,role=target.Role,quantity=1,install=true,positions=new List<string>{position}};
            var total=CompareTotal(trace,"core-used-unit-install",p,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!total.Threw,"matching actual used unit install passes quote / "+total.Text);
            ComparePreview(trace,"core-used-unit-install",p,car,new[]{row});
            var output=CartPreviewLiteralTestClone.Copy(p);PartCart.Apply(output,carId,new[]{row},true,ValuationPolicy.Defaults());
            var root=output.parts.Find(x=>x.instanceId==target.PartId);var placed=root?.condition?.elements?.FirstOrDefault(x=>x.id==newId);
            var returned=output.looseEngineElements.Find(x=>x.element.id==replacedId);
            bool neighborsExact=root?.condition?.elements!=null&&root.condition.elements.Count(x=>beforeNeighbors.ContainsKey(x.id))==beforeNeighbors.Count&&
                root.condition.elements.Where(x=>beforeNeighbors.ContainsKey(x.id)).All(x=>beforeNeighbors[x.id]==TypedCanonical(x,new Dictionary<string,string>()));
            trace.Assert(placed!=null&&TypedCanonical(placed,new Dictionary<string,string>())==newFacts,"BuyUsed unit is installed by actual identity and destination cylinder/valve while retaining all other condition");
            trace.Assert(returned!=null&&TypedCanonical(returned.element,new Dictionary<string,string>())==oldFacts,"BuyUsed install returns displaced member with exact identity and wear/fault data");
            trace.Assert(neighborsExact,"BuyUsed install preserves every unselected installed member exactly");
            trace.Assert(!output.inventory.dealer.Any(x=>x.Id==found.Id),"BuyUsed install removes the used dealer wrapper");
        }

        static void CoreRowsSellPart(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+60);var car=p.cars.Find(x=>x.instanceId==carId);
            var d=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);
            if(d==null){trace.Add("CORE_OPEN|sell-part|no actual compatible set offer");return;}
            string id=PartOperations.Buy(p,p.catalog,carId,d.id,false);var row=new PartCartLine{action=CartAction.SellPart,definitionId=d.id,assetId=id,quantity=1};
            var policy=ValuationPolicy.Defaults();long now=CartPreviewCharacterizationFixtures.FixedNowTicks;
            int sale=Valuations.LoosePriceOracleForChecks(CartPreviewLiteralTestClone.Copy(p),policy,"wholesale",id,false,now);
            var total=CompareTotal(trace,"core-sell-loose-part",p,carId,new[]{row},policy,now);
            trace.Assert(!total.Threw&&Convert.ToInt32(total.Value,CultureInfo.InvariantCulture)==-sale,"SellPart subtracts fixed-time wholesale value / actual="+total.Text+";displayOracle="+sale);
            ComparePreview(trace,"core-sell-loose-part",p,car,new[]{row});var applied=CartPreviewLiteralTestClone.Copy(p);string before=SourceSnapshot(applied);int cash=applied.cash;
            PartCart.Apply(applied,carId,new[]{row},true,policy);
            trace.Assert(SourceSnapshot(applied)==before&&applied.cash==cash&&applied.parts.Any(x=>x.instanceId==id),"Apply skips sale and does not credit or move the part");

        }

        static void CoreRowsSellUnit(Trace trace)
        {
            var policy=ValuationPolicy.Defaults();long now=CartPreviewCharacterizationFixtures.FixedNowTicks;
            CartPreviewCharacterizationFixtures.CreateNormalized(out var up,out var uid,CoreRowsSeed+61);var uc=up.cars.Find(x=>x.instanceId==uid);
            var ud=uc==null?null:up.catalog.parts.Where(x=>x!=null&&x.price>=0&&up.catalog.Fits(up.catalog.Spec(uc.vehicleId),x)&&InventoryUnits.Roles(up,uc,x).Length>0)
                .OrderBy(x=>x.price).ThenBy(x=>x.id,StringComparer.Ordinal).FirstOrDefault();
            if(ud==null){trace.Add("CORE_OPEN|sell-unit|no compatible modeled unit definition");return;}
            string role=InventoryUnits.Roles(up,uc,ud)[0];var unit=InventoryUnits.Buy(up,uid,ud.id,role);
            var sellUnit=new PartCartLine{action=CartAction.SellUnit,definitionId=ud.id,assetId=unit.element.id,quantity=1};
            int unitSale=Valuations.LoosePriceOracleForChecks(CartPreviewLiteralTestClone.Copy(up),policy,"wholesale",unit.element.id,true,now);
            var unitTotal=CompareTotal(trace,"core-sell-loose-unit",up,uid,new[]{sellUnit},policy,now);
            trace.Assert(!unitTotal.Threw&&Convert.ToInt32(unitTotal.Value,CultureInfo.InvariantCulture)==-unitSale,"SellUnit subtracts fixed-time wholesale value / actual="+unitTotal.Text+";displayOracle="+unitSale);
            ComparePreview(trace,"core-sell-loose-unit",up,uc,new[]{sellUnit});var unitApplied=CartPreviewLiteralTestClone.Copy(up);string unitBefore=SourceSnapshot(unitApplied);int unitCash=unitApplied.cash;
            PartCart.Apply(unitApplied,uid,new[]{sellUnit},true,policy);
            trace.Assert(SourceSnapshot(unitApplied)==unitBefore&&unitApplied.cash==unitCash&&unitApplied.looseEngineElements.Any(x=>x.element.id==unit.element.id),"Apply skips sale and preserves the loose-unit identity");

        }

        static void CoreRowsSaleChecks(Trace trace)
        {
            CoreRowsSaleError(trace,"sell-installed-part",CoreRowsSeed+62,CartAction.SellPart,"installed-part");
            CoreRowsSaleError(trace,"sell-installed-unit-member",CoreRowsSeed+69,CartAction.SellUnit,"installed-unit");
            CoreRowsSaleError(trace,"sell-missing-part",CoreRowsSeed+63,CartAction.SellPart,"missing-part");
            CoreRowsSaleError(trace,"sell-wrong-unit-as-part",CoreRowsSeed+64,CartAction.SellPart,"loose-unit");
            CoreRowsSaleError(trace,"sell-missing-unit",CoreRowsSeed+65,CartAction.SellUnit,"missing-unit");
            CoreRowsSaleError(trace,"sell-wrong-part-as-unit",CoreRowsSeed+66,CartAction.SellUnit,"loose-part");
            CoreRowsDuplicateSaleMove(trace);
            CoreRowsSalePurchaseOrder(trace);
        }

        static void CoreRowsSaleError(Trace trace,string name,uint seed,CartAction action,string kind)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,seed);var car=p.cars.Find(x=>x.instanceId==carId);string id,definition;
            if(kind=="installed-part")
            {var binding=car.installed.slots.FirstOrDefault();var part=binding==null?null:p.parts.Find(x=>x.instanceId==binding.partInstanceId);if(part==null){trace.Add("CORE_OPEN|"+name+"|no installed part root");return;}id=part.instanceId;definition=part.definitionId;}
            else if(kind=="installed-unit")
            {var part=car.installed.slots.Select(slot=>p.parts.Find(x=>x.instanceId==slot.partInstanceId)).FirstOrDefault(x=>x?.condition?.elements?.Length>0);
             if(part==null){trace.Add("CORE_OPEN|"+name+"|no installed member identity");return;}id=part.condition.elements[0].id;definition=part.definitionId;}
            else if(kind=="loose-part")
            {var offer=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);if(offer==null){trace.Add("CORE_OPEN|"+name+"|no actual set offer for loose part kind-check");return;}
             id=PartOperations.Buy(p,p.catalog,carId,offer.id,false);definition=offer.id;}
            else if(kind=="loose-unit")
            {var offer=car==null?null:p.catalog.parts.Where(x=>x!=null&&x.price>=0&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),x)&&InventoryUnits.Roles(p,car,x).Length>0)
                    .OrderBy(x=>x.price).ThenBy(x=>x.id,StringComparer.Ordinal).FirstOrDefault();
             if(offer==null){trace.Add("CORE_OPEN|"+name+"|no modeled unit offer for wrong-kind check");return;}
             var unit=InventoryUnits.Buy(p,carId,offer.id,InventoryUnits.Roles(p,car,offer)[0]);id=unit.element.id;definition=unit.definitionId;}
            else
            {id="core-rows/missing/"+name;definition=car.installed.slots.FirstOrDefault()==null?"missing":p.parts.First(x=>x.instanceId==car.installed.slots[0].partInstanceId).definitionId;}
            string expected=action==CartAction.SellPart?"Quote requires an owned loose part without retained descendants.":"Quote requires an owned loose engine element without retained descendants.";
            var result=CompareTotal(trace,name,p,carId,new[]{new PartCartLine{action=action,definitionId=definition,assetId=id,quantity=1}},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,
                typeof(InvalidOperationException).FullName,expected);
            trace.Assert(result.ExceptionType==typeof(InvalidOperationException).FullName&&result.ExceptionMessage==expected,"sale asset failure wins with exact valuation message / "+name+" actual="+result.Text);
        }

        static void CoreRowsDuplicateSaleMove(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+67);var car=p.cars.Find(x=>x.instanceId==carId);
            var d=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);if(d==null){trace.Add("CORE_OPEN|duplicate-sale-move|no actual sale-part offer");return;}
            string id=PartOperations.Buy(p,p.catalog,carId,d.id,false);
            var sale=new PartCartLine{action=CartAction.SellPart,definitionId=d.id,assetId=id};
            var move=new PartCartLine{action=CartAction.Move,definitionId=d.id,assetId=id,destination=InventoryKind.Storage,destinationId=SharedInventory.Storage};
            var a=CompareTotal(trace,"core-sale-then-move-duplicate",p,carId,new[]{sale,move},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,"Duplicate move or invalid quantity.");
            trace.Assert(a.ExceptionMessage=="Duplicate move or invalid quantity.","sale then move fails at the Move branch after shared ID is consumed / "+a.Text);
            var b=CompareTotal(trace,"core-move-then-sale-duplicate",p,carId,new[]{move,sale},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,"Owned item may be used only once in a cart.");
            trace.Assert(b.ExceptionMessage=="Owned item may be used only once in a cart.","move then sale fails at sale asset duplicate guard / "+b.Text);
            var c=CompareTotal(trace,"core-sale-same-id-twice",p,carId,new[]{sale,sale.Copy()},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,"Owned item may be used only once in a cart.");
            trace.Assert(c.ExceptionMessage=="Owned item may be used only once in a cart.","second sale row with same physical ID is rejected");
        }

        static void CoreRowsSalePurchaseOrder(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+68);var car=p.cars.Find(x=>x.instanceId==carId);
            var d=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);if(d==null){trace.Add("CORE_OPEN|sale-purchase-order|no actual set offer");return;}
            string id=PartOperations.Buy(p,p.catalog,carId,d.id,false);var sale=new PartCartLine{action=CartAction.SellPart,definitionId=d.id,assetId=id};
            var buy=new PartCartLine{action=CartAction.NewSet,definitionId=d.id};int saleValue=Valuations.LoosePriceOracleForChecks(CartPreviewLiteralTestClone.Copy(p),ValuationPolicy.Defaults(),"wholesale",id,false,CartPreviewCharacterizationFixtures.FixedNowTicks);
            int cost=CoreRowsPrice(p,d,InventoryUnits.Count(p,car,d),InventoryPricing.NewSet(d),1).Cost;
            var first=CompareTotal(trace,"core-sale-before-purchase",p,carId,new[]{sale,buy},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            var second=CompareTotal(trace,"core-purchase-before-sale",p,carId,new[]{buy,sale},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!first.Threw&&!second.Threw&&Convert.ToInt32(first.Value,CultureInfo.InvariantCulture)==cost-saleValue&&Convert.ToInt32(second.Value,CultureInfo.InvariantCulture)==cost-saleValue,
                "permuting independent sale and purchase retains exact signed operands / sale="+saleValue+";purchase="+cost);
            var outA=CartPreviewLiteralTestClone.Copy(p);var outB=CartPreviewLiteralTestClone.Copy(p);int cash=p.cash;
            PartCart.Apply(outA,carId,new[]{sale,buy},true,ValuationPolicy.Defaults());PartCart.Apply(outB,carId,new[]{buy,sale},true,ValuationPolicy.Defaults());
            int rootsA=outA.parts.Count(x=>x.definitionId==d.id&&!p.parts.Any(old=>old.instanceId==x.instanceId));
            int rootsB=outB.parts.Count(x=>x.definitionId==d.id&&!p.parts.Any(old=>old.instanceId==x.instanceId));
            trace.Assert(rootsA==1&&rootsB==1&&outA.cash==cash-cost&&outB.cash==cash-cost,"Apply skips sale in both row orders and charges only purchase / roots="+rootsA+","+rootsB);
            trace.Add("CORE_SALE_PURCHASE_ORDER|sale="+saleValue+";purchase="+cost+";bothTotals="+(cost-saleValue)+";applyCash="+outA.cash+","+outB.cash+";saleMutation=checkout-only");
        }

        static void CoreRowsStock(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,CoreRowsSeed+70);var car=p.cars.Find(x=>x.instanceId==carId);
            var d=car==null?null:p.catalog.parts.Where(x=>x!=null&&x.price>=0&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),x)&&
                InventoryUnits.Count(p,car,x)>1&&InventoryUnits.Roles(p,car,x).Length>0&&CoreRowsStockFor(p,x.id)!=null)
                .OrderBy(x=>x.price).ThenBy(x=>x.id,StringComparer.Ordinal).FirstOrDefault();
            if(d==null){trace.Add("CORE_OPEN|sequential-stock|no actual stocked compatible multi-member product with individual roles");return;}
            var row=CoreRowsStockFor(p,d.id);if(row==null){trace.Add("CORE_OPEN|sequential-stock|selected definition has no direct stock row");return;}
            int count=InventoryUnits.Count(p,car,d);int authoredUnits=row.units,authoredInitial=row.initialUnits;
            // A valid controlled stock state gives the set count plus one local unit.
            // The following NewUnits row then uses one local unit and one express unit.
            row.units=count+1;row.initialUnits=count+1;
            string invalid=MechanicsOwnership.Validate(p,p.catalog);
            trace.Assert(invalid==null,"controlled stock adjustment remains a valid profile without Ensure / "+(invalid??"valid"));
            if(invalid!=null){trace.Add("CORE_OPEN|sequential-stock|controlled market quantity rejected by current global validator");return;}
            string role=InventoryUnits.Roles(p,car,d).FirstOrDefault();if(role==null){trace.Add("CORE_OPEN|sequential-stock|selected actual definition has no unit role");return;}
            int percent=row.deliveryPercent;
            var rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=d.id},new PartCartLine{action=CartAction.NewUnits,definitionId=d.id,role=role,quantity=2}};
            var set=CoreRowsPriceAt(d,count,InventoryPricing.NewSet(d),1,count+1,percent);
            var units=CoreRowsPriceAt(d,1,InventoryPricing.NewUnit(d,count),2,1,percent);
            int expectedCost=set.Cost+units.Cost,expectedFee=set.Fee+units.Fee,expectedLocal=set.Local+units.Local;
            var total=CompareTotal(trace,"core-stock-sequential-set-units",p,carId,rows,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!total.Threw&&Convert.ToInt32(total.Value,CultureInfo.InvariantCulture)==expectedCost,"sequential local/express Total matches explicit arithmetic / actual="+total.Text+";expected="+expectedCost);
            var fee=CompareFee(trace,"core-stock-sequential-set-units",p,carId,rows);
            trace.Assert(!fee.Threw&&Convert.ToInt32(fee.Value,CultureInfo.InvariantCulture)==expectedFee,"DeliveryFee is summed per-quantity ceiling / actual="+fee.Text+";expected="+expectedFee);
            var applied=CartPreviewLiteralTestClone.Copy(p);int cash=applied.cash;PartCart.Apply(applied,carId,rows,true,ValuationPolicy.Defaults());
            var after=CoreRowsStockFor(applied,d.id);
            trace.Assert(applied.cash==cash-expectedCost&&after.units==Math.Max(0,count+1-expectedLocal),"Apply decrements only local stock and pays cost / after="+after.units+";local="+expectedLocal);
            trace.Assert(set.Local==count&&set.Express==0&&units.Local==1&&units.Express==1,"controlled rows expose set local then partial unit express split");
            trace.Add("CORE_STOCK_MANUAL|definition="+d.id+";authored="+authoredUnits+"/"+authoredInitial+";controlled="+(count+1)+";percent="+percent+
                ";set="+set.Local+"local/"+set.Express+"express;units="+units.Local+"local/"+units.Express+"express;fee="+expectedFee+";cost="+expectedCost+";after="+after.units+";generation="+applied.inventory.market.generation);
        }

        sealed class CoreRowsExpected{public int Cost,Fee,Local,Express;}
        static CoreRowsExpected CoreRowsPrice(PlayerProfile p,PartDefinition d,int units,int basePrice,int quantity)
        {var stock=CoreRowsStockFor(p,d.id);return CoreRowsPriceAt(d,units,basePrice,quantity,stock?.units??0,stock?.deliveryPercent??10);}
        static CoreRowsExpected CoreRowsPriceAt(PartDefinition d,int units,int basePrice,int quantity,int available,int percent)
        {
            var answer=new CoreRowsExpected();
            for(int i=0;i<quantity;i++){int local=Math.Min(units,available),express=units-local;int fee=express==0?0:checked((int)Math.Ceiling(basePrice*(express/(double)units)*percent/100.0));answer.Local+=local;answer.Express+=express;answer.Fee=checked(answer.Fee+fee);answer.Cost=checked(answer.Cost+basePrice+fee);available-=local;}
            return answer;
        }
        static NewPartStock CoreRowsStockFor(PlayerProfile p,string id)
        {
            var market=p.inventory?.market;var definition=p.catalog?.Find(id);if(market?.parts==null||definition==null)return null;
            var direct=market.parts.FirstOrDefault(row=>row!=null&&row.definitions!=null&&row.definitions.Contains(id));
            if(direct!=null)return direct;
            string key=PartsMarket.ProductKey(definition);
            return market.parts.FirstOrDefault(row=>row!=null&&row.definitions!=null&&row.definitions.Any(otherId=>
            {var other=p.catalog.Find(otherId);return other!=null&&PartsMarket.ProductKey(other)==key;}));
        }
        static int CoreRowsCreatedUnitCount(PlayerProfile result,PlayerProfile source,string definition,string role)
        {
            var sourceIds=new HashSet<string>(source.looseEngineElements.Select(x=>x.element?.id).Where(x=>!string.IsNullOrEmpty(x)),StringComparer.Ordinal);
            foreach(var part in source.parts)foreach(var element in part.condition?.elements??Array.Empty<EngineElement>())if(!string.IsNullOrEmpty(element?.id))sourceIds.Add(element.id);
            int count=result.looseEngineElements.Count(x=>x.definitionId==definition&&x.element?.role==role&&!sourceIds.Contains(x.element.id));
            foreach(var part in result.parts.Where(x=>x.definitionId==definition))count+=part.condition?.elements?.Count(x=>x!=null&&x.role==role&&!sourceIds.Contains(x.id))??0;
            return count;
        }
        static string CoreRowsSafe(string value)=>string.IsNullOrEmpty(value)?"empty":new string(value.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
    }
}
#endif
