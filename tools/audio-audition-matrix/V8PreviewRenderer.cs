using System.Text.Json;
using DragLab.Domain;

// Offline only. Identical scripted healthy-engine input for both acoustic profiles.
static class V8Preview
{
    const int Rate=48000,Block=400;
    static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    static EngineSoundFrame Frame(EngineSoundProfile p,string scene,double t)
    {
        double rpm=p.idleRpm,gas=.025,load=.025;
        if(scene=="rev") {
            double blip=Math.Max(Math.Max(0,1-Math.Abs(t-1.8)/.9),Math.Max(0,1-Math.Abs(t-4.4)/1.1));
            rpm+=Smooth(blip)*(5100-p.idleRpm);gas=blip>.05?.9:.025;load=gas*.08;
        }
        if(scene=="hold") {
            if(t>=1&&t<2){rpm=p.idleRpm+Smooth(t-1)*(2500-p.idleRpm);gas=.5;}
            else if(t>=2&&t<3.4){rpm=2500;gas=.25;}
            else if(t>=3.4&&t<4.4){rpm=2500+Smooth(t-3.4)*2000;gas=.75;}
            else if(t>=4.4&&t<6.8){rpm=4500;gas=.35;}
            else if(t>=6.8)rpm=p.idleRpm+(4500-p.idleRpm)*Math.Exp(-(t-6.8)*1.8);
            load=gas*.08;
        }
        if(scene=="pass"&&t>=1&&t<9.4) {
            double u=t-1;int gear=Math.Min(2,(int)(u/2.8));double f=(u%2.8)/2.8;
            double[] low={1800,3400,4000},high={5200,5600,6100};rpm=low[gear]+Smooth(f)*(high[gear]-low[gear]);
            gas=gear>0&&f<.06?0:.96;load=gas*.94;
        }else if(scene=="pass"&&t>=9.4)rpm=p.idleRpm+(6100-p.idleRpm)*Math.Exp(-(t-9.4)*1.5);
        return new EngineSoundFrame(rpm,gas,load,0,.85,idleRoughness:rpm<p.idleRpm+300?p.camOverlap*.15:0,overrun:scene=="pass"&&t>=9.4);
    }
    static void Main(string[] args)
    {
        string folder=args[0];using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"profiles.json")));
        foreach(var scene in new[]{"idle","rev","hold","pass"}) {
            int seconds=scene=="idle"?4:scene=="rev"?7:scene=="hold"?9:12;
            var original=new EngineSoundProfile(doc.RootElement.GetProperty("before"));
            foreach(var side in new[]{"before","after"}) {
                var profile=new EngineSoundProfile(doc.RootElement.GetProperty(side));var synth=new EngineSoundSynth(profile,117);var repeat=new EngineSoundSynth(profile,117);var proposed=new PreviewGrowlSynth(profile,117);var proposedRepeat=new PreviewGrowlSynth(profile,117);
                var pcm=new float[Rate*seconds];var block=new float[Block];var again=new float[Block];
                for(int at=0;at<pcm.Length;at+=Block) {
                    var frame=Frame(original,scene,at/(double)Rate);
                    if(side=="after"){proposed.Fill(block,1,Rate,frame);proposedRepeat.Fill(again,1,Rate,frame);}else{synth.Fill(block,1,Rate,frame);repeat.Fill(again,1,Rate,frame);}
                    if(!block.SequenceEqual(again))throw new Exception("Private seed is not repeatable");
                    if(block.Any(x=>!float.IsFinite(x)||Math.Abs(x)>=1))throw new Exception("Nonfinite or clipped DSP sample");
                    Array.Copy(block,0,pcm,at,Block);
                }
                using var writer=new BinaryWriter(File.Create(Path.Combine(folder,scene+"-"+side+".f32")));foreach(float sample in pcm)writer.Write(sample);
            }
        }
        Console.WriteLine("PASS: eight finite bounded repeatable renders, identical RPM/load/firing input.");
    }
}
