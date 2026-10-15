using System;

namespace DragLab.Domain.Mechanics
{
    // The persisted shared contract. Only identity and health are universal.
    [Serializable]
    public abstract class PartElement
    {
        public string id, definitionId;
        public float health = 1f;
        public virtual PartElement Copy() { return (PartElement)MemberwiseClone(); }
        public abstract string Validate();
    }

    // The existing mechanical simulation consumes EngineElement's role, cylinder,
    // fault and accumulated wear. Specializations enforce their own topology.
    [Serializable]
    public abstract class CylinderMemberElement : EngineElement
    {
        // Pistons, rings, rods, bearings, injectors and ignition units do not
        // occupy individual valve positions. Cylinder -1 means loose inventory.
        public override string Validate()
        {
            return base.Validate() ?? (valve!=-1 ? "Non-valve component has a valve position." : null);
        }
    }

    [Serializable]
    public abstract class ValveTrainMemberElement : EngineElement
    {
        // Cylinder/valve are meaningful on installed valvetrain hardware;
        // -1/-1 is allowed when the member is a new loose spare.
        public override string Validate()
        {
            return base.Validate() ?? (cylinder<0&&valve>=0 ? "Loose valvetrain component has a valve index." : null);
        }
    }

    [Serializable] public sealed class PistonElement : CylinderMemberElement { }
    [Serializable] public sealed class ConnectingRodElement : CylinderMemberElement { }
    [Serializable] public sealed class PistonRingElement : CylinderMemberElement { }
    [Serializable] public sealed class BearingElement : CylinderMemberElement { }
    [Serializable] public sealed class ValveElement : ValveTrainMemberElement { }
    [Serializable] public sealed class ValveSpringElement : ValveTrainMemberElement { }
    [Serializable] public sealed class ValveSealElement : ValveTrainMemberElement { }
    [Serializable] public sealed class FollowerElement : ValveTrainMemberElement { }

    [Serializable]
    public sealed class WheelElement : EngineElement
    {
        // Physical wheel corner, still exposed as cylinder for the existing
        // inventory API; a loose wheel is temporarily positionless (-1).
        public override string Validate()
        {
            return base.Validate() ?? (role!="wheel" || cylinder< -1 || cylinder>3 || valve!=-1
                ? "Invalid individual wheel." : null);
        }
    }

    [Serializable] public sealed class IgnitionElement : CylinderMemberElement { }
    [Serializable] public sealed class SparkPlugElement : CylinderMemberElement { }
    [Serializable] public sealed class GlowPlugElement : CylinderMemberElement { }
    [Serializable] public sealed class FuelSystemElement : CylinderMemberElement { }
}
