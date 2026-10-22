using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Editor
{
    // Editor-only observation/replay. Never changes production DSP, game saves or preferences.
    public static class UnityAudioReferenceExport
    {
        const int Rate=48000, Block=400, Tail=240;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        static readonly FieldInfo[] EngineFields=typeof(EngineSoundFrame).GetFields();
        static readonly MethodInfo Record=typeof(ExhaustMixtureModel).GetMethod("Record",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly FieldInfo PublishedTime=typeof(ExhaustMixtureModel).GetField("publishedTime",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly StringBuilder Report=new StringBuilder();
        static int checks;
        static void Check(bool value,string label)
        {label=label.TrimEnd();Report.AppendLine((value?"PASS ":"FAIL ")+label);checks++;if(!value)throw new InvalidOperationException(label);}
        static string Hash(byte[] bytes)
        {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static string Arg(string name,string fallback)
        {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i<0?fallback:i+1<a.Length?a[i+1]:throw new ArgumentException(name+" needs a value");}
        static void CopyFields(object from,object to)
        {
            foreach(var target in to.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                var source=from.GetType().GetField(target.Name);if(source==null)continue;
                object value=source.GetValue(from);
                target.SetValue(to,target.FieldType==typeof(string)&&value is ulong?((ulong)value).ToString(Inv):value is Array?((Array)value).Clone():value);
            }
        }
        [Serializable] public sealed class EngineData
        {
            public double rpm,throttle,load,boost,volume,slip,speed,releaseStrength,turboSpool,superchargerBoost,chargerLoad,turboBoost;
            public double combustionFraction,idleRoughness,intakeGulp,fuelHesitation,popStrength,airflowKgS,afterfireVolume,exhaustGasTemperatureK;
            public bool active,cut,overrun,twoStep;public int releaseId,popId;public CutMethod cutMethod;public double[] cylinderContribution;
            public static EngineData From(EngineSoundFrame f){var d=new EngineData();CopyFields(f,d);d.cylinderContribution=d.cylinderContribution??Array.Empty<double>();return d;}
            public EngineSoundFrame Frame(RuntimeExhaustState state)=>new EngineSoundFrame(rpm,throttle,load,boost,volume,slip,speed,active,cut,releaseId,releaseStrength,
                turboSpool,superchargerBoost,chargerLoad,turboBoost,combustionFraction,idleRoughness,intakeGulp,fuelHesitation,popId,popStrength,overrun,twoStep,cutMethod,
                cylinderContribution,airflowKgS,state.mixture,afterfireVolume,state,exhaustGasTemperatureK);
        }
        [Serializable] public sealed class Wheel
        {
            public double intensity,slidingSpeed,slip,grip,normalLoadN,radiusM,widthScale,compoundGrip;
            public static Wheel From(TireFeedback f){var d=new Wheel();CopyFields(f,d);return d;}
            public TireFeedback Contact()=>new TireFeedback{intensity=intensity,slidingSpeed=slidingSpeed,slip=slip,grip=grip,normalLoadN=normalLoadN,radiusM=radiusM,widthScale=widthScale,compoundGrip=compoundGrip};
        }
        [Serializable] public sealed class Tires
        {
            public Wheel fl,fr,rl,rr;public double speed,volume;public bool active;
            public static Tires From(TireSoundFrame f)=>new Tires{fl=Wheel.From(f.fl),fr=Wheel.From(f.fr),rl=Wheel.From(f.rl),rr=Wheel.From(f.rr),speed=f.speed,volume=f.volume,active=f.active};
            public TireSoundFrame Frame()=>new TireSoundFrame(fl.Contact(),fr.Contact(),rl.Contact(),rr.Contact(),speed,volume,active);
        }
        [Serializable] public sealed class Reaction
        {
            public int availableSample,index,zone,frontId;public string seed;
            public double pressurePa,riseSeconds,burnSeconds,reactingVolumeM3,flowMps,time,position,fuelKg,oxygenKg,releasedJ,acousticJ,gasK,wallK,intakeK,probability,turboBoost,rpm;
            public bool limiter,twoStep;public AfterfireCause cause;
            public AfterfireLabSettings settings;
            public static Reaction From(ExhaustMixtureModel.Event e,int sample,AfterfireHardwareAcoustics h)
            {var d=new Reaction{availableSample=sample,settings=h.At(e)};CopyFields(e,d);return d;}
            public ExhaustMixtureModel.Event Event()
            {
                object e=new ExhaustMixtureModel.Event();
                foreach(var f in typeof(ExhaustMixtureModel.Event).GetFields())
                {object v=GetType().GetField(f.Name).GetValue(this);f.SetValue(e,f.FieldType==typeof(ulong)?ulong.Parse((string)v,Inv):v);}
                return (ExhaustMixtureModel.Event)e;
            }
        }
        [Serializable] public sealed class Release
        {public int availableSample,index;public double time,strength;}
        [Serializable] public sealed class State
        {
            public int gear;public double clutch,shiftRemaining,engineTime,distance,elapsed,reaction,activeLimitRpm;
            public bool transmissionTorqueCut,launched,finished,limiter,launchControlActive,antiLagActive;
            public static State From(CarState s){var d=new State();CopyFields(s,d);return d;}
        }
        [Serializable] public sealed class Frame
        {public int sample;public double time,mixtureTime;public string stage;public EngineData engine;public Tires tires;public State state;}
        [Serializable] public sealed class Listener
        {
            public string motion="following car; fixed relative geometry";
            public double temperatureK,humidity=.5,pressurePa,reflectionScale=1,longitudinal,lateral=12,sourceHeight=.55,tireHeight=.08,listenerHeight=1.65;
            public TracksidePropagation.Frame Frame()=>new TracksidePropagation.Scene(temperatureK,humidity,pressurePa,reflectionScale).At(longitudinal,lateral,sourceHeight,listenerHeight);
        }
        [Serializable] public sealed class Seeds
        {
            public uint engine=117,engineNoise=0x28a7c5,releaseNoise=0x7a16b59;
            public uint[] tires={0x28a7c5,0x37ac51,0x9df85,0x982ddf};
            public long mixtureMaster=20261020;public string mixtureIdentity,afterfire="Lossless decimal UInt64 strings. Source seed = low32 XOR high32 of the first reaction in each frontId.";
        }
        [Serializable] public sealed class Trace
        {
            public string schema="draglab-unity-audio-trace-v1",id,title,sourceCommit,sourceFixtureSha256,unityVersion=Application.unityVersion;
            public string physicsModel=MechanicsSchema.Model;
            public string timing="Each frame supplies the following 400 stereo samples; events become available only at availableSample and fire no earlier than their exact time. Frame zero binds acoustic state before physics. Final 240 frames hold the last inputs; they do not advance physics.";
            public string units="seconds, metres, m/s, kg, kelvin, pascal, joules; boost is gauge bar. Enums are integers with enumNames. No normalized/loudness-matched PCM or boundary fades.";
            public int sampleRate=Rate,channels=2,physicsRate=120,samplesPerFrame=Block,totalSamples,physicsTicks,tailFrames=Tail;
            public double engineCategory=.85,tireCategory=1,master=1,compressorNoise=1,elapsed,acousticJ;
            public VehicleSpec spec;public Tune tune;public Seeds seeds=new Seeds();public Listener listener;
            public List<Frame> frames=new List<Frame>();public List<Reaction> reactions=new List<Reaction>();public List<Release> releases=new List<Release>();
        }
        // Unlike JsonUtility, this records readonly numeric profile fields too. UInt64
        // is always quoted so JavaScript cannot silently round deterministic seeds.
        internal static string Json(object value)
        {var b=new StringBuilder();Write(b,value);return b.ToString();}
        static void Write(StringBuilder b,object value)
        {
            if(value==null){b.Append("null");return;}
            if(value is string)
            {
                b.Append('"');foreach(char c in (string)value){if(c=='"'||c=='\\')b.Append('\\').Append(c);else if(c<32)b.Append("\\u").Append(((int)c).ToString("x4",Inv));else b.Append(c);}b.Append('"');return;
            }
            if(value is bool){b.Append((bool)value?"true":"false");return;}
            if(value is ulong){Write(b,((ulong)value).ToString(Inv));return;}
            if(value.GetType().IsEnum){b.Append(Convert.ToInt32(value,Inv));return;}
            if(value is double||value is float)
            {double n=Convert.ToDouble(value,Inv);if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidOperationException("Non-finite trace value");b.Append(value is double?n.ToString("G17",Inv):((float)value).ToString("G9",Inv));return;}
            if(value is IConvertible){b.Append(Convert.ToString(value,Inv));return;}
            if(value is IDictionary){b.Append('{');bool first=true;foreach(DictionaryEntry e in (IDictionary)value){if(!first)b.Append(',');first=false;Write(b,(string)e.Key);b.Append(':');Write(b,e.Value);}b.Append('}');return;}
            if(value is IEnumerable){b.Append('[');bool first=true;foreach(var x in (IEnumerable)value){if(!first)b.Append(',');first=false;Write(b,x);}b.Append(']');return;}
            b.Append('{');bool start=true;
            foreach(var f in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {if(!start)b.Append(',');start=false;Write(b,f.Name);b.Append(':');Write(b,f.GetValue(value));}b.Append('}');
        }
        static void Wav(string path,float[] pcm)
        {
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+pcm.Length*3);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);
                w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*6);w.Write((short)6);w.Write((short)24);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(pcm.Length*3);
                foreach(float v in pcm){if(float.IsNaN(v)||float.IsInfinity(v)||Math.Abs(v)>=1)throw new InvalidOperationException("Invalid/clipped reference PCM");int q=(int)Math.Round(v*8388607);w.Write((byte)q);w.Write((byte)(q>>8));w.Write((byte)(q>>16));}
            }
        }
        static void Put(float[] target,float[] block,int sample)=>Array.Copy(block,0,target,sample*2,block.Length);
        static void Capture(string input,string output,string commit)
        {
            byte[] original=File.ReadAllBytes(input);var f=JsonUtility.FromJson<RaceAudioTestFixture>(Encoding.UTF8.GetString(original));
            var race=f.CreateRace();
            try
            {
                var car=race.player;var profile=new EngineSoundProfile(car.spec,car.tune);var control=new EngineSoundController(car.spec,car.tune);
                var sound=new EngineSoundSynth(profile,117,Rate);var tires=new TireSoundSynth();var tireProfile=new TireSoundProfile(car.spec,car.tune);
                var trace=new Trace{id=f.id,title=f.title,sourceCommit=commit,sourceFixtureSha256=Hash(original),physicsTicks=f.ticks.Count,spec=car.spec.Copy(),tune=car.tune.Copy(),
                    totalSamples=(f.ticks.Count+1+Tail)*Block,listener=new Listener{temperatureK=car.spec.induction.ambientTemperatureK,pressurePa=car.spec.induction.ambientPressureBar*100000}};
                trace.seeds.mixtureIdentity=profile.layout+":"+profile.cylinders+":"+profile.litres.ToString("R",Inv);
                var geometry=trace.listener.Frame();var outdoor=new TracksidePropagation(Rate);var tireOutdoor=new TracksidePropagation(Rate);
                var raw=new float[trace.totalSamples*2];var engine=new float[raw.Length];var tire=new float[raw.Length];
                var a=new float[Block*2];var b=new float[a.Length];var hardware=new AfterfireHardwareAcoustics(profile);
                int eventCursor=0,releaseCursor=0;string stage="Initial idle";
                for(int i=0;i<f.ticks.Count+1+Tail;i++)
                {
                    if(i>0&&i<=f.ticks.Count)
                    {
                        var tick=f.ticks[i-1];if(!string.IsNullOrEmpty(tick.stage))stage=tick.stage;car.state.warmupLineLock=tick.lineLock;
                        if(tick.beginCountdown&&!race.BeginCountdown())throw new InvalidOperationException("Cannot stage "+f.id);
                        if(race.phase==RacePhase.Finished)race.Shutdown(tick.input);else race.Step(tick.input);
                    }
                    if(i==f.ticks.Count+1)stage="Hold final audio frame / drain published events";
                    var sf=control.Update(car.state,true,trace.engineCategory,DragMath.FixedStep);
                    var tf=TireSoundFrame.From(car.state,true,trace.tireCategory,car.spec.drive,tireProfile);var state=car.state.audioExhaust;
                    int sample=i*Block;
                    trace.frames.Add(new Frame{sample=sample,time=sample/(double)Rate,mixtureTime=state.mixture.Time,stage=stage,engine=EngineData.From(sf),tires=Tires.From(tf),state=State.From(car.state)});
                    while(eventCursor<state.mixture.Sequence)
                    {if(!state.mixture.Read(++eventCursor,out var e))throw new InvalidOperationException("Lost reaction");trace.reactions.Add(Reaction.From(e,sample,hardware));}
                    while(releaseCursor<state.ReleaseSequence)
                    {if(!state.ReadRelease(++releaseCursor,out var e))throw new InvalidOperationException("Lost release");trace.releases.Add(new Release{availableSample=sample,index=releaseCursor,time=e.time,strength=e.strength});}
                    sound.Fill(a,2,Rate,sf,trace.master,trace.compressorNoise);Put(raw,a,sample);outdoor.Process(a,2,geometry);Put(engine,a,sample);
                    tires.Fill(b,2,Rate,tf,trace.master);tireOutdoor.Process(b,2,geometry.TireHeight());Put(tire,b,sample);
                }
                trace.elapsed=car.state.elapsed;trace.acousticJ=car.state.audioExhaust.mixture.AcousticEnergyJ;
                Check(trace.frames.All(frame=>!string.IsNullOrEmpty(frame.stage)),f.id+" stage labels survive JsonUtility null-to-empty string normalization");
                Check(Math.Abs(trace.elapsed-f.elapsed)<1e-9&&car.state.exhaustPopCount==f.pops,f.id+" unchanged authoritative physics");
                Check(trace.reactions.Count==car.state.audioExhaust.mixture.Sequence&&Math.Abs(trace.reactions.Sum(x=>x.acousticJ)-trace.acousticJ)<1e-8,f.id+" all reaction packets and acoustic joules retained");
                Check(trace.releases.Count==car.state.audioExhaust.ReleaseSequence,f.id+" all exact BOV timestamps retained");
                string path=Path.Combine(output,f.id+".audio-trace.json"),json=Json(trace);File.WriteAllText(path,json,new UTF8Encoding(false));
                // Unity's JSON reader rounds some 17-digit doubles by one ULP.
                // Parse invariant round-trip numbers directly for the parity gate.
                var restored=ExactJson.Read<Trace>(File.ReadAllText(path,Encoding.UTF8));
                Check(Json(profile)==Json(new EngineSoundProfile(restored.spec,restored.tune)),f.id+" effective hardware survives JSON roundtrip");
                string frameJson=Json(trace.frames),restoredJson=Json(restored.frames);
                if(frameJson!=restoredJson){int at=0;while(at<Math.Min(frameJson.Length,restoredJson.Length)&&frameJson[at]==restoredJson[at])at++;Report.AppendLine("ROUNDTRIP at "+at+" / source "+frameJson.Substring(Math.Max(0,at-60),Math.Min(160,frameJson.Length-Math.Max(0,at-60)))+" / restored "+restoredJson.Substring(Math.Max(0,at-60),Math.Min(160,restoredJson.Length-Math.Max(0,at-60))));}
                Check(frameJson==restoredJson,f.id+" all frame numbers and wheel contacts survive JSON roundtrip");
                Check(Json(trace.reactions)==Json(restored.reactions),f.id+" all reaction fields and exact seeds survive JSON roundtrip");
                Replay(restored,raw,engine,tire);
                var combined=new float[engine.Length];for(int i=0;i<combined.Length;i++)combined[i]=engine[i]+tire[i];
                Wav(Path.Combine(output,f.id+".unity-source.wav"),raw);Wav(Path.Combine(output,f.id+".unity-engine.wav"),engine);
                Wav(Path.Combine(output,f.id+".unity-tires.wav"),tire);Wav(Path.Combine(output,f.id+".unity.wav"),combined);
                File.WriteAllBytes(Path.Combine(output,f.id+".race.json"),original);
                // Supplement JSON-serializable authoring state with fully resolved readonly hardware.
                var extra=new Dictionary<string,object>{{"profile",profile},{"runnersM",Enumerable.Range(0,profile.cylinders).Select(profile.acoustics.Runner).ToArray()},
                    {"firingOrder",DragLab.Domain.Mechanics.EngineElements.FiringOrder(profile.cylinders)},
                    {"bankByCylinder",Enumerable.Range(0,profile.cylinders).Select(c=>profile.layout==EngineLayout.Inline?0:c%2).ToArray()},
                    {"afterfireReferenceGain",AfterfireAudioSynth.ReferenceGain},{"exhaustPathM",RuntimeExhaustState.PathLength(profile)}};
                // Keep hardware inside the standalone trace (not a separately required sidecar).
                json=json.Substring(0,json.Length-1)+",\"hardware\":"+Json(extra)+",\"enumNames\":"+Json(EnumNames())+"}";
                File.WriteAllText(path,json,new UTF8Encoding(false));
                Check(File.ReadAllBytes(input).SequenceEqual(original),f.id+" source fixture not rewritten");
                Report.AppendLine("CAPTURE "+f.id+" / "+trace.totalSamples+" samples / "+trace.reactions.Count+" reactions / "+trace.releases.Count+" releases / trace SHA256 "+Hash(Encoding.UTF8.GetBytes(json)));
            }
            finally{race.CloseOpponentWorker();}
        }
        static Dictionary<string,object> EnumNames()
        {
            var result=new Dictionary<string,object>();foreach(var t in new[]{typeof(CutMethod),typeof(FuelSystem),typeof(InductionType),typeof(EngineLayout),typeof(BlowOffType),typeof(AfterfireCause),typeof(ExhaustPartKind)})result.Add(t.Name,Enum.GetNames(t));return result;
        }
        static class ExactJson
        {
            static readonly Dictionary<string,double> Doubles=new Dictionary<string,double>();
            static double ExactDouble(string text)
            {
                if(Doubles.TryGetValue(text,out var found))return found;
                double approximate=double.Parse(text,NumberStyles.Float,Inv);
                if(text.IndexOfAny(new[]{'.','e','E'})<0)return approximate;
                // Older Mono decimal conversion can miss the nearest IEEE value.
                // Compare its neighbours against the exact decimal rational and
                // choose nearest/ties-to-even; never use a tolerance to hide it.
                bool negative=text.StartsWith("-",StringComparison.Ordinal);string digits=negative?text.Substring(1):text;
                int exponentAt=digits.IndexOfAny(new[]{'e','E'}),power=0;
                if(exponentAt>=0){power=int.Parse(digits.Substring(exponentAt+1),Inv);digits=digits.Substring(0,exponentAt);}
                int point=digits.IndexOf('.');if(point>=0){power-=digits.Length-point-1;digits=digits.Remove(point,1);}
                var numerator=System.Numerics.BigInteger.Parse(digits,Inv);var denominator=System.Numerics.BigInteger.One;
                if(power>=0)numerator*=System.Numerics.BigInteger.Pow(10,power);else denominator=System.Numerics.BigInteger.Pow(10,-power);
                var target=numerator<<1074;long bits=BitConverter.DoubleToInt64Bits(Math.Abs(approximate)),best=bits;
                System.Numerics.BigInteger? error=null;
                for(long b=Math.Max(0,bits-2);b<=bits+2&&b<0x7ff0000000000000L;b++)
                {
                    int exponent=(int)(b>>52);long mantissa=b&0xfffffffffffffL;if(exponent>0)mantissa|=1L<<52;
                    var scaled=(new System.Numerics.BigInteger(mantissa))<<Math.Max(0,exponent-1);
                    var delta=System.Numerics.BigInteger.Abs(scaled*denominator-target);
                    if(!error.HasValue||delta<error.Value||(delta==error.Value&&(b&1)==0)){best=b;error=delta;}
                }
                double result=BitConverter.Int64BitsToDouble(negative?best|long.MinValue:best);Doubles.Add(text,result);return result;
            }
            sealed class Number {public string text;}
            sealed class Parser
            {
                readonly string text;int at;
                public Parser(string text){this.text=text;}
                void Space(){while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;}
                void Take(char c){Space();if(at>=text.Length||text[at++]!=c)throw new FormatException("Invalid JSON token");}
                string String()
                {
                    Take('"');var b=new StringBuilder();while(at<text.Length){char c=text[at++];if(c=='"')return b.ToString();
                        if(c=='\\'){if(at>=text.Length)break;c=text[at++];if(c=='u'){b.Append((char)int.Parse(text.Substring(at,4),NumberStyles.HexNumber,Inv));at+=4;continue;}
                            c=c=='n'?'\n':c=='r'?'\r':c=='t'?'\t':c=='b'?'\b':c=='f'?'\f':c;}
                        b.Append(c);}throw new FormatException("Unterminated JSON string");
                }
                public object Value()
                {
                    Space();if(at>=text.Length)throw new FormatException("Missing JSON value");char c=text[at];
                    if(c=='"')return String();
                    if(c=='{'||c=='[')
                    {
                        bool map=c=='{';at++;Space();char end=map?'}':']';var values=new List<object>();var fields=new Dictionary<string,object>();
                        if(at<text.Length&&text[at]==end){at++;return map?(object)fields:values;}
                        while(true){if(map){string name=String();Take(':');fields.Add(name,Value());}else values.Add(Value());Space();
                            if(at<text.Length&&text[at]==end){at++;return map?(object)fields:values;}Take(',');}
                    }
                    foreach(string token in new[]{"true","false","null"})if(at+token.Length<=text.Length&&text.Substring(at,token.Length)==token){at+=token.Length;return token=="null"?null:(object)(token=="true");}
                    int start=at;while(at<text.Length&&"-+0123456789.eE".IndexOf(text[at])>=0)at++;
                    if(at==start)throw new FormatException("Invalid JSON value");return new Number{text=text.Substring(start,at-start)};
                }
                public void End(){Space();if(at!=text.Length)throw new FormatException("Trailing JSON content");}
            }
            static object ConvertValue(object value,Type type)
            {
                if(value==null)return null;
                if(type==typeof(string)||type==typeof(bool))return value;
                if(value is Number){string n=((Number)value).text;if(type.IsEnum)return Enum.ToObject(type,int.Parse(n,Inv));
                    if(type==typeof(double))return ExactDouble(n);if(type==typeof(float))return float.Parse(n,NumberStyles.Float,Inv);return Convert.ChangeType(n,type,Inv);}
                if(type.IsArray){var list=(List<object>)value;var a=Array.CreateInstance(type.GetElementType(),list.Count);for(int i=0;i<list.Count;i++)a.SetValue(ConvertValue(list[i],type.GetElementType()),i);return a;}
                if(typeof(IList).IsAssignableFrom(type)){var list=(IList)Activator.CreateInstance(type);foreach(var item in (List<object>)value)list.Add(ConvertValue(item,type.GetGenericArguments()[0]));return list;}
                var target=Activator.CreateInstance(type);foreach(var pair in (Dictionary<string,object>)value){var field=type.GetField(pair.Key,BindingFlags.Public|BindingFlags.Instance);if(field!=null)field.SetValue(target,ConvertValue(pair.Value,field.FieldType));}return target;
            }
            public static T Read<T>(string json){var p=new Parser(json);var data=p.Value();p.End();return (T)ConvertValue(data,typeof(T));}
        }
        static void Replay(Trace trace,float[] raw,float[] engine,float[] tire)
        {
            var profile=new EngineSoundProfile(trace.spec,trace.tune);var state=new RuntimeExhaustState(profile,new CarState());
            var synth=new EngineSoundSynth(profile,trace.seeds.engine,Rate);var tires=new TireSoundSynth();
            var outside=new TracksidePropagation(Rate);var tireOutside=new TracksidePropagation(Rate);var geometry=trace.listener.Frame();
            var a=new float[Block*2];var b=new float[a.Length];int eventAt=0,releaseAt=0;
            bool exactRaw=true,exactEngine=true,exactTire=true;int firstDifference=-1;double expected=0,actual=0;
            foreach(var f in trace.frames)
            {
                PublishedTime.SetValue(state.mixture,f.mixtureTime);
                while(eventAt<trace.reactions.Count&&trace.reactions[eventAt].availableSample<=f.sample)Record.Invoke(state.mixture,new object[]{trace.reactions[eventAt++].Event()});
                while(releaseAt<trace.releases.Count&&trace.releases[releaseAt].availableSample<=f.sample){var e=trace.releases[releaseAt++];state.RecordRelease(e.strength,e.time);}
                synth.Fill(a,2,Rate,f.engine.Frame(state),trace.master,trace.compressorNoise);
                for(int i=0;i<a.Length;i++){if(a[i]!=raw[f.sample*2+i]&&firstDifference<0){firstDifference=f.sample*2+i;expected=raw[firstDifference];actual=a[i];}exactRaw&=a[i]==raw[f.sample*2+i];}
                outside.Process(a,2,geometry);tires.Fill(b,2,Rate,f.tires.Frame(),trace.master);tireOutside.Process(b,2,geometry.TireHeight());
                for(int i=0;i<a.Length;i++){exactEngine&=a[i]==engine[f.sample*2+i];exactTire&=b[i]==tire[f.sample*2+i];}
            }
            Check(exactRaw,trace.id+" serialized numeric frames/events reproduce source PCM bit for bit without physics / first difference "+firstDifference+" / expected "+expected.ToString("R",Inv)+" actual "+actual.ToString("R",Inv));
            Check(exactEngine&&exactTire,trace.id+" serialized trace reproduces both propagated engine and four-wheel tire PCM bit for bit");
            var d=synth.AfterfireDiagnostics(Rate);var r=synth.ReleaseDiagnostics(Rate);
            Check(eventAt==trace.reactions.Count&&d.Delivered==eventAt&&releaseAt==trace.releases.Count&&r.Delivered==releaseAt,trace.id+" exact event delivery after JSON/UInt64 seed roundtrip");
            Check(d.HistoryOverflows==0&&d.TailSteals==0&&d.PressurePulseOverflows==0&&r.Overflows==0&&r.TailSteals==0,trace.id+" replay has no event overflow or stolen voices");
        }
        static string RebaseInputs(string source,string output,string[] ids)
        {
            string folder=Path.Combine(output,"inputs");Directory.CreateDirectory(folder);
            Check(Path.GetFullPath(source)!=Path.GetFullPath(folder),"rebased fixtures have a separate destination from their historical source");
            var hardware=RaceAudioAuditionVerification.AcousticHardware(true).ToDictionary(f=>f.id);
            var matrix=new StringBuilder("case,physics_model,original_fixture_sha256,driver_ticks_sha256,ticks,old_elapsed,new_elapsed,old_pops,new_pops\n");
            foreach(string id in ids)
            {
                string path=Path.Combine(source,id+".race.json");byte[] original=File.ReadAllBytes(path);
                var f=JsonUtility.FromJson<RaceAudioTestFixture>(Encoding.UTF8.GetString(original));var current=hardware[id];
                current=JsonUtility.FromJson<RaceAudioTestFixture>(JsonUtility.ToJson(current));
                File.WriteAllText(Path.Combine(folder,id+".current-hardware.json"),JsonUtility.ToJson(current),new UTF8Encoding(false));
                // Re-resolve current authored hardware; never just relabel an old spec/model.
                // Abort if this would silently alter acoustic tuning or installed hardware.
                Check(f.id==id&&JsonUtility.ToJson(f.spec)==JsonUtility.ToJson(current.spec),id+" current authored spec matches the original controlled configuration");
                Check(JsonUtility.ToJson(f.tune)==JsonUtility.ToJson(current.tune)&&Json(f.components)==Json(current.components),id+" installed components and tuning match the original controlled configuration");
                string commands=Json(f.ticks);double oldElapsed=f.elapsed;int oldPops=f.pops;
                f.spec=current.spec.Copy();f.tune=current.tune.Copy();f.components=current.components;
                f=JsonUtility.FromJson<RaceAudioTestFixture>(JsonUtility.ToJson(f));
                var race=f.CreateRace();int lastGear=0,shifts=0,limiterTicks=0;var cuts=new RaceAudioCutCounts();
                try
                {
                    foreach(var tick in f.ticks)
                    {
                        var car=race.player;car.state.warmupLineLock=tick.lineLock;
                        if(tick.beginCountdown)Check(race.BeginCountdown(),id+" original input sequence can stage with corrected physics");
                        if(race.phase==RacePhase.Finished)race.Shutdown(tick.input);else race.Step(tick.input);
                        var s=car.state;cuts.Observe(car);if(s.limiter)limiterTicks++;
                        if(s.launched&&s.gear!=lastGear){if(lastGear>0)shifts++;lastGear=s.gear;}
                    }
                    var final=race.player.state;Check(final.finished&&!final.engineFailed&&!final.drivetrainFailed&&!final.stalled,id+" unchanged driver commands finish on current physical contracts");
                    f.elapsed=final.elapsed;f.pops=final.exhaustPopCount;f.releases=final.boostReleaseCount;f.shifts=shifts;f.limiterTicks=limiterTicks;f.cuts=cuts;
                }
                finally{race.CloseOpponentWorker();}
                Check(Json(f.ticks)==commands,id+" every original driver command, stage and tick retained exactly");
                File.WriteAllText(Path.Combine(folder,id+".race.json"),JsonUtility.ToJson(f),new UTF8Encoding(false));
                Check(File.ReadAllBytes(path).SequenceEqual(original),id+" historical input fixture remains unchanged");
                matrix.AppendLine(string.Join(",",new[]{id,MechanicsSchema.Model,Hash(original),Hash(Encoding.UTF8.GetBytes(commands)),f.ticks.Count.ToString(Inv),oldElapsed.ToString("R",Inv),f.elapsed.ToString("R",Inv),oldPops.ToString(Inv),f.pops.ToString(Inv)}));
            }
            File.WriteAllText(Path.Combine(output,"rebased-inputs-matrix.csv"),matrix.ToString(),new UTF8Encoding(false));return folder;
        }
        public static void Run()
        {
            checks=0;Report.Clear();string output=Path.GetFullPath(Arg("--draglab-reference-output",Path.Combine(Root,"reports/unity-audio-reference-20261021")));
            Directory.CreateDirectory(output);string before=JsonUtility.ToJson(UnityEngine.Random.state);
            try
            {
                // New sound inputs cannot silently disappear if production adds fields.
                var absent=EngineFields.Where(f=>f.Name!="afterfire"&&f.Name!="acousticState"&&typeof(EngineData).GetField(f.Name)==null).Select(f=>f.Name).ToArray();
                Check(absent.Length==0,"all public production EngineSoundFrame values have trace fields: "+string.Join(",",absent));
                string commit=Arg("--draglab-reference-commit","unrecorded"),folder=Arg("--draglab-reference-input",Path.Combine(Root,"reports/race-audio-audition"));
                string[] ids=Arg("--draglab-audio-case","03-i4-turbo-hiss,08-v8-carb,09-v8-supercharger,17-v8-burnout,26-launch-hard-ignition").Split(',');
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-reference-rebase")>=0)folder=RebaseInputs(folder,output,ids);
                foreach(string id in ids)Capture(Path.Combine(folder,id+".race.json"),output,commit);
                Check(JsonUtility.ToJson(UnityEngine.Random.state)==before,"capture/replay preserves the gameplay global RNG");
                var files=Directory.GetFiles(output).Where(p=>p.EndsWith(".json",StringComparison.Ordinal)||p.EndsWith(".wav",StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal);
                var hashes=new StringBuilder();foreach(string p in files)hashes.AppendLine(Hash(File.ReadAllBytes(p))+"  "+Path.GetFileName(p));
                File.WriteAllText(Path.Combine(output,"SHA256SUMS.txt"),hashes.ToString(),new UTF8Encoding(false));
                Debug.Log("UNITY AUDIO REFERENCE PASS "+checks+" / "+ids.Length+" configurations / "+output);
            }
            finally{File.WriteAllText(Path.Combine(output,"verification.txt"),Report+"Checks: "+checks+"\n",new UTF8Encoding(false));}
        }
    }
}
