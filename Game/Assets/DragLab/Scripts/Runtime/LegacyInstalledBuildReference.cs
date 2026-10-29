#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Original public wrapper bodies from f22b94d. Catalogue authority remains shared;
    // this Editor reference never calls the later extracted installed-build helpers.
    internal static class LegacyInstalledBuildReference
    {
        public static BuildResolution Resolve(PlayerProfile profile, OwnedCar car, MechanicalCatalog catalog)
        {
            using (var installed = InstalledPartLookup.Capture(profile.parts, car.installed.slots))
            {
                var result = catalog.Resolve(car.vehicleId, installed.CopyDefinitions(), car.tune);
                // TireFitments consumes this sequence synchronously and captures only authored
                // definition copies; no borrowed record or lookup escapes into the result.
                TireFitments.Capture(result.spec,Enumerable.Range(0,installed.Count).Select(i=>installed.EntryAt(i).Part).Where(p=>p!=null),catalog);
                if(string.IsNullOrEmpty(car.engineAssemblyId))result.errors.Add("No engine attached. Attach a stored engine in Assemblies.");
                if(car.engineRebuild!=null)result.errors.Add("Engine is reserved at the rebuild shop. Collect it after the quoted time.");
                if(!string.IsNullOrEmpty(car.garageJobId))result.errors.Add("Car and installed components are reserved by a garage job. Claim or cancel to release.");
                for (int bindingIndex = 0; bindingIndex < installed.Count; bindingIndex++)
                {
                    var entry = installed.EntryAt(bindingIndex);
                    var item = entry.Part;
                    if(item!=null&&catalog.Find(item.definitionId).category==PartCategory.Tires&&item.condition.tires!=null&&item.condition.tires.Length==4){
                        for(int i=0;i<4;i++)if(item.condition.tires[i].Failed)
                            result.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[i]+" tire failed");
                        continue;
                    }
                    if (item != null && (PartSemantics.Wears(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection) && (item.condition.failed || item.condition.health <= 0)) (MechanicalCatalog.DisablingEngineFailure(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection ? result.errors : result.warnings).Add(entry.Slot + " failed: " + item.condition.failureReason);
                }
                return result;
            }
        }
        public static InspectionBuild Capture(PlayerProfile profile, OwnedCar car, Tune adjustments = null, string definitionId = null, string instanceId = null, PartCategory? remove = null, IEnumerable<PartCartLine> cart = null, bool healthy = false)
        {
            if(cart!=null)return PartCart.Preview(profile,car,cart);
            Dictionary<PartCategory, string> map;
            var components = new List<PartInstance>(car.installed.slots.Count);
            // Capture definitions and ordered fitted identities once; only detached typed
            // component copies leave this synchronous borrowed-lookup scope.
            using (var installed = InstalledPartLookup.Capture(profile.parts, car.installed.slots))
            {
                map = installed.CopyDefinitions();
                for (int i = 0; i < installed.Count; i++)
                {
                    var part = installed.EntryAt(i).Part;
                    if (part != null) components.Add(part.Copy());
                }
            }
            PartInstance existing = instanceId == null ? null : profile.parts.Find(p => p.instanceId == instanceId) ?? profile.inventory.dealer.Where(s=>s.part!=null).Select(s=>s.part).FirstOrDefault(p=>p.instanceId==instanceId);
            var definition = profile.catalog.Find(existing == null ? definitionId : existing.definitionId);
            if (definition != null)
            {
                if (!profile.catalog.Fits(profile.catalog.Spec(car.vehicleId), definition)) throw new ArgumentException("Incompatible preview.");
                map[definition.category] = definition.id; components.RemoveAll(p => p.slot == definition.category.ToString());
                var replacement = existing == null ? new PartInstance { instanceId = "preview/" + definition.id, definitionId = definition.id } : existing.Copy();
                replacement.slot = definition.category.ToString(); components.Add(replacement);
            }
            if (remove.HasValue) { map.Remove(remove.Value); components.RemoveAll(p => p.slot == remove.Value.ToString()); }
            if(healthy) foreach(var item in components) {
                var c=item.condition;c.health=1;c.failed=false;c.failureReason="";c.deposits=c.leakSeverity=c.adjustmentError=c.stressExposure=c.serviceWearRemainder=0;c.clutchOilCondition=1;
                foreach(var e in c.elements){e.health=1;e.fault=ElementFault.None;e.severity=0;e.wearRemainder=0;}
                foreach(var t in c.tires){t.health=1;t.failed=false;t.consumedLife=0;}
            }
            var resolved = profile.catalog.Resolve(car.vehicleId, map, adjustments ?? car.tune);
            TireFitments.Capture(resolved.spec,components,profile.catalog);
            if(string.IsNullOrEmpty(car.engineAssemblyId))resolved.errors.Add("No engine attached: stored engines retain their own hardware and liquids.");
            foreach (var p in components) {
                var category=profile.catalog.Find(p.definitionId).category;
                if(category==PartCategory.Tires&&p.condition.tires!=null&&p.condition.tires.Length==4){
                    for(int i=0;i<4;i++)if(p.condition.tires[i].Failed)
                        resolved.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[i]+" tire failed");
                    continue;
                }
                if((PartSemantics.Wears(category)||category==PartCategory.Controls&&resolved.spec.engineBehavior.fueling==FuelSystem.Injection)&&(p.condition.failed||p.condition.health<=0))
                    (MechanicalCatalog.DisablingEngineFailure(category)||category==PartCategory.Controls&&resolved.spec.engineBehavior.fueling==FuelSystem.Injection?resolved.errors:resolved.warnings)
                        .Add("Failed "+p.slot+": "+p.condition.failureReason);
            }
            return new InspectionBuild { build = resolved, components = components, startingFluids=car.fluids.Copy(), startingFuel = new FuelState { litres = car.fuel.litres } };
        }
    }
}
#endif
