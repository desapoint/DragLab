#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Independent raw Apply/mutator closure frozen at S06D2B1L1C0.
    // Intentionally mutates disposable Editor fixtures only. Do not route any
    // helper here through a later shared cart/acquisition/install transition.
    internal static class LegacyC1CartApplyReference
    {
        // Frozen PartCart.cs: Apply. Keep row/quantity order and first-error order literal.
        internal static void Apply(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,bool preview=false,ValuationPolicy policy=null)
        {
            foreach(var line in lines) {
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit)continue;
                if(line.action==CartAction.Move){LegacyC1WholeEngineMoveReference.Move(p,line.assetId,line.destination,line.destinationId);continue;}
                for(int i=0;i<line.quantity;i++) {
                    if(line.action==CartAction.NewSet){BuyPart(p,p.catalog,carId,line.definitionId,line.install&&i==0);continue;}
                    LooseEngineElement unit;
                    if(line.action==CartAction.NewUnits)unit=BuyUnit(p,carId,line.definitionId,line.role);
                    else {var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);if(stock==null||!(stock.IsPart||stock.IsUnit))throw new InvalidOperationException("Used stock unavailable or invalid.");p.cash-=LegacyBuyUsed(stock,policy);p.inventory.dealer.Remove(stock);if(stock.IsPart){stock.part.storageId=SharedInventory.Garage;p.parts.Add(stock.part);if(line.install)LegacyC1WholeEngineMoveReference.Move(p,stock.part.instanceId,InventoryKind.Car,carId);continue;}unit=stock.unit;unit.storageId=SharedInventory.Garage;p.looseEngineElements.Add(unit);}
                    if(line.install&&i<line.positions.Count)InstallUnit(p,carId,unit.element.id,line.positions[i]);
                }
            }
        }

        // Frozen PartOperations.cs: Car.
        static OwnedCar BuyCar(PlayerProfile p,string id){var car=p.cars.Find(c=>c.instanceId==id)??throw new InvalidOperationException("Vehicle no longer exists.");EngineRebuilds.RequireAvailable(car);return car;}

        // Frozen PartOperations.cs: Buy. Supply uses the independently frozen L1B closure.
        static string BuyPart(PlayerProfile profile,MechanicalCatalog catalog,string vehicleId,string definitionId,bool install)
        {
            var car=BuyCar(profile,vehicleId);var part=LegacyMechanicalCatalogReference.Find(catalog, definitionId);
            if(part==null||!LegacyFitsAny(profile,part)||profile.reputation<part.reputationRequired)throw new InvalidOperationException("Part offer requires REP "+(part==null?0:part.reputationRequired)+"; no preceding rank purchase is required.");
            if(install&&!catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog, car.vehicleId),part))throw new InvalidOperationException("Incompatible purchase for selected vehicle.");
            int count=MarketCount(profile,car,part);int price=LegacyScopedSupplyReference.Quote(profile,part,count).cost;if(price<0||profile.cash<price)throw new InvalidOperationException("Insufficient cash.");
            var item=new PartInstance{instanceId=MechanicsOwnership.NewId("part"),definitionId=part.id};
            LegacyEnsureNewPart(catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog, car.vehicleId),part)?UnitSpec(profile,car,part):LegacyReference(profile,part),part,item);
            LegacyScopedSupplyReference.Consume(profile,part,count);profile.parts.Add(item);profile.cash-=price;
            if(install)LegacyC1WholePartMoveReference.ReplaceCar(profile,car,item,catalog);
            return item.instanceId;
        }

        // Frozen PartsMarket.cs: FitsAny and Reference.
        static bool LegacyFitsAny(PlayerProfile p,PartDefinition d)=>d!=null&&Models(p.catalog).Any(m=>p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, m),d));
        static VehicleSpec LegacyReference(PlayerProfile p,PartDefinition d)
        {string model=Models(p.catalog).FirstOrDefault(m=>p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, m),d));if(model==null)throw new InvalidOperationException("No supported engine or vehicle accepts this part.");return LegacyMechanicalCatalogReference.Resolve(p.catalog, model,p.catalog.Recipe(model),new Tune()).spec;}

        // Frozen PartsMarket.cs: Models and Count(PlayerProfile, OwnedCar, PartDefinition).
        static string[] Models(MechanicalCatalog c)=>c.parts.Where(d=>d.category==PartCategory.CylinderBlock).Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        static int MarketCount(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes)return 4;
            if(!EngineElements.Supported(d.category))return 1;
            if(car!=null&&p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, car.vehicleId),d))return UnitCount(p,car,d);
            return Models(p.catalog).Any(m=>p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, m),d))?MarketCount(LegacyReference(p,d),d):1;
        }
        static int MarketCount(VehicleSpec spec,PartDefinition d)
        {
            if(d.category==PartCategory.Ignition&&!CombustionHardware.Diesel(spec)){spec=spec.Copy();spec.combustion.separateSparkPlugs=true;}
            if(d.category==PartCategory.FuelSystem&&d.fuelDelivery!=FuelDeliveryHardware.Factory){spec=spec.Copy();FuelingHardware.Apply(spec,d);}
            return Math.Max(d.category==PartCategory.Tires||d.category==PartCategory.Brakes?4:1,EngineElements.Count(spec,d));
        }

        // Frozen InventoryUnits.cs: Spec, Template, Count and Roles.
        static VehicleSpec UnitSpec(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if(car==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, car.vehicleId),d))return LegacyReference(p,d);
            var map=LegacyC1WholePartMoveReference.CarDefinitions(p,car);map[d.category]=d.id;return LegacyMechanicalCatalogReference.Resolve(p.catalog, car.vehicleId,map,car.tune).spec;
        }
        static PartInstance UnitTemplate(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if((car==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, car.vehicleId),d))&&!LegacyFitsAny(p,d))return new PartInstance{instanceId="unavailable-template",definitionId=d.id};
            var spec=UnitSpec(p,car,d);var pack=new PartInstance{instanceId="offer-template",definitionId=d.id};LegacyEnsureNewPart(spec,d,pack);if(d.category==PartCategory.Tires)LegacyTireEnsure(pack.condition);return pack;
        }
        static int UnitCount(PlayerProfile p,OwnedCar car,PartDefinition d)
        {var t=UnitTemplate(p,car,d);return t.condition.elements.Length>0?t.condition.elements.Length:d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes?4:1;}
        static string[] UnitRoles(PlayerProfile p,OwnedCar car,PartDefinition d)
        {if(d.category==PartCategory.Tires)return new[]{"tire"};if(d.category==PartCategory.Wheels)return new[]{"wheel"};return UnitTemplate(p,car,d).condition.elements.Select(e=>e.role).Distinct().ToArray();}

        // Frozen InventoryUnits.cs: Buy.
        static LooseEngineElement BuyUnit(PlayerProfile p,string carId,string definition,string role)
        {
            var car=LegacyMutableCar(p,carId);var d=LegacyMechanicalCatalogReference.Find(p.catalog, definition);
            if(d==null||!LegacyFitsAny(p,d)||p.reputation<d.reputationRequired||!UnitRoles(p,car,d).Contains(role))throw new InvalidOperationException("Individual unit incompatible or unavailable.");
            int count=UnitCount(p,car,d);int price=LegacyScopedSupplyReference.Quote(p,d,count,true).cost;if(p.cash<price)throw new InvalidOperationException("Insufficient cash for unit.");
            var element=LegacyCreate(d.category);element.id=MechanicsOwnership.NewId("element");element.definitionId=d.id;element.role=role;element.cylinder=-1;element.valve=-1;
            var item=new LooseEngineElement{definitionId=d.id,unitPrice=LegacyNewUnit(d,count),element=element,tireUnit=d.category==PartCategory.Tires};
            LegacyScopedSupplyReference.Consume(p,d,count,true);p.cash-=price;p.looseEngineElements.Add(item);return item;
        }
        static int LegacyNewUnit(PartDefinition d,int count)=>Math.Max(1,checked((int)Math.Ceiling(d.price/(double)Math.Max(1,count))));
        static int LegacyBuyUsed(DealerStock stock,ValuationPolicy policy)=>checked((int)Math.Ceiling(stock.paidPrice*(1+(policy?.dealerResaleMarkup??.35)))+(policy?.dealerHandlingFee??1));

        // Frozen AssemblyOperations.cs: MutableCar.
        static OwnedCar LegacyMutableCar(PlayerProfile p,string id)
        {
            var car=p.cars.Find(c=>c.instanceId==id)??throw new InvalidOperationException("Owned car no longer exists.");EngineRebuilds.RequireAvailable(car);
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run before changing hardware.");return car;
        }
        // Frozen InventoryUnits.cs: Positions.
        static string[] UnitPositions(PlayerProfile p,OwnedCar car,string definition,string role)
        {
            var d=LegacyMechanicalCatalogReference.Find(p.catalog, definition);if(d==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, car.vehicleId),d))return new string[0];
            var pack=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).FirstOrDefault(i=>d.category==PartCategory.Tires||d.category==PartCategory.Wheels?i.slot==d.category.ToString():i.definitionId==definition);
            if(pack==null)return new string[0];if(d.category==PartCategory.Wheels)return Enumerable.Range(0,4).Select(i=>"wheel:"+i).ToArray();if(d.category==PartCategory.Tires)return Enumerable.Range(0,4).Select(i=>"tire:"+i).ToArray();return pack.condition.elements.Where(e=>e.role==role).Select(e=>e.id).ToArray();
        }
        static string UnitPositionLabel(PlayerProfile p,OwnedCar car,string definition,string position)
        {
            if(position.StartsWith("wheel:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(6))];
            if(position.StartsWith("tire:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(5))];
            var e=p.parts.SelectMany(i=>i.condition.elements).FirstOrDefault(i=>i.id==position);return e==null?"Missing position":e.cylinder<0?e.role:"Cylinder "+(e.cylinder+1)+" / "+e.role+(e.valve<0?"":" "+(e.valve+1));
        }

        // Frozen InventoryUnits.cs: Install, WheelFitments.Install/Ensure and the tire branch.
        static void InstallUnit(PlayerProfile p,string carId,string unitId,string position)
        {
            var car=LegacyMutableCar(p,carId);var unit=p.looseEngineElements.Find(u=>u.element.id==unitId)??throw new InvalidOperationException("Unit no longer owned.");
            if(LegacyMechanicalCatalogReference.Find(p.catalog, unit.definitionId)?.category==PartCategory.Wheels){InstallWheel(p,car,unit,position);return;}
            var pack=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).FirstOrDefault(i=>unit.tireUnit?i.slot==PartCategory.Tires.ToString():i.definitionId==unit.definitionId)??throw new InvalidOperationException("Individual replacement needs the same installed specification; use a complete kit to change specification.");
            var incoming=LegacyMechanicalCatalogReference.Find(p.catalog, unit.definitionId);if(incoming==null||!p.catalog.Fits(LegacyMechanicalCatalogReference.Spec(p.catalog, car.vehicleId),incoming))throw new InvalidOperationException("Individual part does not fit this car.");
            if(unit.tire==null){ReplaceEngineElement(p,carId,pack.instanceId,position,unitId);return;}
            if(!UnitPositions(p,car,unit.definitionId,"tire").Contains(position))throw new InvalidOperationException("Choose a valid tire position.");LegacyTireEnsure(pack.condition);int index=int.Parse(position.Substring(5));var old=pack.condition.tires[index];
            string oldDefinition=LegacyTireDefinitionId(pack,index);string oldId=string.IsNullOrEmpty(old.id)?MechanicsOwnership.NewId("tire"):old.id;
            p.looseEngineElements.Add(new LooseEngineElement{definitionId=oldDefinition,unitPrice=LegacyNewUnit(LegacyMechanicalCatalogReference.Find(p.catalog, oldDefinition),4),element=new EngineElement{id=oldId,role="tire",cylinder=-1,valve=-1,health=old.failed?0:Math.Min(old.health,(float)Math.Max(0,1-old.consumedLife))},tireUnit=true,tire=old.Copy()});
            var next=unit.tire.Copy();next.id=unit.element.id;next.definitionId=unit.definitionId;next.lastContactWorkJ=0;pack.condition.elements[index]=next;p.looseEngineElements.Remove(unit);LegacyTireSyncSummary(pack.condition);
        }
        static void InstallWheel(PlayerProfile p,OwnedCar car,LooseEngineElement unit,string position)
        {
            if(!EngineElements.MatchesCategory(PartCategory.Wheels,unit.element))throw new InvalidOperationException("Choose a physical wheel member, not another component type.");
            if(!UnitPositions(p,car,unit.definitionId,"wheel").Contains(position))throw new InvalidOperationException("Choose a valid wheel position.");
            var slot=car.installed.slots.Find(s=>s.slot=="Wheels");var pack=p.parts.Find(i=>i.instanceId==slot.partInstanceId);LegacyWheelEnsure(pack);int corner=int.Parse(position.Substring(6));var old=pack.condition.elements[corner];string oldDefinition=LegacyWheelDefinitionId(pack,corner);
            p.looseEngineElements.Add(new LooseEngineElement{definitionId=oldDefinition,unitPrice=LegacyNewUnit(LegacyMechanicalCatalogReference.Find(p.catalog, oldDefinition),4),element=old.Copy()});
            var next=unit.element.Copy();next.role="wheel";next.definitionId=unit.definitionId;next.cylinder=corner;next.valve=-1;pack.condition.elements[corner]=next;p.looseEngineElements.Remove(unit);
            pack.condition.health=pack.condition.elements.Min(e=>e.health);pack.condition.failed=pack.condition.elements.All(e=>e.Failed);pack.condition.failureReason=pack.condition.failed?"Wheels unavailable":"";pack.condition.serviceCount++;pack.condition.lastService="Individual wheel replaced / "+UnitPositionLabel(p,car,unit.definitionId,position);
        }
        static string LegacyWheelDefinitionId(PartInstance pack,int corner)=>string.IsNullOrEmpty(pack.condition.elements[corner].definitionId)?pack.definitionId:pack.condition.elements[corner].definitionId;
        static string LegacyTireDefinitionId(PartInstance pack,int corner)=>string.IsNullOrEmpty(pack.condition.tires[corner].definitionId)?pack.definitionId:pack.condition.tires[corner].definitionId;
        static void LegacyWheelEnsure(PartInstance pack)
        {if(pack.condition.elements!=null&&pack.condition.elements.Length>0)return;pack.condition.elements=Enumerable.Range(0,4).Select(i=>new WheelElement{id=pack.instanceId+"/wheel/"+i,role="wheel",cylinder=i,valve=-1,health=pack.condition.failed?0:pack.condition.health}).ToArray();}

        // Frozen EngineElementService.cs: Replace, Installed, ReplacePack, RefreshAggregate.
        static PartInstance LegacyInstalled(PlayerProfile p,OwnedCar car,string partId)
        {
            EngineRebuilds.RequireAvailable(car);var item=p.parts.Find(i=>i.instanceId==partId);
            if(item==null||!car.installed.slots.Any(s=>s.partInstanceId==partId)||item.condition.elements==null||item.condition.elements.Length==0)throw new InvalidOperationException("Individual part is no longer installed.");
            if(item.condition.failed)foreach(var e in item.condition.elements)e.health=0;return item;
        }
        static void ReplaceEngineElement(PlayerProfile p,string carId,string partId,string elementId,string spareId=null)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);var item=LegacyInstalled(p,car,partId);
            if(LegacyMechanicalCatalogReference.Find(p.catalog, item.definitionId).category==PartCategory.Tires)throw new InvalidOperationException("Use per-corner tire replacement instead of generic engine-element service.");
            if(LegacyMechanicalCatalogReference.Find(p.catalog, item.definitionId).category==PartCategory.Wheels){int corner=Array.FindIndex(item.condition.elements,e=>e.id==elementId);if(corner<0)throw new InvalidOperationException("Wheel position changed.");var wheel=spareId==null?BuyUnit(p,carId,LegacyWheelDefinitionId(item,corner),"wheel"):p.looseEngineElements.Find(u=>u.element.id==spareId);if(wheel==null||LegacyMechanicalCatalogReference.Find(p.catalog, wheel.definitionId)?.category!=PartCategory.Wheels)throw new InvalidOperationException("Choose an owned compatible wheel.");InstallUnit(p,carId,wheel.element.id,"wheel:"+corner);return;}
            ReplaceEnginePack(p,item,elementId,spareId);
        }
        static int LegacyEnginePrice(PartDefinition d,ComponentCondition c)=>LegacyNewUnit(d,c.elements.Length);
        static void ReplaceEnginePack(PlayerProfile p,PartInstance item,string elementId,string spareId)
        {
            var old=item.condition.elements.FirstOrDefault(e=>e.id==elementId);if(old==null)throw new InvalidOperationException("Individual component changed; refresh the quote.");var d=LegacyMechanicalCatalogReference.Find(p.catalog, item.definitionId);EngineElement replacement;
            if(spareId==null){int count=item.condition.elements.Length;int price=LegacyScopedSupplyReference.Quote(p,d,count,true).cost;if(p.cash<price)throw new InvalidOperationException("Insufficient cash for this individual component.");LegacyScopedSupplyReference.Consume(p,d,count,true);p.cash-=price;replacement=LegacyCreate(d.category);replacement.id=MechanicsOwnership.NewId("element");replacement.role=old.role;replacement.cylinder=old.cylinder;replacement.valve=old.valve;}
            else {var loose=p.looseEngineElements.Find(e=>e.element.id==spareId);var source=loose==null?null:LegacyMechanicalCatalogReference.Find(p.catalog, loose.definitionId);if(source==null||source.category!=d.category||source.fitment!=d.fitment||source.id!=d.id||!EngineElements.MatchesCategory(d.category,loose.element)||loose.element.role!=old.role||loose.element.Failed)throw new InvalidOperationException("Spare needs the same matched specification and role; mixed piston masses/profiles require a complete matched kit.");replacement=loose.element.Copy();replacement.cylinder=old.cylinder;replacement.valve=old.valve;p.looseEngineElements.Remove(loose);}
            int index=Array.IndexOf(item.condition.elements,old);p.looseEngineElements.Add(new LooseEngineElement{definitionId=item.definitionId,unitPrice=LegacyEnginePrice(d,item.condition),element=old.Copy()});item.condition.elements[index]=replacement;RefreshAggregate(item.condition);item.condition.serviceCount++;item.condition.lastService="Individual "+old.role+" replaced / cylinder "+(old.cylinder+1);
        }
        static void RefreshAggregate(ComponentCondition c)
        {if(c.elements==null||c.elements.Length==0)return;c.health=c.elements.Average(e=>e.health);c.failed=c.elements.All(e=>e.Failed);c.failureReason=c.failed?"All individual components unavailable":"";}

        // Frozen TireDynamics.cs: Ensure(ComponentCondition) and SyncSummary.
        static void LegacyTireEnsure(ComponentCondition c)
        {
            if(c.tires.Length==0){if((c.elements!=null&&c.elements.Length!=0)||c.health!=1||c.failed||!string.IsNullOrEmpty(c.failureReason)||c.runningSeconds!=0||c.distanceM!=0||c.stressExposure!=0||c.temperatureC!=20||c.slipEnergyJ!=0||c.deposits!=0||c.leakSeverity!=0||c.adjustmentError!=0||c.serviceWearRemainder!=0||c.serviceCount!=0||c.reconditionCount!=0||!string.IsNullOrEmpty(c.lastService)||c.clutch!=null)throw new InvalidOperationException("Tire pack is missing typed members and contains old state; reset required.");c.tires=new[]{new TireCondition(),new TireCondition(),new TireCondition(),new TireCondition()};}
            if(c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");LegacyTireSyncSummary(c);
        }
        static void LegacyTireSyncSummary(ComponentCondition c)
        {
            if(c.tires==null||c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");c.health=1;c.failed=false;c.failureReason="";c.temperatureC=20;c.slipEnergyJ=c.distanceM=c.runningSeconds=c.stressExposure=0;c.deposits=c.leakSeverity=c.adjustmentError=c.serviceWearRemainder=0;c.serviceCount=c.reconditionCount=0;c.lastService="";c.ClearClutch();
        }

        // Frozen EngineElements.cs: Create.
        static EngineElement LegacyCreate(PartCategory category)
        {
            switch(category)
            {
                case PartCategory.Pistons:return new PistonElement();case PartCategory.Rods:return new ConnectingRodElement();case PartCategory.PistonRings:return new PistonRingElement();case PartCategory.Bearings:return new BearingElement();case PartCategory.Valves:return new ValveElement();case PartCategory.ValveSprings:return new ValveSpringElement();case PartCategory.ValveSeals:return new ValveSealElement();case PartCategory.Followers:return new FollowerElement();case PartCategory.Wheels:return new WheelElement();case PartCategory.Ignition:return new IgnitionElement();case PartCategory.SparkPlugs:return new SparkPlugElement();case PartCategory.GlowPlugs:return new GlowPlugElement();case PartCategory.FuelSystem:return new FuelSystemElement();case PartCategory.Tires:return new TireCondition();default:return new EngineElement();
            }
        }
        // Frozen EngineElements.cs: Ensure used for a newly acquired PartInstance only.
        static void LegacyEnsureNewPart(VehicleSpec v,PartDefinition d,PartInstance p)
        {
            int count=EngineElements.Count(v,d);if(count==0)return;
            if(p.condition.elements!=null&&p.condition.elements.Length>0){if(p.condition.elements.Any(e=>!EngineElements.MatchesCategory(d.category,e)))throw new InvalidOperationException("Installed member type does not match "+d.category+". Reset incompatible save.");return;}
            p.condition.elements=new EngineElement[count];int valves=LegacyPerValve(d.category)?EngineElements.ValveCount(v):1;
            for(int i=0;i<count;i++){var element=LegacyCreate(d.category);element.id=p.instanceId+"/element/"+i;element.cylinder=i/valves;element.valve=LegacyPerValve(d.category)?i%valves:-1;element.role=LegacyRole(d.category,i%valves,valves);element.health=p.condition.failed?0:p.condition.health;p.condition.elements[i]=element;}
            if(d.category==PartCategory.Ignition)for(int i=v.combustion.separateSparkPlugs?0:v.sound.cylinders;i<count;i++){var e=p.condition.elements[i];e.role="ignition coil";e.cylinder=EngineElements.Ignition(v)==IgnitionTopology.Distributor?-1:i-(v.combustion.separateSparkPlugs?0:v.sound.cylinders);}
        }
        static bool LegacyPerValve(PartCategory c)=>c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.ValveSeals||c==PartCategory.Followers;
        static string LegacyRole(PartCategory c,int valve,int valves)=>c==PartCategory.Wheels?"wheel":c==PartCategory.GlowPlugs?"glow plug":c==PartCategory.SparkPlugs||c==PartCategory.Ignition?"spark plug / lead":c==PartCategory.FuelSystem?"injector":c==PartCategory.Bearings?"rod bearing pair":c==PartCategory.PistonRings?"piston ring pack":LegacyPerValve(c)?(valve<valves/2?"intake ":"exhaust ")+(c==PartCategory.Valves?"valve / guide":c==PartCategory.ValveSprings?"spring / retainer":c==PartCategory.ValveSeals?"valve seal":"follower / rocker"):PartSemantics.Friendly(c.ToString());
    }
}
#endif
