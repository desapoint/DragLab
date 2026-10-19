using System;

namespace DragLab.Domain
{
    // Offline opt-in prototype. No Unity API, allocations, coefficient design or
    // transcendental pulse construction in Sample(). Setup runs before playback.
    public sealed class PressureEventCore
    {
        sealed class Filter
        {
            readonly double b0,b1,b2,a1,a2;
            double z1,z2;
            public Filter(int rate,double hz,double q,int kind)
            {
                hz=Math.Min(hz,rate*.42);double w=2*Math.PI*hz/rate,c=Math.Cos(w),s=Math.Sin(w),alpha=s/(2*q),a0=1+alpha;
                a1=-2*c/a0;a2=(1-alpha)/a0;
                if(kind==0){b0=(1-c)/2/a0;b1=(1-c)/a0;b2=b0;}
                else if(kind==1){b0=(1+c)/2/a0;b1=-(1+c)/a0;b2=b0;}
                else{b0=alpha/a0;b1=0;b2=-b0;}
            }
            public double Process(double x){double y=b0*x+z1;z1=b1*x-a1*y+z2;z2=b2*x-a2*y;return y;}
            public void Clear(){z1=z2=0;}
        }
        sealed class Delay
        {
            readonly double[] data;int at;
            public Delay(int size){data=new double[size];}
            public void Push(double x){data[at]=x;at=(at+1)%data.Length;}
            public double Tap(int samples){int i=at-1-samples;while(i<0)i+=data.Length;return data[i];}
            public void Clear(){Array.Clear(data,0,data.Length);at=0;}
            public long Bytes=>data.Length*8L;
        }
        sealed class LowPole
        {
            readonly double b,a;double x1,y1;
            public LowPole(int rate,double hz){double k=Math.Tan(Math.PI*Math.Min(hz,rate*.42)/rate);b=k/(1+k);a=(k-1)/(k+1);}
            public double Process(double x){double y=b*(x+x1)-a*y1;x1=x;y1=y;return y;}
            public void Clear(){x1=y1=0;}
        }
        readonly EngineSoundProfile profile;
        readonly int rate,mask,kernelLength;
        readonly int[] order,banks;
        readonly double[] runners,cylinderGain,mechanicalKernel;
        readonly double[][] kernels;
        readonly double[][] pending;
        readonly double[] rpmGrid={600,1000,1600,2400,3600,5000,6500,8500,12000};
        readonly Delay leftHistory,rightHistory,collectorHistory;
        readonly Filter headL1,headL2,headR1,headR2,hp,lp2,bodyLow,bodyHigh,bodyPass,body96,body208,body415,blockHigh,blockLow,intakeHigh,intakeLow;
        readonly LowPole lp1;
        readonly int[] leftTaps,rightTaps,collectorTaps;
        readonly double throttleAlpha;
        double phase,throttleLow;int cursor,nextCylinder;uint random;
        public double Mechanical {get;private set;}
        public long Events {get;private set;}
        public readonly long[] CylinderEvents;
        public int Rate=>rate;
        public long BufferBytes {get;private set;}
        static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
        public PressureEventCore(EngineSoundProfile profile,int rate=48000,uint seed=171818)
        {
            if(rate<22050||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            this.profile=profile;this.rate=rate;random=seed==0?171818u:seed;
            order=(int[])Mechanics.EngineElements.FiringOrder(profile.cylinders).Clone();
            banks=new int[profile.cylinders];runners=new double[profile.cylinders];cylinderGain=new double[profile.cylinders];CylinderEvents=new long[profile.cylinders];
            double[] lengths={.50,.61,.43,.49,.57,.46,.38,.55};double[] gains={1,.97,1.03,.98,1.02,1,.96,1.04};
            double geometry=Math.Pow(profile.litres/5.0,1.0/3);
            for(int i=0;i<profile.cylinders;i++){banks[i]=profile.layout==EngineLayout.Inline?0:i%2;runners[i]=lengths[i%8]*geometry;cylinderGain[i]=gains[i%8];}
            kernelLength=(int)Math.Round(rate*.028);kernels=new double[rpmGrid.Length*5][];
            for(int r=0;r<rpmGrid.Length;r++)for(int a=0;a<5;a++)
            {
                double throttle=a*.25,rpm=rpmGrid[r],scale=Clamp(1.17-.075*Math.Log(Math.Max(rpm,750)/850,2)-.11*throttle,.78,1.20);
                var kernel=new double[kernelLength];double gain=(.78+.94*throttle)*Math.Pow(Math.Max(rpm,800)/850,-.35);
                for(int j=0;j<kernel.Length;j++)
                {
                    double x=j/(double)rate/scale;
                    kernel[j]=((1-Math.Exp(-x/.00020))*Math.Exp(-x/.0030)-.28*(1-Math.Exp(-x/.00145))*Math.Exp(-x/.0092)+.10*throttle*(1-Math.Exp(-x/.00012))*Math.Exp(-x/.0008))*gain;
                }
                kernels[r*5+a]=kernel;BufferBytes+=kernel.Length*8L;
            }
            mechanicalKernel=new double[(int)Math.Round(rate*.018)];
            for(int j=0;j<mechanicalKernel.Length;j++){double t=j/(double)rate;mechanicalKernel[j]=.23*(1-Math.Exp(-t/.00033))*Math.Exp(-t/.006)*(Math.Sin(2*Math.PI*118*t)+.23*Math.Sin(2*Math.PI*270*t));}
            int size=1;while(size<rate*.12+kernelLength)size*=2;mask=size-1;
            pending=new[]{new double[size],new double[size],new double[size]};BufferBytes+=(3L*size+mechanicalKernel.Length)*8;
            leftHistory=new Delay((int)(rate*.015)+2);rightHistory=new Delay((int)(rate*.015)+2);collectorHistory=new Delay((int)(rate*.05)+2);
            BufferBytes+=leftHistory.Bytes+rightHistory.Bytes+collectorHistory.Bytes;
            leftTaps=Taps(3.1,6.5,11.2);rightTaps=Taps(3.9,8.1,13);collectorTaps=Taps(8.8,17.5,31,46);
            headL1=new Filter(rate,167,2.8,2);headL2=new Filter(rate,345,2,2);headR1=new Filter(rate,154,2.6,2);headR2=new Filter(rate,319,2.2,2);
            double brightness=Math.Sqrt(Clamp(profile.voiceBrightness,.1,2));
            hp=new Filter(rate,25,.707106781,1);lp1=new LowPole(rate,2450*brightness);lp2=new Filter(rate,2450*brightness,1,0);
            bodyLow=new Filter(rate,180,.707106781,0);bodyHigh=new Filter(rate,56,.707106781,1);bodyPass=new Filter(rate,1450,.707106781,0);
            body96=new Filter(rate,96,1.8,2);body208=new Filter(rate,208,2.2,2);body415=new Filter(rate,415,2,2);
            blockHigh=new Filter(rate,47,.707106781,1);blockLow=new Filter(rate,720,.707106781,0);
            intakeHigh=new Filter(rate,145,.707106781,1);intakeLow=new Filter(rate,1200,.707106781,0);
            throttleAlpha=1-Math.Exp(-2*Math.PI*18/rate);
        }
        int[] Taps(params double[] ms){var r=new int[ms.Length];for(int i=0;i<ms.Length;i++)r[i]=(int)Math.Round(rate*ms[i]/1000);return r;}
        double Variation(){random^=random<<13;random^=random>>17;random^=random<<5;return random/(double)uint.MaxValue*2-1;}
        void Schedule(double rpm,double throttle,EngineSoundFrame frame,double overdue)
        {
            int cylinder=order[nextCylinder];nextCylinder=(nextCylinder+1)%order.Length;Events++;CylinderEvents[cylinder]++;
            double contribution=frame.cylinderContribution!=null&&frame.cylinderContribution.Length==order.Length?Clamp(frame.cylinderContribution[cylinder],0,1):1;
            if(frame.cut)contribution=0;
            double gain=cylinderGain[cylinder]*(1+Variation()*(rpm>1500?.008:.027));
            double arrival=runners[cylinder]/(365+85*throttle)*rate-overdue;
            int offset=(int)Math.Floor(arrival);double fraction=arrival-offset;
            int r=0;while(r+1<rpmGrid.Length-1&&rpm>rpmGrid[r+1])r++;
            double rf=Clamp((rpm-rpmGrid[r])/(rpmGrid[r+1]-rpmGrid[r]),0,1);int a=Math.Min(3,(int)(throttle*4));double af=throttle*4-a;
            double[] k00=kernels[r*5+a],k01=kernels[r*5+a+1],k10=kernels[(r+1)*5+a],k11=kernels[(r+1)*5+a+1],target=pending[banks[cylinder]];
            double w00=(1-rf)*(1-af)*gain*contribution,w01=(1-rf)*af*gain*contribution,w10=rf*(1-af)*gain*contribution,w11=rf*af*gain*contribution;
            for(int i=0;i<kernelLength;i++)
            {
                double v=k00[i]*w00+k01[i]*w01+k10[i]*w10+k11[i]*w11;int at=(cursor+offset+i)&mask;
                target[at]+=v*(1-fraction);target[(at+1)&mask]+=v*fraction;
            }
            // Mechanical rotation remains audible during ignition/fuel cuts.
            for(int i=0;i<mechanicalKernel.Length;i++){int at=(cursor+offset+i)&mask;double v=mechanicalKernel[i]*cylinderGain[cylinder];pending[2][at]+=v*(1-fraction);pending[2][(at+1)&mask]+=v*fraction;}
        }
        public double Sample(double rpm,double throttle,EngineSoundFrame frame)
        {
            throttle=Clamp(throttle,0,1);double step=Math.Max(0,rpm)*order.Length/(120*rate);phase+=step;
            while(phase>=1){phase-=1;Schedule(rpm,throttle,frame,phase/Math.Max(1e-12,step));}
            double l=pending[0][cursor],r=pending[1][cursor],block=pending[2][cursor];pending[0][cursor]=pending[1][cursor]=pending[2][cursor]=0;cursor=(cursor+1)&mask;
            leftHistory.Push(l);rightHistory.Push(r);
            double hl=l+.36*leftHistory.Tap(leftTaps[0])-.22*leftHistory.Tap(leftTaps[1])+.12*leftHistory.Tap(leftTaps[2]);
            double hr=r+.33*rightHistory.Tap(rightTaps[0])-.20*rightHistory.Tap(rightTaps[1])+.11*rightHistory.Tap(rightTaps[2]);
            hl+=.09*headL1.Process(hl)+.08*headL2.Process(hl);hr+=.10*headR1.Process(hr)+.06*headR2.Process(hr);
            double collected=hl+hr;collectorHistory.Push(collected);
            collected+=.23*collectorHistory.Tap(collectorTaps[0])-.19*collectorHistory.Tap(collectorTaps[1])+.10*collectorHistory.Tap(collectorTaps[2])-.045*collectorHistory.Tap(collectorTaps[3]);
            double exhaust=lp2.Process(lp1.Process(hp.Process(collected)));
            double body=.60*profile.bassGain*bodyLow.Process(exhaust)+.80*profile.rawPulseGain*bodyPass.Process(bodyHigh.Process(exhaust))+.26*body96.Process(exhaust)+.15*body208.Process(exhaust)+.10*body415.Process(exhaust);
            throttleLow+=throttleAlpha*(throttle-throttleLow);
            Mechanical=.24*blockLow.Process(blockHigh.Process(block))+intakeLow.Process(intakeHigh.Process(collected))*(.06+.12*throttleLow);
            return Math.Tanh(1.23*body)/1.23;
        }
        public void Clear()
        {
            foreach(var p in pending)Array.Clear(p,0,p.Length);leftHistory.Clear();rightHistory.Clear();collectorHistory.Clear();
            headL1.Clear();headL2.Clear();headR1.Clear();headR2.Clear();hp.Clear();lp1.Clear();lp2.Clear();bodyLow.Clear();bodyHigh.Clear();bodyPass.Clear();body96.Clear();body208.Clear();body415.Clear();blockHigh.Clear();blockLow.Clear();intakeHigh.Clear();intakeLow.Clear();
            Mechanical=throttleLow=phase=0;cursor=nextCylinder=0;
        }
    }
}
