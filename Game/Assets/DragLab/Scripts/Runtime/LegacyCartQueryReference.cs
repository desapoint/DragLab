#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>
    /// L1D diagnostic copy of legacy cart query control flow. All changed dependencies must route through independent frozen references.
    /// This file is editor-only and must never become a player fallback.
    /// Keep the original control flow and helper call order intact.
    /// </summary>
    internal static class LegacyCartQueryReference
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void RecordRuntimeJsonCall()=>PartCart.RecordRuntimeJsonCall();

        internal static bool Active(PlayerProfile p) => LegacyScopedSupplyReference.Active(p);
        internal static bool SameCart(PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)
            => LegacyCartCanonicalBoundaryReference.SameCart(p,carId,rows);
        internal static bool Matches(PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)
            => Active(p)&&SameCart(p,carId,rows);
        internal static void ClearReservation(PlayerProfile p) => CartReservations.Clear(p);
        static string[] QueryModels(MechanicalCatalog catalog)=>catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock).Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        static bool QueryFitsAny(PlayerProfile p,PartDefinition d)=>d!=null&&QueryModels(p.catalog).Any(model=>p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,model),d));
        static VehicleSpec QueryReference(PlayerProfile p,PartDefinition d)
        {string model=QueryModels(p.catalog).FirstOrDefault(m=>p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,m),d));if(model==null)throw new InvalidOperationException("No supported engine or vehicle accepts this part.");return LegacyMechanicalCatalogReference.Resolve(p.catalog,model,p.catalog.Recipe(model),new Tune()).spec;}
        static VehicleSpec QueryUnitSpec(PlayerProfile p,OwnedCar car,PartDefinition d)
        {if(car==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,car.vehicleId),d))return QueryReference(p,d);var map=LegacyC1WholePartMoveReference.CarDefinitions(p,car);map[d.category]=d.id;return LegacyMechanicalCatalogReference.Resolve(p.catalog,car.vehicleId,map,car.tune).spec;}
        static PartInstance QueryUnitTemplate(PlayerProfile p,OwnedCar car,PartDefinition d)
        {if((car==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,car.vehicleId),d))&&!QueryFitsAny(p,d))return new PartInstance{instanceId="unavailable-template",definitionId=d.id};var spec=QueryUnitSpec(p,car,d);var pack=new PartInstance{instanceId="offer-template",definitionId=d.id};LegacyEnsureNewPart(spec,d,pack);if(d.category==PartCategory.Tires)LegacyTireEnsure(pack.condition);return pack;}
        static int QueryUnitCount(PlayerProfile p,OwnedCar car,PartDefinition d)
        {var template=QueryUnitTemplate(p,car,d);return template.condition.elements.Length>0?template.condition.elements.Length:d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes?4:1;}
        static string[] QueryUnitRoles(PlayerProfile p,OwnedCar car,PartDefinition d)
        {if(d.category==PartCategory.Tires)return new[]{"tire"};if(d.category==PartCategory.Wheels)return new[]{"wheel"};return QueryUnitTemplate(p,car,d).condition.elements.Select(e=>e.role).Distinct().ToArray();}
        static string[] QueryUnitPositions(PlayerProfile p,OwnedCar car,string definition,string role)
        {var d=LegacyMechanicalCatalogReference.Find(p.catalog,definition);if(d==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,car.vehicleId),d))return new string[0];var pack=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).FirstOrDefault(i=>d.category==PartCategory.Tires||d.category==PartCategory.Wheels?i.slot==d.category.ToString():i.definitionId==definition);if(pack==null)return new string[0];if(d.category==PartCategory.Wheels)return Enumerable.Range(0,4).Select(i=>"wheel:"+i).ToArray();if(d.category==PartCategory.Tires)return Enumerable.Range(0,4).Select(i=>"tire:"+i).ToArray();return pack.condition.elements.Where(e=>e.role==role).Select(e=>e.id).ToArray();}
        static void LegacyTireEnsure(ComponentCondition c)
        {if(c.tires.Length==0){if((c.elements!=null&&c.elements.Length!=0)||c.health!=1||c.failed||!string.IsNullOrEmpty(c.failureReason)||c.runningSeconds!=0||c.distanceM!=0||c.stressExposure!=0||c.temperatureC!=20||c.slipEnergyJ!=0||c.deposits!=0||c.leakSeverity!=0||c.adjustmentError!=0||c.serviceWearRemainder!=0||c.serviceCount!=0||c.reconditionCount!=0||!string.IsNullOrEmpty(c.lastService)||c.clutch!=null)throw new InvalidOperationException("Tire pack is missing typed members and contains old state; reset required.");c.tires=new[]{new TireCondition(),new TireCondition(),new TireCondition(),new TireCondition()};}if(c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");LegacyTireSyncSummary(c);}
        static void LegacyTireSyncSummary(ComponentCondition c)
        {if(c.tires==null||c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");c.health=1;c.failed=false;c.failureReason="";c.temperatureC=20;c.slipEnergyJ=c.distanceM=c.runningSeconds=c.stressExposure=0;c.deposits=c.leakSeverity=c.adjustmentError=c.serviceWearRemainder=0;c.serviceCount=c.reconditionCount=0;c.lastService="";c.ClearClutch();}
        static EngineElement LegacyCreate(PartCategory category)
        {switch(category){case PartCategory.Pistons:return new PistonElement();case PartCategory.Rods:return new ConnectingRodElement();case PartCategory.PistonRings:return new PistonRingElement();case PartCategory.Bearings:return new BearingElement();case PartCategory.Valves:return new ValveElement();case PartCategory.ValveSprings:return new ValveSpringElement();case PartCategory.ValveSeals:return new ValveSealElement();case PartCategory.Followers:return new FollowerElement();case PartCategory.Wheels:return new WheelElement();case PartCategory.Ignition:return new IgnitionElement();case PartCategory.SparkPlugs:return new SparkPlugElement();case PartCategory.GlowPlugs:return new GlowPlugElement();case PartCategory.FuelSystem:return new FuelSystemElement();case PartCategory.Tires:return new TireCondition();default:return new EngineElement();}}
        static void LegacyEnsureNewPart(VehicleSpec v,PartDefinition d,PartInstance p)
        {int count=EngineElements.Count(v,d);if(count==0)return;if(p.condition.elements!=null&&p.condition.elements.Length>0){if(p.condition.elements.Any(e=>!EngineElements.MatchesCategory(d.category,e)))throw new InvalidOperationException("Installed member type does not match "+d.category+". Reset incompatible save.");return;}p.condition.elements=new EngineElement[count];int valves=LegacyPerValve(d.category)?EngineElements.ValveCount(v):1;for(int i=0;i<count;i++){var element=LegacyCreate(d.category);element.id=p.instanceId+"/element/"+i;element.cylinder=i/valves;element.valve=LegacyPerValve(d.category)?i%valves:-1;element.role=LegacyRole(d.category,i%valves,valves);element.health=p.condition.failed?0:p.condition.health;p.condition.elements[i]=element;}if(d.category==PartCategory.Ignition)for(int i=v.combustion.separateSparkPlugs?0:v.sound.cylinders;i<count;i++){var e=p.condition.elements[i];e.role="ignition coil";e.cylinder=EngineElements.Ignition(v)==IgnitionTopology.Distributor?-1:i-(v.combustion.separateSparkPlugs?0:v.sound.cylinders);}}
        static bool LegacyPerValve(PartCategory c)=>c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.ValveSeals||c==PartCategory.Followers;
        static string LegacyRole(PartCategory c,int valve,int valves)=>c==PartCategory.Wheels?"wheel":c==PartCategory.GlowPlugs?"glow plug":c==PartCategory.SparkPlugs||c==PartCategory.Ignition?"spark plug / lead":c==PartCategory.FuelSystem?"injector":c==PartCategory.Bearings?"rod bearing pair":c==PartCategory.PistonRings?"piston ring pack":LegacyPerValve(c)?(valve<valves/2?"intake ":"exhaust ")+(c==PartCategory.Valves?"valve / guide":c==PartCategory.ValveSprings?"spring / retainer":c==PartCategory.ValveSeals?"valve seal":"follower / rocker"):PartSemantics.Friendly(c.ToString());

        public static int Total(PlayerProfile p,string carId,IEnumerable<PartCartLine> source,ValuationPolicy policy=null,long now=0,bool validateBlocks=true,bool includeOwnerReservation=false)
            =>TotalWithClock(p,carId,source,policy,now,validateBlocks,includeOwnerReservation,null);

        internal static int TotalWithClock(PlayerProfile p,string carId,IEnumerable<PartCartLine> source,ValuationPolicy policy,long now,bool validateBlocks,bool includeOwnerReservation,Func<long> readDefaultTime)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);
            var lines=source.ToArray();if(lines.Length==0||lines.Length>100)throw new InvalidOperationException("Cart needs 1–100 distinct offers.");
            policy=policy??ValuationPolicy.Defaults();if(now==0)now=readDefaultTime==null?DragLab.Domain.RuntimeEntropy.UtcNow.Ticks:readDefaultTime();
            var ids=new HashSet<string>();var assets=new HashSet<string>();var positions=new HashSet<string>();var slots=new HashSet<PartCategory>();int total=0;PlayerProfile movePreview=null;
            Func<string,bool,int> salePrices=null;
            // An additive draft may reuse its owner's hold before Capture publishes the new rows.
            // Checkout still requires the exact captured cart and never opts into this preview path.
            var supply=new LegacyScopedSupplyReference.Ledger(p,Matches(p,carId,lines)||includeOwnerReservation&&p.inventory.cart?.carId==carId);
            var moveSlots=new HashSet<string>();
            foreach(var line in lines) {
                var d=line==null?null:LegacyMechanicalCatalogReference.Find(p.catalog,line.definitionId);
                if(line==null||!Enum.IsDefined(typeof(CartAction),line.action)||line.quantity<1||line.quantity>99)throw new InvalidOperationException("Invalid cart action or quantity.");
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Owned item may be used only once in a cart.");
                    if(salePrices==null)salePrices=LegacyCartQueryValuationReference.LoosePriceView(p,policy,"wholesale",now);
                    total=checked(total-salePrices(line.assetId,line.action==CartAction.SellUnit));continue;
                }
                if(line.action==CartAction.Move) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Duplicate move or invalid quantity.");
                    if(movePreview==null){RecordRuntimeJsonCall();RecordRuntimeJsonCall();movePreview=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(movePreview,p.catalog);}LegacyC1WholeEngineMoveReference.Move(movePreview,line.assetId,line.destination,line.destinationId);
                    var moved=p.parts.Find(i=>i.instanceId==line.assetId);if(moved!=null&&line.destination==InventoryKind.Car&&line.destinationId==carId&&!slots.Add(LegacyMechanicalCatalogReference.Find(p.catalog,moved.definitionId).category))throw new InvalidOperationException("Conflicting slot installations.");
                    if(moved!=null&&line.destination!=InventoryKind.Storage&&!moveSlots.Add(line.destination+"/"+line.destinationId+"/"+LegacyMechanicalCatalogReference.Find(p.catalog,moved.definitionId).category))throw new InvalidOperationException("Conflicting destination slot installations.");
                    continue;
                }
                if(d==null||d.price<0||line.install&&!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog,car.vehicleId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Unavailable/incompatible cart offer or reputation.");
                int price=InventoryPricing.NewSet(d);bool unit=line.action==CartAction.NewUnits;
                if(line.action==CartAction.BuyUsed) {
                    var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);
                    if(stock==null||!(stock.IsPart||stock.IsUnit)||stock.DefinitionId!=d.id||line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Used stock unavailable or used twice.");
                    unit=stock.IsUnit;if(unit&&line.role!=stock.unit.element.role)throw new InvalidOperationException("Used unit role changed.");price=InventoryPricing.BuyUsed(stock,policy);
                } else if(!ids.Add(line.action+"/"+d.id+"/"+line.role))throw new InvalidOperationException("Duplicate new offer.");
                if(unit) {
                    if(!QueryUnitRoles(p,car,d).Contains(line.role))throw new InvalidOperationException("This offer has no individually modeled units of that type.");
                    if(line.action==CartAction.NewUnits)price=InventoryPricing.NewUnit(d,QueryUnitCount(p,car,d));
                    if(line.install){if(line.positions==null||line.positions.Count==0||line.positions.Count>line.quantity||line.positions.Any(s=>!QueryUnitPositions(p,car,d.id,line.role).Contains(s)||!positions.Add(s)))throw new InvalidOperationException("Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");}
                } else if(line.install&&!slots.Add(d.category))throw new InvalidOperationException("Only one cart offer can install in each slot. Other offers may be bought as spares.");
                total=checked(total+(line.action==CartAction.BuyUsed?checked(price*line.quantity):supply.Take(d,unit?1:QueryUnitCount(p,car,d),price,line.quantity).cost));
            }
            foreach(var category in slots)if(lines.Any(l=>l.install&&(l.action==CartAction.NewUnits||l.action==CartAction.BuyUsed&&p.inventory.dealer.Any(s=>s.Id==l.assetId&&s.IsUnit))&&LegacyMechanicalCatalogReference.Find(p.catalog,l.definitionId).category==category))throw new InvalidOperationException("Choose a complete set or individual replacements for a slot, not both.");
            if(validateBlocks){
                var blocks=lines.Where(l=>LegacyMechanicalCatalogReference.Find(p.catalog,l.definitionId)?.category==PartCategory.CylinderBlock&&(l.install||l.action==CartAction.Move&&l.destination==InventoryKind.Car)).Where(l=>{var target=p.cars.Find(c=>c.instanceId==(l.action==CartAction.Move?l.destinationId:carId));return target==null||!LegacyC1WholePartMoveReference.CarDefinitions(p,target).TryGetValue(PartCategory.CylinderBlock,out var old)||old!=l.definitionId;}).ToArray();
                if(blocks.Length>0){if(blocks.Any(l=>!l.preparedBlock))throw new InvalidOperationException("Prepare the block and its required components before installation.");var preview=PreviewProfile(p,car,lines);foreach(var targetId in blocks.Select(l=>l.action==CartAction.Move?l.destinationId:carId).Distinct()){var target=preview.cars.Find(c=>c.instanceId==targetId);var result=LegacyResolve(preview,target,preview.catalog);if(!result.Drivable)throw new InvalidOperationException("Block preparation is incomplete: "+string.Join("; ",result.errors));}}
            }
            return total;
        }

        public static int DeliveryFee(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);var ledger=new LegacyScopedSupplyReference.Ledger(p,Matches(p,carId,lines));int fee=0;
            foreach(var row in lines.Where(l=>l.action==CartAction.NewSet||l.action==CartAction.NewUnits)){var d=LegacyMechanicalCatalogReference.Find(p.catalog,row.definitionId);int count=QueryUnitCount(p,car,d);bool unit=row.action==CartAction.NewUnits;fee=checked(fee+ledger.Take(d,unit?1:count,unit?InventoryPricing.NewUnit(d,count):d.price,row.quantity).fee);}return fee;
        }

        public static void Apply(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,bool preview=false,ValuationPolicy policy=null)
            =>LegacyC1CartApplyReference.Apply(p,carId,lines,preview,policy);

        public static PlayerProfile PreviewProfile(PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> lines)
        {
            RecordRuntimeJsonCall();RecordRuntimeJsonCall();var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(copy,p.catalog);copy.cash=int.MaxValue/2;
            var captured=lines.ToArray();bool ownsReservation=SameCart(copy,car.instanceId,captured);copy.inventory.usingCartReservation=Matches(copy,car.instanceId,captured);try{Apply(copy,car.instanceId,captured,true);}finally{copy.inventory.usingCartReservation=false;}if(ownsReservation)ClearReservation(copy);MechanicsOwnership.Ensure(copy,p.catalog);return copy;
        }

        public static InspectionBuild Preview(PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> lines)
        {
            var copy=PreviewProfile(p,car,lines);
            return LegacyCapture(copy,copy.cars.Find(c=>c.instanceId==car.instanceId));
        }

        static InspectionBuild LegacyCapture(PlayerProfile profile,OwnedCar car)
        {
            Dictionary<PartCategory,string> map;var components=new List<PartInstance>(car.installed.slots.Count);
            using(var installed=InstalledPartLookup.Capture(profile.parts,car.installed.slots))
            {
                map=installed.CopyDefinitions();
                for(int i=0;i<installed.Count;i++){var item=installed.EntryAt(i).Part;if(item!=null)components.Add(item.Copy());}
            }
            var resolved=LegacyMechanicalCatalogReference.Resolve(profile.catalog,car.vehicleId,map,car.tune);
            TireFitments.Capture(resolved.spec,components,profile.catalog);
            if(string.IsNullOrEmpty(car.engineAssemblyId))resolved.errors.Add("No engine attached: stored engines retain their own hardware and liquids.");
            foreach(var item in components)
            {
                var category=LegacyMechanicalCatalogReference.Find(profile.catalog,item.definitionId).category;
                if(category==PartCategory.Tires&&item.condition.tires!=null&&item.condition.tires.Length==4)
                {
                    for(int i=0;i<4;i++)if(item.condition.tires[i].Failed)resolved.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[i]+" tire failed");
                    continue;
                }
                if((PartSemantics.Wears(category)||category==PartCategory.Controls&&resolved.spec.engineBehavior.fueling==FuelSystem.Injection)&&(item.condition.failed||item.condition.health<=0))
                    (MechanicalCatalog.DisablingEngineFailure(category)||category==PartCategory.Controls&&resolved.spec.engineBehavior.fueling==FuelSystem.Injection?resolved.errors:resolved.warnings).Add("Failed "+item.slot+": "+item.condition.failureReason);
            }
            return new InspectionBuild{build=resolved,components=components,startingFluids=car.fluids.Copy(),startingFuel=new FuelState{litres=car.fuel.litres}};
        }

        static BuildResolution LegacyResolve(PlayerProfile profile,OwnedCar car,MechanicalCatalog catalog)
        {
            using(var installed=InstalledPartLookup.Capture(profile.parts,car.installed.slots))
            {
                var result=LegacyMechanicalCatalogReference.Resolve(catalog,car.vehicleId,installed.CopyDefinitions(),car.tune);
                var components=Enumerable.Range(0,installed.Count).Select(i=>installed.EntryAt(i).Part).Where(item=>item!=null).ToArray();
                TireFitments.Capture(result.spec,components,catalog);
                if(string.IsNullOrEmpty(car.engineAssemblyId))result.errors.Add("No engine attached. Attach a stored engine in Assemblies.");
                if(car.engineRebuild!=null)result.errors.Add("Engine is reserved at the rebuild shop. Collect it after the quoted time.");
                if(!string.IsNullOrEmpty(car.garageJobId))result.errors.Add("Car and installed components are reserved by a garage job. Claim or cancel to release.");
                for(int i=0;i<installed.Count;i++)
                {
                    var entry=installed.EntryAt(i);var item=entry.Part;
                    if(item!=null&&LegacyMechanicalCatalogReference.Find(catalog,item.definitionId).category==PartCategory.Tires&&item.condition.tires!=null&&item.condition.tires.Length==4)
                    {
                        for(int corner=0;corner<4;corner++)if(item.condition.tires[corner].Failed)result.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[corner]+" tire failed");
                        continue;
                    }
                    if(item!=null)
                    {
                        var category=LegacyMechanicalCatalogReference.Find(catalog,item.definitionId).category;
                        if((PartSemantics.Wears(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection)&&(item.condition.failed||item.condition.health<=0))
                            (MechanicalCatalog.DisablingEngineFailure(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection?result.errors:result.warnings).Add(entry.Slot+" failed: "+item.condition.failureReason);
                    }
                }
                return result;
            }
        }
    }
}
#endif
