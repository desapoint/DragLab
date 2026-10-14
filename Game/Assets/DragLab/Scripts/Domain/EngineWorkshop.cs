using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Domain.Mechanics
{
    public static class EngineWorkshop
    {
        public static PlayerProfile Copy(PlayerProfile p){var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(copy,p.catalog);return copy;}
        public static void Swap(PlayerProfile p,string engineId,string carId)
        {
            var engine=AssemblyOperations.Stored(p,engineId);var car=AssemblyOperations.MutableCar(p,carId);
            if(!AssemblyOperations.Fits(engine,car))throw new InvalidOperationException("Engine mounts are incompatible with this chassis.");
            if(!string.IsNullOrEmpty(car.engineAssemblyId))AssemblyOperations.Detach(p,carId);
            AssemblyOperations.Attach(p,engineId,carId);
        }
        public static PlayerProfile Preview(PlayerProfile p,string engineId,string carId,IEnumerable<PartCartLine> lines=null)
        {
            var pending=(lines??Enumerable.Empty<PartCartLine>()).ToArray();if(pending.Length>0)PartCart.Total(p,p.SelectedCar.instanceId,pending);var copy=pending.Length==0?Copy(p):PartCart.PreviewProfile(p,p.SelectedCar,pending);
            var engine=copy.engines.Find(e=>e.instanceId==engineId)??throw new InvalidOperationException("Engine no longer exists.");
            if(engine.parentVehicleId!=carId)Swap(copy,engineId,carId);MechanicsOwnership.Ensure(copy,p.catalog);return copy;
        }
        public static void BuyPart(PlayerProfile p,string engineId,string definition,int expectedPrice)
        {
            var engine=AssemblyOperations.Stored(p,engineId);var d=p.catalog.Find(definition);
            if(d==null||!MechanicalCatalog.EngineSlot(d.category)||!p.catalog.Fits(p.catalog.Spec(engine.vehicleDefinitionId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Stored engine offer is incompatible or locked.");
            int count=Count(p,engine,d);int price=PartsMarket.Quote(p,d,count).cost;if(price!=expectedPrice||p.cash<price)throw new InvalidOperationException("Price changed or insufficient cash.");
            var item=new PartInstance{instanceId=MechanicsOwnership.NewId("part"),definitionId=d.id};PartsMarket.Consume(p,d,count);p.parts.Add(item);p.cash-=price;AssemblyOperations.InstallPart(p,engineId,item.instanceId);
        }
        public static int MissingPrice(PlayerProfile p,EngineAssemblyInstance engine){var ledger=new PartsMarket.Ledger(p);return Missing(p,engine).Sum(id=>{var d=p.catalog.Find(id);return ledger.Take(d,Count(p,engine,d),d.price).cost;});}
        public static PartInstance[] Parts(PlayerProfile p,EngineAssemblyInstance engine)
        {var bindings=string.IsNullOrEmpty(engine.parentVehicleId)?engine.slots:p.cars.Find(c=>c.instanceId==engine.parentVehicleId).installed.slots.Where(s=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(s.slot))).ToList();return bindings.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).Where(i=>i!=null).ToArray();}
        public static string[] Missing(PlayerProfile p,EngineAssemblyInstance engine)
        {var parts=Parts(p,engine);return p.catalog.Recipe(engine.vehicleDefinitionId).Where(pair=>MechanicalCatalog.EngineSlot(pair.Key)&&!parts.Any(i=>p.catalog.Find(i.definitionId).category==pair.Key)).Select(pair=>pair.Value).ToArray();}
        public static int Count(PlayerProfile p,EngineAssemblyInstance engine,PartDefinition definition)
        {var map=Parts(p,engine).ToDictionary(i=>p.catalog.Find(i.definitionId).category,i=>i.definitionId);map[definition.category]=definition.id;var spec=p.catalog.Resolve(engine.vehicleDefinitionId,map,engine.tune).spec;var template=new PartInstance{instanceId="engine-offer-template",definitionId=definition.id};EngineElements.Ensure(spec,definition,template);return Math.Max(1,template.condition.elements.Length);}
    }
}
