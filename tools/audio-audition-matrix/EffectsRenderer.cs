using System.Text.Json;
using DragLab.Domain;

// Both sound versions receive the identical immutable frames captured from CarSimulation.
static class Program
{
    static void Write(string path,float[] values){using var w=new BinaryWriter(File.Create(path));foreach(var x in values)w.Write(x);}
    static EngineSoundFrame ReadFrame(BinaryReader reader,(string name,string type)[] fields)
    {
        var values=new Dictionary<string,object>();
        foreach(var f in fields){object v;
            if(f.type=="double")v=reader.ReadDouble();else if(f.type=="bool")v=reader.ReadBoolean();else if(f.type=="int")v=reader.ReadInt32();
            else{int length=reader.ReadInt32();var array=new double[length];for(int i=0;i<length;i++)array[i]=reader.ReadDouble();v=array;}
            values[f.name]=v;
        }
        double D(string name)=>(double)values[name];int I(string name)=>(int)values[name];bool B(string name)=>(bool)values[name];
        var contribution=(double[])values["cylinderContribution"];
        return new EngineSoundFrame(D("rpm"),D("throttle"),D("load"),D("boost"),D("volume"),D("slip"),D("speed"),B("active"),B("cut"),I("releaseId"),D("releaseStrength"),
            D("turboSpool"),D("superchargerBoost"),D("chargerLoad"),D("turboBoost"),D("combustionFraction"),D("idleRoughness"),D("intakeGulp"),D("fuelHesitation"),
            I("popId"),D("popStrength"),B("overrun"),B("twoStep"),(CutMethod)I("cutMethod"),contribution.Length==0?null:contribution);
    }
    static void Main(string[] args)
    {
        string folder=args[0];using var index=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"effects-capture/index.json")));
        Directory.CreateDirectory(Path.Combine(folder,"effects-raw"));int cases=0,silentFrames=0;
        foreach(var row in index.RootElement.GetProperty("cases").EnumerateArray()){
            string id=row.GetProperty("id").GetString();using var profileFile=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"effects-capture",id+"-profile.json")));
            var profile=new EngineSoundProfile(profileFile.RootElement);var before=new LegacyEngineSoundSynth(profile);var after=new EngineSoundSynth(profile);
            using var r=new BinaryReader(File.OpenRead(Path.Combine(folder,"effects-capture",id+".frames")));
            int count=r.ReadInt32(),columns=r.ReadInt32();var fields=new (string,string)[columns];
            for(int i=0;i<columns;i++)fields[i]=(r.ReadString(),r.ReadString());
            if(count!=720)throw new Exception("Expected 12 seconds of real 60 Hz audio publication");
            var a=new float[count*800];var b=new float[a.Length];var ba=new float[800];var bb=new float[800];
            for(int i=0;i<count;i++){
                var frame=ReadFrame(r,fields);before.Fill(ba,1,48000,frame);after.Fill(bb,1,48000,frame);
                foreach(float v in ba.Concat(bb))if(!float.IsFinite(v)||Math.Abs(v)>=1)throw new Exception("Invalid audio: "+id);
                if(!frame.active){if(ba.Any(v=>v!=0)||bb.Any(v=>v!=0))throw new Exception("Inactive capture leaked audio");silentFrames++;}
                Array.Copy(ba,0,a,i*800,800);Array.Copy(bb,0,b,i*800,800);
            }
            if(r.BaseStream.Position!=r.BaseStream.Length)throw new Exception("Unexpected trailing capture data");
            Write(Path.Combine(folder,"effects-raw",id+"-before.f32"),a);Write(Path.Combine(folder,"effects-raw",id+"-after.f32"),b);cases++;
        }
        Console.WriteLine($"PASS: {cases} physical-event before/after pairs; bounded PCM and {silentFrames} exact silent publication frames.");
    }
}
