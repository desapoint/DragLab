using System;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public static class PartOperations
    {
        static OwnedCar Car(PlayerProfile profile, string id) { var car=profile.cars.Find(c => c.instanceId == id) ?? throw new InvalidOperationException("Vehicle no longer exists.");EngineRebuilds.RequireAvailable(car);return car; }
        static PartInstance Item(PlayerProfile profile, string id) { return profile.parts.Find(p => p.instanceId == id) ?? throw new InvalidOperationException("Part no longer exists."); }
        public static string Buy(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId, string definitionId, bool install)
        {
            using(var state=new LiveCartApplyState(profile,catalog))
            {
                var item=BuyPart(state,vehicleId,definitionId,install,-1,0,out _);
                return item.instanceId;
            }
        }
        internal static PartInstance BuyPart(ICartApplyState state,string vehicleId,string definitionId,bool install,int rowIndex,int quantityOrdinal)
            =>BuyPart(state,vehicleId,definitionId,install,rowIndex,quantityOrdinal,out _);
        internal static PartInstance BuyPart(ICartApplyState state,string vehicleId,string definitionId,bool install,int rowIndex,int quantityOrdinal,out CartApplyAcquisitionReceipt<PartInstance> receipt)
        {
            receipt=null;
            var car=state.FindCarState(vehicleId);
            if(car==null)throw new InvalidOperationException("Vehicle no longer exists.");
            EngineRebuilds.RequireAvailable(true,car.GarageJobId,car.RebuildActive);
            var catalog=state.Catalog;
            var part=catalog.FindDefinition(definitionId);
            if(part==null||!PartsMarket.FitsAny(state,part)||state.Reputation<part.reputationRequired)
                throw new InvalidOperationException("Part offer requires REP "+(part==null?0:part.reputationRequired)+"; no preceding rank purchase is required.");
            if(install&&!catalog.Fits(catalog.Spec(car.VehicleId),part))throw new InvalidOperationException("Incompatible purchase for selected vehicle.");
            int count=PartsMarket.Count(state,car,part);
            var quote=state.Supply.Quote(part,count,false);
            int price=quote.Cost;
            if(price<0||state.Cash<price)throw new InvalidOperationException("Insufficient cash.");
            var allocated=state.Identities.Allocate("part",rowIndex,quantityOrdinal,"part");
            var item=new PartInstance{instanceId=allocated.DomainFieldValue,definitionId=part.id};
            var memberSpec=catalog.Fits(catalog.Spec(car.VehicleId),part)?InventoryUnits.Spec(state,car,part):PartsMarket.Reference(state,part);
            bool membersUninitialized=item.condition.elements==null||item.condition.elements.Length==0;
            EngineElements.Ensure(memberSpec,part,item);
            if(membersUninitialized&&state.MemberPolicy==PartMoveMemberPolicy.ProjectionRequireExisting&&item.condition.elements!=null)
                for(int index=0;index<item.condition.elements.Length;index++)
                    item.condition.elements[index].id=state.Identities.Allocate("element",rowIndex,quantityOrdinal,"part/elements/"+index).DomainFieldValue;
            var consumed=state.Supply.Consume(part,count,false);
            state.AppendPart(item);
            state.Cash=state.Cash-price;
            if(install)InstalledPartTransition.InstallCarPart((InstalledPartTransitionState)state,catalog,car,item,state.MemberPolicy);
            if(state.CaptureEffects)receipt=new CartApplyAcquisitionReceipt<PartInstance>(item,allocated.Identity,part.id,quote,price,consumed.StockDeltas,consumed.HoldDeltas);
            return item;
        }
        public static void Install(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId, string itemId)
        {
            using(var state=new LiveCartApplyState(profile,catalog))
            {
                var car=state.FindCarState(vehicleId);
                if(car==null)throw new InvalidOperationException("Vehicle no longer exists.");
                EngineRebuilds.RequireAvailable(true,car.GarageJobId,car.RebuildActive);
                var item=state.FindPartReadOnly(itemId)??throw new InvalidOperationException("Part no longer exists.");
                InstalledPartTransition.InstallCarPart((InstalledPartTransitionState)state,state.Catalog,car,item,state.MemberPolicy);
            }
        }
        public static void Remove(PlayerProfile profile, string vehicleId, PartCategory category) { MechanicsOwnership.Remove(profile, Car(profile, vehicleId), category.ToString()); }
        public static int SaleValue(PartDefinition definition, ComponentCondition condition) { return (int)Math.Floor(definition.price * .45 * condition.health * (condition.failed ? .1 : 1)); }
        public static void Sell(PlayerProfile profile, MechanicalCatalog catalog, string itemId)
        {
            var item = Item(profile, itemId);
            if (item.location != PartLocation.Inventory) throw new InvalidOperationException("Remove the installed part before selling it.");
            int sale=catalog.Find(item.definitionId).category==PartCategory.Tires&&item.condition.tires.Length==4?(int)Math.Floor(Enumerable.Range(0,4).Sum(i=>{var t=item.condition.tires[i];return catalog.Find(TireFitments.DefinitionId(item,i)).price*.25*.45*Math.Min(t.health,Math.Max(0,1-t.consumedLife))*(t.failed?.1:1);})):WheelFitments.HasOverrides(item)?(int)Math.Floor(Enumerable.Range(0,4).Sum(i=>catalog.Find(WheelFitments.DefinitionId(item,i)).price*.25*.45*item.condition.elements[i].health*(item.condition.elements[i].Failed?.1:1))):SaleValue(catalog.Find(item.definitionId),item.condition);
            profile.cash = checked(profile.cash + sale); profile.parts.Remove(item);profile.inventory.dealer.Add(new DealerStock{part=item,paidPrice=sale});
        }
        public static void Transfer(PlayerProfile profile, MechanicalCatalog catalog, string donorVehicleId, string targetVehicleId, PartCategory category)
        {
            if (donorVehicleId == targetVehicleId) throw new InvalidOperationException("Select another donor.");
            var donor = Car(profile, donorVehicleId); var binding = donor.installed.slots.Find(s => s.slot == category.ToString());
            if (binding == null) throw new InvalidOperationException("Donor slot is empty.");
            var item = Item(profile, binding.partInstanceId);
            if (!catalog.Fits(catalog.Spec(Car(profile, targetVehicleId).vehicleId), catalog.Find(item.definitionId))) throw new InvalidOperationException("Donor part is incompatible.");
            MechanicsOwnership.Remove(profile, donor, category.ToString()); MechanicsOwnership.Replace(profile, Car(profile, targetVehicleId), item, catalog);
        }
        public static void SafeDefaults(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId)
        {
            var car = Car(profile, vehicleId); var spec = catalog.Spec(car.vehicleId);
            car.tune = new Tune { launchRpm = Math.Min(3200, spec.redlineRpm - 600), shiftRpm = spec.redlineRpm - 500, finalDrive = spec.finalDrive, tirePressurePsi = 30, boostSetting = .65f, superchargerPulley = .9f };
            MechanicsOwnership.ProjectRanks(profile, car, catalog);
            var build = MechanicsOwnership.Resolve(profile, car, catalog);
            // Tune delivery to the weakest authored component; never silently replace/heal it.
            while (car.tune.boostSetting > .35f && PeakTorque(build.spec, build.tune) > build.safeTorqueNm)
            { car.tune.boostSetting = Math.Max(.35f, car.tune.boostSetting - .05f); build = MechanicsOwnership.Resolve(profile, car, catalog); }
        }
        static double PeakTorque(VehicleSpec spec, Tune tune) { double peak = 0; for (double rpm = spec.idleRpm; rpm <= spec.redlineRpm; rpm += 100) peak = Math.Max(peak, DragMath.Torque(spec, tune, rpm)); return peak; }
    }
}
