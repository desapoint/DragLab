using System;

namespace DragLab.Domain
{
    // Streaming C# port of renderPop's V3 pressure/noise core. No audio clips,
    // sample-array rendering or allocations occur when a live reaction arrives.
    // Gameplay and numerical verification use this same preset envelope.
    // Chemistry controls its onset and external gain, never these six decays.
    public sealed class AfterfireLabSource
    {
        struct Pulse
        {
            public int first,last;
            public double y,d,z,zd,fall,fast;
        }
        readonly Pulse[] pulses=new Pulse[40];
        readonly float[] samples=new float[64],pressures=new float[64];
        static readonly float[] Kernel=MakeKernel();
        static readonly double[] Saturation=MakeSaturation();
        // Force table preparation before the first instance constructor returns,
        // rather than permitting beforefieldinit to defer it into an audio call.
        static AfterfireLabSource() { }
        readonly int os,fs;
        int sampleIndex,write,pulseCount,bodyFirst,flameFirst,durationFrames;
        uint random;
        bool started;
        double size,edge,crack,thump,bark,tube,jet,smooth,air,period,decay,flame,duration;
        double al,ac,am,ah,slowA,envA,omega1,omega2,crackBand,dcCoef;
        double dropCrack,dropBody,dropBark,dropTail,dropColour,dropDiffuse,startBody,startFlame;
        double l1,l2,c1,c2,m1,m2,h1,h2,slow,absMod,bp1,bp2,lo1,lo2;
        double crackEnv,bodyEnv,barkEnv,tailEnv,colourEnv,diffuseEnv,bodyAttack,flameAttack,dcX,dcY;
        public double PressureSample {get;private set;}
        public int PulseOverflows {get;private set;}
        public int LatencyFrames=>os==2?8:0;
        public double Duration=>duration;
        public bool Quiet=>!started||sampleIndex>=durationFrames+os*LatencyFrames;
        public AfterfireLabSource(int rate)
        {
            if(rate<8000||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            os=rate>=92000?1:2;fs=rate*os;
        }
        static float[] MakeKernel()
        {
            var k=new float[33];double sum=0;
            for(int i=-16;i<=16;i++)
            {
                double x=i/16.0,window=.42+.5*Math.Cos(Math.PI*x)+.08*Math.Cos(2*Math.PI*x);
                double v=(i==0?.43:Math.Sin(2*Math.PI*.215*i)/(Math.PI*i))*window;
                // Match Float32Array.from: normalize in double, round only once.
                sum+=v;
            }
            for(int i=-16;i<=16;i++)
            {double x=i/16.0;k[i+16]=(float)((i==0?.43:Math.Sin(2*Math.PI*.215*i)/(Math.PI*i))*(.42+.5*Math.Cos(Math.PI*x)+.08*Math.Cos(2*Math.PI*x))/sum);}
            return k;
        }
        static double[] MakeSaturation()
        {
            var table=new double[4097];for(int i=0;i<table.Length;i++)table[i]=Math.Tanh(i/512.0);return table;
        }
        static double Saturate(double x)
        {
            // Cubic Hermite evaluation of the SAME tanh, not a different
            // distortion. Error below 2e-12 on [-8,8]; avoids a managed/native
            // transcendental call twice per output sample on Android/Mono.
            double a=Math.Abs(x),u=a*512;if(u>=4096)return Math.Tanh(x);
            int i=(int)u;double t=u-i,y0=Saturation[i],y1=Saturation[i+1];
            double m0=(1-y0*y0)/512,m1=(1-y1*y1)/512,d=y1-y0;
            double y=y0+t*(m0+t*(3*d-2*m0-m1+t*(-2*d+m0+m1)));return x<0?-y:y;
        }
        double Random()
        {
            unchecked
            {
                random+=0x6D2B79F5u;uint t=random;
                t=(t^(t>>15))*(t|1);t^=t+(t^(t>>7))*(t|61);
                return (t^(t>>14))/4294967296.0;
            }
        }
        double Coeff(double hz)=>1-Math.Exp(-2*Math.PI*Math.Min(hz,fs*.40)/fs);
        double Drop(double tau)=>Math.Exp(-1/(fs*Math.Max(.00001,tau)));
        void Configure(AfterfireLabSettings p,uint seed)
        {
            Clear();started=true;random=seed;
            size=p.size*.01;edge=p.edge*.01;crack=p.crack*.01;thump=p.thump*.01;bark=p.bark*.01;tube=p.tube*.01;jet=p.jet*.01;
            smooth=DragMath.Clamp(p.smooth*.01,0,1);air=DragMath.Clamp(p.air*.01,0,1);
            period=p.pulse*.001;decay=p.decay*.001;flame=p.flameTime*.001;
            duration=DragMath.Clamp(Math.Max(.38,Math.Max(decay*3,flame*2.8))+.14+p.space*.0014,.48,2.6);
            durationFrames=(int)Math.Ceiling(duration*fs/os)*os;
            bodyFirst=(int)Math.Ceiling((.0004+.0008*(1-edge))*fs);flameFirst=(int)Math.Ceiling(.004*fs);
            al=Coeff(90+size*52);ac=Coeff(390+bark*170);am=Coeff(1400+air*650);ah=Coeff(5000+(1-smooth)*4300+air*700);
            slowA=Coeff(22);envA=Coeff(75);
            double center=DragMath.Clamp(p.tune,120,1350);
            // Original V3 values at normal rates; the extra bound keeps its
            // overdamped state-variable filter stable on 8 kHz devices too.
            omega1=2*Math.Sin(Math.PI*center/fs);omega2=2*Math.Sin(Math.PI*Math.Min(fs*.10,center*1.9)/fs);
            dropCrack=Drop(.0026+.008*(1-edge));dropBody=Drop(decay*(.41+.20*size)+.012);
            dropBark=Drop(decay*(.40+.16*bark)+.012);dropTail=Drop(flame*.55+.012);
            dropColour=Drop(.034+decay*.18);dropDiffuse=Drop(.028+.05*size);
            startBody=Drop(.0022);startFlame=Drop(.013);dcCoef=Math.Exp(-2*Math.PI*17/fs);
            crackBand=(.26+.84*air)*(.98-.40*smooth);
            crackEnv=bodyEnv=barkEnv=tailEnv=colourEnv=diffuseEnv=1;
        }
        // One actual burst, before its physically controlled external strength and
        // installed/outdoor transfer. This is also the JS comparison entry point.
        public void Begin(AfterfireLabSettings p,uint seed)
        {
            Configure(p,seed);
            int count=(int)Math.Floor(p.scatter*.01*(2+6*Random())+.5);
            for(int k=0;k<count;k++)
            {
                double at=.014+Math.Pow(Random(),.8)*(.12+.17*p.scatter*.01);
                double width=.001+Random()*(.002+.004*(1-edge));
                double level=(.045+.13*Random())*(.35+.65*p.scatter*.01);
                AddPulse(at,width,0,level,1.75,7);
            }
            double followAt=.009+Random()*.016,followAmp=.075+.12*Random();
            double shockAt=.0004+.0008*(1-edge),widthMain=period*(.70+.55*size);
            double followWidth=widthMain*(.65+.25*Random()),rise=.000085+.00038*(1-edge);
            AddPulse(shockAt,widthMain,rise,1,1.55,30);AddPulse(followAt,followWidth,rise,followAmp,1.55,30);
        }
        int AddPulse(double at,double width,double rise,double amplitude,double exponent,double extent)
        {
            int first=(int)Math.Ceiling(at*fs);double t=first/(double)fs-at,x=t/width,step=1/(fs*width);
            double decay=Math.Exp(-exponent*x),fast=rise>0?decay*Math.Exp(-t/rise):0;
            if(pulseCount==pulses.Length){PulseOverflows++;return -1;}
            int index=pulseCount++;
            pulses[index]=new Pulse{first=first,last=(int)Math.Ceiling((at+width*extent)*fs),
                y=amplitude*(1-x)*decay,d=-amplitude*step*decay,z=amplitude*(1-x)*fast,zd=-amplitude*step*fast,
                fall=Math.Exp(-exponent*step),fast=rise>0?Math.Exp(-exponent*step-1/(fs*rise)):0};
            return index;
        }
        double Shock()
        {
            double sum=0;
            for(int i=0;i<pulseCount;i++)
            {
                var p=pulses[i];if(sampleIndex<p.first)continue;
                if(sampleIndex>=p.last){pulses[i]=pulses[--pulseCount];i--;continue;}
                sum+=p.y-p.z;p.y=(p.y+p.d)*p.fall;p.d*=p.fall;p.z=(p.z+p.zd)*p.fast;p.zd*=p.fast;pulses[i]=p;
            }
            return sum;
        }
        double Internal(out double pressure)
        {
            if(sampleIndex>=durationFrames){sampleIndex++;pressure=0;return 0;}
            double n=Random()*2-1;
            l1+=al*(n-l1);l2+=al*(l1-l2);c1+=ac*(n-c1);c2+=ac*(c1-c2);
            m1+=am*(n-m1);m2+=am*(m1-m2);h1+=ah*(n-h1);h2+=ah*(h1-h2);
            double low=l2*8,chest=(c2-l2)*5.2,middle=(m2-c2)*3.25,upper=(.70*h2+.30*h1-m2)*3.6;
            slow+=slowA*(Math.Abs(chest)-slow);absMod+=envA*(Math.Abs(middle)-absMod);
            double shock=Shock();
            if(sampleIndex>=bodyFirst)bodyAttack=1-(1-bodyAttack)*startBody;
            if(sampleIndex>=flameFirst)flameAttack=1-(1-flameAttack)*startFlame;
            double turbulence=.85+.15*Math.Min(1.8,slow*2.8);
            double sub=low*bodyEnv*bodyAttack*(.11+.26*thump)*(1+.32*size);
            double boom=chest*bodyEnv*bodyAttack*(.11+.26*thump);
            double barkNoise=(middle*.48+chest*.28)*barkEnv*bodyAttack*(.18+.70*bark)*turbulence;
            double pipeInput=shock*.48+(middle*.38+chest*.22)*barkEnv;
            double hi1=pipeInput-lo1-2.1*bp1;bp1+=omega1*hi1;lo1+=omega1*bp1;
            double hi2=pipeInput-lo2-2.35*bp2;bp2+=omega2*hi2;lo2+=omega2*bp2;
            double colour=(bp1*.65+bp2*.31)*(.07+.36*tube)*colourEnv;
            double flow=(middle*.37+upper*.13+chest*.3)*tailEnv*flameAttack*jet*(.12+.35*absMod*3);
            double breath=(middle*.43+upper*.48)*diffuseEnv*crackBand*1.7;
            double highCrack=(upper*.95+middle*.40)*crackEnv*crackBand*(.24+1.35*crack);
            double bodySat=Saturate((sub+boom+barkNoise+colour+flow)*1.35)*.71;
            pressure=shock*(.48+.74*thump);
            double raw=pressure+bodySat+breath+highCrack;
            double high=raw-dcX+dcCoef*dcY;dcX=raw;dcY=high;
            crackEnv*=dropCrack;bodyEnv*=dropBody;barkEnv*=dropBark;tailEnv*=dropTail;colourEnv*=dropColour;diffuseEnv*=dropDiffuse;
            if(crackEnv<1e-24)crackEnv=0;if(colourEnv<1e-24)colourEnv=0;
            sampleIndex++;return high;
        }
        public double Sample()
        {
            PressureSample=0;if(!started||Quiet)return 0;
            double value=0,pressure=0;
            for(int sub=0;sub<os;sub++)
            {
                double x=Internal(out double p);samples[write]=(float)x;pressures[write]=(float)p;
                // The causal symmetric FIR adds eight output frames of known
                // latency, rather than peeking at future live chemistry.
                if(sub==0)
                {
                    if(os==1){value=samples[write];pressure=pressures[write];}
                    else
                    {
                        value=samples[(write-16)&63]*Kernel[16];pressure=pressures[(write-16)&63]*Kernel[16];
                        for(int k=0;k<16;k++)
                        {value+=(samples[(write-k)&63]+samples[(write-32+k)&63])*(double)Kernel[k];pressure+=(pressures[(write-k)&63]+pressures[(write-32+k)&63])*(double)Kernel[k];}
                    }
                }
                write=(write+1)&63;
            }
            // The HTML crops its look-ahead FIR at time zero. Do not play the
            // negative-time startup skirt while the causal FIR fills its eight
            // frames of latency; it is absent from the approved reference WAV.
            if(sampleIndex<=os*LatencyFrames)value=pressure=0;
            PressureSample=pressure;return value;
        }
        public void Clear()
        {
            Array.Clear(samples,0,samples.Length);Array.Clear(pressures,0,pressures.Length);
            sampleIndex=write=pulseCount=0;started=false;
            l1=l2=c1=c2=m1=m2=h1=h2=slow=absMod=bp1=bp2=lo1=lo2=bodyAttack=flameAttack=dcX=dcY=0;
            PressureSample=0;
        }
    }
}
