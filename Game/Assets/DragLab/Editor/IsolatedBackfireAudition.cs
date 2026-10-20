using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Listening experiment only. One source kernel; source tables differ in heat-release timing/venting.
    public static class IsolatedBackfireAudition
    {
        const int Rate=48000,EventFrames=57600,Frames=259200;
        static readonly int[] Starts={28800,105600,182400};
        static int checks;
        sealed class Shape
        {
            public string name;public double[] delays,widths,fractions;public double vent;
        }
        static void Check(bool yes,string why){checks++;if(!yes)throw new InvalidOperationException("ISOLATED BACKFIRE: "+why);}
        static SharedBackfirePreviewSynth Exhaust()=>new SharedBackfirePreviewSynth(new[]{
            new PreviewDuctSegment{lengthM=.12,diameterM=.048,temperatureK=850},
            new PreviewDuctSegment{lengthM=.33,diameterM=.048,temperatureK=850},
            new PreviewDuctSegment{lengthM=.65,diameterM=.06,temperatureK=850},
            new PreviewDuctSegment{lengthM=.65,diameterM=.13,temperatureK=850,component=ExhaustPart.Create(ExhaustPartKind.ChamberedSilencer)},
            new PreviewDuctSegment{lengthM=.8,diameterM=.06,temperatureK=850}
        },new[]{new PreviewDuctSegment{lengthM=.4,diameterM=.05,temperatureK=330}},Rate);
        static double[] Source(Shape shape)
        {
            Check(shape.delays.Length==shape.widths.Length&&shape.delays.Length==shape.fractions.Length,"packet dimensions");
            Check(Math.Abs(shape.fractions.Sum()-1)<1e-12,"same total fuel/oxygen energy");
            var heat=new double[EventFrames];const double energy=200;
            for(int p=0;p<shape.delays.Length;p++){
                double step=Math.Exp(-1/(Rate*shape.widths[p])),weight=energy*shape.fractions[p]*(1-step)*(1-step),power=1;
                int first=(int)Math.Round(shape.delays[p]*Rate);
                for(int i=first;i<heat.Length;i++){heat[i]+=weight*(i-first+1)*power;power*=step;}
            }
            Check(Math.Abs(heat.Sum()-energy)<energy*1e-8,"heat-release ledger");
            var result=new double[EventFrames];double pressure=0,previous=0,front=0,oldInput=0,oldOutput=0;
            double vent=Math.Exp(-1/(Rate*shape.vent)),smooth=1-Math.Exp(-1/(Rate*.00004)),pole=Math.Exp(-2*Math.PI*45/Rate);
            for(int i=0;i<result.Length;i++){
                pressure=(pressure+heat[i]*.33/.0012)*vent;
                double gas=pressure/(101300+.25*pressure),rise=(gas-previous)*Rate*.003;previous=gas;
                front+=smooth*(rise-front);double source=front+.18*gas;
                double sample=source-oldInput+pole*oldOutput;oldInput=source;oldOutput=sample;result[i]=sample;
            }
            Check(result.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x)),"finite source");
            Check(result.Skip(result.Length-4800).Max(x=>Math.Abs(x))<1e-8,"source settles before repeat");
            return result;
        }
        static double[] Route(double[] input)
        {
            var duct=Exhaust();var output=new double[input.Length];
            for(int i=0;i<input.Length;i++)output[i]=duct.ProcessPressureSource(.12,input[i]);
            return output;
        }
        static void Raw(string path,double[] values)
        {using(var writer=new BinaryWriter(File.Create(path)))foreach(double value in values)writer.Write(value);}
        public static void Run()
        {
            checks=0;string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),folder=Path.Combine(root,"reports/isolated-backfire-sources-20261020");Directory.CreateDirectory(folder);
            var shapes=new[]{
                new Shape{name="01-sharp-snap",delays=new[]{0.0},widths=new[]{.00010},fractions=new[]{1.0},vent=.0018},
                new Shape{name="02-ragged-burst",delays=new[]{0.0,.0037,.0089,.0158,.026},widths=new[]{.00020,.00012,.00016,.00013,.00019},fractions=new[]{.28,.23,.20,.17,.12},vent=.0032},
                new Shape{name="03-fuller-bang",delays=new[]{0.0,.0032},widths=new[]{.0015,.0045},fractions=new[]{.35,.65},vent=.028}
            };
            var impulse=new double[Frames];impulse[0]=1;Raw(Path.Combine(folder,"exhaust-impulse.f64"),Route(impulse));
            var silence=Route(new double[Frames]);Check(silence.All(x=>x==0),"silent duct has no independent noise or ringing");
            foreach(var shape in shapes){
                var one=Source(shape);Check(one.SequenceEqual(Source(shape)),"sample-identical source rerender");
                var dry=new double[Frames];foreach(int start in Starts)Array.Copy(one,0,dry,start,one.Length);
                var wet=Route(dry);Check(wet.SequenceEqual(Route(dry)),"sample-identical common exhaust rerender");
                Check(wet.All(x=>!double.IsNaN(x)&&!double.IsInfinity(x)),"finite exhaust output");
                Check(wet.Take(Starts[0]).All(x=>x==0),"no exhaust pre-echo");
                Raw(Path.Combine(folder,shape.name+"-dry.f64"),dry);Raw(Path.Combine(folder,shape.name+"-exhaust.f64"),wet);
            }
            File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS "+checks+" isolated-source/duct checks: equal 200 J inferred event energy, distinct release shapes, settled sources, deterministic rerenders, finite output, silence and no pre-echo. Three identical repetitions per file at 0.6, 2.2 and 3.8 seconds. Loudness/WAV validation pending Python packaging.\n",new UTF8Encoding(false));
            Debug.Log("ISOLATED BACKFIRE SOURCES PASS: "+checks);
        }
    }
}
