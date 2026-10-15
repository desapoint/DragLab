using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public static class ServiceOperations
    {
        // One eligibility rule drives both the UI quote and the actual paid repair.
        public static double BoreHealth(PlayerProfile p,OwnedCar car)
        {
            var slot=car?.installed?.slots?.Find(s=>s.slot==PartCategory.CylinderBlock.ToString());
            var part=slot==null?null:p.parts.Find(i=>i.instanceId==slot.partInstanceId);
            return part==null?0:part.condition.health;
        }
        public static PartDefinition OilFilter(PlayerProfile p,OwnedCar car)
        {
            if(car==null)throw new InvalidOperationException("Service vehicle no longer exists.");
            var slot=car.installed.slots.Find(s=>s.slot==PartCategory.OilFilter.ToString());
            var current=slot==null?null:p.parts.Find(i=>i.instanceId==slot.partInstanceId);
            var id=current?.definitionId??p.catalog.Id(car.vehicleId,PartCategory.OilFilter);
            var filter=p.catalog.Find(id);
            if(filter==null||filter.category!=PartCategory.OilFilter)throw new InvalidOperationException("Oil filter specification is unavailable.");
            return filter;
        }
        public static IEnumerable<ServiceAction> Available(PartDefinition p,ComponentCondition c,double boreHealth=1)
        {
            if(p==null||c==null)yield break;
            if(!c.failed&&(p.category==PartCategory.FuelSystem||p.category==PartCategory.Ignition||p.category==PartCategory.HeadIntake)&&c.deposits>0)yield return ServiceAction.Clean;
            if(!c.failed&&(p.category==PartCategory.Followers||p.category==PartCategory.TimingDrive)&&c.adjustmentError>0)yield return ServiceAction.Adjust;
            if((p.category==PartCategory.Cooling||p.category==PartCategory.CoolingHoses||p.category==PartCategory.OilPan||p.category==PartCategory.OilCooler||p.category==PartCategory.Induction)&&c.leakSeverity>0&&c.health>=.25&&!c.failed)yield return ServiceAction.Reseal;
            if(!c.failed&&c.health>=.35&&c.health<Math.Max(.75,.95-c.reconditionCount*.04)&&(p.category==PartCategory.PistonRings&&boreHealth>=.75||p.category==PartCategory.CylinderBlock||p.category==PartCategory.Valves||p.category==PartCategory.Followers))yield return ServiceAction.Recondition;
        }
        public static string Description(ServiceAction a){return a==ServiceAction.Clean?"Clean deposits (structural wear retained)":a==ServiceAction.Adjust?"Adjust clearance/timing (wear retained)":a==ServiceAction.Reseal?"Repair leak / reseal (wear retained)":"Recondition within service limit (partial recovery)";}
        public static void Apply(PartDefinition d,ComponentCondition c,ServiceAction a,double boreHealth=1)
        {
            if(a==ServiceAction.Recondition&&d.category==PartCategory.PistonRings&&boreHealth<.75)throw new InvalidOperationException("Cylinder bore is too worn for a ring-only repair. Recondition/replace the block first.");
            if(!Available(d,c,boreHealth).Contains(a))throw new InvalidOperationException("This fault/damage cannot be repaired by this service. Replace the part or choose an applicable service.");
            if(a==ServiceAction.Clean)c.deposits=0;if(a==ServiceAction.Adjust)c.adjustmentError=0;if(a==ServiceAction.Reseal)c.leakSeverity=0;
            if(a==ServiceAction.Recondition){c.health=(float)Math.Max(c.health,Math.Max(.75,.95-c.reconditionCount*.04));c.reconditionCount++;foreach(var e in c.elements)if(!e.Failed)e.health=Math.Max(e.health,c.health);}
            c.serviceCount++;c.lastService=Description(a);
        }
        public static void Repair(PlayerProfile p,string carId,string itemId,ServiceAction a)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);var item=p.parts.Find(i=>i.instanceId==itemId);
            if(car==null||item==null||!car.installed.slots.Any(s=>s.partInstanceId==itemId))throw new InvalidOperationException("Service item is no longer installed.");
            Apply(p.catalog.Find(item.definitionId),item.condition,a,BoreHealth(p,car));
        }
        public static double TopUp(PlayerProfile p,string carId,bool oil,double litres)
        {
            var owned=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(owned);if(string.IsNullOrEmpty(owned.engineAssemblyId))throw new InvalidOperationException("No engine attached to service.");if(owned==null)throw new InvalidOperationException("Service car no longer exists.");
            var b=MechanicsOwnership.Resolve(p,owned,p.catalog);var car=new CarSimulation(b.spec,b.tune,p.driving,p.parts.Where(i=>i.ownerId==owned.instanceId||i.ownerId==owned.engineAssemblyId));car.SetFluids(owned.fluids);
            double added=FluidDynamics.Add(car,oil,litres);owned.fluids=car.state.fluids.Copy();return added;
        }
        public static void ChangeOil(PlayerProfile p,string carId)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);if(string.IsNullOrEmpty(car.engineAssemblyId))throw new InvalidOperationException("No engine attached to service.");if(car==null)throw new InvalidOperationException("Service car no longer exists.");
            car.fluids.oilCondition=1;car.fluids.oilLitres=MechanicsOwnership.Resolve(p,car,p.catalog).spec.fluids.oilCapacityLitres;
            PartOperations.Buy(p,p.catalog,carId,OilFilter(p,car).id,true);
        }
        public static void FlushCoolant(PlayerProfile p,string carId)
        {var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);if(string.IsNullOrEmpty(car.engineAssemblyId))throw new InvalidOperationException("No engine attached to service.");if(car==null)throw new InvalidOperationException("Service car no longer exists.");car.fluids.coolantLitres=MechanicsOwnership.Resolve(p,car,p.catalog).spec.fluids.coolantCapacityLitres;}
    }
}
