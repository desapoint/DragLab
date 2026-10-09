using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragLab.Domain
{
    public sealed class TuneTrial
    {
        public string label, vehicleId;
        public Tune tune;
        public bool tractionControl;
        public BenchmarkResult result;
    }
    public static class TuneExperiments
    {
        // Test one adjustment at a time against a captured baseline with identical driver assists.
        public static List<TuneTrial> Sweep(VehicleSpec spec, Tune baseline, bool tractionControl)
        {
            var trials = new List<TuneTrial>();
            Add(trials, spec, "baseline", baseline, tractionControl);
            for (int i = 0; i < 5; i++)
            {
                var tune = baseline.Copy(); tune.launchRpm = 1500 + (spec.redlineRpm - 1800) * i / 4;
                Add(trials, spec, "launch-" + i, tune, tractionControl);
            }
            foreach (float drive in new[] { 2f, 2.8f, 3.4f, 4.1f, 4.8f, 5.5f, 6f })
            {
                var tune = baseline.Copy(); tune.finalDrive = drive;
                Add(trials, spec, "gearing-" + drive.ToString("F1", CultureInfo.InvariantCulture), tune, tractionControl);
            }
            return trials;
        }
        static void Add(List<TuneTrial> trials, VehicleSpec spec, string label, Tune source, bool tractionControl)
        {
            var tune = source.Copy(); tune.ClampTo(spec);
            trials.Add(new TuneTrial { label = label, vehicleId = spec.id, tune = tune, tractionControl = tractionControl,
                result = Benchmark.QuarterMile(spec, tune, new DriverSettings { tractionAssist = tractionControl }) });
        }
        public static string Csv(List<TuneTrial> trials)
        {
            var csv = new StringBuilder("vehicle,trial,launch_rpm,final_drive,pressure_psi,engine_stage,tire_stage,weight_stage,gearbox_stage,traction_control,quarter_s,trap_kmh,delta_s\n");
            if (trials == null || trials.Count == 0) return csv.ToString();
            double baseline = trials[0].result.elapsed;
            foreach (var trial in trials)
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:F0},{3:F2},{4:F1},{5},{6},{7},{8},{9},{10:F4},{11:F2},{12:F4}\n",
                    trial.vehicleId, trial.label, trial.tune.launchRpm, trial.tune.finalDrive, trial.tune.tirePressurePsi, trial.tune.engineLevel,
                    trial.tune.tireLevel, trial.tune.weightLevel, trial.tune.gearboxLevel, trial.tractionControl ? 1 : 0,
                    trial.result.elapsed, trial.result.trapKmh, trial.result.elapsed - baseline);
            return csv.ToString();
        }
    }
}
