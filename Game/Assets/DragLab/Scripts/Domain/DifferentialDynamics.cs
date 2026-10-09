using System;

namespace DragLab.Domain
{
    public enum DifferentialKind { Open, LimitedSlip, Welded, Spool }
    [Serializable] public sealed class DifferentialSpec
    {
        public DifferentialKind kind;
        public float torqueBiasRatio = 1, preloadNm;
        public string Validate() { return Enum.IsDefined(typeof(DifferentialKind), kind) && EngineBehaviorSpec.Range(torqueBiasRatio, 1, 10) && EngineBehaviorSpec.Range(preloadNm, 0, 1000) ? null : "Invalid differential specification."; }
        public DifferentialSpec Copy() { return (DifferentialSpec)MemberwiseClone(); }
    }
    public struct DifferentialResult { public double capacity, leftRequested, rightRequested, leftSlip, rightSlip; }
    // A quasistatic left/right torque allocation, not independent angular-speed contact.
    // Uniform grip preserves the previous axle calibration. Split-grip exposes real
    // open/LSD/locked force differences without adding a fictitious grip multiplier.
    public static class DifferentialDynamics
    {
        public static DifferentialResult Solve(DifferentialSpec data, double requested, double leftGrip, double rightGrip, double radius, double health = 1)
        {
            leftGrip = Math.Max(0, leftGrip); rightGrip = Math.Max(0, rightGrip);
            double sum = leftGrip + rightGrip, weak = Math.Min(leftGrip, rightGrip), strong = Math.Max(leftGrip, rightGrip);
            bool locked = data.kind == DifferentialKind.Welded || data.kind == DifferentialKind.Spool;
            double bias = data.kind == DifferentialKind.LimitedSlip ? 1 + (data.torqueBiasRatio - 1) * health : 1;
            double preloadForce = data.kind == DifferentialKind.LimitedSlip ? data.preloadNm * health / Math.Max(.05, radius) : 0;
            double high = Math.Min(strong, weak * bias + preloadForce);
            double cap = locked ? sum : weak + high;
            double leftShare = locked ? (sum > 0 ? leftGrip / sum : .5) : cap > 0 ? (leftGrip <= rightGrip ? weak : high) / cap : .5;
            double left = requested * leftShare, right = requested - left;
            double ls = Slip(left, leftGrip), rs = Slip(right, rightGrip);
            if (locked) ls = rs = Slip(requested, sum);
            return new DifferentialResult { capacity = cap, leftRequested = left, rightRequested = right, leftSlip = ls, rightSlip = rs };
        }
        static double Slip(double demand, double cap) { return cap <= 0 ? (demand > 0 ? 1.5 : 0) : DragMath.Clamp(Math.Max(0, demand / cap - 1) * .4, 0, 1.5); }
        public static double Grip(float value) { return DragMath.Clamp(value, 0, 1.5); }
    }
}
