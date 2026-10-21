using System;

namespace DragLab.Domain
{
    // Source-relative propagation and pressure-driven shell radiation. Exhaust
    // coloration is generated once in AfterfireLabSource, not filtered again here.
    public sealed class AfterfirePipeResponse
    {
        sealed class Mode
        {
            readonly double b,a1,a2; double z1,z2;
            public Mode(double hz,double q,int rate)
            { double w=2*Math.PI*Math.Min(hz,rate*.4)/rate,alpha=Math.Sin(w)/(2*q),a=1+alpha;b=alpha/a;a1=-2*Math.Cos(w)/a;a2=(1-alpha)/a; }
            public double Sample(double x)
            {
                double y=b*x+z1;z1=-a1*y+z2;z2=-b*x-a2*y;
                // A pressure-driven wall can become silent while the flame's
                // noisy gas body continues. Retire inaudible filter remnants
                // before subnormal arithmetic makes those silent modes costly.
                if(Math.Abs(z1)+Math.Abs(z2)<1e-24)z1=z2=0;
                return y;
            }
            public void Clear(){z1=z2=0;}
        }
        readonly EngineSoundProfile profile;
        readonly int rate,mask;
        readonly double length;
        readonly Mode[,] walls=new Mode[8,2];
        readonly double[] wallGains=new double[8],outletGains=new double[8];
        readonly double[] history;
        int cursor,path,travel;
        double outletGain;
        public bool BeforeTurbine {get;private set;}
        public double Wall {get;private set;}
        public double Outlet {get;private set;}
        public int TravelFrames=>travel;
        public double OutletScale=>outletGain;
        public AfterfirePipeResponse(EngineSoundProfile profile,int rate)
        {
            if(rate<8000||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            this.profile=profile;this.rate=rate;length=RuntimeExhaustState.PathLength(profile);
            int size=1;while(size<rate*.08+2)size*=2;mask=size-1;
            history=new double[size];
            for(int zone=0;zone<8;zone++)
            {
                double source=(zone+.5)/8*length,at=RuntimeExhaustState.HeaderLengthM+(profile.turboCount>0?RuntimeExhaustState.TurbineLengthM:0);
                double nearest=double.MaxValue,hz=160,coupling=.012,attenuation=0;
                foreach(var part in profile.exhaustSystem.parts)
                {
                    double extent=RuntimeExhaustState.PartLength(part),end=at+extent;
                    double down=DragMath.Clamp((end-source)/extent,0,1);
                    attenuation+=part.attenuationDb*part.amount*down/(1+profile.exhaust*.3);
                    double distance=Math.Abs(source-(at+extent*.5));at=end;
                    if(distance>=nearest||part.amount<=0)continue;
                    nearest=distance;hz=part.resonanceHz;
                    double shell=part.kind==ExhaustPartKind.ChamberedSilencer?.07:part.kind==ExhaustPartKind.AbsorptiveSilencer?.045:part.kind==ExhaustPartKind.Resonator?.035:.018;
                    coupling=.012+shell*part.amount/(1+distance*distance);
                }
                walls[zone,0]=new Mode(DragMath.Clamp(hz,90,420),.55,rate);walls[zone,1]=new Mode(DragMath.Clamp(hz*2.7,270,1250),.65,rate);
                wallGains[zone]=Math.Sqrt(coupling);outletGains[zone]=Math.Sqrt(1-coupling)*Math.Pow(10,-attenuation/20);
            }
        }
        public void Begin(ExhaustMixtureModel.Event e)
        {
            Clear();path=e.zone;
            double at=e.position*length,c=Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,300,1500));
            BeforeTurbine=profile.turboCount>0&&at<RuntimeExhaustState.HeaderLengthM;
            // Timbre belongs to the one hardware-parameterized HTML generator.
            // This path owns only power loss, arrival and distinct shell radiation.
            outletGain=outletGains[path]*(BeforeTurbine?Math.Pow(10,-(1.5+3*Math.Max(0,e.turboBoost))/20):1);
            travel=Math.Min(mask,(int)Math.Round(rate*Math.Max(0,.98-e.position)*length/c));
        }
        public double Sample(double source,double coherentPressure)
        {
            Wall=wallGains[path]*(.8*walls[path,0].Sample(coherentPressure)+.3*walls[path,1].Sample(coherentPressure));
            history[cursor]=source;
            Outlet=history[(cursor-travel)&mask]*outletGain;cursor=(cursor+1)&mask;
            return Outlet+Wall;
        }
        public void Clear()
        {
            cursor=0;Wall=Outlet=0;Array.Clear(history,0,history.Length);
            foreach(var p in walls)p.Clear();
        }
    }
}
