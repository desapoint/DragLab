using System;
using System.Collections.Generic;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    [Serializable]
    public class PassRecord
    {
        public double distanceM, elapsedSeconds, reactionSeconds, trapKmh, sixtyFootSeconds, eighthSeconds;
        public Tune tune;
        public DriverSettings driving;
        public static PassRecord Capture(RaceSimulation race)
        {
            var state = race.player.state;
            if (!state.finished || state.disqualified) return null;
            state.splits.TryGetValue(18.288, out double sixty); state.splits.TryGetValue(201.168, out double eighth);
            return new PassRecord { distanceM = race.distance, elapsedSeconds = state.elapsed, reactionSeconds = state.reaction, trapKmh = state.trapSpeed,
                sixtyFootSeconds = sixty, eighthSeconds = eighth, tune = race.player.tune.Copy(), driving = race.player.driver.Copy() };
        }
        public string Compare(PassRecord previous, UnitPreferences units = null)
        {
            if (previous == null) return "First recorded pass for this car.";
            if (Math.Abs(distanceM - previous.distanceM) > .01) return "Previous pass used a different distance; no time comparison.";
            double delta = elapsedSeconds - previous.elapsedSeconds;
            return "Previous pass: ET " + Math.Abs(delta).ToString("F3") + " s " + (delta < 0 ? "faster" : delta > 0 ? "slower" : "unchanged") +
                " / trap " + DisplayUnits.Speed((trapKmh - previous.trapKmh) / 3.6, (units ?? new UnitPreferences()).speed).ToString("+0.0;-0.0;0.0") + " " + DisplayUnits.SpeedLabel((units ?? new UnitPreferences()).speed);
        }
    }
    [Serializable]
    public class OwnedCar
    {
        public string instanceId, chassisId, engineAssemblyId, garageJobId;
        public InstalledBuild installed;
        public FuelState fuel = new FuelState();
        public bool fuelInitialized;
        public FluidState fluids;
        public int serviceHardwareVersion;
        public long thermalUtcTicks;
        public EngineRebuildJob engineRebuild;
        public int massVersion;
        public string vehicleId;
        public Tune tune = new Tune();
        public string paintHex = "#58C7BA";
        public List<Tune> presets = new List<Tune>();
        public double bestQuarterSeconds, bestSixtyFootSeconds, bestEighthSeconds, bestTrapKmh;
        public double bestReactionSeconds = -1;
        public int completedRuns;
        public PassRecord lastPass;
        public void Record(RaceSimulation race)
        {
            var s = race.player.state;
            if (!s.finished || s.disqualified) return;
            completedRuns++;
            lastPass = PassRecord.Capture(race);
            if (bestReactionSeconds < 0 || s.reaction < bestReactionSeconds) bestReactionSeconds = s.reaction;
            bestTrapKmh = Math.Max(bestTrapKmh, s.trapSpeed);
            if (Math.Abs(race.distance - 402.336) < .01) Best(ref bestQuarterSeconds, s.elapsed);
            if (s.splits.TryGetValue(18.288, out double sixty)) Best(ref bestSixtyFootSeconds, sixty);
            if (s.splits.TryGetValue(201.168, out double eighth)) Best(ref bestEighthSeconds, eighth);
        }
        static void Best(ref double previous, double value) { if (previous <= 0 || value < previous) previous = value; }
        public void SavePreset(int slot)
        {
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException("slot");
            while (presets.Count <= slot) presets.Add(null);
            presets[slot] = tune.Copy();
        }
        public bool LoadPreset(int slot)
        {
            if (slot < 0 || slot >= presets.Count || presets[slot] == null) return false;
            // Presets change adjustments, never installed parts or purchases.
            tune.ApplyAdjustments(presets[slot]);
            return true;
        }
    }

    [Serializable]
    public class PlayerProfile
    {
        public int mechanicsVersion;
        public long revision;
        public string selectedOwnedVehicleId;
        public List<PartInstance> parts = new List<PartInstance>();
        public List<LooseEngineElement> looseEngineElements=new List<LooseEngineElement>();
        public List<SavedVehicleSetup> savedSetups = new List<SavedVehicleSetup>();
        public List<EngineAssemblyInstance> engines = new List<EngineAssemblyInstance>();
        public List<RunCheckpoint> runCheckpoints = new List<RunCheckpoint>();
        public int economyVersion;
        public long lastEconomyUtcTicks;
        public List<EconomyReceipt> economyReceipts = new List<EconomyReceipt>();
        public RecoveryWork recoveryWork;
        public GarageBusinessState business;
        [NonSerialized] public MechanicalCatalog catalog;
        public int saveVersion = 1;
        public int drivelineVersion;
        public int equipmentVersion;
        public int cash = 1200;
        public int reputation;
        public int garageLevel = 1;
        public string selectedVehicleId = "starter-hatch";
        public long idleClaimUtcTicks;
        public List<OwnedCar> cars = new List<OwnedCar>();
        public List<string> careerWins = new List<string>();
        public List<CareerRun> careerRuns = new List<CareerRun>();
        public double bestQuarterSeconds;
        public DriverSettings driving = new DriverSettings();
        public int difficulty = 1;

        public static PlayerProfile New(long utcTicks)
        {
            var p = new PlayerProfile { idleClaimUtcTicks = utcTicks, business=new GarageBusinessState{version=1,accruedThroughUtcTicks=utcTicks,ratePerHour=100,capHours=8} };
            p.cars.Add(new OwnedCar { vehicleId = "starter-hatch" });
            return p;
        }
        public PlayerProfile RestartKeepingPreferences(long utcTicks)
        {
            var fresh = New(utcTicks);
            fresh.driving = driving == null ? new DriverSettings() : driving.Copy();
            fresh.difficulty = difficulty;
            // Old queued inventory actions must remain stale after the reset.
            fresh.revision = checked(revision + 1);
            return fresh;
        }
        public OwnedCar SelectedCar { get { return cars.Find(c => c.instanceId == selectedOwnedVehicleId && c.vehicleId == selectedVehicleId) ?? cars.Find(c => c.vehicleId == selectedVehicleId); } }
        public int IdleRate { get { return new[] { 100, 180, 300, 500 }[garageLevel - 1]; } }
        public int PendingIdle(long utcTicks) { return GarageBusiness.Pending(this,GarageBusiness.DefaultPolicy(),utcTicks); }
        public int ClaimIdle(long utcTicks) { return GarageBusiness.Claim(this,GarageBusiness.DefaultPolicy(),utcTicks,false); }
        public bool Upgrade(string category)
        {
            if (mechanicsVersion > 0) return catalog != null && MechanicsOwnership.LegacyUpgrade(this, category);
            var car = SelectedCar;
            if (car == null) return false;
            int level;
            switch (category)
            {
                case "engine": level = car.tune.engineLevel; break;
                case "tires": level = car.tune.tireLevel; break;
                case "weight": level = car.tune.weightLevel; break;
                case "gearbox": level = car.tune.gearboxLevel; break;
                case "exhaust": level = car.tune.exhaustLevel; break;
                case "controls": level = car.tune.controlLevel; break;
                default: return false;
            }
            int cost = UpgradeCost(category, level);
            if (level >= (category == "controls" ? 1 : 3) || cash < cost) return false;
            cash -= cost;
            switch (category)
            {
                case "engine": car.tune.engineLevel++; break;
                case "tires": car.tune.tireLevel++; break;
                case "weight": car.tune.weightLevel++; break;
                case "gearbox": car.tune.gearboxLevel++; break;
                case "exhaust": car.tune.exhaustLevel++; break;
                case "controls": car.tune.controlLevel++; break;
            }
            return true;
        }
        public static int UpgradeCost(string category, int level)
        {
            int basic = category == "engine" ? 600 : category == "tires" ? 400 : category == "weight" ? 500 : category == "exhaust" ? 250 : category == "controls" ? 800 : 450;
            return basic * (level + 1);
        }
        public void AwardRace(bool win, string eventId, int winCash, int lossCash, int winRep)
        {
            cash += win ? winCash : lossCash;
            if (win && !string.IsNullOrEmpty(eventId) && !careerWins.Contains(eventId))
            {
                careerWins.Add(eventId);
                reputation += winRep;
            }
        }
        public void Sanitize(long now)
        {
            if (saveVersion < 1 || saveVersion > MechanicsOwnership.SaveVersion) throw new ArgumentException("Unsupported save version: " + saveVersion);
            cash = Math.Max(0, cash);
            reputation = Math.Max(0, reputation);
            garageLevel = (int)DragMath.Clamp(garageLevel, 1, 4);
            if (idleClaimUtcTicks <= 0) idleClaimUtcTicks = now;
            if (careerWins == null) careerWins = new List<string>();
            if (saveVersion >= 2 && (cars == null || cars.Count == 0 && saveVersion < 5 || cars.Exists(c => c == null || string.IsNullOrEmpty(c.vehicleId)))) throw new ArgumentException("Invalid owned vehicle list.");
            if (cars == null || cars.Count == 0 && saveVersion < 5) cars = New(now).cars;
            cars.RemoveAll(c => c == null || string.IsNullOrEmpty(c.vehicleId));
            if (cars.Count == 0 && saveVersion < 5) cars = New(now).cars;
            foreach (var c in cars)
            {
                if (c.tune == null) c.tune = new Tune();
                if (c.presets == null) c.presets = new List<Tune>();
            }
            if (driving == null) driving = new DriverSettings();
            if (!Enum.IsDefined(typeof(ShiftMode), driving.shifting)) driving.shifting = ShiftMode.Automatic;
            if (!Enum.IsDefined(typeof(LaunchMode), driving.launch)) driving.launch = LaunchMode.Automatic;
            if (!Enum.IsDefined(typeof(WearMode), driving.wearMode)) driving.wearMode = WearMode.Full;
            if (!Enum.IsDefined(typeof(FuelUsageMode), driving.fuelUsage)) driving.fuelUsage = FuelUsageMode.Realistic;
            if(!Enum.IsDefined(typeof(ConsumptionMode),driving.fluidDepletion))driving.fluidDepletion=ConsumptionMode.Realistic;
            if(!Enum.IsDefined(typeof(ConsumptionMode),driving.fluidAging))driving.fluidAging=ConsumptionMode.Realistic;
            if(!Enum.IsDefined(typeof(ConditionEffectMode),driving.conditionEffects))driving.conditionEffects=ConditionEffectMode.Full;
            if(!Enum.IsDefined(typeof(ServiceAssist),driving.serviceAssist))driving.serviceAssist=ServiceAssist.Manual;
            if (!Enum.IsDefined(typeof(SimulationDifficulty), driving.simulationDifficulty)) driving.simulationDifficulty = SimulationDifficulty.Custom;
            driving.wearMultiplier = float.IsNaN(driving.wearMultiplier) || float.IsInfinity(driving.wearMultiplier) ? 1 : (float)DragMath.Clamp(driving.wearMultiplier, 0, 1000);
            driving.reactionSeconds = .18;
            driving.clutchReleaseSeconds = (float)DragMath.Clamp(driving.clutchReleaseSeconds, .02, .6);
            driving.soundVolume = (float)DragMath.Clamp(driving.soundVolume, 0, 1);
            difficulty = (int)DragMath.Clamp(difficulty, 0, 2);
            if (SelectedCar == null && cars.Count > 0) selectedVehicleId = cars[0].vehicleId;
        }
    }

    public sealed class BenchmarkResult
    {
        public double elapsed;
        public double trapKmh;
        public string performanceClass;
        public int points;
    }

    public static class Benchmark
    {
        public static BenchmarkResult QuarterMile(VehicleSpec spec, Tune tune, DriverSettings driver = null)
        {
            var car = new CarSimulation(spec, tune, driver ?? new DriverSettings());
            double stagingTime = 0;
            while (stagingTime - RaceSimulation.GreenAt < car.driver.reactionSeconds)
            {
                stagingTime += DragMath.FixedStep; car.Stage(DragMath.FixedStep, new DriverInput());
            }
            car.Launch(0);
            double time = 0;
            while (car.state.distance < 402.336 && time < 90)
            {
                double before = car.state.distance;
                car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                time += DragMath.FixedStep;
                if (car.state.distance >= 402.336)
                    time -= DragMath.FixedStep * (1 - DragMath.Clamp((402.336 - before) / Math.Max(.000001, car.state.distance - before), 0, 1));
            }
            string cls = time > 18 ? "F" : time > 16.5 ? "E" : time > 15 ? "D" : time > 13.5 ? "C" : time > 12 ? "B" : time > 10.5 ? "A" : "S";
            return new BenchmarkResult { elapsed = time, trapKmh = car.state.speed * 3.6, performanceClass = cls,
                points = (int)DragMath.Clamp((22 - time) * 60, 0, 999) };
        }
    }
}
