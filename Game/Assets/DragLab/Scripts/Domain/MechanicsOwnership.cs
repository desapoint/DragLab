using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class EngineAssemblyInstance {
        public string instanceId, parentVehicleId, vehicleDefinitionId, storageId, customName;
        // Attached bindings/fluids live only in the car. Detached descendants live only here.
        public List<SlotBinding> slots = new List<SlotBinding>();
        public FluidState fluids;
        public Tune tune = new Tune();
    }
    public static class MechanicsOwnership
    {
        public const int SaveVersion = 7;
        public static string NewId(string kind) { return kind + "-" + Guid.NewGuid().ToString("N"); }
        public static void Ensure(PlayerProfile profile, MechanicalCatalog catalog)
        {
            if (profile.mechanicsVersion > MechanicsSchema.Version) throw new InvalidOperationException("Unsupported mechanics version.");
            if (profile.parts == null) profile.parts = new List<PartInstance>();
            if (profile.engines == null) profile.engines = new List<EngineAssemblyInstance>();
            if (profile.runCheckpoints == null) profile.runCheckpoints = new List<RunCheckpoint>();
            // Older schemas never contained seller stock; JsonUtility may create an empty inline object for an absent field.
            if(profile.saveVersion<7&&profile.vehicleStock!=null&&profile.vehicleStock.seed==0&&profile.vehicleStock.generation==0&&profile.vehicleStock.purchases==0&&profile.vehicleStock.completedRuns==0&&(profile.vehicleStock.listings==null||profile.vehicleStock.listings.Count==0))profile.vehicleStock=null;
            profile.catalog = catalog;
            CareerProgression.Ensure(profile);
            GarageBusiness.Ensure(profile);
            PaidEconomy.Ensure(profile);
            if (profile.savedSetups == null) profile.savedSetups = new List<SavedVehicleSetup>();
            foreach (var engine in profile.engines) { if (engine.slots == null) engine.slots = new List<SlotBinding>(); if (engine.tune == null) engine.tune = new Tune(); if (!string.IsNullOrEmpty(engine.parentVehicleId)) engine.fluids = null; }
            if(profile.looseEngineElements==null)profile.looseEngineElements=new List<LooseEngineElement>();
            SharedInventory.Ensure(profile);
            foreach (var car in profile.cars)
            {
                if(car.usedHistory==null)car.usedHistory=new List<string>();
                if(car.engineRebuild!=null&&!car.engineRebuild.active)car.engineRebuild=null;
                if(car.fluids==null||car.serviceHardwareVersion==0)car.fluids=FluidState.Full(catalog.Spec(car.vehicleId).fluids);
                if(!string.IsNullOrEmpty(car.instanceId)&&car.serviceHardwareVersion==0) {
                    foreach(var category in MechanicalCatalog.Slots(catalog.Spec(car.vehicleId)).Where(c=>ServiceHardware.IsCategory(c)&&c!=PartCategory.OilCooler&&c!=PartCategory.SparkPlugs&&c!=PartCategory.GlowPlugs)) {
                        if(car.installed.slots.Exists(s=>s.slot==category.ToString()))continue;
                        var item=new PartInstance{instanceId=NewId("part"),definitionId=catalog.Id(car.vehicleId,category),location=PartLocation.Assembly,ownerId=car.engineAssemblyId,slot=category.ToString()};
                        profile.parts.Add(item);car.installed.slots.Add(new SlotBinding{slot=item.slot,partInstanceId=item.instanceId});
                    }
                }
                car.serviceHardwareVersion=1;
                if (!car.fuelInitialized) { if (car.fuel == null) car.fuel = new FuelState(); if (car.fuel.litres <= 0) car.fuel.litres = catalog.Spec(car.vehicleId).fuel.capacityLitres; car.fuelInitialized = true; }
                if (car.massVersion == 0) { car.tune.driverMassKg = 75; foreach (var preset in (car.presets ?? new List<Tune>()).Where(p => p != null)) preset.driverMassKg = 75; car.massVersion = 1; }
                if (!string.IsNullOrEmpty(car.instanceId))
                {
                    // One-time additive schema migration only. Removed slots after migration stay empty.
                    if (profile.drivelineVersion == 0 && !car.installed.slots.Exists(s => s.slot == PartCategory.Driveshaft.ToString()))
                    {
                        var shaft = new PartInstance { instanceId = NewId("part"), definitionId = catalog.Id(car.vehicleId, PartCategory.Driveshaft), location = PartLocation.Vehicle, ownerId = car.instanceId, slot = PartCategory.Driveshaft.ToString() };
                        profile.parts.Add(shaft); car.installed.slots.Add(new SlotBinding { slot = shaft.slot, partInstanceId = shaft.instanceId });
                    }
                    if (profile.drivelineVersion < 2)
                    {
                        foreach (var category in MechanicalCatalog.Slots(catalog.Spec(car.vehicleId)).Where(c => MechanicalCatalog.DriveBranch(c)))
                        {
                            if (car.installed.slots.Exists(s => s.slot == category.ToString())) continue;
                            var item = new PartInstance { instanceId = NewId("part"), definitionId = catalog.Id(car.vehicleId, category), location = PartLocation.Vehicle, ownerId = car.instanceId, slot = category.ToString() };
                            profile.parts.Add(item); car.installed.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
                        }
                    }
                    if (profile.equipmentVersion < 1) foreach (var category in new[] { PartCategory.Interior, PartCategory.Accessories }) {
                        if (car.installed.slots.Exists(s => s.slot == category.ToString())) continue;
                        var item = new PartInstance { instanceId = NewId("part"), definitionId = catalog.Id(car.vehicleId, category), location = PartLocation.Vehicle, ownerId = car.instanceId, slot = category.ToString() };
                        profile.parts.Add(item); car.installed.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
                    }
                    if (profile.equipmentVersion < 1) foreach (var category in new[] { PartCategory.RevLimiter, PartCategory.TwoStep }) {
                        if (car.installed.slots.Exists(s => s.slot == category.ToString())) continue;
                        if (car.tune.controlLevel == 0 && (category == PartCategory.TwoStep ? !catalog.Spec(car.vehicleId).engineBehavior.twoStep : catalog.Spec(car.vehicleId).engineBehavior.limiter == LimiterStyle.None)) continue;
                        var item = new PartInstance { instanceId = NewId("part"), definitionId = catalog.Id(car.vehicleId, category, car.tune.controlLevel > 0 ? 1 : 0), location = PartLocation.Vehicle, ownerId = car.instanceId, slot = category.ToString() };
                        profile.parts.Add(item); car.installed.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
                    }
                    continue;
                }
                car.instanceId = NewId("vehicle"); car.chassisId = NewId("chassis"); car.engineAssemblyId = NewId("engine");
                car.installed = new InstalledBuild { instanceId = car.instanceId, vehicleDefinitionId = car.vehicleId };
                profile.engines.Add(new EngineAssemblyInstance { instanceId = car.engineAssemblyId, parentVehicleId = car.instanceId, vehicleDefinitionId = car.vehicleId });
                foreach (var slot in catalog.Recipe(car.vehicleId, car.tune))
                {
                    var item = new PartInstance { instanceId = NewId("part"), definitionId = slot.Value,
                        location = MechanicalCatalog.EngineSlot(slot.Key) ? PartLocation.Assembly : PartLocation.Vehicle,
                        ownerId = MechanicalCatalog.EngineSlot(slot.Key) ? car.engineAssemblyId : car.instanceId, slot = slot.Key.ToString() };
                    profile.parts.Add(item); car.installed.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
                }
            }
            // Resolve an owner's hardware once per normalization, rather than once
            // per part. Multi-car checkpoints must not re-resolve hundreds of builds.
            SparkPlugMigration.Apply(profile,catalog);
            var ownerById=new Dictionary<string,OwnedCar>();
            foreach(var car in profile.cars){ownerById[car.instanceId]=car;if(!string.IsNullOrEmpty(car.engineAssemblyId))ownerById[car.engineAssemblyId]=car;}
            var resolvedOwners=new Dictionary<string,VehicleSpec>();
            foreach(var item in profile.parts) {
                var d=catalog.Find(item.definitionId);OwnedCar owner=null;VehicleSpec spec;
                if(!string.IsNullOrEmpty(item.ownerId))ownerById.TryGetValue(item.ownerId,out owner);
                if(d!=null&&owner!=null) {
                    if(!resolvedOwners.TryGetValue(owner.instanceId,out spec)){spec=catalog.Resolve(owner.vehicleId,Definitions(profile,owner),owner.tune).spec;resolvedOwners[owner.instanceId]=spec;}
                    EngineElements.Ensure(spec,d,item);
                }
                if(d!=null&&owner==null&&item.location==PartLocation.Assembly) {
                    var stored=profile.engines.Find(e=>e.instanceId==item.ownerId&&string.IsNullOrEmpty(e.parentVehicleId));
                    if(stored!=null){if(!resolvedOwners.TryGetValue(stored.instanceId,out spec)){spec=catalog.Resolve(stored.vehicleDefinitionId,AssemblyOperations.Definitions(profile,stored),stored.tune).spec;resolvedOwners[stored.instanceId]=spec;}EngineElements.Ensure(spec,d,item);}
                }
                if(d!=null)ClutchHardware.Ensure(d,item.condition);
                if(d?.category==PartCategory.Tires)TireDynamics.Ensure(item.condition);
            }
            profile.equipmentVersion = 1; profile.drivelineVersion = 2; profile.mechanicsVersion = MechanicsSchema.Version; profile.saveVersion = SaveVersion;
            var selected = profile.SelectedCar ?? profile.cars.FirstOrDefault();
            profile.selectedVehicleId = selected?.vehicleId ?? ""; profile.selectedOwnedVehicleId = selected?.instanceId ?? "";
            GarageBusiness.UpgradeLegacySnapshots(profile);
            VehicleListings.Ensure(profile,catalog);
            string error = Validate(profile, catalog); if (error != null) throw new InvalidOperationException(error);
        }
        public static Dictionary<PartCategory, string> Definitions(PlayerProfile profile, OwnedCar car)
        {
            var result = new Dictionary<PartCategory, string>();
            foreach (var binding in car.installed.slots)
            {
                var part = profile.parts.Find(p => p.instanceId == binding.partInstanceId);
                if (part != null) result.Add((PartCategory)Enum.Parse(typeof(PartCategory), binding.slot), part.definitionId);
            }
            return result;
        }
        public static BuildResolution Resolve(PlayerProfile profile, OwnedCar car, MechanicalCatalog catalog)
        {
            var result = catalog.Resolve(car.vehicleId, Definitions(profile, car), car.tune);
            TireFitments.Capture(result.spec,car.installed.slots.Select(b=>profile.parts.Find(p=>p.instanceId==b.partInstanceId)).Where(p=>p!=null),catalog);
            if(string.IsNullOrEmpty(car.engineAssemblyId))result.errors.Add("No engine attached. Attach a stored engine in Assemblies.");
            if(car.engineRebuild!=null)result.errors.Add("Engine is reserved at the rebuild shop. Collect it after the quoted time.");
            if(!string.IsNullOrEmpty(car.garageJobId))result.errors.Add("Car and installed components are reserved by a garage job. Claim or cancel to release.");
            foreach (var binding in car.installed.slots)
            {
                var item = profile.parts.Find(p => p.instanceId == binding.partInstanceId);
                if (item != null && (PartSemantics.Wears(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection) && (item.condition.failed || item.condition.health <= 0)) (MechanicalCatalog.DisablingEngineFailure(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection ? result.errors : result.warnings).Add(binding.slot + " failed: " + item.condition.failureReason);
            }
            return result;
        }
        public static void Replace(PlayerProfile profile, OwnedCar car, PartInstance item, MechanicalCatalog catalog)
        {
            EngineRebuilds.RequireAvailable(car);
            var definition = catalog.Find(item.definitionId);
            if (item.location != PartLocation.Inventory || !catalog.Fits(catalog.Spec(car.vehicleId), definition)) throw new InvalidOperationException("Part must be loose and compatible.");
            if(MechanicalCatalog.EngineSlot(definition.category) && string.IsNullOrEmpty(car.engineAssemblyId)) throw new InvalidOperationException("Attach an engine before installing its parts.");
            string slot = definition.category.ToString(); var old = car.installed.slots.Find(s => s.slot == slot);
            if (old != null) Remove(profile, car, slot);
            item.location = MechanicalCatalog.EngineSlot(definition.category) ? PartLocation.Assembly : PartLocation.Vehicle;
            item.ownerId = item.location == PartLocation.Assembly ? car.engineAssemblyId : car.instanceId; item.slot = slot;
            car.installed.slots.Add(new SlotBinding { slot = slot, partInstanceId = item.instanceId });
            ProjectRanks(profile, car, catalog);
        }
        public static void Remove(PlayerProfile profile, OwnedCar car, string slot)
        {
            EngineRebuilds.RequireAvailable(car);
            var binding = car.installed.slots.Find(s => s.slot == slot);
            if (binding == null) throw new InvalidOperationException("Slot is empty.");
            var item = profile.parts.Find(p => p.instanceId == binding.partInstanceId);
            item.location = PartLocation.Inventory; item.ownerId = item.slot = ""; car.installed.slots.Remove(binding);
        }
        public static void ProjectRanks(PlayerProfile profile, OwnedCar car, MechanicalCatalog catalog)
        {
            var resolved = catalog.Resolve(car.vehicleId, Definitions(profile, car), car.tune).tune;
            car.tune.engineLevel = resolved.engineLevel; car.tune.tireLevel = resolved.tireLevel; car.tune.weightLevel = resolved.weightLevel;
            car.tune.gearboxLevel = resolved.gearboxLevel; car.tune.exhaustLevel = resolved.exhaustLevel; car.tune.controlLevel = resolved.controlLevel;
            car.tune.inductionUpgrade = resolved.inductionUpgrade; car.tune.transmissionUpgrade = resolved.transmissionUpgrade;
            car.tune.ApplyAdjustments(resolved);
        }
        public static bool LegacyUpgrade(PlayerProfile profile, string category)
        {
            PartCategory slot;
            switch (category) { case "engine": slot = PartCategory.HeadIntake; break; case "tires": slot = PartCategory.Tires; break; case "weight": slot = PartCategory.Body; break; case "gearbox": slot = PartCategory.Gearbox; break; case "exhaust": slot = PartCategory.Exhaust; break; case "controls": slot = PartCategory.Controls; break; default: return false; }
            var car = profile.SelectedCar; var map = Definitions(profile, car); string id;
            int level = map.TryGetValue(slot, out id) ? profile.catalog.Find(id).stage : 0;
            int cost = PlayerProfile.UpgradeCost(category, level);
            if (level >= (slot == PartCategory.Controls ? 1 : 3) || profile.cash < cost) return false;
            var item = new PartInstance { instanceId = NewId("part"), definitionId = profile.catalog.Id(car.vehicleId, slot, level + 1) };
            profile.parts.Add(item); Replace(profile, car, item, profile.catalog); profile.cash -= cost; profile.revision++;
            return true;
        }
        public static string Validate(PlayerProfile profile, MechanicalCatalog catalog)
        {
            if (profile.saveVersion != SaveVersion || profile.mechanicsVersion != MechanicsSchema.Version || profile.revision < 0 || profile.cash < 0) return "Invalid profile version/revision/cash.";
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
            foreach(var part in profile.parts)foreach(var element in part.condition.elements??new EngineElement[0])if(!EngineElements.FaultAllowed(catalog.Find(part.definitionId).category,element)||!unique(element.id))return "Invalid/duplicate engine element identity or fault.";
            foreach(var part in profile.parts)foreach(var tire in part.condition.tires??new TireCondition[0])if(!string.IsNullOrEmpty(tire.id)&&!unique(tire.id))return "Duplicate tire identity.";
            string inventoryError=SharedInventory.Validate(profile,identities);if(inventoryError!=null)return inventoryError;
            foreach(var item in profile.parts)foreach(var tire in item.condition.tires??new TireCondition[0])if(!string.IsNullOrEmpty(tire.definitionId)){
                var d=catalog.Find(tire.definitionId);if(d==null||d.category!=PartCategory.Tires)return "Invalid tire specification.";
                var owner=profile.cars.FirstOrDefault(c=>c.installed.slots.Any(s=>s.partInstanceId==item.instanceId));
                if(owner!=null&&!catalog.Fits(catalog.Spec(owner.vehicleId),d))return "Incompatible individual tire.";
            }
            foreach(var car in profile.cars){if(car.engineRebuild!=null&&EngineRebuilds.Validate(profile,car)!=null)return "Invalid engine rebuild reservation.";}
            var runs = new HashSet<string>();
            foreach (var run in profile.runCheckpoints)
                if (run.version != MechanicsSchema.Version || string.IsNullOrEmpty(run.runId) || !runs.Add(run.runId) || !profile.cars.Any(c => c.instanceId == run.ownedVehicleId) || run.tick < 0 || run.revision > profile.revision) return "Invalid run checkpoint.";
            return null;
        }
    }
}

