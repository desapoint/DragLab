using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using M=DragLab.Editor.V8BackfirePreviewModel;

namespace DragLab.Editor
{
    public static class V8BackfireAudition
    {
        const int Rate=48000;const long Master=20261020;static int checks;static long pcmChecks;
        [Serializable] public sealed class Output {public string id,title,file,sha256;public double sectionSeconds,lightPeak,lessTickingPeak;public int physicalPops,publishedEvents;}
        [Serializable] public sealed class Manifest {public long seed=Master;public string order="Original production / 1 s gap / C# light cleanup / 1 s gap / C# less ticking";public List<Output> outputs=new List<Output>();}
        [Serializable] sealed class ReceiptLog {public List<M.Receipt> events;}
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/v8-backfire-matrix-20261020"));
        static void Check(bool yes,string why){checks++;if(!yes)throw new InvalidOperationException("V8 C# PREVIEW: "+why);}
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static short Pcm(double x,int at,int frames){short value=(short)Math.Round(x*32767);if(at<960)value=(short)Math.Round(value*at/959.0);if(at>=frames-3840)value=(short)Math.Round(value*(frames-1-at)/3839.0);return value;}
        static short[] Mix(M.Stem stem,float[] bed,int frames,out double peak)
        {
            var pcm=new short[frames*2];peak=0;
            for(int at=0;at<frames;at++)for(int channel=0;channel<2;channel++){
                int i=at*2+channel;double engine=bed[i],pop=channel==0?stem.left[at]:stem.right[at],clean=engine/(1-Math.Abs(engine)),sum=clean+pop;
                double value=pop==0?engine:sum/(1+Math.Abs(sum));if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>=.99)throw new InvalidOperationException("Invalid/clipped mixed sample");pcmChecks++;peak=Math.Max(peak,Math.Abs(value));pcm[i]=Pcm(value,at,frames);
            }
            return pcm;
        }
        static void Header(BinaryWriter w,int frames){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*4);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*4);w.Write((short)4);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(frames*4);}
        static void FilterChecks()
        {
            int[] indices={0,1,2,5,19,100,500,1000};var impulse=new double[2048];impulse[0]=1;
            double[][] expected={
                new[]{.0086547788572930214,.032161473873792867,.057308158209465269,.087754808072240745,-.0078100745764745974,-.0058517790491207271,.00028000420884974483,1.5435099144065748e-6},
                new[]{.11538480905537912,.31998557736685207,.32465739731192078,-.082606457915277709,-.031532197642290052,.0026180099994013192,-2.3561645752614764e-9,5.3414476402090013e-16},
                new[]{.00024135904904198073,.00095471460249358839,.0018775076792595968,.0043982853408585263,.011930016263102127,.0038339570369223825,-6.6238826395437435e-7,-2.247910345461382e-12},
                new[]{.022398277077357125,.043793188522646131,.041831404581383805,.036457768982183246,.019192538276646739,.0004687040805049919,5.1197756131093689e-12,5.7173024645534379e-22},
                new[]{.17190630900157369,.43362965960941763,.35886709363400893,-.049506455417393742,-.010017445287202607,-.0054893674955306948,.00065384535128262962,-3.3715806618955221e-5},
                new[]{.98712518667152971,-.02558173848039047,-.025246044559514728,-.024240692860329191,-.019650583526130194,-.0019258002612107241,-3.9020207603685283e-5,-5.6421859403795725e-8},
                new[]{.0073392800685376791,.014559218968281247,.014310866085916646,.013454662966690728,.0078001573566690973,-.0040730531660108258,.0002159675965041991,-1.3872860701369529e-6}};
            var actual=new[]{M.Band(impulse,80,1600),M.Band(impulse,350,7000),M.Band(impulse,0,240),M.Band(impulse,0,350,1),M.Band(impulse,60,8600),M.Band(impulse,140,0),M.Peak(impulse,305,2.7)};
            for(int f=0;f<actual.Length;f++)for(int k=0;k<indices.Length;k++)Check(Math.Abs(actual[f][indices[k]]-expected[f][k])<1e-7,"Butterworth/peak reference response "+f+" / "+k);
        }
        public static void Run(){V8BackfireMatrixExport.Run();RenderExisting();}
        // Explicit cached-capture entry: incoming fixtures/reference must already be verified by the exporter.
        public static void RenderExisting()
        {
            checks=0;pcmChecks=0;FilterChecks();string path=Path.Combine(Folder,"jobs.json"),incomingHash=Hash(path);var jobs=JsonUtility.FromJson<V8BackfireMatrixExport.Manifest>(File.ReadAllText(path,Encoding.UTF8));Check(jobs.jobs.Count==10,"ten complete captures required");
            var manifest=new Manifest();int audible=0,quiet=0,physical=0,published=0;var lines=new List<string>();
            foreach(var job in jobs.jobs){
                Check(job.referenceMismatches==0,"unverified production replay");string reference=Path.Combine(Folder,job.reference),referenceHash=Hash(reference);byte[] original=File.ReadAllBytes(reference);Check(original.Length==44+job.frames*4,"original PCM header/length");
                var before=new short[job.frames*2];Buffer.BlockCopy(original,44,before,0,before.Length*2);byte[] rawBed=File.ReadAllBytes(Path.Combine(Folder,job.bed));var bed=new float[job.frames*2];Check(rawBed.Length==bed.Length*4,"engine bed length");Buffer.BlockCopy(rawBed,0,bed,0,rawBed.Length);
                foreach(var probe in job.filters){var input=new double[probe.samples.Length];input[0]=1;var actual=M.Hardware(input,job,probe.position);Check(actual.Zip(probe.samples,(x,y)=>Math.Abs(x-y)).Max()<1e-12,"reuse of authoritative hardware transfer");}
                var light=M.Render(job,Master,M.Cleanup.Light);var less=M.Render(job,Master,M.Cleanup.LessTicking);
                Check(light.events.Count==job.events.Count&&less.events.Count==job.events.Count&&job.events.Count<=job.physicalPops,"published event count");Check(JsonUtility.ToJson(new ReceiptLog{events=light.events})==JsonUtility.ToJson(new ReceiptLog{events=less.events}),"cleanup altered event origins or seed stream");
                for(int i=0;i<light.events.Count;i++){var ev=job.events[i];var r=light.events[i];Check(ev.time==r.time&&ev.strength==r.strength,"retimed physical trigger");Check(r.position>=0||job.induction=="Turbo"||job.induction=="TwinTurbo"||job.induction=="Twincharged","invented pre-turbine region");}
                double aPeak,bPeak;var a=Mix(light,bed,job.frames,out aPeak);var b=Mix(less,bed,job.frames,out bPeak);int prefix=job.events.Count>0?(int)Math.Round(job.events[0].time*Rate):job.frames;
                Check(a.Take(prefix*2).SequenceEqual(before.Take(prefix*2))&&b.Take(prefix*2).SequenceEqual(before.Take(prefix*2)),"engine prefix changed");
                if(job.events.Count==0){quiet++;Check(a.SequenceEqual(before)&&b.SequenceEqual(before),"quiet control gained sound");}
                else{
                    audible++;Check(!a.SequenceEqual(b),"cleanup profiles identical");var ev=job.events[0];string kind;
                    var one=M.Source(job,ev,Master,M.Cleanup.LessTicking,out kind);var again=M.Source(job,ev,Master,M.Cleanup.LessTicking,out kind);var changed=M.Source(job,ev,Master+1,M.Cleanup.LessTicking,out kind);
                    Check(one.SequenceEqual(again),"same-seed source drift");Check(!one.SequenceEqual(changed),"seed did not vary source");
                    var modified=JsonUtility.FromJson<V8BackfireMatrixExport.Job>(JsonUtility.ToJson(job));modified.litres*=1.5;Check(!one.SequenceEqual(M.Source(modified,ev,Master,M.Cleanup.LessTicking,out kind)),"displacement input ignored");
                    modified=JsonUtility.FromJson<V8BackfireMatrixExport.Job>(JsonUtility.ToJson(job));modified.popSharpness*=1.4;Check(!one.SequenceEqual(M.Source(modified,ev,Master,M.Cleanup.LessTicking,out kind)),"sharpness input ignored");
                    modified=JsonUtility.FromJson<V8BackfireMatrixExport.Job>(JsonUtility.ToJson(job));modified.popDecay*=.8;Check(!one.SequenceEqual(M.Source(modified,ev,Master,M.Cleanup.LessTicking,out kind)),"decay input ignored");
                    modified.popDecay=.25;var shortDecay=M.Source(modified,ev,Master,M.Cleanup.LessTicking,out kind);modified.popDecay=3;var longDecay=M.Source(modified,ev,Master,M.Cleanup.LessTicking,out kind);Check(longDecay.Length>shortDecay.Length,"full supported decay range or extended tail lost");
                    var altered=JsonUtility.FromJson<V8BackfireMatrixExport.Event>(JsonUtility.ToJson(ev));altered.rpm*=.65;Check(!one.SequenceEqual(M.Source(job,altered,Master,M.Cleanup.LessTicking,out kind)),"RPM input ignored");
                    altered=JsonUtility.FromJson<V8BackfireMatrixExport.Event>(JsonUtility.ToJson(ev));altered.temperatureK*=1.4;Check(!one.SequenceEqual(M.Source(job,altered,Master,M.Cleanup.LessTicking,out kind)),"temperature input ignored");
                    string region;Check(M.Locate(job,ev,Master,null,out region)==M.Locate(job,ev,Master,null,out region),"same-seed origin drift");Check(Enumerable.Range(0,8).Select(k=>M.Locate(job,ev,Master+k,null,out region)).Distinct().Count()>1,"seed did not vary origin");
                    if(job.id=="31-exhaust-3"){var repeat=M.Render(job,Master,M.Cleanup.LessTicking);Check(less.left.SequenceEqual(repeat.left)&&less.right.SequenceEqual(repeat.right),"full selected-stem nondeterminism");}
                }
                string file=job.id+"-original-A-B.wav";int total=job.frames*3+Rate*2;using(var writer=new BinaryWriter(File.Create(Path.Combine(Folder,file)))){Header(writer,total);writer.Write(original,44,original.Length-44);writer.Write(new byte[Rate*4]);foreach(short value in a)writer.Write(value);writer.Write(new byte[Rate*4]);foreach(short value in b)writer.Write(value);}
                byte[] combined=File.ReadAllBytes(Path.Combine(Folder,file));Check(combined.Length==44+total*4,"full output length");var delivered=new short[total*2];Buffer.BlockCopy(combined,44,delivered,0,delivered.Length*2);
                Check(delivered.Skip((job.frames+Rate)*2).Take(a.Length).SequenceEqual(a),"light section placement");Check(delivered.Skip((job.frames*2+Rate*2)*2).SequenceEqual(b),"less-ticking section placement");
                Check(delivered.Skip(job.frames*2).Take(Rate*2).All(x=>x==0)&&delivered.Skip((job.frames*2+Rate)*2).Take(Rate*2).All(x=>x==0),"comparison separators");
                Check(combined.Skip(44).Take(before.Length*2).SequenceEqual(original.Skip(44)),"original section changed");Check(Hash(reference)==referenceHash,"reference modified");
                File.WriteAllText(Path.Combine(Folder,job.id+"-csharp-events.json"),JsonUtility.ToJson(new ReceiptLog{events=light.events},true),new UTF8Encoding(false));
                manifest.outputs.Add(new Output{id=job.id,title=job.title,file=file,sha256=Hash(Path.Combine(Folder,file)),sectionSeconds=job.frames/(double)Rate,lightPeak=aPeak,lessTickingPeak=bPeak,physicalPops=job.physicalPops,publishedEvents=job.events.Count});
                physical+=job.physicalPops;published+=job.events.Count;lines.Add("PASS "+job.id+": full original / C# light / C# less-ticking; exact reference and engine prefix, same origins/seeds, no new physical triggers.");Debug.Log("V8 C# RENDER "+manifest.outputs.Count+" / 10 "+job.id);
                File.WriteAllText(Path.Combine(Folder,"csharp-manifest.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
            }
            Check(audible==8&&quiet==2,"eight audible / two exact quiet controls");Check(Hash(path)==incomingHash,"physical input modified");
            // Per-sample finite checks are counted separately so the semantic gate count is meaningful.
            File.WriteAllText(Path.Combine(Folder,"csharp-verification.txt"),string.Join("\n",lines)+"\nPASS C# source/filter/seed/control and PCM checks: "+checks+" semantic assertions, "+pcmChecks+" finite/non-clipped PCM sample checks; "+physical+" physical / "+published+" published triggers. Eight audible configurations, two exact quiet controls. Runtime engine/audio untouched; Python is not required for this runner.\n",new UTF8Encoding(false));Debug.Log("V8 C# MATRIX PASS");
        }
    }
}
