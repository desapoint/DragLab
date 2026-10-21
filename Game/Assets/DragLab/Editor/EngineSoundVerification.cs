using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using UnityEngine;
using DragLab.Domain;
using DragLab.Data;

namespace DragLab.Editor
{
    public static class EngineSoundVerification
    {
        public static void RunStandalone()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Debug.Log("Engine audio checks: " + Run(root));
            Debug.Log("Shared audio checks: " + SharedAudioVerification.Run(root));
            Debug.Log("Exhaust-pop checks: " + ExhaustPopVerification.Run(root));
            Debug.Log("Induction checks: " + InductionVerification.Run(root));
            Debug.Log("Bench/exhaust checks: " + EngineBenchVerification.Run(root));
            Debug.Log("Engine concert checks: " + EngineMusicVerification.Run(root));
        }

        static List<string> failures;
        static void Check(bool ok, string message, ref int count) { if(!ok){if(failures==null)throw new InvalidOperationException("AUDIO CHECK FAILED: "+message);failures.Add(message);}count++; }
        static double Rms(float[] data) { double sum = 0; foreach (float v in data) sum += v * v; return Math.Sqrt(sum / data.Length); }
        static double Difference(float[] a, float[] b) { double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]); return sum / a.Length; }
        static double TailDifference(float[] a, float[] b) { double sum = 0; for (int i = a.Length / 2; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]); return sum / (a.Length - a.Length / 2); }
        static double TonePower(float[] data, double hz)
        {
            double sine = 0, cosine = 0; int start = data.Length / 2;
            for (int i = start; i < data.Length; i++) { double phase = 2 * Math.PI * hz * i / 44100; sine += data[i] * Math.Sin(phase); cosine += data[i] * Math.Cos(phase); }
            return (sine * sine + cosine * cosine) / Math.Pow(data.Length - start, 2);
        }
        static void BandEnergy(float[] data, int start, int end, out double lower, out double upper)
        {
            double low = 0; lower = upper = 0;
            double filter = 1 - Math.Exp(-2 * Math.PI * 500 / 44100);
            for (int i = start; i < end; i++) { low += filter * (data[i] - low); lower += low * low; upper += Math.Pow(data[i] - low, 2); }
            lower /= end - start; upper /= end - start;
        }
        static EngineSoundFrame PhysicsAudioUpdate(EngineSoundController controller, InductionProfile profile, CarState state, bool active, double volume, double dt, double load = 1)
        {
            if (active)
            {
                for (int i = 0; i < Math.Round(dt / DragMath.FixedStep); i++) InductionDynamics.Step(profile, state, state.throttle, load, state.shiftRemaining > 0, false, DragMath.FixedStep);
                state.transmittedTorqueNm = state.engineTorqueNm * load;
            }
            return controller.Update(state, active, volume, dt);
        }
        static float[] Steady(VehicleSpec spec, Tune tune, double rpm = 3000, double load = .9, double boost = 0, bool cut = false)
        {
            var synth = new EngineSoundSynth(new EngineSoundProfile(spec, tune)); var data = new float[44100];
            synth.Fill(data, 1, 44100, new EngineSoundFrame(rpm, .9, load, boost, .85, cut: cut)); return data;
        }
        public static int Run(string root)
        {
            int count = 0;
            failures=new List<string>();
            string directory = Path.Combine(root, "reports/audio-previews"); Directory.CreateDirectory(directory);
            var report = new StringBuilder("Original generated PCM auditions: 44.1 kHz mono / 8 seconds: idle, pull, shift, lift.\n");
            foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var timer = Stopwatch.StartNew(); var samples = EngineSoundAudition.Render(car.spec, car.CreateStockTune()); timer.Stop();
                double peak = 0; bool finite = true;
                foreach (float v in samples) { peak = Math.Max(peak, Math.Abs(v)); if (float.IsNaN(v) || float.IsInfinity(v)) finite = false; }
                Check(finite && peak < .95 && Rms(samples) > .015, "Finite audible bounded stock audition: " + car.spec.id, ref count);
                EngineSoundAudition.WriteWav(Path.Combine(directory, car.spec.id + ".wav"), samples);
                report.AppendLine(car.spec.id + " / " + car.spec.sound.cylinders + " cyl / " + car.spec.sound.displacementLitres + " L / RMS " + Rms(samples).ToString("F4") + " / peak " + peak.ToString("F4") + " / render ms " + timer.ElapsedMilliseconds);
            }
            string idleDirectory = Path.Combine(root, "reports/idle-level-comparison"); Directory.CreateDirectory(idleDirectory);
            foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var unboosted = new EngineBenchConfig { vehicle = car.spec.Copy(), tune = car.CreateStockTune(), volume = .75f };
                unboosted.vehicle.sound.idleBoostDb = 0;
                var boosted = unboosted.Copy(); boosted.vehicle.sound.idleBoostDb = 6;
                var before = EngineBenchRenderer.Render(unboosted, EngineBenchScenario.WarmIdle);
                var after = EngineBenchRenderer.Render(boosted, EngineBenchScenario.WarmIdle);
                double peak = 0; foreach (float sample in after) peak = Math.Max(peak, Math.Abs(sample));
                Check(Rms(after) > Rms(before) * 1.6 && peak < .95, "Idle boost audibly raises combustion with bounded output: " + car.spec.id, ref count);
                Check(TailDifference(Steady(unboosted.vehicle, unboosted.tune, rpm: 4500), Steady(boosted.vehicle, boosted.tune, rpm: 4500)) < .000002,
                    "Idle boost leaves loaded revving unchanged: " + car.spec.id, ref count);
                var twoStepFrame = new EngineSoundFrame(3500, 1, .1, 0, .75, twoStep: true);
                var twoStepBefore = new float[44100]; var twoStepAfter = new float[44100];
                new EngineSoundSynth(new EngineSoundProfile(unboosted.vehicle, unboosted.tune)).Fill(twoStepBefore, 1, 44100, twoStepFrame);
                new EngineSoundSynth(new EngineSoundProfile(boosted.vehicle, boosted.tune)).Fill(twoStepAfter, 1, 44100, twoStepFrame);
                Check(Difference(twoStepBefore, twoStepAfter) == 0, "Idle boost leaves 3500 RPM two-step distinct: " + car.spec.id, ref count);
                report.AppendLine(car.spec.id + " / idle RMS before " + Rms(before).ToString("F4") + " / +6 dB " + Rms(after).ToString("F4") + " / peak " + peak.ToString("F4"));
                EngineSoundAudition.WriteWav(Path.Combine(idleDirectory, car.spec.id + "-before.wav"), before);
                EngineSoundAudition.WriteWav(Path.Combine(idleDirectory, car.spec.id + "-after.wav"), after);
            }
            Check(new EngineSoundSpec { idleBoostDb = 13 }.Validate() != null, "Invalid idle boost cannot enter a running sound profile.", ref count);
            var masterProfile = new EngineSoundProfile(new VehicleSpec(), new Tune());
            var masterFrame = new EngineSoundFrame(4000, .8, .6, 0, 1);
            var fullMaster = new float[44100]; var quarterMaster = new float[44100]; var mutedMaster = new float[44100];
            new EngineSoundSynth(masterProfile).Fill(fullMaster, 1, 44100, masterFrame, 1);
            new EngineSoundSynth(masterProfile).Fill(quarterMaster, 1, 44100, masterFrame, .25);
            new EngineSoundSynth(masterProfile).Fill(mutedMaster, 1, 44100, masterFrame, 0);
            Check(Math.Abs(Rms(quarterMaster) / Rms(fullMaster) - .25) < .000001, "Master output gain applies exactly once after saturation.", ref count);
            Check(Rms(mutedMaster) == 0, "DSP master zero produces exact silence with an active running engine.", ref count);
            var ramp = new EngineSoundSynth(masterProfile); ramp.Fill(fullMaster, 1, 44100, masterFrame, 1);
            ramp.Fill(mutedMaster, 1, 44100, masterFrame, 0);
            Check(Math.Abs(mutedMaster[0] - fullMaster[fullMaster.Length - 1]) < .015 && Math.Abs(mutedMaster[mutedMaster.Length - 1]) == 0,
                "Master mute ramps without a gain discontinuity and settles to exact zero.", ref count);
            var combustionMute = new VehicleSpec(); combustionMute.sound.engineGain = 0;
            combustionMute.engineBehavior.fueling = FuelSystem.Carburetor; combustionMute.engineBehavior.mechanicalClatter = 0;
            foreach (var frame in new[] { new EngineSoundFrame(900, 0, 0, 0, 1), new EngineSoundFrame(3500, 1, .1, 0, 1, twoStep: true),
                new EngineSoundFrame(3000, 0, .1, 0, 1, fuelHesitation: 1, overrun: true) })
            {
                var pcm = new float[44100]; new EngineSoundSynth(new EngineSoundProfile(combustionMute, new Tune())).Fill(pcm, 1, 44100, frame);
                Check(Rms(pcm) == 0, "Engine gain zero also mutes idle / two-step / hesitation-overrun combustion color at " + frame.rpm + " RPM.", ref count);
            }
            var spec = new VehicleSpec(); var tune = new Tune(); var baseline = Steady(spec, tune);
            double changeEnergy = 0; for (int i = 5000; i < baseline.Length; i++) changeEnergy += Math.Pow(baseline[i] - baseline[i - 1], 2);
            double roughness = changeEnergy / (baseline.Length - 5000) / Math.Pow(Rms(baseline), 2);
            Check(roughness < .01, "Steady combustion is harmonic rather than broad white noise.", ref count);
            report.AppendLine("Steady NA sample roughness (adjacent-difference energy / signal energy): " + roughness.ToString("F6"));
            var v8 = new VehicleSpec { sound = new EngineSoundSpec { cylinders = 8, displacementLitres = 5, layout = EngineLayout.Crossplane } };
            var older = new VehicleSpec { sound = new EngineSoundSpec { cylinders = 6, displacementLitres = 4.2f }, engineBehavior = new EngineBehaviorSpec { fueling = FuelSystem.Carburetor, camOverlap = .45f } };
            var v8Pull = Steady(v8, tune, rpm: 6000, load: 1);
            double lowV8 = TonePower(v8Pull, 50) + TonePower(v8Pull, 100) + TonePower(v8Pull, 200);
            double highV8 = TonePower(v8Pull, 400) + TonePower(v8Pull, 800) + TonePower(v8Pull, 1200);
            double offV8=TonePower(v8Pull,83)+TonePower(v8Pull,173)+TonePower(v8Pull,373);
            Check(lowV8+highV8>offV8*10, "V8 pressure energy follows crank/firing orders rather than broadband noise.", ref count);
            var carbPull = Steady(older, tune, rpm: 6000, load: 1);
            double lowCarb = TonePower(carbPull, 50) + TonePower(carbPull, 150);
            double highCarb = TonePower(carbPull, 300) + TonePower(carbPull, 600) + TonePower(carbPull, 900);
            Check(highCarb> (TonePower(carbPull,263)+TonePower(carbPull,337))*10, "Six-cylinder pressure events follow their 300 Hz firing rate at 6,000 RPM.", ref count);
            double lowFour = TonePower(Steady(spec, tune, rpm: 6000, load: 1), 50) + TonePower(Steady(spec, tune, rpm: 6000, load: 1), 100);
            double upperFour = TonePower(Steady(spec, tune, rpm: 6000, load: 1), 1000) + TonePower(Steady(spec, tune, rpm: 6000, load: 1), 1400);
            var fourPull=Steady(spec,tune,rpm:6000,load:1);
            Check(TonePower(fourPull,200)+TonePower(fourPull,400)>(TonePower(fourPull,173)+TonePower(fourPull,227))*10, "Four-cylinder pressure events follow the 200 Hz firing rate at 6,000 RPM.", ref count);
            report.AppendLine("6,000 RPM V8 low-bank/crank-to-firing-harmonic power ratio: " + (lowV8 / highV8).ToString("F3"));
            report.AppendLine("6,000 RPM carb six low-bank/crank-to-firing-harmonic power ratio: " + (lowCarb / highCarb).ToString("F3"));
            var neutralSynth = new EngineSoundSynth(new EngineSoundProfile(v8, tune)); var neutralVoice = new float[44100];
            neutralSynth.Fill(neutralVoice, 1, 44100, new EngineSoundFrame(4500, 1, .08, 0, .85));
            var idleSynth = new EngineSoundSynth(new EngineSoundProfile(v8, tune)); var idleVoice = new float[44100];
            idleSynth.Fill(idleVoice, 1, 44100, new EngineSoundFrame(4500, 0, .08, 0, .85));
            var driveVoice = Steady(v8, tune, rpm: 4500, load: 1);
            Check(Rms(neutralVoice) > Rms(idleVoice) * 1.4, "Unloaded V8 throttle produces a substantial roar without requiring drivetrain load.", ref count);
            Check(Rms(neutralVoice) > Rms(driveVoice) * .8, "V8 neutral revs and loaded driving retain a continuous intensity range.", ref count);
            var previousVoice = new float[8192]; var nextVoice = new float[8192];
            neutralSynth.Fill(previousVoice, 1, 44100, new EngineSoundFrame(4500, 1, .08, 0, .85));
            neutralSynth.Fill(nextVoice, 1, 44100, new EngineSoundFrame(4500, 1, 1, 0, .85));
            Check(Math.Abs(nextVoice[0] - previousVoice[previousVoice.Length - 1]) < .03,
                "Drivetrain load transition preserves phase with no hard voice switch/click.", ref count);
            var popSpec = new VehicleSpec { sound = new EngineSoundSpec { engineGain = 0 }, engineBehavior = new EngineBehaviorSpec { mechanicalClatter = 0 } };
            var popController = new EngineSoundController(popSpec, tune); var popSynth = new EngineSoundSynth(new EngineSoundProfile(popSpec, tune));
            var popState = new CarState { rpm = 3000, exhaustPopStrength = .6 };
            // Legacy isolated DSP contract only. Live controllers now require funded chemical events;
            // manually changing an old request counter must not fabricate a real afterfire.
            var legacyPop = new EngineSoundFrame(3000,0,0,0,.8,popId:1,popStrength:.6);
            var popPcm = new float[4096]; popSynth.Fill(popPcm, 1, 44100, legacyPop);
            bool audiblePop = Rms(popPcm) > .005;
            popSynth.Fill(popPcm, 1, 44100, new EngineSoundFrame(3000,0,0,0,0,active:false,popId:1));
            popSynth.Fill(popPcm, 1, 44100, legacyPop);
            Check(audiblePop && Rms(popPcm) == 0, "Legacy isolated pop DSP rebases on pause without replay; live chemistry pause is separately checked.", ref count);
            report.AppendLine("V8 neutral-roar/driving RMS ratio at 4,500 RPM: " + (Rms(neutralVoice) / Rms(driveVoice)).ToString("F3"));
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                if (definition.spec.id == "v8-muscle" || definition.spec.id == "utility-pickup")
                {
                    EngineSoundAudition.WriteWav(Path.Combine(directory, "deep-pull-" + definition.spec.id + ".wav"), EngineSoundAudition.Render(definition.spec, definition.CreateStockTune()));
                    if (definition.spec.id == "v8-muscle")
                    {
                        var blips = PowertrainVerification.NeutralBlips(definition.spec);
                        EngineSoundAudition.WriteWav(Path.Combine(directory, "v8-neutral-blips.wav"), blips);
                        BandEnergy(blips, 44100, 44100 * 2, out double idleLower, out double idleUpper);
                        Check(idleLower > idleUpper * 4, "Warm V8 idle retains predominantly lower-band pressure energy without suppressing the approved pulse transients.", ref count);
                        var launch = PowertrainVerification.V8TwoStep(definition.spec);
                        EngineSoundAudition.WriteWav(Path.Combine(directory, "v8-two-step-3500.wav"), launch);
                        BandEnergy(launch, 44100 * 2, 44100 * 4, out double launchLower, out double launchUpper);
                        Check(launchUpper > idleUpper * 5, "3,500 RPM ignition two-step has revved cut/crackle energy distinct from idle.", ref count);
                        report.AppendLine("V8 3,500 RPM two-step / idle upper-band energy ratio: " + (launchUpper / idleUpper).ToString("F3"));
                    }
                }
            foreach (int cylinders in new[] { 3, 4, 6, 8, 10, 12 })
            {
                spec.sound.cylinders = cylinders;
                var profile = new EngineSoundProfile(spec, tune);
                Check(Math.Abs(profile.FiringHz(3000) - cylinders * 25) < .001, "Correct four-stroke pulse count: " + cylinders, ref count);
                if (cylinders != 4) Check(Difference(baseline, Steady(spec, tune)) > .025, "Cylinder count changes generated PCM: " + cylinders, ref count);
            }
            spec.sound.cylinders = 4; spec.sound.displacementLitres = 5;
            Check(Difference(baseline, Steady(spec, tune)) > .003, "Displacement changes exhaust resonance.", ref count);
            spec.sound.displacementLitres = 1.6f;
            double lightLoadRms = Rms(Steady(spec, tune, load: .1));
            Check(Rms(baseline) > lightLoadRms * 1.05 && Rms(baseline) < lightLoadRms * 1.5,
                "Load remains audible without an excessive neutral-to-driving volume gap.", ref count);
            Check(Difference(baseline, Steady(spec, tune, rpm: 6000)) > .025, "RPM changes firing frequency.", ref count);
            Check(Rms(Steady(spec, tune, cut: true)) < Rms(baseline) * .3, "Limiter/shift cut attenuates combustion.", ref count);
            Check(Difference(baseline, Steady(spec, new Tune { exhaustLevel = 3 })) > .008, "Exhaust upgrade changes generated PCM.", ref count);
            var loaded = new CarState { rpm = 6000, throttle = 1, launched = true, transmittedTorqueNm = 200, speed = 30 };
            var turboTune = new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Turbo };
            var controller = new EngineSoundController(spec, turboTune);
            var turboProfile = new InductionProfile(spec, turboTune);
            for (int i = 0; i < 120; i++) PhysicsAudioUpdate(controller, turboProfile, loaded, true, .8, 1.0 / 60);
            Check(controller.Boost > .25 && controller.ReleaseCount == 0, "Loaded turbo spools without spontaneous releases.", ref count);
            loaded.throttle = 0;
            var release = PhysicsAudioUpdate(controller, turboProfile, loaded, true, .8, 1.0 / 60);
            Check(release.releaseId == 1 && release.releaseStrength > .4, "Charged throttle lift releases once.", ref count);
            for (int i = 0; i < 30; i++) PhysicsAudioUpdate(controller, turboProfile, loaded, true, .8, 1.0 / 60);
            Check(controller.ReleaseCount == 1, "Held lift does not retrigger valve.", ref count);
            loaded.throttle = 1;
            for (int i = 0; i < 120; i++) PhysicsAudioUpdate(controller, turboProfile, loaded, true, .8, 1.0 / 60);
            loaded.shiftRemaining = .25;
            PhysicsAudioUpdate(controller, turboProfile, loaded, true, .8, 1.0 / 60);
            Check(controller.ReleaseCount == 2, "Automatic shift releases charged turbo even with gas held.", ref count);
            controller.Update(loaded, false, .8, .02); loaded.shiftRemaining = 0;
            var resumed = controller.Update(loaded, true, .8, .02);
            Check(controller.ReleaseCount == 2 && resumed.releaseId == 2, "Pause suppresses stale audio releases without resetting simulation pressure.", ref count);
            foreach (var induction in new[] { EngineUpgradeInduction.Factory, EngineUpgradeInduction.Supercharger })
            {
                var hardware = new Tune { engineLevel = 1, inductionUpgrade = induction };
                var control = new EngineSoundController(spec, hardware); var physical = new InductionProfile(spec, hardware);
                var plain = new CarState { rpm = 6000, throttle = 1, launched = true };
                for (int i = 0; i < 120; i++) PhysicsAudioUpdate(control, physical, plain, true, .8, .02);
                plain.throttle = 0; PhysicsAudioUpdate(control, physical, plain, true, .8, .02);
                Check(control.ReleaseCount == 0, "No turbo valve on NA/supercharger: " + induction, ref count);
            }
            var uncharged = new EngineSoundController(spec, turboTune);
            var neutral = new CarState { rpm = 6500, throttle = 1 };
            for (int i = 0; i < 120; i++) PhysicsAudioUpdate(uncharged, turboProfile, neutral, true, .8, .02, 0);
            neutral.throttle = 0; PhysicsAudioUpdate(uncharged, turboProfile, neutral, true, .8, .02, 0);
            Check(uncharged.ReleaseCount == 0, "Unloaded staging revs do not invent charged blow-off.", ref count);
            float[] previousValve = null;
            foreach (var valve in new[] { BlowOffType.Recirculated, BlowOffType.Hiss, BlowOffType.Chirp, BlowOffType.Flutter })
            {
                turboTune.blowOffValve = valve;
                var audition = EngineSoundAudition.Render(spec, turboTune);
                Check(previousValve == null || Difference(previousValve, audition) > .0004, "Distinct valve sample: " + valve, ref count);
                EngineSoundAudition.WriteWav(Path.Combine(directory, "turbo-" + valve.ToString().ToLowerInvariant() + ".wav"), audition);
                previousValve = audition;
            }
            var chargerTune = new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Supercharger };
            var charged = Steady(spec, chargerTune, boost: .5);
            Check(Difference(charged, Steady(spec, new Tune { engineLevel = 1 })) > .005, "Supercharger adds RPM-dependent gear whine.", ref count);
            EngineSoundAudition.WriteWav(Path.Combine(directory, "supercharger.wav"), EngineSoundAudition.Render(spec, chargerTune));
            foreach (EngineUpgradeInduction kit in new[] { EngineUpgradeInduction.Turbo, EngineUpgradeInduction.TwinTurbo, EngineUpgradeInduction.Supercharger, EngineUpgradeInduction.TwinSupercharger, EngineUpgradeInduction.Twincharged })
            {
                var hardware = new Tune { engineLevel = 1, inductionUpgrade = kit };
                var voice = new EngineSoundProfile(spec, hardware); var physical = new InductionProfile(spec, hardware);
                Check(voice.turboCount == physical.turboCount && voice.superchargerCount == physical.superchargerCount,
                    "Audio voices follow physical compressor topology: " + kit, ref count);
                EngineSoundAudition.WriteWav(Path.Combine(directory, "kit-" + kit.ToString().ToLowerInvariant() + ".wav"), EngineSoundAudition.Render(spec, hardware));
            }
            var hardwareSnapshot = new EngineSoundProfile(spec, chargerTune); spec.sound.cylinders = 12; chargerTune.exhaustLevel = 3;
            Check(hardwareSnapshot.cylinders == 4 && hardwareSnapshot.exhaust == 0, "Sound hardware is captured independently.", ref count);
            var synthMute = new EngineSoundSynth(hardwareSnapshot); var buffer = new float[2048];
            synthMute.Fill(buffer, 2, 48000, new EngineSoundFrame(4000, 1, 1, 1, 0));
            Check(Rms(buffer) == 0, "Muted stereo DSP is exactly silent.", ref count);
            synthMute.Fill(buffer, 2, 48000, new EngineSoundFrame(4000, 1, 1, 1, 1, active: false));
            Check(Rms(buffer) == 0, "Paused/menu DSP clears output and release tail.", ref count);
            synthMute.Fill(buffer, 2, 48000, new EngineSoundFrame(4000, 1, 1, 1, .8));
            bool stereo = true; for (int i = 0; i < buffer.Length; i += 2) if (buffer[i] != buffer[i + 1]) stereo = false;
            Check(stereo && Rms(buffer) > .01, "48 kHz stereo DSP interleaves equal audible channels.", ref count);
            var savedTune = new Tune { engineLevel = 2, exhaustLevel = 2, inductionUpgrade = EngineUpgradeInduction.Turbo, blowOffValve = BlowOffType.Chirp };
            var restored = JsonUtility.FromJson<Tune>(JsonUtility.ToJson(savedTune));
            Check(restored.exhaustLevel == 2 && restored.inductionUpgrade == EngineUpgradeInduction.Turbo && restored.blowOffValve == BlowOffType.Chirp,
                "Sound upgrades/valve survive JSON roundtrip.", ref count);
            var ledger = PlayerProfile.New(DateTime.UtcNow.Ticks); ledger.Upgrade("exhaust"); ledger.SelectedCar.SavePreset(0);
            ledger.SelectedCar.tune.exhaustLevel = 2; ledger.SelectedCar.tune.inductionUpgrade = EngineUpgradeInduction.Supercharger;
            ledger.SelectedCar.LoadPreset(0);
            Check(ledger.cash == 950 && ledger.SelectedCar.tune.exhaustLevel == 2 && ledger.SelectedCar.tune.inductionUpgrade == EngineUpgradeInduction.Supercharger,
                "Exhaust purchase charges once and tune loading preserves installed hardware.", ref count);
            spec.sound.displacementLitres = float.NaN;
            Check(spec.Validate() != null, "Invalid audio authoring is rejected.", ref count);
            VerifyApprovedVoice(report, ref count);
            report.AppendLine((failures.Count==0?"PASS: ":"FAIL: "+failures.Count+" failures / ")+count+" audio checks. Playback signal checks do not establish human listening quality or mobile DSP performance.");
            foreach(string failure in failures)report.AppendLine("FAIL: "+failure);
            File.WriteAllText(Path.Combine(root, "reports/audio-verification.txt"), report.ToString());
            if(failures.Count>0)throw new InvalidOperationException("AUDIO CHECK FAILED: "+string.Join("; ",failures));
            return count;
        }

        static void VerifyApprovedVoice(StringBuilder report, ref int count)
        {
            var spec = new VehicleSpec { displacementLitres = 5,
                sound = new EngineSoundSpec { cylinders = 8, layout = EngineLayout.Crossplane } };
            var profile = new EngineSoundProfile(spec, new Tune());
            var frame = new EngineSoundFrame(4800, .9, .9, 0, .85,
                cylinderContribution: new double[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            var first = new float[48000]; var repeat = new float[48000]; var other = new float[48000];
            string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
            new EngineSoundSynth(profile, 117).Fill(first, 1, 48000, frame);
            new EngineSoundSynth(profile, 117).Fill(repeat, 1, 48000, frame);
            new EngineSoundSynth(profile, 491).Fill(other, 1, 48000, frame);
            Check(Difference(first, repeat) == 0 && Difference(first, other) > .00001,
                "Organic pulse variation repeats by private seed and changes with another seed.", ref count);
            Check(randomBefore == JsonUtility.ToJson(UnityEngine.Random.state) && frame.rpm == 4800 && frame.cylinderContribution[0] == 1,
                "Audio variation leaves gameplay RNG and captured physical input unchanged.", ref count);
            var chunks = new float[48000]; var block = new float[400]; var synth = new EngineSoundSynth(profile);
            for (int offset = 0; offset < chunks.Length; offset += block.Length)
            {
                synth.Fill(block, 1, 48000, frame); Array.Copy(block, 0, chunks, offset, block.Length);
            }
            Check(Difference(first, chunks) == 0, "Output is sample-exact across callback block partitioning.", ref count);
            var damaged = new float[48000];
            new EngineSoundSynth(profile).Fill(damaged, 1, 48000, new EngineSoundFrame(4800, .9, .9, 0, .85,
                cylinderContribution: new double[] { 0, 1, 1, 1, 1, 1, 1, 1 }));
            Check(Difference(first, damaged)/Math.Max(.000001,Rms(first)) > .05,
                "Deeper V8 voice still exposes the actual missing cylinder contribution.", ref count);
            var petrol = new VehicleSpec(); petrol.sound.engineGain = 0; petrol.engineBehavior.mechanicalClatter = .4f;
            var diesel = petrol.Copy(); diesel.engineBehavior.fueling = FuelSystem.Diesel;
            var petrolTick = Steady(petrol, new Tune(), rpm: 900, load: 0);
            var dieselTick = Steady(diesel, new Tune(), rpm: 900, load: 0);
            Check(Rms(dieselTick) > Rms(petrolTick) * 10 && Rms(petrolTick) > 0,
                "Healthy petrol ticking is softened while diesel retains its mechanical texture.", ref count);
            foreach (int rate in new[] { 32000, 44100, 48000, 96000 })
            {
                var kit = new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Twincharged };
                var mixed = new EngineSoundSynth(new EngineSoundProfile(spec, kit),sampleRate:rate); var pcm = new float[4096];
                mixed.Fill(pcm, 2, rate, new EngineSoundFrame(8000, 1, 1, 1, .85,
                    turboSpool: 1, turboBoost: .6, superchargerBoost: .4, chargerLoad: .8));
                bool valid = true;
                for (int i = 0; i < pcm.Length; i += 2)
                    valid &= !float.IsNaN(pcm[i]) && !float.IsInfinity(pcm[i]) && Math.Abs(pcm[i]) < 1 && !float.IsNaN(pcm[i+1]) && !float.IsInfinity(pcm[i+1]) && Math.Abs(pcm[i+1]) < 1;
                Check(valid && Rms(pcm) > 0, "Bounded stereo twincharge at " + rate + " Hz.", ref count);
            }
            var callback = new float[2048]; synth.Fill(callback, 1, 48000, frame);
            var timer = new Stopwatch(); long startBytes = GC.GetAllocatedBytesForCurrentThread(); timer.Start();
            for (int i = 0; i < 128; i++) synth.Fill(callback, 1, 48000, frame);
            timer.Stop(); long bytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
            Check(bytes == 0, "Warmed shared audio callback allocates zero managed bytes.", ref count);
            report.AppendLine("Approved V8 callback: " + (timer.Elapsed.TotalMilliseconds / 128).ToString("F3")
                + " ms per 2048 mono samples / 42.667 ms signal; allocated bytes " + bytes + ". Editor CPU observation, not a mobile guarantee.");
        }
    }
}
