using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Offline comparison only. The production synthesizer and game assets are unchanged.
    public static class BisonBackfirePreview
    {
        const int Rate=48000, Block=400;
        const double Start=17.9, End=20.1;
        // Separate preview voice for accumulated-fuel afterfire; limiter cracks keep their existing renderer.
        sealed class Explosion
        {
            struct Cloud { public double age,strength,low,body,slow,air,timeScale,brightness; public uint random; }
            readonly Cloud[] clouds=new Cloud[4];
            readonly int kind;
            readonly EngineSoundProfile profile;
            int next;
            public Explosion(int kind,EngineSoundProfile profile){this.kind=kind;this.profile=profile;for(int i=0;i<clouds.Length;i++)clouds[i].age=10;}
            public void Trigger(double strength,EngineSoundFrame frame)
            {
                // Acoustic response proxies, not additional fuel/heat simulation or event generation.
                double flow=Math.Max(0,frame.airflowKgS);
                double charge=Math.Max(0,frame.boost);
                double scale=DragMath.Clamp(.65+.045*profile.litres+.22*strength+.18*charge+.3*flow,.6,1.6);
                clouds[next]=new Cloud{strength=strength*profile.popGain,timeScale=scale*profile.popDecay,brightness=DragMath.Clamp(1+.18*charge+.2*flow,.8,1.5)*profile.popSharpness,random=(uint)(0x72913+next*7919)};
                next=(next+1)%clouds.Length;
            }
            public double Sample()
            {
                double sum=0;
                for(int i=0;i<clouds.Length;i++){
                    var c=clouds[i];if(c.age>=1.2)continue;
                    c.random^=c.random<<13;c.random^=c.random>>17;c.random^=c.random<<5;
                    double noise=c.random/(double)uint.MaxValue*2-1;
                    double decay=(kind==6?.13:kind==7?.19:.25)*c.timeScale;
                    double cutoff=((kind==7?650:1050)*Math.Exp(-c.age/.35)+180)*c.brightness;
                    double coefficient=1-Math.Exp(-2*Math.PI*cutoff/Rate);
                    c.low+=coefficient*(noise-c.low);c.body+=coefficient*(c.low-c.body);
                    c.slow+=(1-Math.Exp(-2*Math.PI*55/Rate))*(c.body-c.slow);
                    c.air+=(1-Math.Exp(-2*Math.PI*2400/Rate))*(noise-c.air);
                    double attack=1-Math.Exp(-c.age/(kind==6?.003:.009));
                    double envelope=attack*Math.Exp(-c.age/decay);
                    // Broad overlapping gas-release lobes, rather than sharp repeated impacts.
                    for(int lobe=0;lobe<2;lobe++){
                        double delay=lobe==0?.045:.12;
                        double t=c.age-delay;
                        if(t>0)envelope+=.32*(1-Math.Exp(-t/.018))*Math.Exp(-t/(decay*.8));
                    }
                    double pressure=attack*(Math.Exp(-c.age/.024)-.34*Math.Exp(-c.age/.07));
                    double fade=Math.Min(1,(1.2-c.age)/.08);
                    sum+=(.65*pressure+4.5*(c.body-c.slow)*envelope+.14*c.air*envelope)*c.strength*6*fade;
                    c.age+=1.0/Rate;clouds[i]=c;
                }
                return sum;
            }
        }
        sealed class Burst
        {
            readonly int kind;
            readonly Explosion explosion;
            double age=10, strength, low, bass, tail, last;
            readonly double[] reflections=new double[4096];
            int reflectionAt;
            uint random=0x91732a5u;
            public Burst(int kind,EngineSoundProfile profile){this.kind=kind;if(kind>=6)explosion=new Explosion(kind,profile);}
            double Noise(){random^=random<<13;random^=random>>17;random^=random<<5;return random/(double)uint.MaxValue*2-1;}
            public void Trigger(EngineSoundFrame frame){if(explosion!=null){explosion.Trigger(frame.popStrength,frame);return;}tail=last;age=0;strength=frame.popStrength;}
            public double Sample()
            {
                if(explosion!=null)return explosion.Sample();
                if(kind>=3)return Bang();
                if(age>.35){last=0;return 0;}
                double positive=kind==1?.0045:kind==2?.0025:.0016;
                double negative=kind==1?.014:kind==2?.008:.005;
                double decay=kind==1?.025:kind==2?.038:.012;
                double cutoff=kind==1?2200:kind==2?3800:4800;
                low+=(1-Math.Exp(-2*Math.PI*cutoff/Rate))*(Noise()-low);
                bass+=(1-Math.Exp(-2*Math.PI*65/Rate))*(low-bass);
                double attack=1-Math.Exp(-age/.00009);
                double pressure=Math.Exp(-age/positive)-positive/negative*Math.Exp(-age/negative);
                double turbulence=(low-bass)*Math.Exp(-age/decay);
                double extra=0;
                // Two small irregular reflections, never a repeating pitched oscillator.
                if(kind==2){
                    if(age>=.013)extra+=.18*Math.Exp(-(age-.013)/.006)*(low-bass);
                    if(age>=.031)extra+=.11*Math.Exp(-(age-.031)/.008)*(low-bass);
                }
                double value=((kind==1?1.25:.85)*pressure+(kind==2?2.8:2.1)*turbulence+extra)*attack*strength;
                value+=tail;tail*=Math.Exp(-1.0/(Rate*.002));last=value;age+=1.0/Rate;return value;
            }
            double Bang()
            {
                if(age>.65){last=0;return 0;}
                double decay=kind==3?.042:kind==4?.075:.105;
                double cutoff=kind==3?5200:kind==4?2900:3900;
                low+=(1-Math.Exp(-2*Math.PI*cutoff/Rate))*(Noise()-low);
                bass+=(1-Math.Exp(-2*Math.PI*45/Rate))*(low-bass);
                double positive=kind==4?.007:.0035,negative=kind==4?.026:.016;
                double pressure=Math.Exp(-age/positive)-positive/negative*Math.Exp(-age/negative);
                double blast=(low-bass)*(6*Math.Exp(-age/.006)+2.9*Math.Exp(-age/decay));
                double direct=(3.2*pressure+blast)*(1-Math.Exp(-age/.00007));
                // Feed-forward reflections of the actual burst, with no tonal oscillator or feedback.
                double echo=.24*reflections[(reflectionAt-624+4096)%4096]
                    +.12*reflections[(reflectionAt-1488+4096)%4096];
                reflections[reflectionAt]=direct;reflectionAt=(reflectionAt+1)%4096;
                double value=(direct+echo)*strength*(kind==3?2.7:kind==4?3.1:2.8);
                value+=tail;tail*=Math.Exp(-1.0/(Rate*.002));last=value;age+=1.0/Rate;return value;
            }
        }
        sealed class Alternative
        {
            readonly Burst burst;
            readonly ExhaustFilter exhaust;
            readonly double[] travel;
            readonly int delay;
            readonly double upstreamReflection;
            int travelAt;
            double input,output;
            int lastPop;
            public Alternative(int kind,EngineSoundProfile profile,int sourcePart=0,double remainingMetres=0,double temperatureK=650,double firstAmount=1)
            {
                burst=new Burst(kind,profile);
                var setup=profile.exhaustSystem.Copy();
                Require(sourcePart>=0&&sourcePart<=setup.parts.Length,"Source must lie on exhaust path");
                var downstream=new ExhaustPart[setup.parts.Length-sourcePart];Array.Copy(setup.parts,sourcePart,downstream,0,downstream.Length);
                if(downstream.Length>0)downstream[0].amount*=(float)firstAmount;
                setup.parts=downstream;exhaust=new ExhaustFilter(setup,profile.exhaust);exhaust.Configure(Rate);
                if(remainingMetres>0){
                    // Approximate gas sound speed; geometry/temperature are explicit preview assumptions.
                    double speed=Math.Sqrt(1.34*287*temperatureK);
                    delay=Math.Max(1,(int)Math.Round(remainingMetres/speed*Rate));travel=new double[delay*4+1];
                    upstreamReflection=sourcePart==0?.12:.2;
                }
            }
            public void Frame(EngineSoundFrame frame){if(frame.popId!=lastPop){lastPop=frame.popId;burst.Trigger(frame);}}
            public double Sample(double volume)
            {
                double raw=burst.Sample()*.3*volume;
                if(travel!=null){
                    travel[travelAt]=raw;
                    raw=travel[(travelAt-delay+travel.Length)%travel.Length]
                        +upstreamReflection*travel[(travelAt-delay*3+travel.Length)%travel.Length];
                    travelAt=(travelAt+1)%travel.Length;
                }
                double value=exhaust.Process(raw);
                double filtered=value-input+.995*output;input=value;output=filtered;return filtered;
            }
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("BACKFIRE PREVIEW: "+message);}
        static double Unsaturate(float value)=>value/(1-Math.Abs(value));
        static float Saturate(double value)=>(float)(value/(1+Math.Abs(value)));
        static void Wav(string path,List<float> values)
        {
            using(var w=new BinaryWriter(File.Create(path))){
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+values.Count*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*4);w.Write((short)4);w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));w.Write(values.Count*2);
                foreach(float value in values){Require(!float.IsNaN(value)&&!float.IsInfinity(value)&&Math.Abs(value)<1,"Invalid preview PCM");w.Write((short)Math.Round(value*32767));}
            }
        }
        public static void Run()
        {Render(0);}
        public static void RunBang()
        {Render(1);}
        public static void RunExplosion()
        {Render(2);}
        public static void RunLocationStudy()
        {Render(3);}
        public static void RunAfterfireStudies()
        {Render(2);Render(3);}
        static void Render(int generation)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string folder=Path.Combine(root,generation==3?"reports/backfire-location-variants-20261020":generation==2?"reports/backfire-explosion-variants-20261020":generation==1?"reports/backfire-bang-variants-20261020":"reports/backfire-pop-variants-20261020");Directory.CreateDirectory(folder);
            double end=generation>=2?20.9:End;
            var report=new StringBuilder("Preview only; no production audio/physics changes.\n");
            foreach(string id in new[]{"29-exhaust-1","30-exhaust-2"}){
                var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(root,"reports/race-audio-audition",id+".race.json"),Encoding.UTF8));
                var race=fixture.CreateRace();
                try{
                    var car=race.player;var controller=new EngineSoundController(car.spec,car.tune);
                    var original=new EngineSoundSynth(new EngineSoundProfile(car.spec,car.tune),sampleRate:Rate);
                    var spec=car.spec.Copy();spec.sound.exhaustPopGain=0;
                    var bed=new EngineSoundSynth(new EngineSoundProfile(spec,car.tune),sampleRate:Rate);
                    var profile=new EngineSoundProfile(car.spec,car.tune);
                    int offset=generation*3;
                    double metres=generation>=2?3.2:0;
                    var alternatives=generation==3?new[]{new Alternative(7,profile,0,3.2),new Alternative(7,profile,0,1.3,650,.5),new Alternative(7,profile,profile.exhaustSystem.parts.Length,.35)}
                        :new[]{new Alternative(offset,profile,0,metres),new Alternative(offset+1,profile,0,metres),new Alternative(offset+2,profile,0,metres)};
                    var buffers=new[]{new List<float>(),new List<float>(),new List<float>(),new List<float>(),new List<float>()};
                    var full=new float[Block*2];var without=new float[Block*2];
                    int first=(int)Math.Round(Start*Rate),last=(int)Math.Round(end*Rate),events=0,previousPop=0,mismatches=0;
                    double beforeDifference=0,eventDifference=0;int beforeSamples=0,eventSamples=0;
                    using(var reference=new BinaryReader(File.OpenRead(Path.Combine(root,"reports/audio-ding-excerpts-20261020",id+".wav")))){
                        reference.BaseStream.Position=44+first*4L;
                        for(int tick=0;tick<fixture.ticks.Count&&tick*Block<last;tick++){
                            var command=fixture.ticks[tick];car.state.warmupLineLock=command.lineLock;
                            if(command.beginCountdown)Require(race.BeginCountdown(),"Cannot stage fixture");
                            if(race.phase==RacePhase.Finished)race.Shutdown(command.input);else race.Step(command.input);
                            var frame=controller.Update(car.state,true,.85,DragMath.FixedStep);
                            original.Fill(full,2,Rate,frame);bed.Fill(without,2,Rate,frame);
                            foreach(var alternate in alternatives)alternate.Frame(frame);
                            if(frame.popId!=previousPop){previousPop=frame.popId;if(tick*Block>=first)events++;}
                            for(int sample=0;sample<Block;sample++){
                                var added=new[]{alternatives[0].Sample(frame.volume),alternatives[1].Sample(frame.volume),alternatives[2].Sample(frame.volume)};
                                int at=tick*Block+sample;if(at<first||at>=last)continue;
                                for(int channel=0;channel<2;channel++){
                                    int index=sample*2+channel;float a=full[index],b=without[index];
                                    if((short)Math.Round(a*32767)!=reference.ReadInt16())mismatches++;
                                    buffers[0].Add(a);buffers[1].Add(b);
                                    for(int v=0;v<3;v++)buffers[v+2].Add(added[v]==0?b:Saturate(Unsaturate(b)+added[v]));
                                    double difference=(a-b)*(a-b);
                                    if(at<18.80*Rate){beforeDifference+=difference;beforeSamples++;}
                                    else if(at<19.00*Rate){eventDifference+=difference;eventSamples++;}
                                }
                            }
                        }
                    }
                    Require(mismatches==0,"Original reference changed: "+id+" / "+mismatches);
                    Require(events==2,"Expected exactly two physical pops inside crop");
                    Require(beforeDifference/Math.Max(1,beforeSamples)<1e-10,"Engine bed differs before the pop");
                    Require(eventDifference/Math.Max(1,eventSamples)>1e-7,"No original pop energy isolated");
                    var names=generation==3?new[]{"original","no-pop-control","J-before-silencer","K-inside-silencer","L-after-silencer"}
                        :generation==2?new[]{"original","no-pop-control","G-full-afterfire","H-deep-afterfire","I-rolling-afterfire"}
                        :generation==1?new[]{"original","no-pop-control","D-sharp-bang","E-deep-bang","F-long-bark"}
                        :new[]{"original","no-pop-control","A-dry-crack","B-deep-pop","C-raspy-bark"};
                    for(int i=0;i<names.Length;i++){Require(buffers[i].Count==(last-first)*2,"Wrong preview length");Wav(Path.Combine(folder,id+"-"+names[i]+".wav"),buffers[i]);}
                    report.AppendLine("PASS "+id+": original PCM16 matches identified source exactly; same engine bed, two actual pop triggers, finite bounded stereo; pre-pop bed unchanged. Original-pop difference RMS "+Math.Sqrt(eventDifference/eventSamples));
                }finally{race.CloseOpponentWorker();}
            }
            File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log("BISON BACKFIRE PREVIEWS PASS");
        }
    }
}
