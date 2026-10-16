using System.Text.Json;
using DragLab.Domain;

// OFFLINE AUDITION ONLY: scripted healthy-engine frames, no player state or Unity API.
static class Program
{
    const int Rate=48000, Block=400, Seconds=10;
    static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    static double N(JsonElement e,string key,double fallback)=>e.TryGetProperty(key,out var v)?v.GetDouble():fallback;
    static EngineSoundFrame Frame(EngineSoundProfile p,JsonElement spec,double t,ref double spool,ref double boost,ref double sc,ref double lastGas,ref int release)
    {
        double rpm,gas,load;
        if(t<1.5){rpm=p.idleRpm;gas=.025;load=.025;}
        else if(t<3.5){double rev=Math.Max(0,1-Math.Abs(t-2.4)/.8);rpm=p.idleRpm+Smooth(rev)*(Math.Min(4700,p.redlineRpm-400)-p.idleRpm);gas=rev>.08?.82:.025;load=gas*.08;}
        else if(t<8){rpm=p.idleRpm+Smooth((t-3.5)/4)*(p.redlineRpm-p.idleRpm-400);gas=.94;load=.90;}
        else{rpm=p.idleRpm+(p.redlineRpm-p.idleRpm-400)*Math.Exp(-(t-8)*1.4);gas=.025;load=.025;}
        var d=spec.GetProperty("induction");var o=spec.GetProperty("audition");double size=N(o,"size",1),pulley=N(o,"pulley",1),dt=Block/(double)Rate;
        double threshold=N(d,"turboThresholdRpm",1800)+(size-1)*1800;
        double envelope=Smooth((rpm-threshold)/(N(d,"turboFullBoostRpm",3600)-N(d,"turboThresholdRpm",1800)));
        double target=p.turboCount>0?Math.Sqrt(gas*(.06+.94*load))*envelope:0;
        double time=p.spoolSeconds*Math.Pow(size,1.7)*(p.turboCount==2?N(d,"twinTurboSpoolFactor",.72):1);
        spool+=(target-spool)*(1-Math.Exp(-dt/(target>spool?time:N(d,"turboCoastSeconds",.55))));
        double prior=boost,turboTarget=p.turboCount>0?p.boostBar*envelope*spool*spool*gas:0;
        boost+=(turboTarget-boost)*(1-Math.Exp(-dt/(turboTarget>boost?N(d,"manifoldFillSeconds",.075):N(d,"valveReleaseSeconds",.045))));
        double scMax=N(d,"superchargerBoostBar",.5)*pulley*pulley;
        double bypass=p.induction==InductionType.Twincharged?Math.Clamp((boost/Math.Max(.01,scMax)-.35)/.5,0,1):0;
        double scTarget=p.superchargerCount>0?scMax*Math.Clamp(rpm/N(d,"superchargerFullBoostRpm",1600),0,1)*gas*(.08+.92*load)*(1-bypass):0;
        sc+=(scTarget-sc)*(1-Math.Exp(-dt/N(d,"superchargerResponseSeconds",.025)));
        if(p.fueling!=FuelSystem.Diesel&&lastGas-gas>.25&&prior>.1)release++;
        lastGas=gas;
        double total=p.turboCount>0&&p.superchargerCount>0?(1.013+boost)*(1+sc/1.013)-1.013:boost+sc;
        return new EngineSoundFrame(rpm,gas,load,total,.85,turboSpool:spool,turboBoost:boost,superchargerBoost:sc,chargerLoad:load*(1-bypass),releaseId:release,releaseStrength:Math.Clamp(prior/Math.Max(.1,p.boostBar),0,1),idleRoughness:rpm<p.idleRpm+300?p.camOverlap*.15:0,overrun:t>=8);
    }
    static float[] Render(JsonElement row,bool after,uint seed=117,int block=Block)
    {
        var spec=row.GetProperty("profile");var p=new EngineSoundProfile(spec);
        var before=new EngineSoundSynth(p);var proposed=new AuditionSynth(p,seed);
        var result=new float[Rate*Seconds];var buffer=new float[block];double spool=0,boost=0,sc=0,last=0;int release=0;
        // Input is captured every 400 samples for both sides. Phase and filter states persist.
        for(int at=0;at<result.Length;at+=block){var f=Frame(p,spec,at/(double)Rate,ref spool,ref boost,ref sc,ref last,ref release);if(after)proposed.Fill(buffer,1,Rate,f);else before.Fill(buffer,1,Rate,f);Array.Copy(buffer,0,result,at,block);}
        foreach(float x in result)if(!float.IsFinite(x)||Math.Abs(x)>1)throw new Exception("Invalid DSP sample");
        return result;
    }
    static void Write(string path,float[] values){using var writer=new BinaryWriter(File.Create(path));foreach(var x in values)writer.Write(x);}
    static void Main(string[] args)
    {
        string folder=args[0];using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"cases.json")));
        bool parity=args.Length>1&&args[1]=="--parity";
        var cases=doc.RootElement.EnumerateArray().Select(x=>x.Clone()).ToArray();int limit=args.Length>1&&!parity?int.Parse(args[1]):cases.Length;
        cases=cases.Take(limit).ToArray();Directory.CreateDirectory(Path.Combine(folder,"raw"));int done=0;
        Parallel.ForEach(cases,new ParallelOptions{MaxDegreeOfParallelism=4},row=>{
            string id=row.GetProperty("id").GetString();string a=Path.Combine(folder,"raw",id+"-before.f32"),b=Path.Combine(folder,"raw",id+"-after.f32");
            var first=Render(row,false);var second=Render(row,true);
            if(parity){
                if(!first.SequenceEqual(second))throw new Exception("Integrated/approved PCM mismatch: "+id);
                if(args.Length>2){var bytes=File.ReadAllBytes(Path.Combine(args[2],id+"-after.f32"));var recorded=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,recorded,0,bytes.Length);if(!first.SequenceEqual(recorded))throw new Exception("Published audition PCM mismatch: "+id);}
            }else{Write(a,first);Write(b,second);}
            if(Interlocked.Increment(ref done)%100==0)Console.WriteLine($"Rendered {done}/{cases.Length} pairs");
        });
        foreach(var row in cases.Where((x,i)=>i%100==0)){
            var a=Render(row,true);var b=Render(row,true);var c=Render(row,true,491);
            if(!a.SequenceEqual(b)||a.SequenceEqual(c))throw new Exception("Variation determinism failed");
        }
        Console.WriteLine($"PASS: {cases.Length} finite bounded {(parity?"integrated/approved sample-exact comparisons":"before/after pairs")}; repeatability and private-seed variation checks passed.");
    }
}
