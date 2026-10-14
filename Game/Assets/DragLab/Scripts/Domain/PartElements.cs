using System;
using DragLab.Domain;

namespace DragLab.Domain.Mechanics
{
    // Shared contract for individually owned parts; subclasses keep category-specific data.
    [Serializable]
    public abstract class PartElement
    {
        public string id, definitionId;
        public float health = 1f;
        public virtual PartElement Copy() { return (PartElement)MemberwiseClone(); }
        public abstract string Validate();
    }

    // Kept as distinct serialized types so category-specific member data can grow
    // without duplicating unrelated fields on every member.
    [Serializable] public sealed class PistonElement : EngineElement { }
    [Serializable] public sealed class PistonRingElement : EngineElement { }
    [Serializable] public sealed class ValveElement : EngineElement { }
    [Serializable] public sealed class WheelElement : EngineElement { }
}
