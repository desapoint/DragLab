using System.Collections.Generic;
using DragLab.Domain;

namespace DragLab.Editor
{
    // Diagnostic adapter retains every event for JSON ledgers; runtime uses bounded history.
    public sealed class BackfireMixturePreview : ExhaustMixtureModel
    {
        public readonly List<Event> events = new List<Event>();
        public BackfireMixturePreview(long master, string id, double lengthM = 2.5, double radiusM = .032, int count = 8, double initialK = 650,
            double stoichOxygenPerFuel = OxygenPerFuel, double heatingJPerKg = HeatJPerKg)
            : base(master, id, lengthM, radiusM, count, initialK, stoichOxygenPerFuel, heatingJPerKg) { }
        public new void Step(Input input)
        {
            base.Step(input);
            while (events.Count < Sequence)
            {
                if (!Read(events.Count + 1, out var value)) throw new System.InvalidOperationException("Preview event history overflow.");
                events.Add(value);
            }
        }
    }
}
