using System.Text.Json;
using DragLab.Domain;

// Unmodified production synthesizer with the same RPM/throttle/load control trace.
static class ReferenceBaseline
{
    static void Main(string[] args)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[0],"profile.json")));
        var p=new EngineSoundProfile(doc.RootElement);var synth=new EngineSoundSynth(p,117);
        using var trace=new BinaryReader(File.OpenRead(Path.Combine(args[0],"trace.f64")));
        using var pcm=new BinaryWriter(File.Create(Path.Combine(args[0],"current.f32")));
        var block=new float[400];
        while(trace.BaseStream.Position<trace.BaseStream.Length) {
            double rpm=trace.ReadDouble(),gas=trace.ReadDouble(),load=trace.ReadDouble();
            synth.Fill(block,1,48000,new EngineSoundFrame(rpm,gas,load,0,.85,idleRoughness:rpm<1100?.10:0));
            foreach(float value in block){if(!float.IsFinite(value)||Math.Abs(value)>=1)throw new Exception("Invalid production DSP sample");pcm.Write(value);}
        }
        Console.WriteLine("PASS: current production DSP baseline, source unchanged.");
    }
}
