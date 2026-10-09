using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragLab.Domain
{
    public struct EngineDynoSample
    {
        public int tick, gear;
        public double seconds, rpm, torqueNm, horsepower, shaftTorqueNm, throttle, clutch, brake, load, boostBar;
        public bool shiftCut, limiter;
        public double flywheelInertiaKgM2, engineRotationalInertiaKgM2, flywheelEnergyJ, flywheelAngularMomentum;
        public static EngineDynoSample From(int tick, CarState s, double load)
        {
            return new EngineDynoSample { tick = tick, seconds = tick * DragMath.FixedStep,
                rpm = s.rpm, torqueNm = s.engineTorqueNm, horsepower = s.engineHorsepower,
                shaftTorqueNm = s.transmittedTorqueNm, gear = s.gear, throttle = s.throttle,
                clutch = s.clutch, brake = s.brake, load = load, boostBar = s.boostBar,
                shiftCut = s.transmissionTorqueCut || (s.shiftRemaining > 0 && s.gear > 0 && s.transmittedTorqueNm == 0), limiter = s.limiter,
                flywheelInertiaKgM2 = s.flywheelInertiaKgM2, engineRotationalInertiaKgM2 = s.engineRotationalInertiaKgM2,
                flywheelEnergyJ = s.flywheelEnergyJ, flywheelAngularMomentum = s.flywheelAngularMomentum };
        }
    }
    public sealed class EngineDynoRun
    {
        public readonly int id;
        public readonly string label;
        public readonly EngineBenchConfig config;
        readonly List<EngineDynoSample> samples = new List<EngineDynoSample>();
        public IReadOnlyList<EngineDynoSample> Samples => samples;
        public bool visible = true;
        public string endedBy { get; internal set; }
        public bool Complete => endedBy != null;
        public double MaxRpm { get; private set; }
        public double MaxHorsepower { get; private set; }
        public double MaxEngineNm { get; private set; }
        public double MaxShaftNm { get; private set; }
        public double MinShaftNm { get; private set; }
        internal EngineDynoRun(int id, string label, EngineBenchConfig config)
        { this.id = id; this.label = label; this.config = config.Copy(); }
        internal void Add(EngineDynoSample sample)
        {
            samples.Add(sample);
            MaxRpm = Math.Max(MaxRpm, sample.rpm); MaxHorsepower = Math.Max(MaxHorsepower, sample.horsepower);
            MaxEngineNm = Math.Max(MaxEngineNm, sample.torqueNm); MaxShaftNm = Math.Max(MaxShaftNm, sample.shaftTorqueNm);
            MinShaftNm = Math.Min(MinShaftNm, sample.shaftTorqueNm);
        }
        public string Csv()
        {
            var text = new StringBuilder("tick,seconds,rpm,engine_nm,engine_hp,shaft_nm,gear,throttle,clutch,brake,dyno_load,boost_bar,shift_cut,limiter,flywheel_inertia_kg_m2,total_engine_inertia_kg_m2,flywheel_energy_j,flywheel_angular_momentum_kg_m2_s\n");
            foreach (var s in samples)
            {
                text.Append(s.tick).Append(',');
                foreach (double value in new[] { s.seconds, s.rpm, s.torqueNm, s.horsepower, s.shaftTorqueNm, (double)s.gear, s.throttle, s.clutch, s.brake, s.load, s.boostBar })
                    text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                text.Append(s.shiftCut ? 1 : 0).Append(',').Append(s.limiter ? 1 : 0);
                foreach (double value in new[] { s.flywheelInertiaKgM2, s.engineRotationalInertiaKgM2, s.flywheelEnergyJ, s.flywheelAngularMomentum })
                    text.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
                text.Append('\n');
            }
            return text.ToString();
        }
    }
    // Full fixed-tick, chronological traces. RPM is not a sorting key: a lift,
    // downshift, cut or repeated RPM must remain part of the same connected path.
    public sealed class EngineBenchDyno
    {
        public const int HistoryLimit = 6, SegmentTicks = 120 * 600;
        readonly List<EngineDynoRun> history = new List<EngineDynoRun>();
        public IReadOnlyList<EngineDynoRun> History => history;
        public EngineDynoRun Active { get; private set; }
        public int Tick { get; private set; }
        int nextId;
        public void Begin(string label, EngineBenchConfig config, CarState state, double load)
        {
            Finish("new run"); Tick = 0;
            Active = new EngineDynoRun(++nextId, label, config);
            Active.Add(EngineDynoSample.From(0, state, load));
        }
        public void Sample(CarState state, double load)
        {
            if (Active == null) return;
            Active.Add(EngineDynoSample.From(++Tick, state, load));
            if (Tick < SegmentTicks) return;
            // Bounded capture segments preserve their beginning and end. Never
            // silently turn the live graph into a rolling window.
            string label = Active.label; var config = Active.config;
            Finish("10 minute segment"); Begin(label, config, state, load);
        }
        public void Finish(string reason)
        {
            if (Active == null) return;
            Active.endedBy = reason;
            if (Active.Samples.Count > 1) history.Add(Active);
            while (history.Count > HistoryLimit) history.RemoveAt(0);
            Active = null;
        }
        public void ClearHistory() { history.Clear(); }
    }
}
