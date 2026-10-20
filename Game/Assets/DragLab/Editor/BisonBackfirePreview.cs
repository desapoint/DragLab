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
        sealed class Burst
        {
            readonly int kind;
            double age=10, strength, low, bass, tail, last;
            uint random=0x91732a5u;
            public Burst(int kind){this.kind=kind;}
            double Noise(){random^=random<<13;random^=random>>17;random^=random<<5;return random/(double)uint.MaxValue*2-1;}
            public void Trigger(double value){tail=last;age=0;strength=value;}
            public double Sample()
            {
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
        }
        sealed class Alternative
        {
            readonly Burst burst;
            readonly ExhaustFilter exhaust;
            double input,output;
            int lastPop;
            public Alternative(int kind,EngineSoundProfile profile)
            {burst=new Burst(kind);exhaust=new ExhaustFilter(profile.exhaustSystem,profile.exhaust);exhaust.Configure(Rate);}
            public void Frame(EngineSoundFrame frame){if(frame.popId!=lastPop){lastPop=frame.popId;burst.Trigger(frame.popStrength);}}
            public double Sample(double volume)
            {
                double value=exhaust.Process(burst.Sample()*.3*volume);
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
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string folder=Path.Combine(root,"reports/backfire-pop-variants-20261020");Directory.CreateDirectory(folder);
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
                    var alternatives=new[]{new Alternative(0,profile),new Alternative(1,profile),new Alternative(2,profile)};
                    var buffers=new[]{new List<float>(),new List<float>(),new List<float>(),new List<float>(),new List<float>()};
                    var full=new float[Block*2];var without=new float[Block*2];
                    int first=(int)Math.Round(Start*Rate),last=(int)Math.Round(End*Rate),events=0,previousPop=0,mismatches=0;
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
                    var names=new[]{"original","no-pop-control","A-dry-crack","B-deep-pop","C-raspy-bark"};
                    for(int i=0;i<names.Length;i++){Require(buffers[i].Count==(last-first)*2,"Wrong preview length");Wav(Path.Combine(folder,id+"-"+names[i]+".wav"),buffers[i]);}
                    report.AppendLine("PASS "+id+": original PCM16 matches identified source exactly; same engine bed, two actual pop triggers, finite bounded stereo; pre-pop bed unchanged. Original-pop difference RMS "+Math.Sqrt(eventDifference/eventSamples));
                }finally{race.CloseOpponentWorker();}
            }
            File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log("BISON BACKFIRE PREVIEWS PASS");
        }
    }
}
