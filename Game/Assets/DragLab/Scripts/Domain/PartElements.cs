using System;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Domain.Mechanics
{
    // Future common ownership model for individually replaceable part members.
    // Intentionally not wired into saved PlayerProfile yet: legacy inline
    // EngineElement[] cannot be changed to SerializeReference without a save migration.
    [Serializable]
    public abstract class PartElement
    {
        public string id, definitionId;
        public float health = 1f;

        protected string ValidateIdentity()
        {
            return string.IsNullOrEmpty(id) || !EngineBehaviorSpec.Range(health, 0, 1)
                ? "Invalid part element identity or health." : null;
        }

        public abstract PartElement Copy();
        public abstract string Validate();
    }

    [Serializable]
    public sealed class PistonPartElement : PartElement
    {
        public int cylinder;
        public ElementFault fault;
        public float severity;
        public double wearRemainder;

        public override PartElement Copy() { return (PistonPartElement)MemberwiseClone(); }
        public override string Validate()
        {
            return ValidateIdentity() ?? (cylinder < 0 || cylinder > 15 || !EngineBehaviorSpec.Range(severity, 0, 1)
                || !MechanicsSchema.Finite(wearRemainder) || wearRemainder < 0
                || !Enum.IsDefined(typeof(ElementFault), fault) ? "Invalid piston element." : null);
        }
    }

    [Serializable]
    public sealed class ValvePartElement : PartElement
    {
        public int cylinder, valve;
        public ElementFault fault;
        public float severity;
        public double wearRemainder;

        public override PartElement Copy() { return (ValvePartElement)MemberwiseClone(); }
        public override string Validate()
        {
            return ValidateIdentity() ?? (cylinder < 0 || cylinder > 15 || valve < 0 || valve > 7
                || !EngineBehaviorSpec.Range(severity, 0, 1)
                || !MechanicsSchema.Finite(wearRemainder) || wearRemainder < 0
                || !Enum.IsDefined(typeof(ElementFault), fault) ? "Invalid valve element." : null);
        }
    }

    [Serializable]
    public sealed class WheelPartElement : PartElement
    {
        public int corner;
        public ElementFault fault;
        public float severity;
        public double wearRemainder;

        public override PartElement Copy() { return (WheelPartElement)MemberwiseClone(); }
        public override string Validate()
        {
            return ValidateIdentity() ?? (corner < 0 || corner > 3
                || !EngineBehaviorSpec.Range(severity, 0, 1)
                || !MechanicsSchema.Finite(wearRemainder) || wearRemainder < 0
                || !Enum.IsDefined(typeof(ElementFault), fault) ? "Invalid wheel element." : null);
        }
    }

    [Serializable]
    public sealed class TirePartElement : PartElement
    {
        public int corner;
        public bool failed;
        public string failureReason = "";
        public double temperatureC = 20, slipEnergyJ, distanceM, lastContactWorkJ, consumedLife, runningSeconds;

        public override PartElement Copy() { return (TirePartElement)MemberwiseClone(); }

        public override string Validate()
        {
            if (ValidateIdentity() != null || corner < 0 || corner > 3) return "Invalid tire element identity or position.";
            return ToLegacy().Validate();
        }

        // Lossless adapter to existing persistence during migration.
        public static TirePartElement FromLegacy(TireCondition tire, int corner)
        {
            if (tire == null || tire.Validate() != null || corner < 0 || corner > 3)
                throw new ArgumentException("Invalid legacy tire or corner.");
            return new TirePartElement
            {
                id = tire.id, definitionId = tire.definitionId, health = tire.health,
                corner = corner, failed = tire.failed, failureReason = tire.failureReason,
                temperatureC = tire.temperatureC, slipEnergyJ = tire.slipEnergyJ,
                distanceM = tire.distanceM, lastContactWorkJ = tire.lastContactWorkJ,
                consumedLife = tire.consumedLife, runningSeconds = tire.runningSeconds
            };
        }

        public TireCondition ToLegacy()
        {
            return new TireCondition
            {
                id = id, definitionId = definitionId, health = health,
                failed = failed, failureReason = failureReason,
                temperatureC = temperatureC, slipEnergyJ = slipEnergyJ,
                distanceM = distanceM, lastContactWorkJ = lastContactWorkJ,
                consumedLife = consumedLife, runningSeconds = runningSeconds
            };
        }
    }

    [Serializable]
    public sealed class PartElementSet
    {
        // Unity stores derived types, unlike its ordinary inline class arrays.
        // Keep this isolated until all save/restore paths are upgraded together.
        [SerializeReference] public PartElement[] elements = new PartElement[0];

        public PartElementSet Copy()
        {
            return new PartElementSet { elements = Array.ConvertAll(elements, e => e == null ? null : e.Copy()) };
        }

        public string Validate()
        {
            if (elements == null || elements.Length > 128) return "Invalid part element collection.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in elements)
                if (e == null || e.Validate() != null || !ids.Add(e.id))
                    return "Invalid or duplicate part element.";
            return null;
        }

        public static PartElementSet FromLegacyTires(TireCondition[] tires)
        {
            if (tires == null || tires.Length != 4) throw new ArgumentException("Four legacy tires are required.");
            var result = new PartElementSet { elements = new PartElement[4] };
            for (int i = 0; i < 4; i++) result.elements[i] = TirePartElement.FromLegacy(tires[i], i);
            if (result.Validate() != null) throw new ArgumentException("Invalid legacy tire identities.");
            return result;
        }

        public TireCondition[] ToLegacyTires()
        {
            if (elements == null || elements.Length != 4) throw new InvalidOperationException("Four tire elements are required.");
            var result = new TireCondition[4];
            foreach (var part in elements)
            {
                var tire = part as TirePartElement;
                if (tire == null || tire.Validate() != null || result[tire.corner] != null)
                    throw new InvalidOperationException("Tire element type or position mismatch.");
                result[tire.corner] = tire.ToLegacy();
            }
            for (int i = 0; i < 4; i++)
                if (result[i] == null) throw new InvalidOperationException("Missing tire position.");
            return result;
        }
    }
}
