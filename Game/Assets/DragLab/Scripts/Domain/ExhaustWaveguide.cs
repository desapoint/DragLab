using System;
using System.Collections.Generic;

namespace DragLab.Domain
{
    public enum AcousticModelMode { Approximate, Simulated }

    [Serializable]
    public sealed class ExhaustWaveSpec
    {
        // Version zero/null means derive both models and geometry from fitted hardware.
        // Version one is an explicit configuration, including Approximate + Approximate.
        public int version;
        public AcousticModelMode pipeMode, mufflerMode;
        public float headerLengthM = .75f, collectorLengthM = .55f, tailLengthM = 1.15f;
        public float headerDiameterMm = 40, collectorDiameterMm = 58, tailDiameterMm = 60;
        public float wallLoss = .15f, reflectionStrength = .82f, gasTemperatureK, outputGain = 1;
        public float chamberLengthM = .55f, expansionRatio = 3, packing = .45f;
        public float resonatorHz = 180, resonatorQ = 2, resonatorDepth = .48f;
        public ExhaustWaveSpec Copy() => (ExhaustWaveSpec)MemberwiseClone();
        public static ExhaustWaveSpec Approximate() => new ExhaustWaveSpec { version = 1 };
        public string Validate()
        {
            if (version == 0) return null;
            if (version != 1 || !Enum.IsDefined(typeof(AcousticModelMode), pipeMode) || !Enum.IsDefined(typeof(AcousticModelMode), mufflerMode)) return "Invalid exhaust acoustic model/version.";
            if (!Range(headerLengthM,.1,3) || !Range(collectorLengthM,.05,3) || !Range(tailLengthM,.05,5) || !Range(headerDiameterMm,15,100) || !Range(collectorDiameterMm,20,130) || !Range(tailDiameterMm,18,130)) return "Invalid acoustic pipe geometry.";
            if (!Range(wallLoss,0,.85) || !Range(reflectionStrength,0,1) || !Range(outputGain,0,3) || gasTemperatureK != 0 && !Range(gasTemperatureK,300,1200)) return "Invalid acoustic pipe losses or temperature.";
            if (!Range(chamberLengthM,.1,1.8) || !Range(expansionRatio,1,10) || !Range(packing,0,1) || !Range(resonatorHz,45,900) || !Range(resonatorQ,.4,12) || !Range(resonatorDepth,0,.95)) return "Invalid acoustic muffler geometry.";
            return null;
        }
        static bool Range(float v,double lo,double hi) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= lo - 1e-7 && v <= hi + 1e-7;
        public static ExhaustWaveSpec Resolve(ExhaustWaveSpec source, ExhaustSetup fitted, double litres, bool turbo)
        {
            if (source != null && source.version != 0) { string error = source.Validate(); if (error != null) throw new ArgumentException(error); return source.Copy(); }
            double chamber = 0, packing = 0, expansion = 0, hz = 0, amount = 0, other = 0;
            foreach (var part in fitted.parts)
            {
                if (part.kind == ExhaustPartKind.AbsorptiveSilencer || part.kind == ExhaustPartKind.ChamberedSilencer)
                {
                    chamber += RuntimeExhaustState.PartLength(part) * part.amount; amount += part.amount;
                    bool absorptive = part.kind == ExhaustPartKind.AbsorptiveSilencer;
                    packing += (absorptive ? .65 : .12) * part.amount;
                    expansion += (absorptive ? 1.4 : 3) * part.amount; hz += part.resonanceHz * part.amount;
                }
                else other += RuntimeExhaustState.PartLength(part) * part.amount;
            }
            double diameter = 54 * Math.Pow(litres / 2, .18);
            return new ExhaustWaveSpec {
                version = 1, pipeMode = AcousticModelMode.Simulated, mufflerMode = AcousticModelMode.Simulated,
                headerLengthM = (float)RuntimeExhaustState.HeaderLengthM,
                collectorLengthM = (float)(.35 + (turbo ? RuntimeExhaustState.TurbineLengthM : 0)), tailLengthM = (float)Math.Min(5,.45 + other),
                headerDiameterMm = (float)Math.Max(15,Math.Min(100,diameter)), collectorDiameterMm = (float)Math.Max(20,Math.Min(130,diameter * 1.15)), tailDiameterMm = (float)Math.Max(18,Math.Min(130,diameter)),
                chamberLengthM = (float)Math.Max(.1,Math.Min(1.8,chamber)), packing = (float)(amount > 0 ? packing / amount : 0),
                // One explicit digital pressure-unit calibration for all hardware,
                // not per-car/output normalization or additional spectral processing.
                outputGain = 2.5f,
                expansionRatio = (float)(amount > 0 ? 1 + (expansion / amount - 1) * Math.Min(1,amount) : 1),
                resonatorHz = (float)Math.Max(45,Math.Min(900,amount > 0 ? hz / amount : 180))
            };
        }
    }

    // Port of the supplied Phase 3.5 linearized traveling-wave / muffler model.
    // Geometry and coefficients are prepared before playback; Sample allocates nothing.
    // These acoustic pressure waves do not replace the authoritative gas/torque solver.
    public sealed class ExhaustWaveguide
    {
        static double Clamp(double v,double lo,double hi) => Math.Max(lo,Math.Min(hi,v));
        sealed class Delay
        {
            readonly double[] values; readonly int mask; int cursor;
            public Delay(double length) { int size=1; while(size<length+2)size<<=1; values=new double[size]; mask=size-1; }
            public void Push(double v) { values[cursor]=v; cursor=(cursor+1)&mask; }
            public double Tap(double samples) { double f=Math.Max(0,samples); int i=(int)f; double a=values[(cursor-1-i)&mask],b=values[(cursor-2-i)&mask]; return a+(b-a)*(f-i); }
            public void Clear() { Array.Clear(values,0,values.Length); cursor=0; }
            public long Bytes => values.Length * 8L;
        }
        sealed class LowPass
        {
            readonly double alpha; double y;
            public LowPass(int rate,double hz) { alpha=1-Math.Exp(-2*Math.PI*Math.Min(hz,rate*.42)/rate); }
            public double Sample(double x) { y+=alpha*(x-y); if(Math.Abs(y)<1e-24)y=0; return y; }
            public void Clear() { y=0; }
        }
        sealed class Band
        {
            readonly double b,a1,a2; double z1,z2;
            public Band(int rate,double hz,double q) { double w=2*Math.PI*Math.Min(hz,rate*.42)/rate,a=Math.Sin(w)/(2*q),norm=1+a; b=a/norm; a1=-2*Math.Cos(w)/norm; a2=(1-a)/norm; }
            public double Sample(double x) { double y=b*x+z1; z1=-a1*y+z2; z2=-b*x-a2*y; if(Math.Abs(z1)+Math.Abs(z2)<1e-24)z1=z2=0; return y; }
            public void Clear() { z1=z2=0; }
        }
        sealed class Section
        {
            public readonly Delay forward, reverse;
            public readonly LowPass forwardLow, reverseLow;
            public readonly double length, gain;
            public double delay;
            public Section(int rate,double length,double loss)
            {
                this.length=length; forward=new Delay(length/Math.Sqrt(1.33*287*300)*rate+12); reverse=new Delay(length/Math.Sqrt(1.33*287*300)*rate+12);
                gain=Math.Exp(-(.08+loss*1.45)*length); double hz=Math.Min(rate*.4,13000/(1+loss*length*2.5));
                forwardLow=new LowPass(rate,hz); reverseLow=new LowPass(rate,hz);
            }
            public void Clear() { forward.Clear(); reverse.Clear(); forwardLow.Clear(); reverseLow.Clear(); }
        }
        sealed class Path
        {
            readonly Section[] sections;
            readonly double[] impedance, junctionReflection, forwardIn, reverseIn, forwardOut, reverseOut;
            readonly int rate; readonly double sourceReflection,endReflection,outputGain;
            public double SoundSpeed {get;private set;}
            public double Reflected {get;private set;}
            public double HeaderDelay => sections[0].delay;
            public long Bytes {get;private set;}
            public Path(int rate,ExhaustWaveSpec p,bool chamber)
            {
                this.rate=rate; int n=chamber?4:3;
                double[] lengths=chamber?new double[]{p.headerLengthM,p.collectorLengthM,p.chamberLengthM,p.tailLengthM}:new double[]{p.headerLengthM,p.collectorLengthM,p.tailLengthM};
                double[] diameters=chamber?new double[]{p.headerDiameterMm,p.collectorDiameterMm,p.tailDiameterMm*Math.Sqrt(p.expansionRatio),p.tailDiameterMm}:new double[]{p.headerDiameterMm,p.collectorDiameterMm,p.tailDiameterMm};
                sections=new Section[n]; impedance=new double[n]; junctionReflection=new double[n-1];
                forwardIn=new double[n]; reverseIn=new double[n]; forwardOut=new double[n]; reverseOut=new double[n];
                sourceReflection=.18*p.reflectionStrength; endReflection=-.76*p.reflectionStrength; outputGain=p.outputGain*.36;
                for(int i=0;i<n;i++){ sections[i]=new Section(rate,lengths[i],p.wallLoss); impedance[i]=1/(diameters[i]*diameters[i]); Bytes+=sections[i].forward.Bytes+sections[i].reverse.Bytes; }
                for(int i=0;i<n-1;i++)junctionReflection[i]=Clamp((impedance[i+1]-impedance[i])/(impedance[i+1]+impedance[i]),-.96,.96)*p.reflectionStrength;
                Temperature(p.gasTemperatureK>0?p.gasTemperatureK:650);
            }
            public void Temperature(double kelvin) { SoundSpeed=Math.Sqrt(1.33*287*Clamp(kelvin,300,1200)); foreach(var section in sections)section.delay=section.length/SoundSpeed*rate; }
            public double Sample(double engine,double pop,double position)
            {
                int n=sections.Length;
                for(int i=0;i<n;i++){var s=sections[i];forwardIn[i]=s.forwardLow.Sample(s.forward.Tap(s.delay))*s.gain;reverseIn[i]=s.reverseLow.Sample(s.reverse.Tap(s.delay))*s.gain;}
                double pos=Clamp(position,0,1);int junction=pos<.34?0:pos<.76?Math.Min(1,n-2):n-2;
                forwardOut[0]=engine+sourceReflection*reverseIn[0]+(junction==0?pop*.5:0);
                for(int i=0;i<n-1;i++){
                    double r=junctionReflection[i],forward=forwardIn[i],back=reverseIn[i+1];
                    reverseOut[i]=r*forward+(1-r)*back;forwardOut[i+1]=(1+r)*forward-r*back;
                    if(junction==i){forwardOut[i+1]+=pop*.5;reverseOut[i]+=pop*.5;}
                }
                reverseOut[n-1]=endReflection*forwardIn[n-1];Reflected=reverseIn[0];
                double result=((1-endReflection)*forwardIn[n-1]+(pos>=.76?pop*.33:0))*outputGain;
                for(int i=0;i<n;i++){sections[i].forward.Push(Clamp(forwardOut[i],-12,12));sections[i].reverse.Push(Clamp(reverseOut[i],-12,12));}
                return result;
            }
            public void Clear() { foreach(var s in sections)s.Clear(); Array.Clear(forwardIn,0,forwardIn.Length);Array.Clear(reverseIn,0,reverseIn.Length);Array.Clear(forwardOut,0,forwardOut.Length);Array.Clear(reverseOut,0,reverseOut.Length);Reflected=0; }
        }
        sealed class Muffler
        {
            readonly LowPass low; readonly Band notch; readonly Delay room;
            readonly double echo,packing,gain,mix,expansion; readonly bool withWave;
            public long Bytes=>room.Bytes;
            public Muffler(int rate,ExhaustWaveSpec p,bool withWave)
            {
                this.withWave=withWave;packing=p.packing;expansion=p.expansionRatio;
                low=new LowPass(rate,Clamp(7200/(1+packing*6*p.chamberLengthM),300,rate*.40));notch=new Band(rate,p.resonatorHz,p.resonatorQ);room=new Delay(rate*.022+6);
                echo=p.chamberLengthM/Math.Sqrt(1.33*287*(p.gasTemperatureK>0?p.gasTemperatureK:650))*2*rate;
                gain=Math.Exp(-packing*p.chamberLengthM*1.45);mix=p.resonatorDepth;
            }
            public double Sample(double x) { double y=x;if(!withWave){room.Push(x);y=x-.19*expansion/5*room.Tap(echo);} double bass=low.Sample(y);y=y*(1-packing*.84)+bass*(packing*.84);y-=notch.Sample(y)*mix;return y*gain; }
            public void Clear() { low.Clear();notch.Clear();room.Clear(); }
        }
        readonly Path leftPath,rightPath;
        readonly Muffler leftMuffler,rightMuffler;
        readonly ExhaustFilter leftFilter,rightFilter;
        readonly ExhaustWaveSpec spec;
        double temperature;
        public bool Waves=>leftPath!=null;
        public bool PhysicalMuffler=>leftMuffler!=null;
        public double Left {get;private set;}
        public double Right {get;private set;}
        public double SoundSpeed=>leftPath?.SoundSpeed??0;
        public double HeaderDelayFrames=>leftPath?.HeaderDelay??0;
        public double Reflected=>leftPath?.Reflected??0;
        public long BufferBytes {get;private set;}
        public ExhaustWaveguide(ExhaustWaveSpec source,int rate,ExhaustSetup hardware=null,int stage=0)
        {
            if(rate<8000||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            spec=source?.Copy()??throw new ArgumentNullException(nameof(source));string error=spec.Validate();if(spec.version!=1||error!=null)throw new ArgumentException(error??"Resolve acoustic configuration before playback.");
            bool hasMuffler=hardware==null;
            var retained=new List<ExhaustPart>();
            if(hardware!=null)foreach(var part in hardware.parts){bool silencer=part.kind==ExhaustPartKind.AbsorptiveSilencer||part.kind==ExhaustPartKind.ChamberedSilencer;hasMuffler|=silencer&&part.amount>0;if(spec.mufflerMode!=AcousticModelMode.Simulated||!silencer)retained.Add(part.Copy());}
            bool muffler=spec.mufflerMode==AcousticModelMode.Simulated&&hasMuffler;
            if(spec.pipeMode==AcousticModelMode.Simulated){leftPath=new Path(rate,spec,muffler);rightPath=new Path(rate,spec,muffler);BufferBytes+=leftPath.Bytes+rightPath.Bytes;}
            if(muffler){leftMuffler=new Muffler(rate,spec,Waves);rightMuffler=new Muffler(rate,spec,Waves);BufferBytes+=leftMuffler.Bytes+rightMuffler.Bytes;}
            if(hardware!=null){var setup=new ExhaustSetup{version=1,parts=retained.ToArray()};leftFilter=new ExhaustFilter(setup,stage);rightFilter=new ExhaustFilter(setup,stage);leftFilter.Configure(rate);rightFilter.Configure(rate);}
            temperature=spec.gasTemperatureK>0?spec.gasTemperatureK:650;
        }
        public void SetGasTemperature(double kelvin)
        {
            if(!Waves||spec.gasTemperatureK>0||double.IsNaN(kelvin)||double.IsInfinity(kelvin))return;
            kelvin=Clamp(kelvin,300,1200);if(kelvin==temperature)return;temperature=kelvin;leftPath.Temperature(kelvin);rightPath.Temperature(kelvin);
        }
        public void Process(double left,double right,double popLeft=0,double popRight=0,double position=.5)
        {
            if(Waves){left=leftPath.Sample(left,popLeft,position);right=rightPath.Sample(right,popRight,position);}
            if(PhysicalMuffler){left=leftMuffler.Sample(left);right=rightMuffler.Sample(right);}
            Left=leftFilter==null?left:leftFilter.Process(left);Right=rightFilter==null?right:rightFilter.Process(right);
        }
        public void Clear() { leftPath?.Clear();rightPath?.Clear();leftMuffler?.Clear();rightMuffler?.Clear();leftFilter?.Clear();rightFilter?.Clear();Left=Right=0; }
    }
}
