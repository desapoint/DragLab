using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Presentation;
using DriveType=DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    public static class TireFeedbackVerification
    {
        public static void VerifyEffects()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/tire-feedback"));Directory.CreateDirectory(folder);
            int checks=0;var log=new StringBuilder("Contact-based tire feedback, deterministic DSP, master volume and bounded sprite smoke.\n");
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"effects-verification.txt"),log.ToString(),new UTF8Encoding(false));if(!ok)throw new InvalidOperationException(label);checks++;};
            var state=new CarState{wheelRadiusM=.3,wheelContactInitialized=true,speed=0};
            for(int i=0;i<4;i++){state.contact.omega[i]=100;state.contact.slip[i]=1;state.contact.force[i]=3000;}
            check(TireFeedback.Read(state,0).intensity>.8,"A stationary burnout is audible without a body-speed gate");
            state.contact.force[0]=0;check(TireFeedback.Read(state,0).intensity==0,"An unloaded freely rotating wheel emits no road sound/smoke");state.contact.force[0]=3000;
            state.speed=30;for(int i=0;i<4;i++)state.contact.slip[i]=0;
            check(TireFeedback.Read(state,0).intensity==0,"Pure rolling emits no slip feedback");
            state.contact.omega[0]=0;state.contact.slip[0]=-1;
            check(TireFeedback.Read(state,0).intensity>.8,"A locked braking tire uses the same contact feedback");
            state.speed=0;state.contact.omega[0]=100;state.contact.slip[0]=1;
            foreach(int rate in new[]{44100,48000})foreach(int channels in new[]{1,2}) {
                var frame=TireSoundFrame.From(state,true,1);var pcm=new float[rate*channels];var again=new float[pcm.Length];
                new TireSoundSynth().Fill(pcm,channels,rate,frame);new TireSoundSynth().Fill(again,channels,rate,frame);
                check(pcm.SequenceEqual(again),rate+" Hz / "+channels+" channels repeats sample-exactly");
                check(pcm.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x))&&pcm.Max(x=>Math.Abs(x))<.5&&Rms(pcm)>.008,"Bounded audible rubber friction / "+rate+" / "+channels);
                var quiet=new float[pcm.Length];new TireSoundSynth().Fill(quiet,channels,rate,frame,.25);
                check(Math.Abs(Rms(quiet)/Rms(pcm)-.25)<1e-6,"Master volume applies once / "+rate+" / "+channels);
                new TireSoundSynth().Fill(quiet,channels,rate,frame,0);check(quiet.All(x=>x==0),"Master zero mutes tire DSP exactly");
                var synth=new TireSoundSynth();synth.Fill(quiet,channels,rate,frame);synth.Fill(quiet,channels,rate,null);check(quiet.All(x=>x==0),"Silence/reset clears tire voice");
                if(channels==1&&rate==44100)EngineSoundAudition.WriteWav(Path.Combine(folder,"stationary-burnout.wav"),pcm,rate);
                var rolling=TireSoundFrame.From(new CarState{wheelContactInitialized=true,wheelRadiusM=.3},true,1);
                synth.Fill(quiet,channels,rate,rolling);check(Rms(quiet)<1e-7,"Grip recovery has no persistent scrub layer");
                var changed=new TireSoundSynth();var transition=new float[rate*channels];changed.Fill(quiet,channels,rate,rolling);changed.Fill(transition,channels,rate,frame);
                check(Math.Abs(transition[0])<.001,"Slip onset ramps without a sudden click");
            }
            var acoustic=new CarState{slip=.9,speed=10,rpm=4500};
            check(TireSoundFrame.From(acoustic,true,1).fl.intensity>0,"Acoustic-only bench retains its authored slip slider");
            check(TireSoundFrame.From(acoustic,true,1,DriveType.RWD).fl.intensity==0&&TireSoundFrame.From(acoustic,true,1,DriveType.RWD).rl.intensity>0,"Authored acoustic RWD slip affects the rear axle only");
            var smokeObject=new GameObject("Smoke verification");var smoke=smokeObject.AddComponent<TireSmokeView>();smoke.Initialize(30);smoke.SetContacts(Vector3.right,Vector3.left);
            try {
                state.speed=0;for(int i=0;i<4;i++){state.contact.omega[i]=100;state.contact.slip[i]=1;state.contact.force[i]=3000;}
                for(int i=0;i<500;i++)smoke.Step(state,1f/60);
                check(smoke.ActiveCount>0&&smoke.ActiveCount<=96&&smokeObject.transform.childCount==96,"Sustained four-wheel smoke reuses a fixed pool");
                for(int i=0;i<4;i++){state.contact.slip[i]=0;state.contact.omega[i]=0;}for(int i=0;i<120;i++)smoke.Step(state,1f/60);
                check(smoke.ActiveCount==0,"Smoke fades after slip stops");smoke.Clear();check(smoke.ActiveCount==0&&smoke.FrontEmitted==0&&smoke.RearEmitted==0,"Reset clears particles and emission history");
                for(int i=0;i<120;i++)smoke.Step(acoustic,1f/60,true,DriveType.RWD);
                check(smoke.RearEmitted>0&&smoke.FrontEmitted==0,"Acoustic-only authored slip also previews particles on the selected driven axle");
            } finally {UnityEngine.Object.DestroyImmediate(smokeObject);}
            var definition=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First();var config=new EngineBenchConfig{vehicle=definition.spec.Copy(),tune=definition.CreateStockTune()};
            var a=EngineBenchRenderer.Render(config,EngineBenchScenario.Burnout);var b=EngineBenchRenderer.Render(config,EngineBenchScenario.Burnout);
            check(a.SequenceEqual(b)&&a.All(x=>Math.Abs(x)<1),"Acoustic burnout WAV includes the new layer and repeats exactly with output headroom");
            File.WriteAllText(Path.Combine(folder,"effects-verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));UnityEngine.Debug.Log("TIRE EFFECTS PASS: "+checks);
        }
        static double Rms(float[] pcm){double energy=0;foreach(float sample in pcm)energy+=sample*sample;return Math.Sqrt(energy/pcm.Length);}
        public static void Diagnose()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/tire-feedback"));Directory.CreateDirectory(folder);
            int checks=0;var log=new StringBuilder("Clutch capacity/power audit; no physics changes.\n");
            var matrix=new StringBuilder("car,pedal,clampNm,shaftNm,engineInputRpm,outputRpm,inputPortW,outputPortW,slipLossW\n");
            var perf=new StringBuilder("car,mode,ticks,elapsedMs,allocatedBytes,gc0\n");
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"clutch-audit.txt"),log.ToString(),new UTF8Encoding(false));if(!ok)throw new InvalidOperationException(label);checks++;};
            foreach(var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id)) {
                var tune=definition.CreateStockTune();tune.partsResolved=true;tune.transmissionUpgrade=TransmissionUpgrade.Manual;
                var driver=new DriverSettings{shifting=ShiftMode.ManualClutch,launch=LaunchMode.Manual,clutchAssist=false,tractionAssist=false,throttleAssist=false,wearMode=WearMode.None};
                double last=double.MaxValue;
                foreach(double pedal in new[]{0.0,.55,.75,1.0}) {
                    var car=new CarSimulation(definition.spec,tune,driver);var state=car.state;state.rpm=5000;state.clutch=pedal;state.combustionFraction=1;
                    var hardware=car.mechanics.Definition(car.mechanics.Part(PartCategory.Clutch));
                    double capacity=car.spec.ClutchCapacity*car.tune.CapacityMultiplier*state.clutchCapacityFactor*ClutchHardware.Engagement(hardware,pedal,5000);
                    double output=ClutchDynamics.Transmit(car.spec,car.tune,state,1,3000,true,false,false,1.0/960,car.induction,car.engine,car.wheels,4,1,car.ReflectedWheelInertia(4),hardware);
                    double inputPort=output*(5000+state.rpm)*.5/FlywheelDynamics.RpmPerRadian,outputPort=output*3000/FlywheelDynamics.RpmPerRadian;
                    double oilDrag=pedal>=.98&&hardware.clutchEnvironment==ClutchEnvironment.Wet?5:0;
                    check(Math.Abs(output)<=capacity+oilDrag+1e-6&&output<=last+1e-6,definition.spec.id+" capacity falls with pedal disengagement (bounded wet drag allowed) / "+pedal+" / "+output.ToString("F2")+" Nm");last=output;
                    check(outputPort<=inputPort+1e-6,definition.spec.id+" slower output shaft receives less power than engine-side friction port / "+pedal);
                    matrix.AppendLine(string.Join(",",new[]{definition.spec.id,pedal.ToString(CultureInfo.InvariantCulture),capacity.ToString("F3",CultureInfo.InvariantCulture),output.ToString("F3",CultureInfo.InvariantCulture),"5000","3000",inputPort.ToString("F3",CultureInfo.InvariantCulture),outputPort.ToString("F3",CultureInfo.InvariantCulture),(inputPort-outputPort).ToString("F3",CultureInfo.InvariantCulture)}));
                }
                foreach(bool neutral in new[]{true,false}) {
                    var car=new CarSimulation(definition.spec,tune,driver);car.state.rpm=5000;
                    // Warm allocations/JIT before measurement; this includes real contact and wear preparation.
                    for(int i=0;i<60;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=.6,clutch=.55},neutral,false);
                    long before=GC.GetAllocatedBytesForCurrentThread();int gc=GC.CollectionCount(0);var timer=Stopwatch.StartNew();
                    for(int i=0;i<600;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=.6,clutch=.55},neutral,false);
                    timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                    perf.AppendLine(definition.spec.id+","+(neutral?"neutral":"loaded")+",600,"+timer.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+","+bytes+","+(GC.CollectionCount(0)-gc));
                    if(!neutral)check(car.state.clutchFrictionPowerW>=0&&car.state.contact.Validate()==null,definition.spec.id+" full shared coupling publishes finite contact and nonnegative dissipated power");
                }
                var spinning=new CarSimulation(definition.spec,tune,driver);spinning.state.rpm=5000;spinning.state.speed=10;spinning.state.gear=1;spinning.state.launched=true;spinning.state.clutch=1;spinning.state.wheelContactInitialized=true;spinning.state.contact.speed=10;
                for(int wheel=0;wheel<4;wheel++)spinning.state.contact.omega[wheel]=30/spinning.wheels.radius;
                double beforeSpin=spinning.state.contact.omega.Max();spinning.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=1,clutch=1},false,false);
                check(Math.Abs(spinning.state.transmittedTorqueNm)<=5.001,definition.spec.id+" open clutch sends only bounded wet drag to independent wheels");
                check(spinning.state.contact.omega.Max()>spinning.state.speed/spinning.wheels.radius&&spinning.state.contact.omega.Max()<beforeSpin,definition.spec.id+" stored wheel momentum survives open clutch while friction slows it");
                var coupled=new CarSimulation(definition.spec,tune,driver);var partial=new CarSimulation(definition.spec,tune,driver);
                foreach(var car in new[]{coupled,partial}) {
                    car.tune.frontLeftGrip=car.tune.frontRightGrip=car.tune.rearLeftGrip=car.tune.rearRightGrip=.15f;
                    double ratio=car.spec.gears[1]*car.tune.finalDrive;
                    car.state.rpm=6000;car.state.gear=2;car.state.launched=true;car.state.speed=2500/FlywheelDynamics.RpmPerRadian/ratio*car.wheels.radius;car.state.contact.speed=car.state.speed;car.state.wheelContactInitialized=true;
                    for(int w=0;w<4;w++)car.state.contact.omega[w]=car.state.speed/car.wheels.radius;
                }
                partial.state.clutch=.75;
                coupled.Step(DragMath.FixedStep,new DriverInput{throttle=1});partial.Step(DragMath.FixedStep,new DriverInput{throttle=1,clutch=.75});
                check(partial.state.transmittedTorqueNm<coupled.state.transmittedTorqueNm,definition.spec.id+" partial clamp limits actual road-tick torque / full "+coupled.state.transmittedTorqueNm.ToString("F2")+" / partial "+partial.state.transmittedTorqueNm.ToString("F2"));
                check(partial.state.contact.slipWorkJ.Sum()<coupled.state.contact.slipWorkJ.Sum(),definition.spec.id+" reduced clutch torque reduces new tire-slip work under matched low-grip initial conditions");
                check(partial.state.clutchFrictionPowerW>0&&partial.state.rpm>partial.GearboxInputRpm,definition.spec.id+" actual partial coupling dissipates positive power with signed engine/shaft mismatch");
            }
            File.WriteAllText(Path.Combine(folder,"clutch-power-matrix.csv"),matrix.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"physics-allocation-matrix.csv"),perf.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"clutch-audit.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            UnityEngine.Debug.Log("TIRE FEEDBACK DIAGNOSTICS PASS: "+checks);
        }
    }
}
