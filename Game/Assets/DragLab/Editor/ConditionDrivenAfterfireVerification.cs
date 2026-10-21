using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class ConditionDrivenAfterfireVerification
    {
        public static string OutputDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/condition-afterfire-20261021"));
        static string Folder=>OutputDirectory;
        static readonly StringBuilder report=new StringBuilder();
        static int checks;
        static void Check(bool ok,string label)
        {
            report.AppendLine((ok?"PASS ":"FAIL ")+label);checks++;
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"causality.txt"),report+"Checks: "+checks+"\n",new UTF8Encoding(false));
            if(!ok)throw new InvalidOperationException(label);
        }
        static ExhaustMixtureModel.Input Feed() => new ExhaustMixtureModel.Input { dt=1.0/120,fuelKg=3e-6,airKg=5e-5,chamberBurnFraction=.1,intakeK=350,wallTargetK=1000,rpm=4200 };
        public static void Run()
        {
            checks=0;report.Clear();
            var spec=new VehicleSpec();var state=new CarState {rpm=5000,combustionAdvanceDegrees=25,engineSupplyAvailable=true};
            var early=CombustionExhaustBoundary.Evaluate(spec,state,3e-6,5e-5,350);
            state.antiLagActive=state.launchControlActive=state.limiter=true;state.exhaustEnergy=1;
            var flags=CombustionExhaustBoundary.Evaluate(spec,state,3e-6,5e-5,350);
            Check(early.burnFraction==flags.burnFraction&&early.flameFraction==flags.flameFraction&&early.outletK==flags.outletK,"mode labels/energy request alone cannot alter the cylinder boundary");
            state.combustionAdvanceDegrees=-40;
            var late=CombustionExhaustBoundary.Evaluate(spec,state,3e-6,5e-5,350);
            Check(late.burnFraction<early.burnFraction&&late.flameFraction>early.flameFraction,"actual retarded ignition leaves more burning charge at valve opening");
            state.ignitionFiringFraction=0;
            var cut=CombustionExhaustBoundary.Evaluate(spec,state,3e-6,5e-5,350);
            Check(cut.burnFraction==0&&cut.flameFraction==0&&cut.outletK==350,"complete ignition interruption supplies unburned charge without inventing flame");
            state.ignitionFiringFraction=1;
            Check(CombustionExhaustBoundary.Evaluate(spec,state,0,5e-5,350).flameFraction==0,"no delivered fuel means no outgoing flame");
            var profile=new EngineSoundProfile(spec,new Tune());
            foreach(int rate in new[]{44100,48000})
            {
                var a=new ExhaustMixtureModel(3456,"labels",initialK:1000);var b=new ExhaustMixtureModel(3456,"labels",initialK:1000);
                var x=Feed();var y=Feed();y.limiter=y.twoStep=true;y.cause=AfterfireCause.AntiLag;
                var sa=new AfterfireAudioSynth(profile,rate);var sb=new AfterfireAudioSynth(profile,rate);sa.Connect(a);sb.Connect(b);
                bool equal=true,physical=true;int fronts=0;double minDuration=1,maxDuration=0,minPressure=double.MaxValue,maxPressure=0;
                for(int tick=0;tick<240;tick++)
                {
                    // Repeated charge/cut/recovery transitions, with a continuing hot exhaust.
                    x.fuelKg=y.fuelKg=tick%60<45?3e-6:0;
                    a.Step(x);b.Step(y);
                    for(int i=0;i<rate/120;i++)equal&=sa.Sample()==sb.Sample();
                }
                for(int i=1;i<=a.Sequence;i++)
                {
                    a.Read(i,out var e);b.Read(i,out var f);
                    physical&=e.fuelKg==f.fuelKg&&e.time==f.time&&e.pressurePa==f.pressurePa&&e.frontId==f.frontId;
                    physical&=e.frontId>0&&e.pressurePa>0&&e.riseSeconds>0&&e.burnSeconds>=e.riseSeconds&&e.acousticJ>0&&e.acousticJ<e.releasedJ;
                    minDuration=Math.Min(minDuration,e.burnSeconds);maxDuration=Math.Max(maxDuration,e.burnSeconds);
                    minPressure=Math.Min(minPressure,e.pressurePa);maxPressure=Math.Max(maxPressure,e.pressurePa);
                    if(i==1||a.Read(i-1,out var previous)&&previous.frontId!=e.frontId)fronts++;
                }
                Check(a.Sequence>0&&a.Sequence==b.Sequence&&physical,"identical physical history regardless of limiter/two-step/cause labels / "+rate);
                Check(equal,"sample-identical backfire PCM when only mode labels differ / "+rate);
                Check(maxPressure>minPressure*2&&maxDuration>minDuration*1.5,"reaction strength and duration follow changing physical charge / "+rate);
                Check(sa.HistoryOverflows==0&&sa.TailSteals==0,"condition-driven burst/continuous-burn delivery budget / "+rate);
                Check(Math.Abs(a.FuelIn-a.ChamberBurned-a.FuelOut-a.FuelBurned-a.RetainedFuel)<1e-10&&Math.Abs(a.FuelBurned*a.heatingJPerKg-a.ExhaustHeatJ-a.AcousticEnergyJ)<1e-7,"fuel and chemical/acoustic energy ledgers conserved / "+rate);
                report.AppendLine("MEASURE "+a.Sequence+" reactions, front transitions "+fronts+", burn seconds "+minDuration+".."+maxDuration+", pressure Pa "+minPressure+".."+maxPressure);
            }
            var shortEvent=new ExhaustMixtureModel.Event { gasK=900,position=.4,acousticJ=.2,pressurePa=4000,riseSeconds=.0005,burnSeconds=.003 };
            var slowEvent=shortEvent;slowEvent.riseSeconds=.012;slowEvent.burnSeconds=.08;
            var fast=AfterfireAudioSynth.Describe(shortEvent,profile);var slow=AfterfireAudioSynth.Describe(slowEvent,profile);
            Check(slow.seconds>fast.seconds*2&&slow.stretch>fast.stretch&&slow.riseCutoff<fast.riseCutoff,"physical reaction descriptor records rise/burn scales; it no longer defines the preset playback envelope");
            shortEvent.twoStep=shortEvent.limiter=true;shortEvent.cause=AfterfireCause.AntiLag;
            var tagged=AfterfireAudioSynth.Describe(shortEvent,profile);
            Check(tagged.stretch==fast.stretch&&tagged.kind==fast.kind&&tagged.riseCutoff==fast.riseCutoff,"diagnostic reaction descriptor ignores all operating-mode labels");
            var cold=new ExhaustMixtureModel(3456,"cold",initialK:298);var coldInput=Feed();coldInput.inletK=298;coldInput.intakeK=coldInput.wallTargetK=298;
            coldInput.twoStep=coldInput.limiter=true;coldInput.cause=AfterfireCause.AntiLag;
            for(int i=0;i<240;i++)cold.Step(coldInput);
            Check(cold.Sequence==0,"cold unignited mixture cannot backfire even with all mode labels asserted");
            var prelitCold=new ExhaustMixtureModel(3456,"quenched",initialK:298);
            foreach(var zone in prelitCold.zones){zone.flame=1;zone.frontId=99;}
            for(int i=0;i<60;i++)prelitCold.Step(coldInput);
            Check(prelitCold.Sequence==0,"cold walls/gas quench an inherited flame without mode-triggered ignition");
            var trace=new ExhaustMixtureModel(3456,"trace",initialK:1000);var traceInput=Feed();traceInput.fuelKg=1e-8;traceInput.chamberBurnFraction=0;
            foreach(var zone in trace.zones){zone.flame=1;zone.frontId=99;}
            for(int i=0;i<240;i++)trace.Step(traceInput);
            Check(trace.Sequence==0,"inherited flame cannot burn trace fuel below the flammability window");
            var vented=new ExhaustMixtureModel(3456,"vent",initialK:1000);
            foreach(var zone in vented.zones)zone.pressurePa=100000;
            vented.Step(new ExhaustMixtureModel.Input{dt=1.0/120,intakeK=350,wallTargetK=1000});
            Check(vented.zones.All(z=>z.pressurePa>0&&z.pressurePa<100000),"stored pressure vents continuously without fresh heat release");
            Debug.Log("CONDITION AFTERFIRE PASS "+checks);
        }
        public static void Checkpoint()
        {
            Run();RuntimeAudioIntegrationVerification.OutputDirectory=Folder;
            RuntimeAudioIntegrationVerification.Full();RuntimeAudioIntegrationVerification.CheckRaces();
        }
        public static void Build(){PlayerBuild.BuildPlayerOnly();}
        public static void Recheck()
        {
            Run();RuntimeAudioIntegrationVerification.OutputDirectory=Folder;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();
        }
        public static void RecheckAndBuild(){Recheck();Build();}
    }
}
