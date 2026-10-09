using System;

namespace DragLab.Domain
{
    public enum ExhaustPartKind { Pipe, AbsorptiveSilencer, ChamberedSilencer, Resonator, CatalyticConverter }

    [Serializable]
    public sealed class ExhaustPart
    {
        public ExhaustPartKind kind;
        public float amount = 1;
        public float attenuationDb;
        public float lowPassHz = 12000;
        public float pulseSoftening;
        public float resonanceHz = 180, resonanceGainDb, resonanceQ = 1;
        public ExhaustPart Copy() { return (ExhaustPart)MemberwiseClone(); }
        public static ExhaustPart Create(ExhaustPartKind kind)
        {
            var p = new ExhaustPart { kind = kind };
            switch (kind)
            {
                case ExhaustPartKind.AbsorptiveSilencer: p.attenuationDb = 5; p.lowPassHz = 2200; p.pulseSoftening = .45f; break;
                case ExhaustPartKind.ChamberedSilencer: p.attenuationDb = 4; p.lowPassHz = 2800; p.resonanceGainDb = 3; p.resonanceQ = 1.3f; p.pulseSoftening = .2f; break;
                case ExhaustPartKind.Resonator: p.attenuationDb = .5f; p.resonanceHz = 160; p.resonanceGainDb = -9; p.resonanceQ = 2; break;
                case ExhaustPartKind.CatalyticConverter: p.attenuationDb = 1.5f; p.lowPassHz = 4500; p.pulseSoftening = .15f; break;
            }
            return p;
        }
        public string Validate()
        {
            if (!Enum.IsDefined(typeof(ExhaustPartKind), kind) || !Range(amount, 0, 1) || !Range(attenuationDb, 0, 30) || !Range(lowPassHz, 100, 18000) ||
                !Range(pulseSoftening, 0, 1) || !Range(resonanceHz, 40, 5000) || !Range(resonanceGainDb, -24, 12) || !Range(resonanceQ, .25, 8)) return "Invalid exhaust component acoustics.";
            return null;
        }
        static bool Range(float x, double lo, double hi) { return !float.IsNaN(x) && !float.IsInfinity(x) && x >= lo && x <= hi; }
    }

    [Serializable]
    public sealed class ExhaustSetup
    {
        public int version;
        public ExhaustPart[] parts = new ExhaustPart[0];
        public ExhaustSetup Copy()
        {
            var copy = new ExhaustSetup { version = version, parts = new ExhaustPart[parts == null ? 0 : parts.Length] };
            for (int i = 0; i < copy.parts.Length; i++) copy.parts[i] = parts[i] == null ? null : parts[i].Copy();
            return copy;
        }
        public string Validate()
        {
            if (parts == null || parts.Length > 8) return "Use zero to eight exhaust components.";
            foreach (var part in parts) if (part == null || part.Validate() != null) return "Invalid exhaust component.";
            return null;
        }
        public static ExhaustSetup Preset(int index)
        {
            if (index == 3) return new ExhaustSetup { version = 1 }; // Open acoustic path.
            return new ExhaustSetup { version = 1, parts = index == 0 ? new[] {
                ExhaustPart.Create(ExhaustPartKind.CatalyticConverter), ExhaustPart.Create(ExhaustPartKind.Resonator), ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer) } : index == 1 ? new[] {
                ExhaustPart.Create(ExhaustPartKind.CatalyticConverter), ExhaustPart.Create(ExhaustPartKind.ChamberedSilencer) } : new[] { ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer) } };
        }
    }

    // Allocated only when hardware is captured. Coefficients change only on sample-rate changes.
    // Low-pass, second-pole pulse softening and a peaking EQ approximate each part's acoustic transfer.
    public sealed class ExhaustFilter
    {
        sealed class Node
        {
            public readonly ExhaustPart part;
            public readonly double gain;
            public double low, softer, x1, x2, y1, y2, filter, b0, b1, b2, a1, a2;
            public Node(ExhaustPart part, int stage)
            {
                this.part = part.Copy();
                this.part.attenuationDb /= 1 + stage * .3f;
                this.part.lowPassHz *= 1 + stage * .15f;
                gain = Math.Pow(10, -this.part.attenuationDb * part.amount / 20);
            }
            public void Configure(int rate)
            {
                filter = 1 - Math.Exp(-2 * Math.PI * Math.Min(rate * .42, part.lowPassHz) / rate);
                double w = 2 * Math.PI * Math.Min(rate * .42, part.resonanceHz) / rate;
                double alpha = Math.Sin(w) / (2 * part.resonanceQ), a = Math.Pow(10, part.resonanceGainDb * part.amount / 40);
                double normal = 1 + alpha / a;
                b0 = (1 + alpha * a) / normal; b1 = -2 * Math.Cos(w) / normal; b2 = (1 - alpha * a) / normal;
                a1 = b1; a2 = (1 - alpha / a) / normal;
            }
            public double Process(double input)
            {
                low += filter * (input - low); softer += filter * (low - softer);
                double shaped = input + part.amount * (low + part.pulseSoftening * (softer - low) - input);
                double output = b0 * shaped + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = shaped; y2 = y1; y1 = output;
                return output * gain;
            }
            public void Clear() { low = softer = x1 = x2 = y1 = y2 = 0; }
        }
        readonly Node[] nodes;
        int rate;
        public ExhaustFilter(ExhaustSetup setup, int stage)
        {
            var parts = setup == null || setup.parts == null ? new ExhaustPart[0] : setup.parts;
            nodes = new Node[parts.Length];
            for (int i = 0; i < parts.Length; i++) nodes[i] = new Node(parts[i], stage);
        }
        public void Configure(int sampleRate)
        {
            if (rate == sampleRate) return;
            rate = sampleRate; foreach (var node in nodes) node.Configure(rate);
        }
        public double Process(double value) { foreach (var node in nodes) if (node.part.amount > 0) value = node.Process(value); return value; }
        public void Clear() { foreach (var node in nodes) node.Clear(); }
    }
}
