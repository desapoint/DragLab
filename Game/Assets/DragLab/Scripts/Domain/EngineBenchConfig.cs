using System;

namespace DragLab.Domain
{
    [Serializable]
    public sealed class EngineBenchConfig
    {
        public int version = 1;
        public VehicleSpec vehicle = new VehicleSpec();
        public Tune tune = new Tune();
        public EngineBenchControls controls = new EngineBenchControls();
        // Captured offline audition level only. Live master volume is application-wide.
        // Retained for version-1 JSON and deterministic A/B WAV compatibility.
        public float volume = .35f;
        public EngineBenchConfig Copy() { return new EngineBenchConfig { version = version, vehicle = vehicle.Copy(), tune = tune.Copy(), controls = controls.Copy(), volume = volume }; }
        public string Validate()
        {
            if (version != 1 || vehicle == null || tune == null || controls == null) return "Invalid bench configuration version/content.";
            if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 1) return "Invalid bench volume.";
            return vehicle.Validate();
        }
    }

    public static class EngineBenchRenderer
    {
        public static EngineBenchConfig ScenarioConfig(EngineBenchConfig source, EngineBenchScenario scenario)
        {
            var copy = source.Copy(); copy.tune.coldStart = scenario == EngineBenchScenario.ColdWarmup;
            if (scenario != EngineBenchScenario.FixedRpmHold) copy.controls.engineEnabled = true;
            return copy;
        }
        // Exact offline replay: 120 Hz physics, 60 Hz audio publication, 44.1 kHz PCM.
        // A new synth resets phases, filter memory and seeded event noise every run.
        public static float[] Render(EngineBenchConfig source, EngineBenchScenario scenario)
        {
            var config = ScenarioConfig(source, scenario);
            var bench = new EngineTestBench(config.vehicle, config.tune);
            var controller = new EngineSoundController(bench.spec, bench.tune);
            var synth = new EngineSoundSynth(new EngineSoundProfile(bench.spec, bench.tune));
            var tires = new TireSoundSynth();var tireBlock=new float[735];
            int ticks = EngineBenchScenarios.DurationTicks(scenario);
            var pcm = new float[ticks / 2 * 735]; var block = new float[735];
            for (int tick = 0; tick < ticks; tick += 2)
            {
                bench.Step(EngineBenchScenarios.At(scenario, tick, bench.spec, config.controls));
                bench.Step(EngineBenchScenarios.At(scenario, tick + 1, bench.spec, config.controls));
                synth.Fill(block, 1, 44100, controller.Update(bench.state, bench.state.rpm > 0, 1, 1.0 / 60, bench.audioLoad), config.volume);
                tires.Fill(tireBlock,1,44100,TireSoundFrame.From(bench.state,true,1,bench.spec.drive),config.volume);
                for(int i=0;i<block.Length;i++)block[i]+=tireBlock[i];
                Array.Copy(block, 0, pcm, tick / 2 * 735, 735);
            }
            return pcm;
        }
    }
}
