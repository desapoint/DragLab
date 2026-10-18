using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public enum IgnitionTopology { Auto, Distributor, CoilOnPlug, WastedSpark }
    public enum ElementFault { None, Disabled, CompressionLeak, WeakSpring, StuckOpen, DroppedValve, Seized }
    [Serializable] public class EngineElement : PartElement
    {
        public string role;
        public int cylinder, valve;
        public float severity; public double wearRemainder;
        public ElementFault fault;
        public new EngineElement Copy(){return (EngineElement)MemberwiseClone();}
        public override string Validate(){return string.IsNullOrEmpty(id)||string.IsNullOrEmpty(role)||cylinder< -1||cylinder>15||valve< -1||valve>7||!EngineBehaviorSpec.Range(health,0,1)||!EngineBehaviorSpec.Range(severity,0,1)||!MechanicsSchema.Finite(wearRemainder)||wearRemainder<0||!EngineElements.FaultAllowed(this)?"Invalid individual component identity, condition, type or fault.":null;}
        public virtual bool Failed {get{return health<=0||fault==ElementFault.Disabled||fault==ElementFault.DroppedValve||fault==ElementFault.Seized;}}
    }
    [Serializable] public sealed class LooseEngineElement
    {
        public string definitionId, storageId;
        public int unitPrice;
        [UnityEngine.SerializeReference] public EngineElement element;
        public bool tireUnit;

        // Compatibility accessor for existing inventory and shop callers.
        // There is no second serialized condition: the tire IS the concrete member.
        public TireCondition tire
        {
            get { return element as TireCondition; }
            set
            {
                if (value == null) return;
                if (string.IsNullOrEmpty(value.id) && element != null) value.id = element.id;
                if (string.IsNullOrEmpty(value.definitionId)) value.definitionId = definitionId;
                element = value;
            }
        }
    }
    public static class EngineElements
    {
        static readonly Dictionary<Type,PartCategory> memberCategories=new Dictionary<Type,PartCategory>{
            {typeof(PistonElement),PartCategory.Pistons},{typeof(ConnectingRodElement),PartCategory.Rods},
            {typeof(PistonRingElement),PartCategory.PistonRings},{typeof(BearingElement),PartCategory.Bearings},
            {typeof(ValveElement),PartCategory.Valves},{typeof(ValveSpringElement),PartCategory.ValveSprings},
            {typeof(ValveSealElement),PartCategory.ValveSeals},{typeof(FollowerElement),PartCategory.Followers},
            {typeof(WheelElement),PartCategory.Wheels},{typeof(TireCondition),PartCategory.Tires},
            {typeof(IgnitionElement),PartCategory.Ignition},{typeof(SparkPlugElement),PartCategory.SparkPlugs},
            {typeof(GlowPlugElement),PartCategory.GlowPlugs},{typeof(FuelSystemElement),PartCategory.FuelSystem}
        };
        static IReadOnlyList<ElementFault> FaultList(params ElementFault[] values)=>Array.AsReadOnly(values);
        static readonly IReadOnlyList<ElementFault> ordinaryFaults=FaultList(ElementFault.None,ElementFault.Disabled);
        static readonly IReadOnlyList<ElementFault> coreFaults=FaultList(ElementFault.None,ElementFault.Disabled,ElementFault.Seized);
        static readonly Dictionary<PartCategory,IReadOnlyList<ElementFault>> memberFaults=new Dictionary<PartCategory,IReadOnlyList<ElementFault>>{
            {PartCategory.Pistons,FaultList(ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak,ElementFault.Seized)},
            {PartCategory.Rods,coreFaults},{PartCategory.Bearings,coreFaults},
            {PartCategory.PistonRings,FaultList(ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak)},
            {PartCategory.Valves,FaultList(ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak,ElementFault.StuckOpen,ElementFault.DroppedValve)},
            {PartCategory.ValveSprings,FaultList(ElementFault.None,ElementFault.Disabled,ElementFault.WeakSpring)},
            {PartCategory.Tires,FaultList(ElementFault.None)},
            {PartCategory.Wheels,ordinaryFaults},{PartCategory.ValveSeals,ordinaryFaults},{PartCategory.Followers,ordinaryFaults},
            {PartCategory.Ignition,ordinaryFaults},{PartCategory.SparkPlugs,ordinaryFaults},{PartCategory.GlowPlugs,ordinaryFaults},{PartCategory.FuelSystem,ordinaryFaults}
        };
        public static IReadOnlyList<ElementFault> Faults(PartCategory category)=>memberFaults.TryGetValue(category,out var values)?values:Array.Empty<ElementFault>();
        public static bool FaultAllowed(EngineElement element)=>element!=null&&memberCategories.TryGetValue(element.GetType(),out var category)&&Faults(category).Contains(element.fault);
        public static int ValveCount(VehicleSpec v){return v.valvesPerCylinder>0?v.valvesPerCylinder:(v.fuelSystemConverted?v.originalFueling:v.engineBehavior.fueling)==FuelSystem.Carburetor?2:4;}
        public static bool Supported(PartCategory c){return c==PartCategory.Wheels||c==PartCategory.Pistons||c==PartCategory.Rods||c==PartCategory.PistonRings||c==PartCategory.Bearings||c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.ValveSeals||c==PartCategory.Followers||c==PartCategory.Ignition||c==PartCategory.SparkPlugs||c==PartCategory.GlowPlugs||c==PartCategory.FuelSystem;}
        public static int Count(VehicleSpec v,PartDefinition d){if(!Supported(d.category))return 0;if(d.category==PartCategory.Wheels)return 4;if(d.category==PartCategory.FuelSystem&&(v.engineBehavior.fueling==FuelSystem.Carburetor||d.fuelDelivery==FuelDeliveryHardware.ThrottleBodyInjection))return 0;return d.category==PartCategory.Ignition&&v.combustion.separateSparkPlugs?CoilCount(v):v.sound.cylinders*(PerValve(d.category)?ValveCount(v):1)+(d.category==PartCategory.Ignition?CoilCount(v):0);}
        public static IgnitionTopology Ignition(VehicleSpec v){return v.ignitionTopology!=IgnitionTopology.Auto?v.ignitionTopology:(v.fuelSystemConverted?v.originalFueling:v.engineBehavior.fueling)==FuelSystem.Carburetor?IgnitionTopology.Distributor:IgnitionTopology.CoilOnPlug;}
        static int CoilCount(VehicleSpec v){return Ignition(v)==IgnitionTopology.Distributor?1:Ignition(v)==IgnitionTopology.WastedSpark?(v.sound.cylinders+1)/2:v.sound.cylinders;}
        static bool PerValve(PartCategory c){return c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.ValveSeals||c==PartCategory.Followers;}
        // Central factory: purchases, replacements, used stock and new assemblies
        // must preserve the physical category, even when a member is loose.
        public static EngineElement Create(PartCategory category)
        {
            switch(category)
            {
                case PartCategory.Pistons: return new PistonElement();
                case PartCategory.Rods: return new ConnectingRodElement();
                case PartCategory.PistonRings: return new PistonRingElement();
                case PartCategory.Bearings: return new BearingElement();
                case PartCategory.Valves: return new ValveElement();
                case PartCategory.ValveSprings: return new ValveSpringElement();
                case PartCategory.ValveSeals: return new ValveSealElement();
                case PartCategory.Followers: return new FollowerElement();
                case PartCategory.Wheels: return new WheelElement();
                case PartCategory.Ignition: return new IgnitionElement();
                case PartCategory.SparkPlugs: return new SparkPlugElement();
                case PartCategory.GlowPlugs: return new GlowPlugElement();
                case PartCategory.FuelSystem: return new FuelSystemElement();
                case PartCategory.Tires: return new TireCondition();
                default: return new EngineElement();
            }
        }
        // Used when a physical member is reclassified between kit categories,
        // for example during the one-time ignition/spark-plug split. Do not
        // reset wear, severity, role or identity during that reclassification.
        public static EngineElement CopyAsCategory(PartCategory category,EngineElement existing)
        {
            if(existing==null||existing is TireCondition||category==PartCategory.Tires)
                throw new InvalidOperationException("Invalid mechanical member conversion.");
            var copy=Create(category);
            copy.id=existing.id;copy.definitionId=existing.definitionId;
            copy.health=existing.health;copy.role=existing.role;
            copy.cylinder=existing.cylinder;copy.valve=existing.valve;
            copy.severity=existing.severity;copy.wearRemainder=existing.wearRemainder;
            copy.fault=existing.fault;
            return copy;
        }
        // Do not infer type from role strings: a coil, plug or injector can have
        // identical topology but still represents a different physical asset.
        public static bool MatchesCategory(PartCategory category,EngineElement element)
        {
            return element!=null&&memberCategories.TryGetValue(element.GetType(),out var actual)&&actual==category;
        }
        public static void Ensure(VehicleSpec v,PartDefinition d,PartInstance p)
        {
            int count=Count(v,d);if(count==0)return;
            if(p.condition.elements!=null&&p.condition.elements.Length>0)
            {
                if(p.condition.elements.Any(e=>!MatchesCategory(d.category,e)))
                    throw new InvalidOperationException("Installed member type does not match "+d.category+". Reset incompatible save.");
                return; // Never recreate existing individual damage.
            }
            p.condition.elements=new EngineElement[count];int valves=PerValve(d.category)?ValveCount(v):1;
            for(int i=0;i<count;i++){var element=Create(d.category);element.id=p.instanceId+"/element/"+i;element.cylinder=i/valves;element.valve=PerValve(d.category)?i%valves:-1;element.role=Role(d.category,i%valves,valves);element.health=p.condition.failed?0:p.condition.health;p.condition.elements[i]=element;}
            if(d.category==PartCategory.Ignition)for(int i=v.combustion.separateSparkPlugs?0:v.sound.cylinders;i<count;i++){var e=p.condition.elements[i];e.role="ignition coil";e.cylinder=Ignition(v)==IgnitionTopology.Distributor?-1:i-(v.combustion.separateSparkPlugs?0:v.sound.cylinders);}
        }
        static string Role(PartCategory c,int valve,int valves){return c==PartCategory.Wheels?"wheel":c==PartCategory.GlowPlugs?"glow plug":c==PartCategory.SparkPlugs||c==PartCategory.Ignition?"spark plug / lead":c==PartCategory.FuelSystem?"injector":c==PartCategory.Bearings?"rod bearing pair":c==PartCategory.PistonRings?"piston ring pack":PerValve(c)?(valve<valves/2?"intake ":"exhaust ")+(c==PartCategory.Valves?"valve / guide":c==PartCategory.ValveSprings?"spring / retainer":c==PartCategory.ValveSeals?"valve seal":"follower / rocker"):PartSemantics.Friendly(c.ToString());}
        public static bool NeedsService(ComponentCondition c){return c.elements!=null&&c.elements.Any(e=>e.health<.5||e.fault!=ElementFault.None);}
        public static bool FaultAllowed(PartCategory c,EngineElement e)=>MatchesCategory(c,e)&&Faults(c).Contains(e.fault);
        public static double Effect(EngineElement e,double severity)
        {
            if(e.Failed)return 0;
            double effect=1-(1-e.health)*.35*severity;
            if(e.fault==ElementFault.CompressionLeak||e.fault==ElementFault.StuckOpen)effect*=1-e.severity;
            return DragMath.Clamp(effect,0,1);
        }
        public static bool CanAdvance(ComponentCondition old,ComponentCondition next)
        {
            if(old.elements==null||old.elements.Length==0)return true;
            if(next.elements==null||next.elements.Length!=old.elements.Length)return false;
            for(int i=0;i<old.elements.Length;i++){var a=old.elements[i];var b=next.elements[i];if(a==null||b==null||a.GetType()!=b.GetType()||a.id!=b.id||(a.definitionId??"")!=(b.definitionId??"")||a.cylinder!=b.cylinder||a.valve!=b.valve||a.role!=b.role||b.health>a.health||a.fault!=ElementFault.None&&(b.fault!=a.fault||b.severity<a.severity))return false;}return true;
        }
        public static void Prepare(CarSimulation car)
        {
            var s=car.state;int n=car.spec.sound.cylinders;
            if(s.cylinderContribution==null||s.cylinderContribution.Length!=n){s.cylinderContribution=new double[n];s.cylinderFuelDelivery=new double[n];}
            for(int i=0;i<n;i++){s.cylinderContribution[i]=1;s.cylinderFuelDelivery[i]=1;}
            s.individualValveFloat=0;
            foreach(var p in car.mechanics.parts){var d=car.mechanics.Definition(p);Ensure(car.spec,d,p);if(p.condition.elements==null||d.category==PartCategory.Wheels||d.category==PartCategory.Tires)continue;if(p.condition.failed)foreach(var element in p.condition.elements)element.health=0;
                foreach(var e in p.condition.elements){
                    if(e.cylinder>=n)continue;
                    if(d.category==PartCategory.GlowPlugs)continue;
                    if(e.fault==ElementFault.Seized||e.fault==ElementFault.DroppedValve||d.category==PartCategory.Rods&&e.Failed){s.engineFailed=true;s.engineDamage=1;}
                    double effect=Effect(e,car.driver.ConditionSeverity);
                    if(e.role=="ignition coil"&&(e.cylinder<0||Ignition(car.spec)==IgnitionTopology.WastedSpark)){for(int cylinder=0;cylinder<n;cylinder++)if(e.cylinder<0||cylinder==e.cylinder||cylinder==(e.cylinder+(n+1)/2)%n)s.cylinderContribution[cylinder]*=effect;continue;}
                    if(d.category==PartCategory.ValveSeals)continue;
                    if(d.category==PartCategory.ValveSprings){double safe=car.spec.valvetrain.springSafeRpm*(1-(1-e.health)*.4*car.driver.ConditionSeverity)*(e.fault==ElementFault.WeakSpring?1-e.severity*.65:1)/Math.Max(1,car.spec.valvetrain.profileDemandRpm/car.spec.redlineRpm);double floating=DragMath.Clamp((s.rpm/Math.Max(500,safe)-1)*4,0,.95);s.individualValveFloat=Math.Max(s.individualValveFloat,floating);effect*=1-floating;}
                    if(e.cylinder<0)continue;
                    if((d.category==PartCategory.Valves||d.category==PartCategory.Followers||d.category==PartCategory.ValveSprings)&&(e.Failed||d.category==PartCategory.ValveSprings)&&e.fault!=ElementFault.DroppedValve){double share=1.0/(ValveCount(car.spec)/2);effect=1-(1-effect)*share;}
                    s.cylinderContribution[e.cylinder]*=effect;
                    if(d.category==PartCategory.FuelSystem)s.cylinderFuelDelivery[e.cylinder]*=effect;
                }
            }
            s.cylinderCombustionFactor=s.cylinderContribution.Average();s.cylinderFuelFactor=s.cylinderFuelDelivery.Average();
            s.mechanicalPowerFactor*=s.cylinderCombustionFactor;
            if(s.cylinderCombustionFactor<.001){s.engineSupplyAvailable=false;s.stalled=true;}
        }
        public static void Wear(PartInstance part,double loss)
        {
            if(loss<=0||part.condition.elements==null)return;
            foreach(var e in part.condition.elements)ComponentWear.Apply(e,loss);
        }
        public static void TickLocal(CarSimulation car,double dt)
        {
            if(car.driver.WearRate<=0||car.state.stalled||car.state.engineFailed)return;
            var springs=car.mechanics.Part(PartCategory.ValveSprings);var valves=car.mechanics.Part(PartCategory.Valves);
            if(springs==null||valves==null)return;
            foreach(var e in springs.condition.elements){if(e.fault!=ElementFault.WeakSpring&&e.health>.5)continue;double safe=car.spec.valvetrain.springSafeRpm*(.6+.4*e.health)*(e.fault==ElementFault.WeakSpring?1-e.severity*.65:1);double excess=Math.Max(0,car.state.rpm/Math.Max(500,safe)-1);var valve=valves.condition.elements.FirstOrDefault(v=>v.cylinder==e.cylinder&&v.valve==e.valve);if(valve==null)continue;ComponentWear.Apply(valve,excess*excess*.015*dt*car.driver.WearRate);if(valve.health<=0){valve.fault=car.spec.interferenceEngine?ElementFault.DroppedValve:ElementFault.Disabled;valve.severity=1;}}
        }
        static readonly int[][] orders=Enumerable.Range(1,16).Select(n=>n==4?new[]{0,2,3,1}:n==6?new[]{0,4,2,5,1,3}:n==8?new[]{0,7,3,2,5,4,6,1}:Enumerable.Range(0,n).ToArray()).ToArray();
        public static int[] FiringOrder(int n){return orders[Math.Max(1,Math.Min(16,n))-1];}
        public static double Ripple(CarState s){var gains=s.cylinderContribution;if(gains==null||gains.Length<2||s.cylinderCombustionFactor<=.001)return 1;return 1+.12*(PulseFactor(s,gains.Length)-s.cylinderCombustionFactor)/Math.Max(.1,s.cylinderCombustionFactor);}
        public static double LocalLoss(CarSimulation car,PartCategory category){var part=car.mechanics.Part(category);return part==null||part.condition.elements==null||part.condition.elements.Length==0?0:part.condition.elements.Average(e=>1-Effect(e,car.driver.ConditionSeverity));}
        public static double PulseFactor(CarState s,int n){if(s.cylinderContribution==null||s.cylinderContribution.Length!=n)return 1;int phase=Math.Min(n-1,(int)((Math.Max(0,s.crankCycles)%1)*n));return s.cylinderContribution[FiringOrder(n)[phase]];}
    }
}
