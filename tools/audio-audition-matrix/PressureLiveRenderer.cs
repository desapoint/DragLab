using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DragLab.Domain;

static class PressureLiveRenderer
{
    const int Rate=48000,Block=400;
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    static double Rms(float[] x){double e=0;foreach(float v in x)e+=v*v;return Math.Sqrt(e/x.Length);}
    static float[] Render(EngineSoundProfile p,EngineSoundFrame[] frames,bool frozen=false,int chunk=Block,uint seed=117,int rate=Rate)
    {
        var live=new EngineSoundSynth(p,seed,sampleRate:rate);var prior=new FrozenApprovedSynth(p,seed);
        var result=new float[frames.Length*Block];var buffer=new float[chunk];
        for(int f=0;f<frames.Length;f++)for(int at=0;at<Block;at+=chunk)
        {
            int n=Math.Min(chunk,Block-at);if(buffer.Length!=n)buffer=new float[n];
            if(frozen)prior.Fill(buffer,1,rate,frames[f]);else live.Fill(buffer,1,rate,frames[f]);
            Array.Copy(buffer,0,result,f*Block+at,n);
        }
        foreach(float v in result)if(!float.IsFinite(v)||Math.Abs(v)>=1)throw new Exception("Unbounded/nonfinite PCM");Check(true,"Bounded render");return result;
    }
    static EngineSoundProfile Variant(JsonElement source,int cylinders,int layout,int induction)
    {
        var p=JsonNode.Parse(source.GetRawText());p["sound"]["cylinders"]=cylinders;p["sound"]["layout"]=layout;
        p["displacementLitres"]=Math.Min(12,Math.Max(.4,cylinders*.45));p["audition"]["topology"]=induction;
        return new EngineSoundProfile(JsonSerializer.SerializeToElement(p));
    }
    static void Main(string[] args)
    {
        string root=args[0];using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"cases.json")));
        var rows=doc.RootElement.EnumerateArray().ToArray();var stats=new List<object>();
        // Populate the bounded shared cache with the normal device rates first.
        var initial=new EngineSoundProfile(rows[0].GetProperty("profile"));new EngineSoundSynth(initial);new EngineSoundSynth(initial,sampleRate:32000);new EngineSoundSynth(initial,sampleRate:96000);
        foreach(var row in rows)
        {
            string id=row.GetProperty("id").GetString();var profile=new EngineSoundProfile(row.GetProperty("profile"));
            var frames=ComparisonInputs.Frames(profile);var full=Render(profile,frames);
            if(id=="StarterHatch"||id=="V8Muscle"){
                var stable=Enumerable.Repeat(new EngineSoundFrame(4500,.9,.85,0,.85),120).ToArray();
                Check(Render(profile,stable).SequenceEqual(Render(profile,stable,true)),"Approved steady NA pressure voice changed: "+id);
            }
            if(id=="StarterHatch"||id=="V8Muscle"||id=="RallySedan")
            {
                Check(full.SequenceEqual(Render(profile,frames,false,128)),"Callback boundary changed output");
                Check(full.SequenceEqual(Render(profile,frames)),"Same seed not exact");Check(!full.SequenceEqual(Render(profile,frames,seed:491)),"Seed variation absent");
                using var writer=new BinaryWriter(File.Create(Path.Combine(root,id+"-live.f32")));foreach(float v in full)writer.Write(v);
            }
            stats.Add(new{id,rms=Rms(full),peak=full.Max(x=>Math.Abs(x))});
        }
        var baseProfile=rows[0].GetProperty("profile");var steady=new EngineSoundFrame(4500,.85,.85,.6,.85,turboSpool:.8,turboBoost:.6,airflowKgS:.18);
        for(int n=1;n<=16;n++)foreach(int layout in new[]{0,1,2})foreach(int induction in new[]{0,1,2,3,4,5})
        {
            var p=Variant(baseProfile,n,layout,induction);var synth=new EngineSoundSynth(p,sampleRate:Rate);var pcm=new float[4096];synth.Fill(pcm,2,Rate,steady);
            Check(pcm.All(v=>float.IsFinite(v)&&Math.Abs(v)<1)&&Rms(pcm)>0,"Cylinder/layout/induction grid invalid");
        }
        var turboProfile=Variant(baseProfile,8,2,1);
        foreach(int rate in new[]{8000,22050,32000,44100,48000,96000})
        {
            var synth=new EngineSoundSynth(turboProfile,sampleRate:rate);var pcm=new float[4096];synth.Fill(pcm,2,rate,steady);
            Check(pcm.All(v=>float.IsFinite(v)&&Math.Abs(v)<1),"Device sample rate failed");
        }
        var perf=new List<object>();
        foreach(var config in new[]{(4,0,1,6000.0),(8,2,0,6500.0),(16,1,5,12000.0)})
        {
            var p=Variant(baseProfile,config.Item1,config.Item2,config.Item3);var s=new EngineSoundSynth(p,sampleRate:Rate);
            var frame=new EngineSoundFrame(config.Item4,1,1,1,.85,turboSpool:1,turboBoost:.6,superchargerBoost:.4,chargerLoad:1,airflowKgS:.25);
            var buffer=new float[1024];for(int i=0;i<24;i++)s.Fill(buffer,2,Rate,frame);
            var times=new double[256];var timer=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<times.Length;i++){timer.Restart();s.Fill(buffer,2,Rate,frame);timer.Stop();times[i]=timer.Elapsed.TotalMilliseconds;}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(times);
            Check(allocated==0,"Warmed callback allocated");Check(times[242]<512.0/Rate*1000*.25,"Desktop callback p95 exceeds 25% signal budget");
            perf.Add(new{cylinders=config.Item1,layout=config.Item2,induction=config.Item3,rpm=config.Item4,rate=Rate,framesPerCallback=512,allocatedBytes=allocated,p95Milliseconds=times[242],maxMilliseconds=times[^1]});
        }
        var one=new EngineSoundProfile(baseProfile);var failed=Enumerable.Repeat(1.0,one.cylinders).ToArray();failed[0]=0;
        var healthyFrame=new EngineSoundFrame(3000,.8,.9,0,.85);var wornFrame=new EngineSoundFrame(3000,.8,.9,0,.85,cylinderContribution:failed);
        Check(!Render(one,Enumerable.Repeat(healthyFrame,120).ToArray()).SequenceEqual(Render(one,Enumerable.Repeat(wornFrame,120).ToArray())),"Cylinder wear lost");
        var cutFrame=new EngineSoundFrame(3000,.8,.9,0,.85,cut:true);
        Check(Rms(Render(one,Enumerable.Repeat(cutFrame,120).ToArray()))<Rms(Render(one,Enumerable.Repeat(healthyFrame,120).ToArray()))*.3,"Cut attenuation lost");
        File.WriteAllText(Path.Combine(root,"verification.json"),JsonSerializer.Serialize(new{checks,cars=stats,performance=perf},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} signal checks; {rows.Length} authored cars; 288 cylinder/layout/induction combinations; sample rates, allocations and callback budget; reviewed NA voices sample-exact.");
    }
}
