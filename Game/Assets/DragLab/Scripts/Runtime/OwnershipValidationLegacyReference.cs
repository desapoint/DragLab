#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;

namespace DragLab.Domain.Mechanics
{
    // Test-only frozen scan reference copied from the original MechanicsOwnership.Validate body at S02c baseline 7e6ea10.
    // Keep semantic changes out of this oracle; it intentionally uses the original ordered List.Find paths.
    internal static class OwnershipValidationLegacyReference
    {
        internal static string Validate(PlayerProfile profile, MechanicalCatalog catalog)
        {
            if (profile.saveVersion != MechanicsOwnership.SaveVersion || profile.mechanicsVersion != MechanicsSchema.Version || profile.revision < 0 || profile.cash < 0) return "Invalid profile version/revision/cash.";
            if(profile.cars.Count == 0 ? !string.IsNullOrEmpty(profile.selectedOwnedVehicleId) || !string.IsNullOrEmpty(profile.selectedVehicleId) : !profile.cars.Any(c=>c.instanceId==profile.selectedOwnedVehicleId&&c.vehicleId==profile.selectedVehicleId))return "Invalid selected owned vehicle reference.";
            string stockError=VehicleListings.Validate(profile,catalog);if(stockError!=null)return stockError;
            string economyError = PaidEconomy.ValidateState(profile); if (economyError != null) return economyError;
            string careerError = CareerProgression.Validate(profile); if (careerError != null) return careerError;
            string businessError=GarageBusiness.Validate(profile);if(businessError!=null)return businessError;
            var identities = new HashSet<string>(); var bindings = new HashSet<string>();
            Func<string, bool> unique = id => !string.IsNullOrEmpty(id) && identities.Add(id);
            foreach (var car in profile.cars)
            {
                if(car.usedHistory==null||car.usedHistory.Count>16||car.usedHistory.Any(n=>string.IsNullOrWhiteSpace(n)||n.Length>160||n.Any(char.IsControl)))return "Invalid owned history.";
                if(!MechanicsSchema.Finite(car.mileageKm)||car.mileageKm<0)return "Invalid owned mileage.";
                if (MassDynamics.ValidateInputs(catalog.Spec(car.vehicleId),car.tune) != null) return "Invalid owned mass/placement inputs.";
                if(car.serviceHardwareVersion!=1||car.fluids==null||car.fluids.Validate(catalog.Spec(car.vehicleId).fluids)!=null)return "Invalid owned fluids/service migration.";
                if(car.thermalUtcTicks<0||car.thermalUtcTicks>DateTime.MaxValue.Ticks)return "Invalid owned thermal clock.";
                if (car.fuel == null || car.fuel.Validate(catalog.Spec(car.vehicleId).fuel) != null || !car.fuelInitialized || car.massVersion != 1) return "Invalid fuel/mass state.";
                if (!unique(car.instanceId) || !unique(car.chassisId) || car.installed == null || car.installed.version != MechanicsSchema.Version || car.installed.instanceId != car.instanceId || car.installed.vehicleDefinitionId != car.vehicleId) return "Invalid vehicle/chassis/build identity.";
                var engine = profile.engines.Find(e => e.instanceId == car.engineAssemblyId);
                if (string.IsNullOrEmpty(car.engineAssemblyId)) {
                    if(car.engineRebuild!=null||car.installed.slots.Any(b=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(b.slot)))||car.fluids.oilLitres!=0||car.fluids.coolantLitres!=0)return "Engine-less chassis has engine descendants/resources.";
                } else if (engine == null || engine.parentVehicleId != car.instanceId || engine.vehicleDefinitionId != car.vehicleId || engine.slots.Count != 0) return "Invalid engine parent.";
                var slots = new HashSet<string>();
                foreach (var binding in car.installed.slots)
                {
                    PartCategory category;
                    if (!Enum.TryParse(binding.slot, out category) || !Enum.IsDefined(typeof(PartCategory), category) || !slots.Add(binding.slot) || !bindings.Add(binding.partInstanceId)) return "Duplicate/invalid slot binding.";
                    var item = profile.parts.Find(p => p.instanceId == binding.partInstanceId);
                    var definition = item == null ? null : catalog.Find(item.definitionId);
                    var location = MechanicalCatalog.EngineSlot(category) ? PartLocation.Assembly : PartLocation.Vehicle;
                    string owner = location == PartLocation.Assembly ? car.engineAssemblyId : car.instanceId;
                    if (item == null || definition == null || definition.category != category || item.location != location || item.ownerId != owner || item.slot != binding.slot || !catalog.Fits(catalog.Spec(car.vehicleId), definition)) return "Part parent/fitment does not match its slot.";
                }
            }
            foreach (var engine in profile.engines) {
                if(engine.customName!=null&&(engine.customName.Length>VehicleCollection.NameLimit||engine.customName.Any(char.IsControl)))return "Invalid engine name.";
                if(!unique(engine.instanceId)||engine.slots==null||engine.tune==null)return "Invalid/duplicate engine assembly.";
                if(!string.IsNullOrEmpty(engine.parentVehicleId)) { if(!profile.cars.Any(c=>c.instanceId==engine.parentVehicleId&&c.engineAssemblyId==engine.instanceId))return "Orphan attached engine assembly."; continue; }
                if(engine.fluids==null||engine.fluids.Validate(catalog.Spec(engine.vehicleDefinitionId).fluids)!=null)return "Invalid stored engine fluids.";
                var slots=new HashSet<string>();
                foreach(var binding in engine.slots){
                    PartCategory category;
                    if(!Enum.TryParse(binding.slot,out category)||!MechanicalCatalog.EngineSlot(category)||!slots.Add(binding.slot)||!bindings.Add(binding.partInstanceId))return "Invalid/duplicate stored engine slot.";
                    var item=profile.parts.Find(i=>i.instanceId==binding.partInstanceId);var d=item==null?null:catalog.Find(item.definitionId);
                    if(d==null||d.category!=category||item.location!=PartLocation.Assembly||item.ownerId!=engine.instanceId||item.slot!=binding.slot||!catalog.Fits(catalog.Spec(engine.vehicleDefinitionId),d))return "Invalid stored engine descendant/fitment.";
                }
            }
            foreach(var setup in profile.savedSetups)if(SavedSetups.Validate(setup,catalog)!=null||!unique(setup.id))return "Invalid/duplicate saved setup.";
            foreach (var item in profile.parts)
                if (!unique(item.instanceId) || item.Validate() != null || catalog.Find(item.definitionId) == null || (item.location != PartLocation.Inventory) != bindings.Contains(item.instanceId)) return "Duplicate/invalid/orphan part instance.";
            foreach(var loose in profile.looseEngineElements)if(loose==null||loose.element==null||loose.element.Validate()!=null||catalog.Find(loose.definitionId)==null||loose.unitPrice<0||!unique(loose.element.id))return "Invalid loose engine element.";
            foreach(var part in profile.parts)
            {
                var definition=catalog.Find(part.definitionId);
                if(definition!=null&&definition.category!=PartCategory.Clutch&&part.condition.clutch!=null)
                    return "Non-clutch components cannot own clutch-fluid state.";
                foreach(var element in part.condition.elements??new EngineElement[0])
                    if(!EngineElements.FaultAllowed(definition.category,element)||!unique(element.id))
                        return "Invalid/duplicate typed element identity, category or fault.";
            }
            foreach(var part in profile.parts.Where(p=>catalog.Find(p.definitionId).category==PartCategory.Tires))
                if(part.condition.elements==null||part.condition.elements.Length!=4||part.condition.elements.Any(e=>!(e is TireCondition)))return "Tire sets must contain four typed tire elements.";
            if(profile.driving==null||profile.driving.guaranteedFittingParts<0||profile.driving.guaranteedFittingParts>20)return "Invalid market guarantee difficulty.";
            // Validate dealer identities first, before market descendant enumeration.
            string inventoryError=SharedInventory.Validate(profile,identities);if(inventoryError!=null)return inventoryError;
            string marketError=PartsMarket.Validate(profile);if(marketError!=null)return marketError;
            foreach(var item in profile.parts)foreach(var tire in item.condition.tires??new TireCondition[0])if(!string.IsNullOrEmpty(tire.definitionId)){
                var d=catalog.Find(tire.definitionId);if(d==null||d.category!=PartCategory.Tires)return "Invalid tire specification.";
                var owner=profile.cars.FirstOrDefault(c=>c.installed.slots.Any(s=>s.partInstanceId==item.instanceId));
                if(owner!=null&&!catalog.Fits(catalog.Spec(owner.vehicleId),d))return "Incompatible individual tire.";
            }
            foreach(var item in profile.parts.Where(i=>catalog.Find(i.definitionId).category==PartCategory.Wheels)){if(item.condition.elements.Length!=4)return "Four wheel members required.";foreach(var e in item.condition.elements){var d=catalog.Find(string.IsNullOrEmpty(e.definitionId)?item.definitionId:e.definitionId);if(d==null||d.category!=PartCategory.Wheels||e.role!="wheel"||e.cylinder<0||e.cylinder>3)return "Invalid individual wheel specification.";var owner=profile.cars.FirstOrDefault(c=>c.installed.slots.Any(s=>s.partInstanceId==item.instanceId));if(owner!=null&&!catalog.Fits(catalog.Spec(owner.vehicleId),d))return "Individual wheel does not fit.";}if(item.condition.elements.Select(e=>e.cylinder).Distinct().Count()!=4)return "Duplicate wheel positions.";}
            foreach(var car in profile.cars){if(car.engineRebuild!=null&&EngineRebuilds.Validate(profile,car)!=null)return "Invalid engine rebuild reservation.";}
            var runs = new HashSet<string>();
            foreach (var run in profile.runCheckpoints)
                if (run.version != MechanicsSchema.Version || string.IsNullOrEmpty(run.runId) || !runs.Add(run.runId) || !profile.cars.Any(c => c.instanceId == run.ownedVehicleId) || run.tick < 0 || run.revision > profile.revision) return "Invalid run checkpoint.";
            return null;
        }
    }
}
#endif
