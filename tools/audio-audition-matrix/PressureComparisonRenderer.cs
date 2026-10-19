using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DragLab.Domain;

// Offline stock-car comparisons: identical immutable control frames for both voices.
static class PressureComparisonRenderer
{
    const int Rate=48000,Block=400,Seconds=14;
    static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    static EngineSoundFrame[] Frames(EngineSoundProfile p)
    {
        var frames=new EngineSoundFrame[Rate*Seconds/Block];double spool=0,boost=0,sc=0,lastGas=0;int release=0;
        for(int i=0;i<frames.Length;i++)
        {
            double t=i*Block/(double)Rate,rpm,gas,load,dt=Block/(double)Rate;
            if(t<1.5){rpm=p.idleRpm;gas=.025;load=.025;}
            else if(t<6){double u=t<3.4?Smooth((t-1.5)/1.9):1-Smooth((t-3.4)/2.6);rpm=p.idleRpm+u*(Math.Min(6000,p.redlineRpm*.90)-p.idleRpm);gas=t<3.5?.92:.025;load=gas*.08;}
            else if(t<11){rpm=p.idleRpm+Smooth((t-6)/5)*(p.redlineRpm*.90-p.idleRpm);gas=.94;load=.90;}
            else{rpm=p.idleRpm+(p.redlineRpm*.90-p.idleRpm)*(1-Smooth((t-11)/2.4));gas=.025;load=.025;}
            double envelope=Smooth((rpm-1800)/1900),target=p.turboCount>0?Math.Sqrt(gas*(.06+.94*load))*envelope:0;
            spool+=(target-spool)*(1-Math.Exp(-dt/(target>spool?p.spoolSeconds:.55)));
            double prior=boost,bt=p.turboCount>0?p.boostBar*envelope*spool*spool*gas:0;
            boost+=(bt-boost)*(1-Math.Exp(-dt/(bt>boost?.075:.045)));
            double st=p.superchargerCount>0?p.boostBar*Math.Clamp(rpm/1600,0,1)*gas*(.08+.92*load):0;
            sc+=(st-sc)*(1-Math.Exp(-dt/.025));
            if(lastGas-gas>.25&&prior>.1&&p.fueling!=FuelSystem.Diesel)release++;lastGas=gas;
            frames[i]=new EngineSoundFrame(rpm,gas,load,boost+sc,.85,turboSpool:spool,turboBoost:boost,superchargerBoost:sc,chargerLoad:load,releaseId:release,releaseStrength:Math.Clamp(prior/Math.Max(.1,p.boostBar),0,1),idleRoughness:rpm<p.idleRpm+300?p.camOverlap*.15:0,overrun:gas<.1&&rpm>p.idleRpm+900);
        }
        return frames;
    }
    static float[] Render(EngineSoundProfile p,EngineSoundFrame[] frames,bool after,int chunk=Block,uint seed=117)
    {
        var baseline=new EngineSoundSynth(p,seed);var candidate=new PressureAuditionSynth(p,seed);
        var output=new float[frames.Length*Block];var scratch=new float[chunk];
        for(int frame=0;frame<frames.Length;frame++)for(int at=0;at<Block;at+=chunk)
        {
            int n=Math.Min(chunk,Block-at);if(scratch.Length!=n)scratch=new float[n];
            if(after)candidate.Fill(scratch,1,Rate,frames[frame]);else baseline.Fill(scratch,1,Rate,frames[frame]);
            Array.Copy(scratch,0,output,frame*Block+at,n);
        }
        foreach(float x in output)if(!float.IsFinite(x)||Math.Abs(x)>1)throw new Exception("Nonfinite or unbounded sample");
        return output;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static double Energy(float[] x){double e=0;foreach(var v in x)e+=v*v;return e/x.Length;}
    static void Write(string path,float[] x){using var f=new BinaryWriter(File.Create(path));foreach(float v in x)f.Write(v);}
    static object CoreChecks(EngineSoundProfile p)
    {
        var frame=new EngineSoundFrame(3000,.8,.9,0,.85);
        var core=new PressureEventCore(p);for(int i=0;i<Rate;i++)core.Sample(3000,.8,frame);
        long initial=core.Events,allocationStart=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        // StartNew allocates the timing object; measure audio separately after it.
        allocationStart=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<Rate*2;i++)core.Sample(3000,.8,frame);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-allocationStart;watch.Stop();
        Check(allocated==0,"Core Sample allocated managed memory");Check(core.Events-initial==p.cylinders*50,"Incorrect 3000 RPM firing rate");
        Check(core.CylinderEvents.Max()-core.CylinderEvents.Min()<=1,"Cylinder sequence unbalanced");
        var zero=new double[p.cylinders];var disabled=new EngineSoundFrame(3000,.8,.9,0,.85,cylinderContribution:zero);
        var muted=new PressureEventCore(p);double energy=0;for(int i=0;i<Rate;i++){double v=muted.Sample(3000,.8,disabled);energy+=v*v;}
        Check(energy==0,"Disabled cylinders still emitted combustion pressure");
        var healthy=new EngineSoundFrame(3000,.8,.9,0,.85);var contributions=Enumerable.Repeat(1.0,p.cylinders).ToArray();contributions[0]=0;
        var single=new EngineSoundFrame(3000,.8,.9,0,.85,cylinderContribution:contributions);var healthyCore=new PressureEventCore(p);var wornCore=new PressureEventCore(p);double difference=0;
        for(int i=0;i<Rate;i++){double delta=healthyCore.Sample(3000,.8,healthy)-wornCore.Sample(3000,.8,single);difference+=delta*delta;}
        Check(difference>1e-8,"An individual failed cylinder did not change pressure output");
        var cut=new EngineSoundFrame(3000,.8,.9,0,.85,cut:true);var cutCore=new PressureEventCore(p);energy=0;
        for(int i=0;i<Rate;i++){double v=cutCore.Sample(3000,.8,cut);energy+=v*v;}Check(energy==0,"Cut engine emitted combustion pressure");
        var scratch=new float[Block];var full=new PressureAuditionSynth(p);for(int i=0;i<20;i++)full.Fill(scratch,1,Rate,frame);
        long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<120;i++)full.Fill(scratch,1,Rate,frame);
        long fullAllocated=GC.GetAllocatedBytesForCurrentThread()-start;Check(fullAllocated==0,"Candidate Fill allocated managed memory");
        return new{rate=Rate,managedBytesPerCoreSampleRun=allocated,managedBytesPerFullFillRun=fullAllocated,bufferBytes=core.BufferBytes,desktopMillisecondsForTwoAudioSeconds=watch.Elapsed.TotalMilliseconds};
    }
    static void Main(string[] args)
    {
        string folder=args[0];using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"cases.json")));
        var measurements=new List<object>();
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string id=row.GetProperty("id").GetString();var p=new EngineSoundProfile(row.GetProperty("profile"));var frames=Frames(p);
            var before=Render(p,frames,false);var after=Render(p,frames,true);
            Check(after.SequenceEqual(Render(p,frames,true)),"Nonrepeatable render");
            Check(!after.SequenceEqual(Render(p,frames,true,Block,491)),"Private seed did not affect combustion variation");
            Check(after.SequenceEqual(Render(p,frames,true,128)),"Audio depends on callback buffer boundary");
            Check(!before.SequenceEqual(after)&&Energy(after)>1e-8,"Candidate is unchanged or silent");
            var open=JsonNode.Parse(row.GetProperty("profile").GetRawText());open["audition"]["exhaustPreset"]=3;
            Check(!after.SequenceEqual(Render(new EngineSoundProfile(JsonSerializer.SerializeToElement(open)),frames,true)),"Installed exhaust path no longer affects the candidate");
            if(p.turboCount>0)
            {
                var noWhistle=JsonNode.Parse(row.GetProperty("profile").GetRawText());noWhistle["sound"]["turboGain"]=0;
                Check(!after.SequenceEqual(Render(new EngineSoundProfile(JsonSerializer.SerializeToElement(noWhistle)),frames,true)),"Turbo layer was lost");
                var noRelease=JsonNode.Parse(row.GetProperty("profile").GetRawText());noRelease["sound"]["blowOffGain"]=0;
                Check(!after.SequenceEqual(Render(new EngineSoundProfile(JsonSerializer.SerializeToElement(noRelease)),frames,true)),"Release layer was lost");
            }
            var noPop=new EngineSoundFrame(3000,.7,.7,0,.85);var withPop=new EngineSoundFrame(3000,.7,.7,0,.85,popId:1,popStrength:.8);
            Check(!Render(p,Enumerable.Repeat(noPop,24).ToArray(),true).SequenceEqual(Render(p,Enumerable.Repeat(withPop,24).ToArray(),true)),"Exhaust-pop events were lost");
            Write(Path.Combine(folder,id+"-before.f32"),before);Write(Path.Combine(folder,id+"-after.f32"),after);
            using(var f=new StreamWriter(Path.Combine(folder,id+"-controls.csv"))){f.WriteLine("sample,rpm,throttle,load,turboBoost,turboSpool,releaseId");for(int i=0;i<frames.Length;i++){var v=frames[i];f.WriteLine(FormattableString.Invariant($"{i*Block},{v.rpm:R},{v.throttle:R},{v.load:R},{v.turboBoost:R},{v.turboSpool:R},{v.releaseId}"));}}
            measurements.Add(new{id,checks=CoreChecks(p)});Console.WriteLine("PASS: "+id+" before/after, replay, variation, block boundaries, cuts/wear, allocation checks");
        }
        File.WriteAllText(Path.Combine(folder,"dsp-checks.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions{WriteIndented=true}));
    }
}
