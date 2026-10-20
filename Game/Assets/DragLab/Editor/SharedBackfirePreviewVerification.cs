using System;
using System.Diagnostics;
using System.Linq;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class SharedBackfirePreviewVerification
    {
        static int checks;
        public static string Summary {get;private set;}
        static void Check(bool yes,string message){checks++;if(!yes)throw new InvalidOperationException("SHARED SOURCE CHECK: "+message);}
        static PreviewCombustionEvent Event()=>new PreviewCombustionEvent{
            fuelMg=20,oxygenMg=80,heatingJPerKg=44e6,oxygenPerFuel=3.43,burnEfficiency=.7,burnSeconds=.025,mixingSeconds=.07,
            volumeLitres=3,temperatureK=650,pressureKPa=101.3,sourceMetres=.1,ignited=true,seed=171818
        };
        static PreviewDuctSegment[] Path(double length=1,double temperature=650,ExhaustPart part=null,bool turbine=false)=>new[]{
            new PreviewDuctSegment{lengthM=.4*length,diameterM=.06,temperatureK=temperature},
            new PreviewDuctSegment{lengthM=.3*length,diameterM=.08,temperatureK=temperature,turbine=turbine},
            new PreviewDuctSegment{lengthM=.6*length,diameterM=.10,temperatureK=temperature,component=part}
        };
        static SharedBackfirePreviewSynth Synth(int rate=48000,double length=1,double temp=650,ExhaustPart part=null,bool turbine=false,PreviewCombustionVoicing voicing=null)
        {return new SharedBackfirePreviewSynth(Path(length,temp,part,turbine),Path(.4,330),rate,voicing:voicing);}
        static double[] Render(PreviewCombustionEvent input,int rate=48000,double length=1,double temp=650,ExhaustPart part=null,bool turbine=false,double boost=.7,PreviewCombustionVoicing voicing=null)
        {
            var synth=Synth(rate,length,temp,part,turbine,voicing);synth.SetBoost(boost);synth.Trigger(input);
            var output=new double[rate*2];for(int i=0;i<output.Length;i++)output[i]=synth.Sample();return output;
        }
        static double Energy(double[] values)=>values.Sum(x=>x*x);
        static double Brightness(double[] values,int rate)
        {
            double low=0,energy=0,total=0,coefficient=1-Math.Exp(-2*Math.PI*2000/rate);
            foreach(double v in values){low+=coefficient*(v-low);energy+=(v-low)*(v-low);total+=v*v;}
            return energy/Math.Max(1e-20,total);
        }
        static int Arrival(double[] values)=>Array.FindIndex(values,x=>Math.Abs(x)>1e-10);
        static void Throws(Action action,string label){bool threw=false;try{action();}catch(ArgumentException){threw=true;}Check(threw,label);}
        public static void RunChecks()
        {
            checks=0;var input=Event();var baseline=Render(input);
            var roundTrip=UnityEngine.JsonUtility.FromJson<PreviewCombustionEvent>(UnityEngine.JsonUtility.ToJson(input));
            Check(baseline.SequenceEqual(Render(roundTrip)),"combustion packet JSON retains every rendering input");
            Check(baseline.SequenceEqual(Render(input)),"same parameters and seed are sample-identical");
            Check(baseline.SequenceEqual(Render(input,voicing:new PreviewCombustionVoicing())),"explicit default calibration retains original shared source");
            var calibration=new PreviewCombustionVoicing{pressureCoupling=4,turbulenceCoupling=.12,frontCoupling=1.2};
            var pressureVersion=Render(input,voicing:calibration);
            Check(!baseline.SequenceEqual(pressureVersion)&&pressureVersion.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x)),"pressure/front calibration changes character with finite output");
            Check(pressureVersion.SequenceEqual(Render(input,voicing:calibration)),"pressure-front version remains deterministic");
            var thermal=new PreviewCombustionVoicing{heatReleaseCoupling=1,pressureCoupling=2,turbulenceCoupling=.1,frontCoupling=1,structureCoupling=.15};
            var coupled=Synth(voicing:thermal);coupled.Trigger(input);
            for(int i=0;i<96000;i++){double value=coupled.Sample();if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("Non-finite thermal source");}
            Check(Math.Abs(coupled.ReleasedHeatJ-coupled.AcceptedHeatJ)<input.EnergyJ*1e-7,"thermal heat release integrates to fuel/oxygen-limited energy");
            Check(coupled.PressureSquaredSamples>0&&coupled.TurbulenceSquaredSamples>0&&coupled.StructureSquaredSamples>0,"pressure, discharge turbulence and damped body response share combustion excitation");
            var inert=Synth(voicing:thermal);var noOxygen=input;noOxygen.oxygenMg=0;inert.Trigger(noOxygen);
            for(int i=0;i<2000;i++)inert.Sample();
            Check(inert.ReleasedHeatJ==0&&inert.PressureSquaredSamples==0&&inert.StructureSquaredSamples==0,"no oxygen suppresses heat and every coupled source component");
            var sharp=thermal.Copy();sharp.burnTimeScale=.1;sharp.frontMilliseconds=.08;sharp.reflectionDecaySeconds=.24;sharp.reflectionMix=.5;sharp.turbulenceCoupling=.02;
            var quick=input;quick.burnSeconds=.0012;quick.mixingSeconds=.006;quick.volumeLitres=.6;
            var dull=Render(quick,voicing:thermal);var bright=Render(quick,voicing:sharp);
            Check(Brightness(bright,48000)>Brightness(dull,48000)*1.5,"faster heat release and unsmoothed front increase relative energy above 2 kHz");
            var noBloom=sharp.Copy();noBloom.reflectionMix=0;var direct=Render(quick,voicing:noBloom);
            double tail=Energy(bright.Skip(4800).Take(9600).ToArray()),oldTail=Energy(direct.Skip(4800).Take(9600).ToArray());
            Check(tail>oldTail*1.5,"pressure-fed reflections retain more 100-300 ms decay than direct source");
            Check(bright.SequenceEqual(Render(quick,voicing:sharp)),"sharp attack and reflection tail remain sample-deterministic");
            Check(Arrival(bright)==Arrival(direct),"longer decay does not delay or pre-echo the direct bang");
            Check(Energy(Render(noOxygen,voicing:sharp))==0,"reflection network cannot generate sound without combustion excitation");
            var changed=input;changed.seed++;Check(!baseline.SequenceEqual(Render(changed)),"subtle turbulence varies with explicit seed");
            Check(baseline.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x)&&Math.Abs(x)<5),"bounded finite raw source/routing");
            changed=input;changed.fuelMg=0;Check(Energy(Render(changed))==0,"fuel cut produces no explosion");
            changed=input;changed.oxygenMg=0;Check(Energy(Render(changed))==0,"oxygen absence produces no explosion");
            changed=input;changed.ignited=false;Check(Energy(Render(changed))==0,"unignited input produces no explosion");
            changed=input;changed.oxygenMg=input.oxygenPerFuel*input.fuelMg*.25;Check(Math.Abs(changed.EnergyJ/input.EnergyJ-.25)<1e-10,"oxygen stoichiometrically limits burnable mass");
            Check(Energy(Render(changed))<Energy(baseline),"lower combustible energy reduces acoustic output");
            changed=input;changed.sourceMetres=1.29;var downstream=Render(changed);Check(Arrival(downstream)<Arrival(baseline),"downstream source arrives sooner");
            Check(Arrival(Render(input,length:1.5))>Arrival(baseline),"longer geometry increases flight time");
            Check(Arrival(Render(input,temp:1000))<Arrival(Render(input,temp:400)),"hotter propagation path changes flight time");
            var wide=Path();foreach(var section in wide)section.diameterM*=1.4;
            var wideSynth=new SharedBackfirePreviewSynth(wide,Path(.4,330),48000);wideSynth.Trigger(input);
            var wideOutput=new double[baseline.Length];for(int i=0;i<wideOutput.Length;i++)wideOutput[i]=wideSynth.Sample();
            Check(!baseline.SequenceEqual(wideOutput),"absolute pipe/outlet diameter affects radiation as well as impedance ratios");
            var absorptive=ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer);
            Check(Energy(Render(input,part:absorptive))<Energy(baseline),"upstream combustion passes through fitted silencer");
            var after=Render(changed,part:absorptive);
            Check(downstream.Take(40).SequenceEqual(after.Take(40)),"downstream first arrival bypasses upstream silencer; later reflections may differ");
            Check(!after.SequenceEqual(downstream),"upstream hardware still contributes reflected response");
            Check(Energy(Render(input,turbine:true))<Energy(baseline),"upstream source crosses turbine damping");
            Check(downstream.Take(40).SequenceEqual(Render(changed,turbine:true).Take(40)),"downstream first arrival bypasses upstream turbine");
            changed=input;changed.intake=true;changed.sourceMetres=.1;Check(!baseline.SequenceEqual(Render(changed)),"intake fault uses intake route with same pressure algorithm");
            changed=input;changed.burnSeconds=.075;changed.mixingSeconds=.18;var longBurn=Render(changed);
            Check(Energy(longBurn.Skip(12000).Take(12000).ToArray())>Energy(baseline.Skip(12000).Take(12000).ToArray()),"longer combustion/mixing produces sustained late energy");
            foreach(int rate in new[]{22050,44100,48000,96000}){
                var values=Render(input,rate);Check(Energy(values)>1e-5&&values.All(x=>!double.IsNaN(x)&&Math.Abs(x)<5),"prepared finite output at "+rate);
            }
            changed=input;changed.fuelMg=double.NaN;Throws(()=>Synth().Trigger(changed),"NaN input rejected");
            changed=input;changed.sourceMetres=99;Throws(()=>Synth().Trigger(changed),"source outside path rejected");
            changed=input;changed.oxygenPerFuel=0;Throws(()=>Synth().Trigger(changed),"invalid fuel chemistry rejected");
            var capacity=Synth();for(int i=0;i<48;i++)Check(capacity.Trigger(input),"prepared simultaneous voice "+i);
            Check(!capacity.Trigger(input)&&capacity.RejectedOverlap==1,"capacity failure reported without overwriting active tails");
            var live=Synth(voicing:sharp);live.Trigger(input);for(int i=0;i<1000;i++)live.Sample();
            long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            // Stopwatch is allocated outside the allocation measurement below.
            allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<48000;i++)live.Sample();
            long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;watch.Stop();
            Check(bytes==0,"sample callback uses no managed allocation");
            Check(live.RejectedOverlap==0,"single-event test loses no voice");
            Summary="PASS "+checks+" shared generator/routing checks; deterministic seeded pressure/turbulence, fuel/oxygen suppression, source-path routing, geometry/temperature/turbine effects, overlap, rates and zero sample-loop allocations. Desktop single-event 1 s loop: "+watch.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms; not Android profiling.\n";
        }
    }
}
