using System;

namespace DragLab.Domain
{
    public enum InductionType { NaturallyAspirated, Turbo, Supercharger, TwinTurbo, TwinSupercharger, Twincharged }
    public enum EngineUpgradeInduction { Factory, Turbo, Supercharger, TwinTurbo, TwinSupercharger, Twincharged }
    public enum BlowOffType { Factory, Recirculated, Hiss, Chirp, Flutter }
    public enum EngineLayout { Inline, V, Crossplane }

    [Serializable]
    public sealed class EngineSoundSpec
    {
        public int version;
        public int cylinders = 4;
        public float displacementLitres = 1.6f;
        public EngineLayout layout;
        public InductionType induction;
        public BlowOffType blowOff = BlowOffType.Recirculated;
        public int exhaust;
        public float boostBar = .6f;
        public float spoolSeconds = .5f;
        public float superchargerRatio = 18;
        public float engineGain = .8f, turboGain = .24f, superchargerGain = .28f, blowOffGain = .55f;
        public float idleBoostDb = 6;
        public float exhaustBassGain = 1, exhaustBrightness = 1, rawPulseGain = 1;
        public float exhaustPopGain = 1, exhaustPopSharpness = 1, exhaustPopDecay = 1;
        public ExhaustSetup exhaustSystem = new ExhaustSetup();
        public EngineSoundSpec Copy() { var copy = (EngineSoundSpec)MemberwiseClone(); copy.exhaustSystem = exhaustSystem == null ? new ExhaustSetup() : exhaustSystem.Copy(); return copy; }
        public string Validate()
        {
            if (exhaustSystem != null && exhaustSystem.Validate() != null) return exhaustSystem.Validate();
            if (cylinders < 1 || cylinders > 16) return "Sound cylinder count must be 1–16.";
            if (!Valid(displacementLitres, .3f, 12)) return "Sound displacement must be 0.3–12 litres.";
            if (!Enum.IsDefined(typeof(EngineLayout), layout) || !Enum.IsDefined(typeof(InductionType), induction) ||
                !Enum.IsDefined(typeof(BlowOffType), blowOff) || blowOff == BlowOffType.Factory) return "Invalid sound hardware type.";
            if (exhaust < 0 || exhaust > 3) return "Exhaust sound must be 0–3.";
            if (!Valid(boostBar, 0, 3) || !Valid(spoolSeconds, .05f, 3) || !Valid(superchargerRatio, 2, 40)) return "Invalid induction sound settings.";
            if (!Valid(engineGain, 0, 2) || !Valid(turboGain, 0, 2) || !Valid(superchargerGain, 0, 2) || !Valid(blowOffGain, 0, 2)) return "Sound gains must be 0–2.";
            if (!Valid(idleBoostDb, -6, 12)) return "Idle boost must be -6 to +12 dB.";
            if (!Valid(exhaustBassGain, 0, 3) || !Valid(exhaustBrightness, 0, 2) || !Valid(rawPulseGain, 0, 3)) return "Invalid exhaust voice controls (0 = nominal).";
            if (!Valid(exhaustPopGain, 0, 2) || !Valid(exhaustPopSharpness, .25f, 2.5f) || !Valid(exhaustPopDecay, .25f, 3)) return "Invalid exhaust-pop gain/sharpness/decay.";
            return null;
        }
        static bool Valid(float v, float lo, float hi) { return !float.IsNaN(v) && !float.IsInfinity(v) && v >= lo && v <= hi; }
    }

    // Immutable hardware captured at race start. Pressure and release events come from fixed-tick simulation.
    public sealed class EngineSoundProfile
    {
        public readonly int cylinders, exhaust, engineStage;
        public readonly int turboCount, superchargerCount;
        public readonly double litres, boostBar, spoolSeconds, chargerRatio, engineGain, turboGain, chargerGain, releaseGain;
        public readonly EngineLayout layout;
        public readonly InductionType induction;
        public readonly BlowOffType valve;
        public readonly double idleRpm, redlineRpm;
        public readonly FuelSystem fueling;
        public readonly double camOverlap, mixtureVariation, mechanicalClatter;
        public readonly double bassGain, voiceBrightness, rawPulseGain;
        public readonly ExhaustSetup exhaustSystem;
        public readonly double popGain, popSharpness, popDecay;
        public readonly double idleBoostGain;
        public readonly double turboSize,turboFlowCapacityKgS;
        public EngineSoundProfile(VehicleSpec spec, Tune tune)
        {
            var hardware = spec.sound ?? new EngineSoundSpec();
            var physical = new InductionProfile(spec, tune);
            cylinders = hardware.cylinders; litres = spec.DisplacementLitres; layout = hardware.layout;
            engineStage = Math.Max(0, Math.Min(3, tune.engineLevel));
            induction = physical.type; turboCount = physical.turboCount; superchargerCount = physical.superchargerCount;
            exhaust = Math.Min(3, hardware.exhaust + Math.Max(0, tune.exhaustLevel));
            valve = physical.valve;
            boostBar = (turboCount > 0 ? physical.data.turboBoostBar : physical.data.superchargerBoostBar) * physical.boostSetting;
            spoolSeconds = physical.data.turboSpoolSeconds;
            chargerRatio = hardware.superchargerRatio * physical.pulley; engineGain = hardware.engineGain;
            turboGain = hardware.turboGain; chargerGain = hardware.superchargerGain; releaseGain = hardware.blowOffGain;
            idleRpm = spec.idleRpm; redlineRpm = spec.redlineRpm;
            var behavior = spec.engineBehavior ?? new EngineBehaviorSpec();
            fueling = behavior.fueling; camOverlap = behavior.camOverlap;
            mixtureVariation = behavior.mixtureVariation; mechanicalClatter = behavior.mechanicalClatter;
            bassGain = hardware.exhaustBassGain > 0 ? hardware.exhaustBassGain : 1;
            voiceBrightness = hardware.exhaustBrightness > 0 ? hardware.exhaustBrightness : 1;
            rawPulseGain = hardware.rawPulseGain > 0 ? hardware.rawPulseGain : 1;
            popGain = hardware.exhaustPopGain; popSharpness = hardware.exhaustPopSharpness; popDecay = hardware.exhaustPopDecay;
            idleBoostGain = Math.Pow(10, hardware.idleBoostDb / 20);
            turboSize=physical.size;turboFlowCapacityKgS=physical.data.turboUnitFlowKgS*physical.size*Math.Max(1,physical.turboCount);
            exhaustSystem = hardware.exhaustSystem == null ? new ExhaustSetup() : hardware.exhaustSystem.Copy();
        }
        public double FiringHz(double rpm) { return rpm * cylinders / 120; } // Four-stroke combustion events.
    }

    // One immutable reference is published to DSP; no mutable CarState/Tune or Unity API reaches the audio thread.
    public sealed class EngineSoundFrame
    {
        public readonly double rpm, throttle, load, boost, volume, slip, speed, releaseStrength;
        public readonly double turboSpool, superchargerBoost, chargerLoad;
        public readonly double turboBoost;
        public readonly bool active, cut;
        public readonly int releaseId;
        public readonly double combustionFraction, idleRoughness, intakeGulp, fuelHesitation, popStrength;
        public readonly int popId;
        public readonly bool overrun;
        public readonly bool twoStep;
        public readonly CutMethod cutMethod;
        public readonly double[] cylinderContribution;
        public readonly double airflowKgS;
        public EngineSoundFrame(double rpm, double throttle, double load, double boost, double volume,
            double slip = 0, double speed = 0, bool active = true, bool cut = false, int releaseId = 0, double releaseStrength = 0,
            double turboSpool = 0, double superchargerBoost = 0, double chargerLoad = 0, double turboBoost = -1,
            double combustionFraction = 1, double idleRoughness = 0, double intakeGulp = 0, double fuelHesitation = 0,
            int popId = 0, double popStrength = 0, bool overrun = false, bool twoStep = false, CutMethod cutMethod = CutMethod.Fuel, double[] cylinderContribution = null, double airflowKgS = -1)
        {
            this.rpm = rpm; this.throttle = throttle; this.load = load; this.boost = boost; this.volume = volume;
            this.slip = slip; this.speed = speed; this.active = active; this.cut = cut;
            this.releaseId = releaseId; this.releaseStrength = releaseStrength;
            this.turboSpool = turboSpool; this.superchargerBoost = superchargerBoost; this.chargerLoad = chargerLoad;
            this.turboBoost = turboBoost;
            this.combustionFraction = combustionFraction; this.idleRoughness = idleRoughness; this.intakeGulp = intakeGulp;
            this.fuelHesitation = fuelHesitation; this.popId = popId; this.popStrength = popStrength; this.overrun = overrun;
            this.twoStep = twoStep; this.cutMethod = cutMethod;this.cylinderContribution=cylinderContribution==null?null:(double[])cylinderContribution.Clone();
            this.airflowKgS=airflowKgS;
        }
    }

    public sealed class EngineSoundController
    {
        readonly EngineSoundProfile profile;
        readonly VehicleSpec spec;
        readonly Tune tune;
        double boost, releaseStrength;
        bool wasActive;
        int releaseId, lastPhysicalRelease;
        int popId, lastPhysicalPop;
        public double Boost { get { return boost; } }
        public int ReleaseCount { get { return lastPhysicalRelease; } }
        public EngineSoundController(VehicleSpec spec, Tune tune)
        {
            this.spec = spec; this.tune = tune.Copy(); profile = new EngineSoundProfile(spec, this.tune);
        }
        public EngineSoundFrame Update(CarState state, bool active, double volume, double dt, double loadOverride = -1)
        {
            double gas = DragMath.Clamp(state.throttle, 0, 1);
            double torque = Math.Max(1, state.engineTorqueNm);
            double load = state.launched ? DragMath.Clamp(Math.Abs(state.transmittedTorqueNm) / torque, 0, 1) : .08 * gas;
            if (loadOverride >= 0) load = DragMath.Clamp(loadOverride, 0, 1);
            bool shifting = state.shiftRemaining > 0;
            if (!active || state.engineFailed || state.stalled)
            {
                boost = releaseStrength = 0; wasActive = false; lastPhysicalRelease = state.boostReleaseCount;
                lastPhysicalPop = state.exhaustPopCount;
                return new EngineSoundFrame(state.rpm, gas, load, 0, 0, active: false, releaseId: releaseId, popId: popId);
            }
            if (wasActive && state.boostReleaseCount != lastPhysicalRelease)
            {
                releaseId++; releaseStrength = state.boostReleaseStrength;
            }
            if (wasActive && state.exhaustPopCount != lastPhysicalPop) popId++;
            lastPhysicalPop = state.exhaustPopCount;
            wasActive = true; lastPhysicalRelease = state.boostReleaseCount; boost = state.boostBar;
            return new EngineSoundFrame(state.rpm, gas, load, boost, DragMath.Clamp(volume, 0, 1), state.slip, state.speed,
                true, state.limiter || state.transmissionTorqueCut, releaseId, releaseStrength, state.turboSpool, state.superchargerBoostBar,
                DragMath.Clamp(state.superchargerDriveTorqueNm / torque, 0, 1), state.turboBoostBar,
                state.combustionFraction, state.idleRoughness, state.intakeGulp, state.fuelHesitation,
                popId, state.exhaustPopStrength, state.launched && gas < .1 && state.rpm > spec.idleRpm + 900,
                state.launchControlActive, state.activeCutMethod,state.cylinderContribution,state.airflowKgS);
        }
    }

    // Stateful sample generator shared by live DSP, tests and WAV auditions. No allocations per sample/block.
    public sealed class EngineSoundSynth
    {
        readonly EngineSoundProfile profile;
        readonly ExhaustFilter exhaustFilter;
        readonly ExhaustPopSynth pop;
        readonly uint seed;
        PressureCombustionCore core44100,core48000,otherCore;
        TurboAcoustics turbo44100,turbo48000,otherTurbo;
        public bool IsSampleRatePrepared(int rate)=>rate==44100?core44100!=null:rate==48000?core48000!=null:otherCore!=null&&otherCore.Rate==rate;
        // Call on the owner/setup thread, never while Fill is running.
        public void PrepareSampleRate(int rate)
        {
            if(IsSampleRatePrepared(rate))return;
            var core=new PressureCombustionCore(profile,rate,seed);
            var turbo=profile.turboCount>0?new TurboAcoustics(profile,rate):null;
            if(rate==44100){core44100=core;turbo44100=turbo;}else if(rate==48000){core48000=core;turbo48000=turbo;}else{otherCore=core;otherTurbo=turbo;}
        }
        double firingPhase, crankPhase, chargerPhase, chargerSecondPhase, releasePhase, rpm, gain, airLow, dcIn, dcOut;
        double releaseAge = 10, releaseStrength;
        int lastRelease;
        double gulpPhase, clatterPhase;
        double voiceLoad, voiceThrottle;
        double twoStepMix;
        double outputGain;
        bool outputGainInitialized;
        int lastPop;
        uint random = 0x28a7c5u;
        double clatterLow;
        const double Tau = Math.PI * 2;
        public EngineSoundSynth(EngineSoundProfile profile,uint seed=117,int sampleRate=0)
        {
            this.seed=seed==0?117u:seed;this.profile=profile;rpm=profile.idleRpm;
            exhaustFilter=new ExhaustFilter(profile.exhaustSystem,profile.exhaust);pop=new ExhaustPopSynth(profile);
            // Legacy offline callers support both common rates. Live sessions pass
            // the device rate and allocate only that instance before publication.
            if(sampleRate==0){PrepareSampleRate(44100);PrepareSampleRate(48000);}else PrepareSampleRate(sampleRate);
        }
        double Noise() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random / (double)uint.MaxValue * 2 - 1; }
        static double Advance(double phase, double hz, int rate) { return (phase + hz / rate) % 1; }
        public void Fill(float[] data, int channels, int sampleRate, EngineSoundFrame frame, double masterVolume = 1)
        {
            if(!IsSampleRatePrepared(sampleRate))throw new InvalidOperationException("PrepareSampleRate must run before audio playback.");
            var pressureCore=sampleRate==44100?core44100:sampleRate==48000?core48000:otherCore;
            var turboCore=sampleRate==44100?turbo44100:sampleRate==48000?turbo48000:otherTurbo;
            double targetOutputGain = double.IsNaN(masterVolume) || double.IsInfinity(masterVolume) ? 0 : DragMath.Clamp(masterVolume, 0, 1);
            if (!outputGainInitialized) { outputGain = targetOutputGain; outputGainInitialized = true; }
            if (!frame.active || frame.volume <= 0)
            {
                Array.Clear(data, 0, data.Length); gain = airLow = dcIn = dcOut = clatterLow = 0; releaseAge = 10;
                lastRelease = frame.releaseId; lastPop = frame.popId; pop.Clear(); exhaustFilter.Clear(); pressureCore.Clear(); if(turboCore!=null)turboCore.Clear(); return;
            }
            if (frame.releaseId != lastRelease) { lastRelease = frame.releaseId; releaseAge = 0; releaseStrength = frame.releaseStrength; }
            pop.Configure(sampleRate);
            if (frame.popId != lastPop) { lastPop = frame.popId; pop.Trigger(frame.popStrength, frame.twoStep); }
            bool carbVoice = profile.fueling == FuelSystem.Carburetor;
            bool dieselVoice=profile.fueling==FuelSystem.Diesel;
            double excitation = Math.Max(frame.load, frame.throttle * (profile.cylinders>=8||carbVoice?.8:.65));
            double smooth = 1 - Math.Exp(-1.0 / (sampleRate * .018));
            double toneSmooth = 1 - Math.Exp(-1.0 / (sampleRate * .12));
            double gainSmooth = 1 - Math.Exp(-1.0 / (sampleRate * .045));
            double masterSmooth = 1 - Math.Exp(-1.0 / (sampleRate * .008));
            double clatterSmooth = 1 - Math.Exp(-Tau * 950 / sampleRate);
            // These combustion color layers were calibrated at the authored 0.8
            // engine gain. Include them in its control without altering that voice.
            double combustionColorGain = profile.engineGain / (double).8f;
            exhaustFilter.Configure(sampleRate);
            for (int i = 0; i < data.Length; i += channels)
            {
                rpm += (frame.rpm - rpm) * smooth;
                voiceLoad += (excitation - voiceLoad) * toneSmooth; voiceThrottle += (frame.throttle - voiceThrottle) * gainSmooth;
                twoStepMix += ((frame.twoStep ? 1 : 0) - twoStepMix) * gainSmooth;
                double idleMix = DragMath.Clamp(1 - (rpm - profile.idleRpm) / 1200, 0, 1) * (1 - twoStepMix);
                double idlePresence = 1 + (profile.idleBoostGain - 1) * (frame.twoStep ? 0 : idleMix * (1 - voiceThrottle));
                double targetGain = frame.volume * profile.engineGain * (.30 + voiceThrottle * .18 + voiceLoad * .30) * idlePresence * (frame.cut ? .12 : frame.combustionFraction);
                gain += (targetGain - gain) * gainSmooth;
                double firing = profile.FiringHz(rpm);
                firingPhase=Advance(firingPhase,firing,sampleRate);crankPhase=Advance(crankPhase,rpm/120,sampleRate);
                double phase=firingPhase*Tau,noise=Noise();
                double pulse=pressureCore.Sample(rpm,voiceThrottle,frame);
                double value=pulse*gain*(1+profile.exhaust*.16);
                double carb=carbVoice?1:0;
                gulpPhase = Advance(gulpPhase, 90 + frame.throttle * 75, sampleRate);
                double outsideExhaust = (Math.Sin(Tau * gulpPhase) + .25 * Math.Sin(Tau * gulpPhase * 2)) * frame.intakeGulp * .09 * frame.volume;
                value += Math.Sin(Tau * crankPhase * 2) * frame.fuelHesitation * .06 * frame.volume * combustionColorGain;
                // Open-throttle intake/exhaust rotation remains audible while launch cuts interrupt combustion.
                value += twoStepMix * voiceThrottle * (Math.Sin(phase) * .12 + Math.Sin(phase * 2) * .045) * frame.volume * combustionColorGain;
                clatterPhase = Advance(clatterPhase, Math.Min(1900 + rpm * .04, sampleRate * .2), sampleRate);
                double clatter=Math.Sin(Tau*clatterPhase)*Math.Pow(Math.Max(0,Math.Cos(Tau*crankPhase*2)),dieselVoice?12:6);
                clatterLow+=clatterSmooth*(clatter-clatterLow);
                outsideExhaust+=(dieselVoice?clatter*.004:clatterLow*.0005)*profile.mechanicalClatter*frame.volume;
                if (frame.overrun && carb > 0) value += Math.Sin(phase) * Math.Pow(.5 + .5 * Math.Sin(Tau * crankPhase), 2) * .06 * profile.mixtureVariation * frame.volume * combustionColorGain;
                value += pop.Sample() * (dieselVoice?0:.3) * frame.volume;
                value = exhaustFilter.Process(value) + outsideExhaust + pressureCore.Mechanical*gain;
                if(turboCore!=null)value+=turboCore.Sample(frame,pressureCore.TurbineResponse,noise)*frame.volume;
                if (profile.superchargerCount > 0)
                {
                    double hz = Math.Min(rpm / 60 * profile.chargerRatio, sampleRate * .19);
                    chargerPhase = Advance(chargerPhase, hz, sampleRate);
                    double whine = Math.Sin(Tau * chargerPhase) + .18 * Math.Sin(Tau * chargerPhase * 2);
                    if (profile.superchargerCount == 2) { chargerSecondPhase = Advance(chargerSecondPhase, hz * .991, sampleRate); whine = (whine + Math.Sin(Tau * chargerSecondPhase)) * .65; }
                    double scFraction = frame.superchargerBoost > 0 ? frame.superchargerBoost / Math.Max(.1, profile.boostBar) : profile.turboCount == 0 ? frame.boost / Math.Max(.1, profile.boostBar) : 0;
                    value += (.82*whine+(hz*3<sampleRate*.42?.06*Math.Sin(Tau*chargerPhase*3):0)) * .15 * profile.chargerGain * (.06 + .6 * scFraction + .3 * frame.chargerLoad) * frame.volume;
                }
                if (!dieselVoice && releaseAge < 1.2 && profile.turboCount > 0)
                {
                    double envelope = Math.Min(1, releaseAge / .006) * Math.Exp(-releaseAge * (profile.valve == BlowOffType.Recirculated ? 9 : 6));
                    double releaseHz = Math.Max(600, (profile.valve == BlowOffType.Chirp ? 4500 : 2200) - releaseAge * 2600);
                    releaseHz = Math.Min(releaseHz, sampleRate * .2); releasePhase = Advance(releasePhase, releaseHz, sampleRate);
                    airLow += .09 * (noise - airLow);
                    double air = profile.valve == BlowOffType.Recirculated ? airLow * .55 : profile.valve == BlowOffType.Chirp ?
                        Math.Sin(Tau * releasePhase) * .35 + airLow * .12 : profile.valve == BlowOffType.Flutter ?
                        (airLow * .45 + Math.Sin(Tau * releasePhase) * .18) * Math.Pow(.5 + .5 * Math.Sin(releaseAge * Tau * (28 - releaseAge * 12)), 3) : airLow * .75;
                    value += air * envelope * releaseStrength * profile.releaseGain * frame.volume;
                    releaseAge += 1.0 / sampleRate;
                }
                // Remove DC and softly saturate the combined layers with headroom for other game audio.
                double filtered = value - dcIn + .995 * dcOut; dcIn = value; dcOut = filtered;
                outputGain += (targetOutputGain - outputGain) * masterSmooth;
                if (Math.Abs(outputGain - targetOutputGain) < .000001) outputGain = targetOutputGain;
                float sample = (float)(filtered / (1 + Math.Abs(filtered)) * outputGain);
                for (int channel = 0; channel < channels; channel++) data[i + channel] = sample;
            }
        }
    }
}
