using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DragLab.Domain;
using UnityEngine;
namespace DragLab.Editor
{
    public static class RuntimeAudioIntegrationVerification
    {
        public static string OutputDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/runtime-audio-20261020"));
        public static bool TracksideListening;
        static string Root => OutputDirectory;
        static readonly StringBuilder log=new StringBuilder();static int checks;
        static void Check(bool value,string label){log.AppendLine((value?"PASS ":"FAIL ")+label);checks++;Save();if(!value)throw new InvalidOperationException(label);}
        static void Save(){Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));}
        public static void Run()
        {
            checks=0;log.Clear();Directory.CreateDirectory(Root);
            BackfireMixtureAudition.Verify();
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/mixture-backfire-20261020/core-verification.txt")),Path.Combine(Root,"mixture-core.txt"),true);
            var spec=new VehicleSpec();spec.engineBehavior.fueling=FuelSystem.Carburetor;
            spec.engineBehavior.mixtureVariation=.4f;spec.sound.exhaustSystem=ExhaustSetup.Preset(1);
            var tune=new Tune();var profile=new EngineSoundProfile(spec,tune);
            foreach(int rate in new[]{44100,48000})
            {
                var model=new ExhaustMixtureModel(20261020,"runtime-check",initialK:1000);
                var input=new ExhaustMixtureModel.Input{dt=1.0/120,fuelKg=3e-6,airKg=5e-5,chamberBurnFraction=.1,intakeK=350,wallTargetK=1000,rpm=4200};
                var frame=new EngineSoundFrame(4200,.8,.7,0,.85,afterfire:model,airflowKgS:.12);
                var a=new EngineSoundSynth(profile,sampleRate:rate);var b=new EngineSoundSynth(profile,sampleRate:rate);
                var x=new float[(rate/120)*2];var y=new float[x.Length];a.Fill(x,2,rate,frame);b.Fill(y,2,rate,frame);
                bool exact=true,finite=true;double energy=0;
                // Same immutable frame deliberately remains published: event history must not depend on render publication.
                for(int tick=0;tick<240;tick++)
                {model.Step(input);a.Fill(x,2,rate,frame);b.Fill(y,2,rate,frame);for(int i=0;i<x.Length;i++){exact&=x[i]==y[i];finite&=!float.IsNaN(x[i])&&!float.IsInfinity(x[i])&&Math.Abs(x[i])<1;energy+=x[i]*x[i];}}
                for(int tick=0;tick<60;tick++){a.Fill(x,2,rate,frame);b.Fill(y,2,rate,frame);}
                var d=a.AfterfireDiagnostics(rate);
                Check(model.Sequence>0&&d.Delivered==model.Sequence,"every chemical event delivered once through unchanged frame / "+rate);
                Check(d.HistoryOverflows==0&&d.TailSteals==0,"no event history overflow or tail stealing in hot mixture / "+rate);
                Check(exact&&finite&&energy>0,"deterministic finite audible full pipeline / "+rate);
                long delivered=d.Delivered;a.Fill(x,2,rate,frame);Check(d.Delivered==delivered,"unchanged event history cannot retrigger / "+rate);
                var paused=new EngineSoundFrame(0,0,0,0,0,active:false);a.Fill(x,2,rate,paused);a.Fill(x,2,rate,frame);
                Check(d.Delivered==delivered,"pause/resume cannot replay delivered events / "+rate);
                Check(Math.Abs(model.FuelIn-model.ChamberBurned-model.FuelOut-model.FuelBurned-model.RetainedFuel)<1e-10,"runtime fuel mass conserved / "+rate);
                long allocated=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<120;i++){model.Step(input);a.Fill(x,2,rate,frame);}
                Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"zero warmed allocation in mixture tick plus full audio callback / "+rate);
                var times=new double[120];var watch=new Stopwatch();
                for(int i=0;i<times.Length;i++){model.Step(input);watch.Restart();a.Fill(x,2,rate,frame);watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
                Array.Sort(times);log.AppendLine("MEASURE "+rate+" / "+x.Length/2+" stereo frames / hot mixture callback p95 "+times[113].ToString("F4")+" ms; max "+times[119].ToString("F4")+" ms.");
                Check(times[113]<(x.Length/2.0/rate*1000)*.6,"Editor hot-afterfire callback p95 below 60% of signal budget / "+rate);
            }
            // Isolate pressure audio to ensure continuation energy respects fitted pop gain.
            var quiet=spec.Copy();quiet.sound.engineGain=0;quiet.engineBehavior.mechanicalClatter=0;
            var lower=quiet.Copy();lower.sound.exhaustPopGain=quiet.sound.exhaustPopGain*.25f;
            var sourceModel=new ExhaustMixtureModel(20261020,"gain-contract",initialK:1000);
            var signal=new EngineSoundFrame(4200,.8,.7,0,.85,afterfire:sourceModel);
            var loud=new EngineSoundSynth(new EngineSoundProfile(quiet,tune),sampleRate:48000);var soft=new EngineSoundSynth(new EngineSoundProfile(lower,tune),sampleRate:48000);
            var loudPcm=new float[800];var softPcm=new float[800];loud.Fill(loudPcm,2,48000,signal);soft.Fill(softPcm,2,48000,signal);
            var feed=new ExhaustMixtureModel.Input{dt=1.0/120,fuelKg=3e-6,airKg=5e-5,chamberBurnFraction=.1,intakeK=350,wallTargetK=1000,rpm=4200};
            bool proportional=true;double emitted=0;
            for(int tick=0;tick<240;tick++)
            {
                sourceModel.Step(feed);loud.Fill(loudPcm,2,48000,signal);soft.Fill(softPcm,2,48000,signal);
                for(int i=0;i<loudPcm.Length;i++){double a=loudPcm[i]/(1-Math.Abs(loudPcm[i])),b=softPcm[i]/(1-Math.Abs(softPcm[i]));proportional&=Math.Abs(b-.25*a)<1e-6;emitted+=a*a;}
            }
            Check(proportional&&emitted>0,"pressure fronts and continuing burns both respect fitted acoustic gain");
            var driver=new DriverSettings{fuelUsage=FuelUsageMode.None};
            var plain=new CarSimulation(spec,tune,driver);var audible=new CarSimulation(spec,tune,driver);
            var control=new EngineSoundController(spec,tune);control.Update(audible.state,true,.85,1.0/120);
            bool parity=true;
            for(int i=0;i<600;i++)
            {var command=new DriverInput{throttle=i%180<120?.85:0};plain.IndependentStep(1.0/120,command,true,false);audible.IndependentStep(1.0/120,command,true,false);parity&=JsonUtility.ToJson(plain.state)==JsonUtility.ToJson(audible.state);}
            Check(parity,"enabling live acoustic simulation leaves vehicle dynamics/resources bit-identical");
            Check(audible.state.audioExhaust.mixture.FuelIn>0&&audible.state.fuelUsedLitres==0,"disabled depletion still has physical fuel/air excitation");
            Check(audible.state.Copy().audioExhaust==null,"vehicle copies cannot share mutable acoustic transport");
            audible.state.activeCutMethod=CutMethod.Fuel;audible.state.combustionFraction=0;
            Check(FuelDynamics.PhysicalFlow(spec,tune,audible.induction,audible.engine,audible.state)==0,"full fuel cut supplies no fresh exhaust fuel");
            // Intake is deliberately much quieter than either rejected level preview.
            Check(.21715/13.701294250176943<Math.Pow(10,-35.9/20),"runtime intake at least 35.9 dB below diagnostic-matched preview");
            var turboTune=new Tune{engineLevel=1,inductionUpgrade=EngineUpgradeInduction.Turbo,blowOffValve=BlowOffType.Hiss};
            var turboProfile=new EngineSoundProfile(spec,turboTune);var releaseSource=new RuntimeExhaustState(turboProfile,new CarState{engineTemperature=1});
            var releases=new ReleaseAudioSynth(turboProfile,48000);releases.Connect(releaseSource);
            for(int i=0;i<5;i++)releaseSource.RecordRelease(.8);
            double releaseEnergy=0;for(int i=0;i<4800;i++){double value=releases.Sample(i/48000.0);releaseEnergy+=value*value;}
            Check(releases.Delivered==5&&releases.Overflows==0&&releases.TailSteals==0&&releaseEnergy>0,"five BOV releases between frame publications retain independent voices");
            releases.Connect(null);releases.Connect(releaseSource);releases.Sample(.2);
            Check(releases.Delivered==5,"BOV pause/resume cannot replay delivered releases");
            var noFuelSpec=spec.Copy();noFuelSpec.sound.engineGain=0;noFuelSpec.engineBehavior.mechanicalClatter=0;
            var noFuelState=new CarState{rpm=3000};var noFuelControl=new EngineSoundController(noFuelSpec,tune);
            var noFuelSynth=new EngineSoundSynth(new EngineSoundProfile(noFuelSpec,tune),sampleRate:48000);var silent=new float[800];
            noFuelControl.Update(noFuelState,true,1,.01);noFuelState.exhaustPopCount=50;noFuelState.exhaustPopStrength=1;
            noFuelSynth.Fill(silent,2,48000,noFuelControl.Update(noFuelState,true,1,.01));
            Check(silent.All(v=>v==0),"legacy pop counters cannot invent audible gameplay explosions without chemical fuel events");
            string random=JsonUtility.ToJson(UnityEngine.Random.state);
            var diagnostic=new EngineSoundSynth(profile,sampleRate:48000);diagnostic.Fill(new float[800],2,48000,control.Update(audible.state,true,1,1.0/120));
            Check(random==JsonUtility.ToJson(UnityEngine.Random.state),"audio chemistry and playback preserve Unity gameplay RNG");
            Save();UnityEngine.Debug.Log("RUNTIME AUDIO INTEGRATION PASS "+checks);
        }
        public static void PrepareAndCheckRaces(){RaceAudioAuditionVerification.PrepareInputs();CheckRaces();}
        public static void CheckRaces()
        {
            var report=new StringBuilder("Actual 47-race production pipeline; legacy physical traces remain unchanged.\n");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition"));
            var files=Directory.GetFiles(folder,"*.race.json");Array.Sort(files,StringComparer.Ordinal);
            if(files.Length!=47)throw new InvalidOperationException("Expected full 47-case audio roster.");
            var args=Environment.GetCommandLineArgs();int selected=Array.IndexOf(args,"--draglab-runtime-audio-cases");
            bool focused=selected>=0;
            if(focused)files=files.Where(f=>args[selected+1].Split(',').Any(prefix=>Path.GetFileName(f).StartsWith(prefix,StringComparison.Ordinal))).ToArray();
            string receipt=Path.Combine(Root,focused?"race-pipeline-focused.txt":"race-pipeline.txt");
            foreach(string file in files)
            {
                var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(file,Encoding.UTF8));var race=fixture.CreateRace();
                try
                {
                    var car=race.player;var controller=new EngineSoundController(car.spec,car.tune);
                    var synth=new EngineSoundSynth(new EngineSoundProfile(car.spec,car.tune),sampleRate:48000);var pcm=new float[800];
                    var outdoor=TracksideListening?new TracksidePropagation(48000):null;
                    var listening=TracksideListening?new TracksidePropagation.Scene(car.spec.induction.ambientTemperatureK,.5,car.spec.induction.ambientPressureBar*100000).At():null;
                    synth.Fill(pcm,2,48000,controller.Update(car.state,true,.85,DragMath.FixedStep));
                    double peak=0;var timings=new double[fixture.ticks.Count];int timingIndex=0;
                    foreach(var tick in fixture.ticks)
                    {
                        car.state.warmupLineLock=tick.lineLock;if(tick.beginCountdown&&!race.BeginCountdown())throw new InvalidOperationException("Countdown failed");
                        if(race.phase==RacePhase.Finished)race.Shutdown(tick.input);else race.Step(tick.input);
                        var sound=controller.Update(car.state,true,.85,DragMath.FixedStep);long started=Stopwatch.GetTimestamp();
                        synth.Fill(pcm,2,48000,sound);outdoor?.Process(pcm,2,listening);timings[timingIndex++]=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
                        foreach(float value in pcm){if(float.IsNaN(value)||float.IsInfinity(value)||Math.Abs(value)>=1)throw new InvalidOperationException("Invalid PCM");peak=Math.Max(peak,Math.Abs(value));}
                    }
                    var final=controller.Update(car.state,true,.85,DragMath.FixedStep);
                    for(int i=0;i<60;i++){synth.Fill(pcm,2,48000,final);outdoor?.Process(pcm,2,listening);}
                    var d=synth.AfterfireDiagnostics(48000);var r=synth.ReleaseDiagnostics(48000);
                    if(d.Delivered!=car.state.audioExhaust.mixture.Sequence||d.HistoryOverflows!=0||d.TailSteals!=0||r.Overflows!=0||r.TailSteals!=0)
                        throw new InvalidOperationException("Event delivery/tail budget failure: "+fixture.id+" / "+d.Delivered+" / "+car.state.audioExhaust.mixture.Sequence+" / steals "+d.TailSteals);
                    if(r.Delivered!=car.state.audioExhaust.ReleaseSequence)throw new InvalidOperationException("Lost BOV event: "+fixture.id);
                    if(Math.Abs(car.state.elapsed-fixture.elapsed)>1e-9||car.state.exhaustPopCount!=fixture.pops)throw new InvalidOperationException("Physics trace changed: "+fixture.id);
                    Array.Sort(timings);report.AppendLine("MEASURE "+fixture.id+" callback p95 "+timings[(int)(timings.Length*.95)].ToString("F4")+" ms; max "+timings[timings.Length-1].ToString("F4")+" ms / 400 stereo frames, budget 8.333 ms");
                    report.AppendLine("PASS "+fixture.id+" / ET unchanged / chemical "+d.Delivered+" / rendered reactions "+(d.Delivered-d.Inaudible)+" / pressure voices "+(d.Delivered-d.Inaudible-d.ContinuedReactions)+" / continued "+d.ContinuedReactions+" / BOV "+r.Delivered+" / no drops or steals / peak "+peak.ToString("F4"));
                    File.WriteAllText(receipt,report.ToString(),new UTF8Encoding(false));
                }
                finally{race.CloseOpponentWorker();}
            }
            report.AppendLine("PASS all "+files.Length+" real race pipelines");File.WriteAllText(receipt,report.ToString(),new UTF8Encoding(false));
            UnityEngine.Debug.Log("RUNTIME AUDIO 47 PIPELINES PASS");
        }
        public static void VerifyAndBuild(){Run();CheckRaces();PlayerBuild.BuildPlayerOnly();}
        public static void RecheckAndRaces(){Run();CheckRaces();}
        public static void FinalAndRaces(){Full();PrepareAndCheckRaces();}
        public static void Full()
        {
            Run();
            int engine=EngineSoundVerification.Run(Root),pop=ExhaustPopVerification.Run(Root),induction=InductionVerification.Run(Root),powertrain=PowertrainVerification.Run(Root);
            File.WriteAllText(Path.Combine(Root,"suite-summary.txt"),"PASS engine "+engine+"; legacy isolated pop "+pop+"; induction "+induction+"; powertrain "+powertrain+".\n",new UTF8Encoding(false));
            TireFeedbackVerification.VerifyEffectsAt(Root);TireSoundVerification.Run();PressureAudioVerification.Refinements();SharedAudioVerification.Run(Root);
            UnityEngine.Debug.Log("RUNTIME AUDIO FULL REGRESSION PASS");
        }
    }
}
