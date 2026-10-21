using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DragLab.Domain;
using C=System.Numerics.Complex;

namespace DragLab.Editor
{
    // Full shared acoustic source, preview-only. No trigger, engine, or simulation ownership.
    public static class V8BackfirePreviewModel
    {
        public const int Rate=48000;
        public enum Cleanup { Light, LessTicking }
        public enum Texture { Reference, Damped, NoShell }
        [Serializable] public sealed class Receipt {public int index;public double time,strength,position,travel;public ulong timbreSeed;public string kind,region;}
        public sealed class Stem {public double[] left,right;public List<Receipt> events=new List<Receipt>();}
        sealed class Rng
        {
            ulong state;bool spare;double gaussian;
            public Rng(ulong seed){state=seed+0x9e3779b97f4a7c15UL;Next();}
            uint Next(){unchecked{ulong old=state;state=old*6364136223846793005UL+1442695040888963407UL;uint x=(uint)(((old>>18)^old)>>27);int r=(int)(old>>59);return (x>>r)|(x<<((-r)&31));}}
            public double U()=> (Next()+.5)/4294967296.0;
            public double U(double a,double b)=>a+(b-a)*U();
            public double N(){if(spare){spare=false;return gaussian;}double radius=Math.Sqrt(-2*Math.Log(U())),phase=2*Math.PI*U();gaussian=radius*Math.Sin(phase);spare=true;return radius*Math.Cos(phase);}
        }
        public static ulong Seed(long master,string config,int index,int tag) => BackfirePressureSource.Seed(master,config,index,tag);
        static double Clamp(double x,double a,double b)=>Math.Max(a,Math.Min(b,x));
        public static double[] Band(double[] x,double low=0,double high=0,int order=2)=>BackfirePressureSource.Band(x,low,high,order);
        public static double[] Peak(double[] x,double hz,double q)=>BackfirePressureSource.Peak(x,hz,q);
        static double[] Low(double[] x,double hz,int order=2)=>Band(x,0,hz,order);
        public static double[] Burst(ulong seed,string kind,Cleanup cleanup,Texture texture=Texture.Reference)=>BackfirePressureSource.Burst(seed,kind,(BackfirePressureSource.Cleanup)cleanup,(BackfirePressureSource.Texture)texture);
        static void Tap(double[] target,double[] input,int shift,double gain){for(int i=shift;i<target.Length;i++)target[i]+=gain*input[i-shift];}
        static readonly string[] RegionNames={"pre_turbine","downpipe","cat_zone","midpipe","resonator","silencer","tailpipe"};
        static double[] Weights(string cause)
        {switch(cause){case "rev_limiter":return new[]{.015,.29,.20,.24,.13,.10,.025};case "fuel_cut_limiter":return new[]{0.0,.14,.15,.30,.24,.15,.02};case "two_step":return new[]{.065,.31,.22,.19,.12,.08,.015};case "upshift_cut":return new[]{.055,.33,.19,.20,.13,.07,.025};case "downshift":return new[]{.02,.19,.23,.26,.17,.115,.015};case "anti_lag":return new[]{.45,.30,.11,.08,.04,.02,0};case "idle_blip":return new[]{0.0,.10,.19,.31,.23,.15,.02};default:return new[]{.008,.15,.17,.31,.19,.14,.032};}}
        static bool Turbo(V8BackfireMatrixExport.Job job)=>job.induction=="Turbo"||job.induction=="TwinTurbo"||job.induction=="Twincharged";
        public static double Locate(V8BackfireMatrixExport.Job job,V8BackfireMatrixExport.Event ev,long master,double? previous,out string region)
        {
            var ranges=new List<Tuple<int,double,double>>{Tuple.Create(1,.012,.135),Tuple.Create(6,.91,.99)};if(Turbo(job))ranges.Add(Tuple.Create(0,-.14,-.035));double cursor=.14;
            foreach(var section in job.sections){if(section.start>cursor+.02)ranges.Add(Tuple.Create(3,cursor+.005,section.start-.005));int role=section.part.kind==ExhaustPartKind.CatalyticConverter?2:section.part.kind==ExhaustPartKind.Resonator?4:(section.part.kind==ExhaustPartKind.AbsorptiveSilencer||section.part.kind==ExhaustPartKind.ChamberedSilencer)?5:-1;if(role>=0&&section.part.amount>0)ranges.Add(Tuple.Create(role,section.start,section.end));cursor=Math.Max(cursor,section.end);}if(cursor<.90)ranges.Add(Tuple.Create(3,cursor+.002,.90));
            var rng=new Rng(Seed(master,job.id,ev.index,44));if(previous.HasValue&&rng.U()<.24){region="coherent_hotspot";return Clamp(previous.Value+rng.N()*.038,Turbo(job)?-.14:0,.98);}
            var weights=Weights(ev.scenario);var valid=Enumerable.Range(0,7).Where(i=>ranges.Any(r=>r.Item1==i)&&weights[i]>0).ToArray();double roll=rng.U()*valid.Sum(i=>weights[i]);int chosen=valid.Last();foreach(int i in valid){roll-=weights[i];if(roll<0){chosen=i;break;}}var options=ranges.Where(r=>r.Item1==chosen).ToArray();var range=options[(int)(rng.U()*options.Length)];region=RegionNames[chosen];return rng.U(range.Item2,range.Item3);
        }
        public static double[] Hardware(double[] source,V8BackfireMatrixExport.Job job,double position)
        {var setup=job.exhaust.Copy();for(int i=0;i<setup.parts.Length;i++){var section=job.sections[i];setup.parts[i].amount*=(float)Clamp((section.end-position)/(section.end-section.start),0,1);}var filter=new ExhaustFilter(setup,job.exhaustStage);filter.Configure(Rate);var y=new double[source.Length];for(int i=0;i<y.Length;i++)y[i]=filter.Process(source[i]);return y;}
        public static double[] Source(V8BackfireMatrixExport.Job job,V8BackfireMatrixExport.Event ev,long master,Cleanup cleanup,out string kind,Texture texture=Texture.Reference)
        {
            kind=ev.strength<.28?"crackle":ev.limiter||ev.twoStep||ev.strength>.72?"crack":"normal";ulong seed=Seed(master,job.id,ev.index,11);var rng=new Rng(Seed((long)(seed&0x7fffffffffffffffUL),job.id,ev.index,77));
            var dry=Burst(seed,kind,cleanup,texture);double stretch=1+.90*rng.U(-.14,.17),depth=1+.9*rng.U(-.27,.29),bite=1+.9*rng.U(-.32,.37),grain=1+.9*rng.U(-.24,.32),amplitude=1+.9*rng.U(-.11,.12);
            double engine=Clamp(Math.Pow(job.litres/2,.06)*Math.Pow(4500/Math.Max(700,ev.rpm),.05)*Math.Pow(650/Math.Max(300,ev.temperatureK),.05),.8,1.25);stretch*=engine*Clamp(job.popDecay,.25,3)/Math.Pow(Clamp(job.popSharpness,.25,2.5),.25);
            var y=new double[(int)Math.Ceiling(dry.Length*Math.Max(1,stretch))];for(int i=0;i<y.Length;i++){double at=i/stretch;int j=(int)at;if(j<dry.Length-1)y[i]=dry[j]+(dry[j+1]-dry[j])*(at-j);else if(j==dry.Length-1)y[i]=dry[j]*(at==j?1:0);}
            var bass=Low(y,450);var lowmid=Low(y,2550);for(int i=0;i<y.Length;i++)y[i]=(depth*bass[i]+grain*(lowmid[i]-bass[i])+bite*(y[i]-lowmid[i]))*amplitude;return y;
        }
        public static double[] Propagate(double[] source,V8BackfireMatrixExport.Job job,V8BackfireMatrixExport.Event ev,long master,double position,out double travel)
        {
            var rng=new Rng(Seed(master,job.id,ev.index,91));var y=(double[])source.Clone();int n=y.Length;
            if(position<0){if(!Turbo(job))throw new InvalidOperationException("No turbine at this source");double boost=Math.Max(0,ev.turboBoost),gain=Math.Pow(10,-(1.5+3*boost)/20);var low=Low(y,1700/(1+.8*boost));var high=Low(y,4800/(1+.3*boost));for(int i=0;i<n;i++)y[i]=gain*(.77*low[i]+.15*high[i]);Shift(y,(int)Math.Round(Math.Abs(position)*.0035*Rate));}
            y=Hardware(y,job,position);double path=Math.Max(0,.98-Math.Max(0,position));var softened=Low(y,6800-1650*path);for(int i=0;i<n;i++)y[i]=.83*y[i]+.17*softened[i];var scatter=new double[n];
            for(int k=0;k<3;k++){int shift=(int)Math.Round((rng.U(.0025,.0085)+(2-k)*path*.0035)*Rate);double gain=(.115-.021*k)*(1+.38*path)*rng.U(.75,1.25)*(k==1?-1:1);Tap(scatter,Low(y,rng.U(2300,5700)),shift,gain);}for(int i=0;i<n;i++)y[i]+=scatter[i];
            double silencer=0,resonator=0;foreach(var section in job.sections){double coverage=Clamp((section.end-position)/(section.end-section.start),0,1)*section.part.amount;if(section.part.kind==ExhaustPartKind.Resonator)resonator+=coverage;if(section.part.kind==ExhaustPartKind.ChamberedSilencer||section.part.kind==ExhaustPartKind.AbsorptiveSilencer)silencer+=coverage;}
            var chamber=new double[n];double[,] modes={{410,2,.28},{940,2.6,.19},{2100,3,.115}};
            for(int k=0;k<3;k++){var mode=Peak(y,modes[k,0]*rng.U(.78,1.20),modes[k,1]);double decay=rng.U(.009,.025);for(int i=0;i<n;i++)chamber[i]+=modes[k,2]*mode[i]*Math.Exp(-i/(Rate*decay));}
            for(int i=0;i<n;i++)y[i]+=.4*.9*(1+.55*Math.Min(1,silencer)+.20*Math.Min(1,resonator))*chamber[i];
            int delay=(int)Math.Round(Rate*path*job.lengthM/Math.Sqrt(1.34*287*Clamp(ev.temperatureK,300,1500)));Shift(y,delay);travel=delay/(double)Rate;return y;
        }
        static void Shift(double[] x,int delay){if(delay==0)return;for(int i=x.Length-1;i>=0;i--)x[i]=i>=delay?x[i-delay]:0;}
        public static Stem Render(V8BackfireMatrixExport.Job job,long master,Cleanup cleanup,Texture texture=Texture.Reference)
        {
            var mono=new double[job.frames];var stem=new Stem();double? previous=null;string lastScenario=null;double lastTime=-10;
            foreach(var ev in job.events){string region,kind;bool recent=ev.scenario==lastScenario&&ev.time-lastTime<.42;double position=Locate(job,ev,master,recent?previous:null,out region);var source=Source(job,ev,master,cleanup,out kind,texture);double travel;var emitted=Propagate(source,job,ev,master,position,out travel);int start=(int)Math.Round(ev.time*Rate);for(int i=0;i<emitted.Length&&start+i<mono.Length;i++)mono[start+i]+=ev.strength*job.popGain*emitted[i];stem.events.Add(new Receipt{index=ev.index,time=ev.time,strength=ev.strength,position=position,travel=travel,timbreSeed=Seed(master,job.id,ev.index,11),kind=kind,region=region});previous=position;lastScenario=ev.scenario;lastTime=ev.time;}
            stem.left=(double[])mono.Clone();stem.right=(double[])mono.Clone();var room=Low(Band(mono,140,0),3600);Tap(stem.left,room,(int)Math.Round(.011*Rate),.07);Tap(stem.right,room,(int)Math.Round(.020*Rate),.082);double gain=1.35*.85*.31*3;for(int i=0;i<mono.Length;i++){stem.left[i]*=gain;stem.right[i]*=gain;}return stem;
        }
    }
}
