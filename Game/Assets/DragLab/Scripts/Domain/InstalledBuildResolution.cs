using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // One ordered fitted component view. Legacy callers may borrow Part only for the
    // synchronous call; the new concrete input supplies its own private copies.
    internal readonly struct InstalledBuildComponent
    {
        internal readonly string Slot;
        internal readonly string BindingId;
        internal readonly PartInstance Part;
        internal InstalledBuildComponent(string slot,PartInstance part,string bindingId=null){Slot=slot;BindingId=bindingId;Part=part;}
    }

    internal static class InstalledBuildResolution
    {
        internal static BuildResolution Resolve(MechanicalCatalog catalog,string vehicleId,
            IDictionary<PartCategory,string> definitions,Tune tune,IEnumerable<InstalledBuildComponent> components,
            bool hasAttachedEngine,bool rebuildReserved,bool garageJobReserved,
            string missingEngineMessage,string rebuildMessage,string garageJobMessage,bool inspectionFailureText)
        {
            var result=catalog.Resolve(vehicleId,definitions,tune);
            TireFitments.Capture(result.spec,components.Where(component=>component.Part!=null).Select(component=>component.Part),catalog);
            if(!hasAttachedEngine)result.errors.Add(missingEngineMessage);
            if(rebuildReserved&&rebuildMessage!=null)result.errors.Add(rebuildMessage);
            if(garageJobReserved&&garageJobMessage!=null)result.errors.Add(garageJobMessage);
            foreach(var component in components)
            {
                var item=component.Part;
                if(item==null)continue;
                var category=catalog.Find(item.definitionId).category;
                if(category==PartCategory.Tires&&item.condition.tires!=null&&item.condition.tires.Length==4)
                {
                    for(int i=0;i<4;i++)if(item.condition.tires[i].Failed)
                        result.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[i]+" tire failed");
                    continue;
                }
                if((PartSemantics.Wears(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection)&&
                    (item.condition.failed||item.condition.health<=0))
                    (MechanicalCatalog.DisablingEngineFailure(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection?
                        result.errors:result.warnings).Add((inspectionFailureText?"Failed ":"")+component.Slot+
                            (inspectionFailureText?": ":" failed: ")+item.condition.failureReason);
            }
            return result;
        }

        internal static BuildResolution Resolve(ICartCatalogRead catalog,string vehicleId,
            IDictionary<PartCategory,string> definitions,Tune tune,IEnumerable<InstalledBuildComponent> components,
            bool hasAttachedEngine,bool rebuildReserved,bool garageJobReserved,
            string missingEngineMessage,string rebuildMessage,string garageJobMessage,bool inspectionFailureText)
        {
            var result=catalog.Resolve(vehicleId,definitions,tune);
            TireFitments.Capture(result.spec,components.Where(component=>component.Part!=null).Select(component=>component.Part),catalog);
            if(!hasAttachedEngine)result.errors.Add(missingEngineMessage);
            if(rebuildReserved&&rebuildMessage!=null)result.errors.Add(rebuildMessage);
            if(garageJobReserved&&garageJobMessage!=null)result.errors.Add(garageJobMessage);
            foreach(var component in components)
            {
                var item=component.Part;
                if(item==null)continue;
                var category=catalog.FindDefinition(item.definitionId).category;
                if(category==PartCategory.Tires&&item.condition.tires!=null&&item.condition.tires.Length==4)
                {
                    for(int i=0;i<4;i++)if(item.condition.tires[i].Failed)
                        result.warnings.Add(new[]{"Front left","Front right","Rear left","Rear right"}[i]+" tire failed");
                    continue;
                }
                if((PartSemantics.Wears(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection)&&
                    (item.condition.failed||item.condition.health<=0))
                    (MechanicalCatalog.DisablingEngineFailure(category)||category==PartCategory.Controls&&result.spec.engineBehavior.fueling==FuelSystem.Injection?
                        result.errors:result.warnings).Add((inspectionFailureText?"Failed ":"")+component.Slot+
                            (inspectionFailureText?": ":" failed: ")+item.condition.failureReason);
            }
            return result;
        }
    }
}
