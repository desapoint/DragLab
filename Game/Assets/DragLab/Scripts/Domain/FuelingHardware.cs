using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    public enum FuelDeliveryHardware { Factory, ThrottleBodyInjection, PortInjection }
    public static class FuelingHardware
    {
        public static FuelSystem Type(VehicleSpec spec, IDictionary<PartCategory,string> installed, MechanicalCatalog catalog)
        {
            string id; var p=installed.TryGetValue(PartCategory.FuelSystem,out id)?catalog.Find(id):null;
            return p!=null && p.fuelDelivery!=FuelDeliveryHardware.Factory ? FuelSystem.Injection : spec.fuelSystemConverted ? spec.originalFueling : spec.engineBehavior.fueling;
        }
        public static bool ControllerSupports(PartDefinition controller, VehicleSpec spec)
        {
            if(controller==null || controller.category!=PartCategory.Controls)return false;
            if(CombustionHardware.Diesel(spec))return controller.supportsDiesel&&controller.controllerCylinderCapacity>=spec.sound.cylinders;
            if(controller.supportsDiesel)return false;
            // Older replay definitions predate controller capability metadata. They supported
            // every factory injected engine; preserve that behavior for those captures only.
            if(controller.injectionControlMetadataVersion==0)return spec.engineBehavior.fueling==FuelSystem.Injection;
            return controller.supportsInjection && controller.controllerCylinderCapacity>=spec.sound.cylinders;
        }
        public static void Apply(VehicleSpec spec, PartDefinition part)
        {
            if(part.category!=PartCategory.FuelSystem || part.fuelDelivery==FuelDeliveryHardware.Factory)return;
            if(!spec.fuelSystemConverted)spec.originalFueling=spec.engineBehavior.fueling;
            spec.fuelSystemConverted=true;
            var b=spec.engineBehavior;b.fueling=FuelSystem.Injection;
            b.idleControl=part.injectionIdleControl;b.mixtureVariation=part.injectionMixtureVariation;
            b.acceleratorPump=.98f;b.hesitationSeconds=.12f;b.coldRoughness=.18f;
            // Retain cylinders, layout, cam, clatter, torque curve, exhaust and fitted limiter's default cut.
        }
        public static void AddConversions(MechanicalCatalog catalog, VehicleSpec spec)
        {
            var original=spec.fuelSystemConverted?spec.originalFueling:spec.engineBehavior.fueling;
            if(original!=FuelSystem.Carburetor)return;
            foreach(var delivery in new[]{FuelDeliveryHardware.ThrottleBodyInjection,FuelDeliveryHardware.PortInjection}) {
                bool port=delivery==FuelDeliveryHardware.PortInjection;
                var p=catalog.Find(catalog.Id(spec.id,PartCategory.FuelSystem,port?1:0)).Copy();
                p.id=spec.id+"/FuelSystem/"+(port?"EfiPortInjection":"EfiThrottleBody");
                p.fuelDelivery=delivery;p.name=spec.displayName+" / "+(port?"port-injection conversion kit":"throttle-body EFI conversion kit");
                p.quality="Aftermarket";p.price=port?2500:1500;p.massKg=port?16:11;
                p.injectionIdleControl=port?.96f:.88f;p.injectionMixtureVariation=port?.025f:.045f;
                p.description="Replaces carburetor with "+(port?"port injectors and compatible intake":"an injected throttle body")+", high-pressure pump/lines and sensors. Requires an injection-capable Controls part (fixed EFI controller or programmable engine controller). Improves metering, cold behavior and throttle response; retains cam/engine/exhaust character. No automatic horsepower or limiter/two-step/anti-lag unlock. Original carb can be retained and reinstalled.";
                catalog.parts.Add(p);
            }
            var ecu=catalog.Find(catalog.Id(spec.id,PartCategory.Controls)).Copy();
            ecu.id=spec.id+"/Controls/EfiStreet";ecu.name=spec.displayName+" / EFI fuel controller / fixed calibration";
            ecu.quality="Aftermarket";ecu.price=650;ecu.massKg=.9f;ecu.supportsInjection=true;ecu.controllerCylinderCapacity=spec.sound.cylinders;
            ecu.description="Injection-capable controller and conversion harness. Runs compatible EFI kits with fixed calibration. No programmable rev limiter, two-step, preload or anti-lag included; those require their appropriate modules/controller.";
            catalog.parts.Add(ecu);
        }
    }
}
