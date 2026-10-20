using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Real input replay plus explicitly inferred acoustic gas packets. No career state or runtime DSP edits.
    public static class TempestBackfireComparison
    {
        const int Rate=48000,Block=400;
        const double SourceCoupling=4;
        [Serializable] public sealed class Receipt
        {
            public double time,rpm,boost;public bool twoStep,limiter,antiLag;public PreviewCombustionEvent packet;
        }
        [Serializable] public sealed class Output
        {
            public string file,sha256;public double peak,rms,heatJ,releasedHeatJ,pressureSquared,turbulenceSquared,structureSquared;
            public int accepted,overflow;public PreviewCombustionVoicing voicing;
        }
        [Serializable] public sealed class Manifest
        {
            public string vehicle="Tempest inline-four turbo, free-flow exhaust, ignition cuts/two-step and anti-lag";
            public string reference="00-in-game-reference.wav",referenceSha256;
            public double sourceCoupling=SourceCoupling;
            public int sampleRate=Rate,channels=2,frames,physicalPops,publishedPopEvents,referenceMismatches;
            public List<Receipt> events=new List<Receipt>();public List<Output> options=new List<Output>();
        }
        static void Require(bool yes,string why){if(!yes)throw new InvalidOperationException("TEMPEST COMPARISON: "+why);}
        static void Header(BinaryWriter w,int frames)
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*4);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);
            w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*4);w.Write((short)4);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(frames*4);
        }
        static short Pcm(double value,int frame,int total)
        {
            Require(!double.IsNaN(value)&&!double.IsInfinity(value)&&Math.Abs(value)<1,"invalid PCM");
            short sample=(short)Math.Round(value*32767);
            if(frame<960)sample=(short)Math.Round(sample*frame/959.0);
            if(frame>=total-3840)sample=(short)Math.Round(sample*(total-1-frame)/3839.0);
            return sample;
        }
        static string Hash(string path)
        {using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        static void SameEvents(Manifest actual,Manifest prior)
        {
            Require(actual.events.Count==prior.events.Count,"published event count changed");
            for(int i=0;i<actual.events.Count;i++){
                var a=actual.events[i];var b=prior.events[i];
                Require(a.twoStep==b.twoStep&&a.limiter==b.limiter&&a.antiLag==b.antiLag&&a.packet.seed==b.packet.seed&&a.packet.intake==b.packet.intake&&a.packet.ignited==b.packet.ignited,"event flags/seed changed at "+i);
                var x=Scalars(a);var y=Scalars(b);
                for(int j=0;j<x.Length;j++)Require(Math.Abs(x[j]-y[j])<=1e-12*Math.Max(1,Math.Abs(y[j])),"event scalar changed at "+i+" field "+j+": "+x[j].ToString("R")+" vs "+y[j].ToString("R"));
            }
        }
        static double[] Scalars(Receipt r)
        {
            var p=r.packet;return new[]{r.time,r.rpm,r.boost,p.fuelMg,p.oxygenMg,p.oxygenPerFuel,p.heatingJPerKg,p.burnEfficiency,p.burnSeconds,p.mixingSeconds,p.volumeLitres,p.temperatureK,p.pressureKPa,p.sourceMetres};
        }
        static PreviewCombustionEvent Packet(CarState state,EngineSoundFrame frame,EngineSoundProfile profile,int index,double manifold,double downstream)
        {
            bool shortCut=frame.twoStep||state.limiter;
            double strength=DragMath.Clamp(frame.popStrength,0,1);
            double mass=(shortCut?1.2+strength*2.0:6+strength*24)*profile.litres/2;
            return new PreviewCombustionEvent{
                fuelMg=mass,oxygenMg=mass*3.5,oxygenPerFuel=3.43,heatingJPerKg=44e6,burnEfficiency=.72,
                burnSeconds=shortCut?.0008+strength*.0005:.002+strength*.002,mixingSeconds=shortCut?.006:.02,
                volumeLitres=shortCut?.6:1.2,temperatureK=650+state.exhaustHeat*180,pressureKPa=101.3+Math.Max(0,frame.turboBoost)*40,
                sourceMetres=state.antiLagActive||shortCut?manifold:downstream,intake=false,ignited=true,seed=(uint)(171818+index*7919)
            };
        }
        public static void Run()=>RenderComparison(false);
        public static void RunSharper()=>RenderComparison(true);
        static void RenderComparison(bool sharper)
        {
            SharedBackfirePreviewVerification.RunChecks();
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),baseline=Path.Combine(root,"reports/tempest-backfire-comparison-20261020");
            string folder=sharper?Path.Combine(root,"reports/tempest-backfire-sharp-20261020"):baseline;Directory.CreateDirectory(folder);
            // Revised auditions replay the original captured commands and retain the reference bytes.
            var fixture=sharper?JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(baseline,"tempest-turbo-open.race.json"),Encoding.UTF8)):RaceAudioAuditionVerification.CaptureBackfireComparison(folder);
            Require(fixture.pops>=100,"configuration must generate at least 100 real physical pops");
            string captured=Path.Combine(baseline,"tempest-turbo-open.wav"),reference=Path.Combine(folder,"00-in-game-reference.wav");File.Copy(captured,reference,true);
            var manifest=new Manifest{frames=fixture.ticks.Count*Block,physicalPops=fixture.pops,referenceSha256=Hash(reference)};
            var race=fixture.CreateRace();var writers=new List<BinaryWriter>();
            try{
                var car=race.player;var profile=new EngineSoundProfile(car.spec,car.tune);var controller=new EngineSoundController(car.spec,car.tune);
                var original=new EngineSoundSynth(profile,sampleRate:Rate);var spec=car.spec.Copy();spec.sound.exhaustPopGain=0;
                var bed=new EngineSoundSynth(new EngineSoundProfile(spec,car.tune),sampleRate:Rate);
                var settings=new[]{
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=1.8,turbulenceCoupling=.10,frontCoupling=.9,frontMilliseconds=.65,structureCoupling=.10,expansionScale=1},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=3,turbulenceCoupling=.08,frontCoupling=1.25,frontMilliseconds=.85,structureCoupling=.15,expansionScale=1.2},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=4,turbulenceCoupling=.05,frontCoupling=.55,frontMilliseconds=1.5,structureCoupling=.2,expansionScale=1.8},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=2,turbulenceCoupling=.15,frontCoupling=1.8,frontMilliseconds=.4,structureCoupling=.08,expansionScale=.85},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=3,turbulenceCoupling=.2,frontCoupling=1,frontMilliseconds=1.1,structureCoupling=.15,expansionScale=2}
                };
                if(sharper)settings=new[]{
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=1.8,turbulenceCoupling=.02,frontCoupling=.9,frontMilliseconds=.08,structureCoupling=.10,expansionScale=1,burnTimeScale=.12,reflectionDecaySeconds=.18,reflectionMix=.40},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=3,turbulenceCoupling=.02,frontCoupling=1.25,frontMilliseconds=.10,structureCoupling=.15,expansionScale=1.2,burnTimeScale=.10,reflectionDecaySeconds=.24,reflectionMix=.50},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=4,turbulenceCoupling=.01,frontCoupling=.8,frontMilliseconds=.15,structureCoupling=.20,expansionScale=1.8,burnTimeScale=.16,reflectionDecaySeconds=.30,reflectionMix=.55},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=2,turbulenceCoupling=.025,frontCoupling=1.8,frontMilliseconds=.04,structureCoupling=.08,expansionScale=.85,burnTimeScale=.08,reflectionDecaySeconds=.20,reflectionMix=.45},
                    new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=3,turbulenceCoupling=.02,frontCoupling=1.1,frontMilliseconds=.08,structureCoupling=.15,expansionScale=2,burnTimeScale=.12,reflectionDecaySeconds=.40,reflectionMix=.65}
                };
                var names=new[]{"01-pressure-pop.wav","02-combustion-bang.wav","03-deep-report.wav","04-fast-crack.wav","05-long-release.wav"};
                var generators=new SharedBackfirePreviewSynth[5];double manifold=0,downstream=0;
                for(int i=0;i<5;i++){
                    generators[i]=SharedBackfireAudition.PreparePhysicalComparison(car.spec,car.tune,settings[i],out manifold,out downstream);
                    manifest.options.Add(new Output{file=names[i],voicing=settings[i]});
                    var writer=new BinaryWriter(File.Create(Path.Combine(folder,names[i])));Header(writer,manifest.frames);writers.Add(writer);
                }
                var full=new float[Block*2];var dry=new float[Block*2];var source=new double[5];int previousPop=0;
                using(var reader=new BinaryReader(File.OpenRead(reference))){
                    reader.BaseStream.Position=44;
                    for(int tick=0;tick<fixture.ticks.Count;tick++){
                        var command=fixture.ticks[tick];car.state.warmupLineLock=command.lineLock;
                        if(command.beginCountdown)Require(race.BeginCountdown(),"cannot stage replay");
                        if(race.phase==RacePhase.Finished)race.Shutdown(command.input);else race.Step(command.input);
                        var frame=controller.Update(car.state,true,.85,DragMath.FixedStep);original.Fill(full,2,Rate,frame);bed.Fill(dry,2,Rate,frame);
                        foreach(var generator in generators)generator.SetBoost(frame.turboBoost);
                        if(frame.popId!=previousPop){
                            previousPop=frame.popId;var packet=Packet(car.state,frame,profile,manifest.events.Count,manifold,downstream);
                            manifest.events.Add(new Receipt{time=tick/120.0,rpm=frame.rpm,boost=frame.boost,twoStep=frame.twoStep,limiter=car.state.limiter,antiLag=car.state.antiLagActive,packet=packet});
                            foreach(var generator in generators)Require(generator.Trigger(packet),"lost real event");
                        }
                        for(int sample=0;sample<Block;sample++){
                            int at=tick*Block+sample;
                            for(int v=0;v<5;v++)source[v]=generators[v].Sample()*.85*SourceCoupling;
                            for(int ch=0;ch<2;ch++){
                                int i=sample*2+ch;short expected=reader.ReadInt16();
                                if(Pcm(full[i],at,manifest.frames)!=expected)manifest.referenceMismatches++;
                                double clean=dry[i]/(1-Math.Abs(dry[i]));
                                for(int v=0;v<5;v++){
                                    double value=source[v]==0?dry[i]:(clean+source[v])/(1+Math.Abs(clean+source[v]));
                                    var output=manifest.options[v];output.peak=Math.Max(output.peak,Math.Abs(value));output.rms+=value*value;
                                    writers[v].Write(Pcm(value,at,manifest.frames));
                                }
                            }
                        }
                    }
                }
                manifest.publishedPopEvents=manifest.events.Count;
                if(sharper){
                    var prior=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(baseline,"manifest.json"),Encoding.UTF8));
                    Require(manifest.referenceSha256==prior.referenceSha256&&manifest.frames==prior.frames,"original reference changed");
                    SameEvents(manifest,prior);
                }
                Require(manifest.referenceMismatches==0,"production reference diverged from exact replay");
                Require(manifest.publishedPopEvents>=100,"not enough actual published events");
                foreach(var writer in writers)writer.Dispose();writers.Clear();
                for(int i=0;i<5;i++){
                    var output=manifest.options[i];var generator=generators[i];output.rms=Math.Sqrt(output.rms/(manifest.frames*2.0));output.sha256=Hash(Path.Combine(folder,output.file));
                    output.accepted=generator.Accepted;output.overflow=generator.RejectedOverlap;output.heatJ=generator.AcceptedHeatJ;output.releasedHeatJ=generator.ReleasedHeatJ;
                    output.pressureSquared=generator.PressureSquaredSamples;output.turbulenceSquared=generator.TurbulenceSquaredSamples;output.structureSquared=generator.StructureSquaredSamples;
                    Require(output.accepted==manifest.publishedPopEvents&&output.overflow==0,"voice accounting mismatch");
                    Require(Math.Abs(output.heatJ-output.releasedHeatJ)<output.heatJ*1e-6,"heat-release ledger mismatch");
                }
                File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS production reference sample-identical on real input replay; "+manifest.physicalPops+" physical pops / "+manifest.publishedPopEvents+" published audio events.\nPASS five matched options; shared real triggers, unchanged dry engine bed, zero dropped voices, fuel/oxygen-limited heat-release accounting.\n"+SharedBackfirePreviewVerification.Summary,new UTF8Encoding(false));
                Debug.Log("TEMPEST REFERENCE AND FIVE OPTIONS PASS");
            }finally{foreach(var writer in writers)writer.Dispose();race.CloseOpponentWorker();}
        }
    }
}
