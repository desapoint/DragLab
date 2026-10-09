using System;

namespace DragLab.Domain
{
    // Short, monophonic transcriptions of public-domain classical themes.
    // The existing engine voice is the instrument; no separate music oscillator.
    public sealed class EngineBenchMusic
    {
        public static readonly string[] Titles = { "Ode to Joy / Beethoven", "Für Elise / Beethoven", "In the Hall of the Mountain King / Grieg" };
        readonly int[] notes, ends;
        readonly int octave;
        public readonly int song;
        public int DurationTicks => ends[ends.Length - 1];
        public string Title => Titles[song];
        public EngineBenchMusic(int song, VehicleSpec spec)
        {
            if (song < 0 || song >= Titles.Length) throw new ArgumentOutOfRangeException(nameof(song));
            this.song = song;
            // MIDI note names, duration in half-beats. Melody only, no arrangement.
            string score = song == 0 ?
                "E4:2 E4:2 F4:2 G4:2 G4:2 F4:2 E4:2 D4:2 C4:2 C4:2 D4:2 E4:2 E4:3 D4:1 D4:4 " +
                "E4:2 E4:2 F4:2 G4:2 G4:2 F4:2 E4:2 D4:2 C4:2 C4:2 D4:2 E4:2 D4:3 C4:1 C4:4" : song == 1 ?
                "E5:1 D#5:1 E5:1 D#5:1 E5:1 B4:1 D5:1 C5:1 A4:3 R:1 C4:1 E4:1 A4:1 B4:3 R:1 E4:1 G#4:1 B4:1 C5:3 R:1 " +
                "E5:1 D#5:1 E5:1 D#5:1 E5:1 B4:1 D5:1 C5:1 A4:3 R:1 C4:1 E4:1 A4:1 B4:3 R:1 E4:1 C5:1 B4:1 A4:4" :
                "B3:1 C#4:1 D4:1 E4:1 F#4:1 D4:1 F#4:2 F4:1 C#4:1 F4:2 E4:1 C4:1 E4:2 " +
                "B3:1 C#4:1 D4:1 E4:1 F#4:1 D4:1 F#4:1 B4:1 A4:1 F#4:1 D4:1 F#4:1 A4:4 " +
                "B3:1 C#4:1 D4:1 E4:1 F#4:1 D4:1 F#4:2 F4:1 C#4:1 F4:2 E4:1 C4:1 E4:2 " +
                "B3:1 C#4:1 D4:1 E4:1 F#4:1 D4:1 F#4:1 B4:1 A4:1 F#4:1 D4:1 F#4:1 B3:4";
            var events = score.Split(' '); notes = new int[events.Length]; ends = new int[events.Length];
            int halfBeatTicks = song == 0 ? 30 : song == 1 ? 26 : 24;
            int total = 0, min = 127, max = 0;
            for (int i = 0; i < events.Length; i++)
            {
                var pair = events[i].Split(':'); notes[i] = Midi(pair[0]);
                total += int.Parse(pair[1]) * halfBeatTicks; ends[i] = total;
                if (notes[i] >= 0) { min = Math.Min(min, notes[i]); max = Math.Max(max, notes[i]); }
            }
            // Transpose whole octaves together, preserving intervals instead of
            // clamping individual notes onto idle/redline. Pick the highest fit.
            bool found = false;
            for (int shift = 3; shift >= -6; shift--)
                if (Rpm(min + shift * 12, spec) >= spec.idleRpm * 1.1 && Rpm(max + shift * 12, spec) <= spec.redlineRpm * .90)
                { octave = shift; found = true; break; }
            if (!found) throw new ArgumentException("This engine's idle/redline range is too narrow for the selected melody.");
        }
        static int Midi(string name)
        {
            if (name == "R") return -1;
            int pitch = name[0] == 'C' ? 0 : name[0] == 'D' ? 2 : name[0] == 'E' ? 4 : name[0] == 'F' ? 5 : name[0] == 'G' ? 7 : name[0] == 'A' ? 9 : 11;
            if (name.Length == 3) pitch++;
            return (name[name.Length - 1] - '0' + 1) * 12 + pitch;
        }
        static double Rpm(int midi, VehicleSpec spec)
        { return 440 * Math.Pow(2, (midi - 69) / 12.0) * 120 / spec.sound.cylinders; }
        int EventAt(int tick)
        {
            for (int i = 0; i < ends.Length; i++) if (tick < ends[i]) return i;
            return ends.Length - 1;
        }
        public EngineBenchControls At(int tick, VehicleSpec spec)
        {
            int i = EventAt(Math.Max(0, tick));
            return new EngineBenchControls { fixedRpm = true, targetRpm = notes[i] < 0 ? spec.idleRpm : (float)Rpm(notes[i] + octave * 12, spec),
                throttle = notes[i] < 0 ? 0 : .65f, load = notes[i] < 0 ? 0 : (i % 4 == 0 ? .45f : .28f),
                gear = 0, launchControls = false, allowFailure = false, warmthOverride = 1, damageOverride = 0 };
        }
        public float GainAt(int tick)
        {
            if (tick < 0 || tick >= DurationTicks) return 0;
            int i = EventAt(tick); if (notes[i] < 0) return 0;
            int start = i == 0 ? 0 : ends[i - 1], length = ends[i] - start;
            double phase = (tick - start) / (double)length;
            // Brief release separates repeated notes; gentle envelope avoids clicks.
            return (float)(Math.Min(1, (tick - start) / 3.0) * DragMath.Clamp((.94 - phase) / .12, 0, 1));
        }
        public static EngineBenchConfig PerformanceConfig(EngineBenchConfig source)
        {
            var config = source.Copy();
            config.tune.controlLevel = 1; config.tune.limiter = LimiterStyle.None;
            config.tune.twoStep = config.tune.launchPreload = config.tune.antiLag = EquipmentSetting.Off;
            config.vehicle.sound.exhaustPopGain = 0;
            config.vehicle.sound.turboGain = config.vehicle.sound.superchargerGain = config.vehicle.sound.blowOffGain = 0;
            return config;
        }
        // Offline audition uses exactly the same note controls and engine DSP.
        public float[] Render(EngineBenchConfig source)
        {
            const int rate = 44100;
            var config = PerformanceConfig(source); var bench = new EngineTestBench(config.vehicle, config.tune);
            var controller = new EngineSoundController(bench.spec, bench.tune);
            var synth = new EngineSoundSynth(new EngineSoundProfile(bench.spec, bench.tune));
            var pcm = new float[DurationTicks * rate / 120]; int offset = 0;
            for (int tick = 0; tick < DurationTicks; tick++)
            {
                var control = At(tick, bench.spec); bench.Step(control);
                int end = (tick + 1) * rate / 120; var block = new float[end - offset];
                synth.Fill(block, 1, rate, controller.Update(bench.state, true, GainAt(tick), DragMath.FixedStep, bench.audioLoad), config.volume);
                Array.Copy(block, 0, pcm, offset, block.Length); offset = end;
            }
            return pcm;
        }
    }
}
