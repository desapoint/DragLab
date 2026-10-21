using System;
using System.IO;

namespace DragLab.Domain
{
    public static class EngineSoundAudition
    {
        public const int Rate = 44100;
        public static float[] Render(VehicleSpec spec, Tune source)
        {
            var tune = source.Copy(); tune.ClampTo(spec);
            var controller = new EngineSoundController(spec, tune);
            var induction = new InductionProfile(spec, tune);
            var behavior = new EngineBehaviorProfile(spec, tune);
            var synth = new EngineSoundSynth(new EngineSoundProfile(spec, tune));
            var output = new float[Rate * 8]; var block = new float[Rate / 60];
            var state = new CarState();
            controller.Update(state,true,.85,DragMath.FixedStep);
            for (int frame = 0; frame < 480; frame++)
            {
                double t = frame / 60.0;
                state.rpm = t < 1 ? spec.idleRpm : spec.idleRpm + (spec.redlineRpm - spec.idleRpm - 300) * Math.Min(1, (t - 1) / 5);
                if (t >= 6) state.rpm = Math.Max(spec.idleRpm, state.rpm - (t - 6) * 2200);
                state.throttle = t < 1 || t >= 6 ? .03 : .95;
                state.launched = t >= 1; state.speed = state.launched ? 25 : 0;
                state.transmittedTorqueNm = DragMath.Torque(spec, tune, state.rpm) * (t < 1 || t >= 6 ? .03 : .9);
                state.shiftRemaining = t >= 4 && t < 4.2 ? .2 : 0;
                for (int tick = 0; tick < 2; tick++)
                {
                    EngineBehaviorDynamics.BeginTick(spec, tune, behavior, state, state.throttle, !state.launched, DragMath.FixedStep);
                    state.transmissionTorqueCut = state.shiftRemaining > 0;
                    InductionDynamics.Step(induction, state, state.throttle, state.launched ? .9 : 0,
                        state.transmissionTorqueCut, state.combustionFraction <= 0, DragMath.FixedStep);
                    state.audioExhaust.Step(spec,induction,state,FuelDynamics.PhysicalFlow(spec,tune,induction,behavior,state,false),DragMath.FixedStep);
                }
                state.transmittedTorqueNm = state.engineTorqueNm * .9;
                var parameters = controller.Update(state, true, .85, 1.0 / 60);
                synth.Fill(block, 1, Rate, parameters); Array.Copy(block, 0, output, frame * block.Length, block.Length);
            }
            return output;
        }
        public static void WriteWav(string path, float[] samples, int rate = Rate)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); writer.Write(36 + samples.Length * 2);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); writer.Write(samples.Length * 2);
                foreach (float sample in samples) writer.Write((short)(DragMath.Clamp(sample, -1, 1) * 32767));
            }
        }
    }
}
