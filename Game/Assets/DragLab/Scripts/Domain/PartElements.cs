using System;

namespace DragLab.Domain.Mechanics
{
    // Common persisted identity and condition contract. Concrete element types
    // are carried by ComponentCondition.elements via SerializeReference.
    [Serializable]
    public abstract class PartElement
    {
        public string id, definitionId;
        public float health = 1f;
        public virtual PartElement Copy() { return (PartElement)MemberwiseClone(); }
        public abstract string Validate();
    }

    // Every physical category with individual members has its own runtime and
    // serialized type. Shared cylinder/fault/wear members remain on EngineElement
    // for the current physics, inspection and inventory consumers.
    [Serializable] public sealed class PistonElement : EngineElement { }
    [Serializable] public sealed class ConnectingRodElement : EngineElement { }
    [Serializable] public sealed class PistonRingElement : EngineElement { }
    [Serializable] public sealed class BearingElement : EngineElement { }
    [Serializable] public sealed class ValveElement : EngineElement { }
    [Serializable] public sealed class ValveSpringElement : EngineElement { }
    [Serializable] public sealed class ValveSealElement : EngineElement { }
    [Serializable] public sealed class FollowerElement : EngineElement { }
    [Serializable] public sealed class WheelElement : EngineElement
    {
        public override string Validate()
        {
            if(base.Validate()!=null || role!="wheel" || cylinder< -1 || cylinder>3 || valve!=-1)
                return "Invalid individual wheel.";
            return null;
        }
    }
    [Serializable] public sealed class IgnitionElement : EngineElement { }
    [Serializable] public sealed class SparkPlugElement : EngineElement { }
    [Serializable] public sealed class GlowPlugElement : EngineElement { }
    [Serializable] public sealed class FuelSystemElement : EngineElement { }
}
