using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
namespace DragLab.Domain.Mechanics
{
    // Seller truth and player knowledge are separate. Public estimates never copy hidden faults.
    public static class VehicleInspection
    {
        public static bool Known(VehicleOffer offer)=>!offer.used||offer.inspected||offer.firstStarter;
        public static double Overall(MechanicalCatalog catalog,VehicleOffer offer)=>offer.parts.Where(p=>PartSemantics.Wears(catalog.Find(p.definitionId).category)).Select(p=>catalog.Find(p.definitionId).category==PartCategory.Tires?(double)TireDynamics.LowestHealth(p.condition):p.condition.health).DefaultIfEmpty(1).Average();
        public static BuildResolution Hardware(MechanicalCatalog catalog,VehicleOffer offer){var b=catalog.Resolve(offer.model,offer.parts.ToDictionary(p=>catalog.Find(p.definitionId).category,p=>p.definitionId),offer.stock);TireFitments.Capture(b.spec,offer.parts,catalog);return b;}
        public static void Assess(MechanicalCatalog catalog,VehicleOffer offer,BuildResolution prepared=null)
        {
            var result=VehicleAssessmentCache.Prepare(catalog,offer,prepared).GetAwaiter().GetResult();offer.engineRuns=result.engineRuns;offer.drives=result.drives;
        }
        public static void Migrate(MechanicalCatalog catalog,VehicleOffer offer,bool firstPurchase)
        {
            if(offer.disclosureVersion!=0)return;
            // Previously visible contents remain known; never retrofit hidden damage or refill resources.
            offer.disclosureVersion=1;offer.inspected=true;offer.firstStarter=firstPurchase&&offer.listingId.StartsWith("starter:",StringComparison.Ordinal);
            offer.historyNotes=new List<string>();offer.reportedCondition=Overall(catalog,offer);offer.estimateSeed=1;Assess(catalog,offer);
        }
        public static VehicleOffer PublicOffer(MechanicalCatalog catalog,VehicleOffer offer,Tune factoryTune=null)
        {
            if(Known(offer))return offer;
            factoryTune=factoryTune??Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v=>v.spec.id==offer.model).CreateStockTune();
            var build=catalog.Resolve(offer.model,catalog.Recipe(offer.model),factoryTune);
            double health=DragMath.Clamp(offer.reportedCondition+(RivalBehavior.Noise(offer.estimateSeed,1)-.5)*.1,.35,.98);
            var view=new VehicleOffer{model=offer.model,listingId=offer.listingId,paint=offer.paint,used=true,mileageKm=offer.mileageKm,price=offer.price,reputation=offer.reputation,stock=build.tune.Copy(),fuel=new FuelState{litres=build.spec.fuel.capacityLitres*.5},fluids=FluidState.Full(build.spec.fluids),parts=new List<PartInstance>(),disclosureVersion=1,inspected=false,reportedCondition=offer.reportedCondition,engineRuns=offer.engineRuns,drives=offer.drives,estimateSeed=offer.estimateSeed,historyNotes=new List<string>()};
            view.fluids.oilLitres*=.8;view.fluids.coolantLitres*=.8;
            foreach(var slot in catalog.Recipe(offer.model)){
                var d=catalog.Find(slot.Value);var p=new PartInstance{instanceId="estimate/"+offer.listingId+"/"+slot.Key,definitionId=d.id,slot=slot.Key.ToString(),condition=new ComponentCondition{health=(float)(PartSemantics.Wears(d.category)?health:1)}};
                EngineElements.Ensure(build.spec,d,p);ClutchHardware.Ensure(d,p.condition);if(d.category==PartCategory.Tires){TireDynamics.SeedNew(p.condition);for(int i=0;i<4;i++){var tire=p.condition.tires[i];tire.id=p.instanceId+"/tire/"+i;tire.definitionId=d.id;}}view.parts.Add(p);
            }
            return view;
        }
        public static void Reveal(PlayerProfile profile,VehicleOffer offer)
        {
            VehicleListings.RequireCurrent(profile,offer);if(string.IsNullOrEmpty(offer.listingId)||Known(offer))throw new InvalidOperationException("This listing is already fully disclosed.");
            offer.inspected=true;profile.vehicleStock.knowledgeRevision=checked(profile.vehicleStock.knowledgeRevision+1);
        }
        public static string Validate(VehicleOffer offer)
        {
            if(offer.disclosureVersion!=1||!MechanicsSchema.Finite(offer.reportedCondition)||offer.reportedCondition<0||offer.reportedCondition>1||offer.historyNotes==null||offer.historyNotes.Count>16||offer.historyNotes.Any(n=>string.IsNullOrWhiteSpace(n)||n.Length>160||n.Any(char.IsControl)))return "Invalid vehicle disclosure/history.";
            if(offer.firstStarter&&(!offer.used||!offer.inspected||!offer.engineRuns||!offer.drives||offer.parts.Any(p=>p.condition.tires!=null&&p.condition.tires.Length==4?TireDynamics.AnyFailed(p.condition)||TireDynamics.LowestHealth(p.condition)<.25:p.condition.failed||p.condition.health<.25||p.condition.elements.Any(e=>e.fault!=ElementFault.None))))return "Invalid starter disclosure or condition.";
            return null;
        }
    }
}
