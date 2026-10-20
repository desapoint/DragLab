using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Preview bridge only: keep authoritative race/synthesis, export pop-free engine and actual triggers.
    public static class V8BackfireMatrixExport
    {
        const int Rate=48000,Block=400;
        [Serializable] public sealed class Event
        {
            public int index,popId;public double time,strength,rpm,throttle,load,turboBoost,boost,spool,temperatureK,combustion,airflow;
            public bool twoStep,limiter,antiLag,overrun;public string cut,scenario;public double[] cylinderContribution;
        }
        [Serializable] public sealed class Section {public double start,end;public ExhaustPart part;}
        [Serializable] public sealed class FilterReference {public double position;public double[] samples;}
        [Serializable] public sealed class Job
        {
            public string id,title,reference,bed,fuel,induction,valve,layout,cut,limiter;
            public int frames,cylinders,exhaustStage,physicalPops,referenceMismatches;
            public double litres,popGain,popSharpness,popDecay,lengthM;
            public List<Section> sections=new List<Section>();public List<FilterReference> filters=new List<FilterReference>();
            public ExhaustSetup exhaust;public RaceAudioAuditionVerification.Result physical;
            public List<Event> events=new List<Event>();
        }
        [Serializable] public sealed class Manifest {public int sampleRate=Rate,channels=2;public List<Job> jobs=new List<Job>();}
        static void Check(bool yes,string why){if(!yes)throw new InvalidOperationException("V8 EXPORT: "+why);}
        static short Pcm(double value,int at,int total)
        {
            short sample=(short)Math.Round(value*32767);
            if(at<960)sample=(short)Math.Round(sample*at/959.0);
            if(at>=total-3840)sample=(short)Math.Round(sample*(total-1-at)/3839.0);
            return sample;
        }
        static void ExportHardware(Job job,int turboCount)
        {
            Func<ExhaustPart,double> length=p=>(p.kind==ExhaustPartKind.AbsorptiveSilencer||p.kind==ExhaustPartKind.ChamberedSilencer)?.65:p.kind==ExhaustPartKind.Resonator?.32:.22;
            double cursor=1.1+(turboCount>0?.18:0);job.lengthM=cursor+job.exhaust.parts.Sum(length)+.8;
            foreach(var part in job.exhaust.parts){double end=cursor+length(part);job.sections.Add(new Section{start=cursor/job.lengthM,end=end/job.lengthM,part=part.Copy()});cursor=end;}
            foreach(double at in new[]{0.0,.5,.95}){
                var setup=job.exhaust.Copy();for(int i=0;i<setup.parts.Length;i++){
                    var section=job.sections[i];setup.parts[i].amount*=(float)DragMath.Clamp((section.end-at)/(section.end-section.start),0,1);
                }
                var filter=new ExhaustFilter(setup,job.exhaustStage);filter.Configure(Rate);var data=new double[2048];
                for(int i=0;i<data.Length;i++)data[i]=filter.Process(i==0?1:0);
                job.filters.Add(new FilterReference{position=at,samples=data});
            }
        }
        public static void Run()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),folder=Path.Combine(root,"reports/v8-backfire-matrix-20261020");Directory.CreateDirectory(folder);
            var captures=RaceAudioAuditionVerification.CaptureV8Matrix(folder);var manifest=new Manifest();
            foreach(var capture in captures){
                var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(folder,capture.id+".race.json"),Encoding.UTF8));
                var race=fixture.CreateRace();
                try{
                    var car=race.player;var profile=new EngineSoundProfile(car.spec,car.tune);var controller=new EngineSoundController(car.spec,car.tune);
                    var original=new EngineSoundSynth(profile,sampleRate:Rate);var spec=car.spec.Copy();spec.sound.exhaustPopGain=0;
                    var bed=new EngineSoundSynth(new EngineSoundProfile(spec,car.tune),sampleRate:Rate);
                    var job=new Job{id=capture.id,title=capture.title,reference=capture.file,bed=capture.id+".bed.f32",frames=fixture.ticks.Count*Block,
                        cylinders=profile.cylinders,litres=profile.litres,fuel=profile.fueling.ToString(),induction=profile.induction.ToString(),valve=profile.valve.ToString(),layout=profile.layout.ToString(),
                        cut=car.engine.cut.ToString(),limiter=car.engine.limiter.ToString(),exhaustStage=profile.exhaust,exhaust=profile.exhaustSystem.Copy(),
                        popGain=profile.popGain,popSharpness=profile.popSharpness,popDecay=profile.popDecay,physicalPops=fixture.pops,physical=capture};
                    ExportHardware(job,profile.turboCount);
                    var full=new float[Block*2];var dry=new float[Block*2];int previousPop=0,previousGear=car.state.gear;double shifted=-10;bool downshift=false;
                    using(var reader=new BinaryReader(File.OpenRead(Path.Combine(folder,job.reference))))using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,job.bed)))){
                        reader.BaseStream.Position=44;
                        for(int tick=0;tick<fixture.ticks.Count;tick++){
                            double time=tick/120.0;var command=fixture.ticks[tick];car.state.warmupLineLock=command.lineLock;
                            if(command.beginCountdown)Check(race.BeginCountdown(),"staging failed");
                            if(race.phase==RacePhase.Finished)race.Shutdown(command.input);else race.Step(command.input);
                            var state=car.state;if(state.gear!=previousGear){shifted=time;downshift=state.gear<previousGear;}
                            var frame=controller.Update(state,true,.85,DragMath.FixedStep);original.Fill(full,2,Rate,frame);bed.Fill(dry,2,Rate,frame);
                            if(frame.popId!=previousPop){
                                previousPop=frame.popId;
                                string scenario=state.antiLagActive?"anti_lag":frame.twoStep?"two_step":state.limiter?(frame.cutMethod==CutMethod.Fuel?"fuel_cut_limiter":"rev_limiter"):
                                    time-shifted<.35?(downshift?"downshift":"upshift_cut"):frame.overrun?"throttle_lift":"idle_blip";
                                job.events.Add(new Event{index=job.events.Count,popId=frame.popId,time=time,strength=frame.popStrength,rpm=frame.rpm,throttle=frame.throttle,load=frame.load,
                                    turboBoost=frame.turboBoost,boost=frame.boost,spool=frame.turboSpool,temperatureK=650+state.exhaustHeat*180,combustion=frame.combustionFraction,airflow=frame.airflowKgS,
                                    twoStep=frame.twoStep,limiter=state.limiter,antiLag=state.antiLagActive,overrun=frame.overrun,cut=frame.cutMethod.ToString(),scenario=scenario,cylinderContribution=frame.cylinderContribution});
                            }
                            for(int i=0;i<full.Length;i++){
                                if(Pcm(full[i],tick*Block+i/2,job.frames)!=reader.ReadInt16())job.referenceMismatches++;
                                Check(!float.IsNaN(dry[i])&&!float.IsInfinity(dry[i])&&Math.Abs(dry[i])<1,"invalid bed PCM");writer.Write(dry[i]);
                            }
                            previousGear=state.gear;
                        }
                    }
                    Check(job.referenceMismatches==0,job.id+" production replay PCM mismatch");
                    Check(job.events.Count<=job.physicalPops,job.id+" more acoustic triggers than physical events");
                    manifest.jobs.Add(job);File.WriteAllText(Path.Combine(folder,"jobs.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
                    Debug.Log("V8 PCM EXPORT "+manifest.jobs.Count+" / "+captures.Length+" "+job.id+" events "+job.events.Count);
                }finally{race.CloseOpponentWorker();}
            }
            File.WriteAllText(Path.Combine(folder,"unity-verification.txt"),"PASS "+captures.Length+" complete authoritative race captures and exact production PCM replays; original engine/induction/cuts/condition DSP retained in pop-free beds. No career state or production DSP changes.\n",new UTF8Encoding(false));
            Debug.Log("V8 MATRIX EXPORT PASS");
        }
    }
}
