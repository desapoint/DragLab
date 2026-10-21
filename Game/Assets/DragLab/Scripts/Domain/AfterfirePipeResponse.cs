using System;

namespace DragLab.Domain
{
    // Prepared, source-relative outlet and upstream-return paths. This is a
    // reduced-order acoustic transfer, not a change to exhaust chemistry.
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
        readonly double length,softAlpha,scatterAlpha;
        readonly ExhaustFilter[] outlets=new ExhaustFilter[8],returns=new ExhaustFilter[8];
        readonly Mode[,] walls=new Mode[8,2];
        readonly Mode headerLow,headerMid;
        readonly double[] wallGains=new double[8],outletGains=new double[8];
        readonly double[] history;
        readonly float[] returnHistory;
        readonly int[] runners;
        int cursor,path,travel,collectorTap;
        double rise,riseAlpha,soft,scatter,turbineLow,turbineHigh,returnLow,returnSoft;
        double turbineAlpha,turbineHighAlpha,turbineGain;
        public bool BeforeTurbine {get;private set;}
        public double Wall {get;private set;}
        public double Outlet {get;private set;}
        public AfterfirePipeResponse(EngineSoundProfile profile,int rate)
        {
            if(rate<8000||rate>192000)throw new ArgumentOutOfRangeException(nameof(rate));
            this.profile=profile;this.rate=rate;length=RuntimeExhaustState.PathLength(profile);
            int size=1;while(size<rate*.08+2)size*=2;mask=size-1;
            history=new double[size];returnHistory=new float[size];runners=new int[profile.cylinders];
            for(int zone=0;zone<8;zone++)
            {
                double source=(zone+.5)/8*length,cursor=RuntimeExhaustState.HeaderLengthM+(profile.turboCount>0?RuntimeExhaustState.TurbineLengthM:0);
                var forward=profile.exhaustSystem.Copy();
                var backward=new ExhaustSetup{parts=new ExhaustPart[forward.parts.Length*2]};
                double nearest=double.MaxValue,hz=160,coupling=.012;
                for(int i=0;i<forward.parts.Length;i++)
                {
                    var original=profile.exhaustSystem.parts[i];double extent=RuntimeExhaustState.PartLength(original),end=cursor+extent;
                    double down=DragMath.Clamp((end-source)/extent,0,1);
                    forward.parts[i].amount*= (float)down;
                    var upstream=original.Copy();upstream.amount*= (float)(1-down);
                    // A reflected wave traverses upstream components twice.
                    backward.parts[forward.parts.Length-1-i]=upstream;backward.parts[forward.parts.Length+i]=upstream.Copy();
                    double distance=Math.Abs(source-(cursor+extent*.5));cursor=end;
                    if(distance>=nearest||original.amount<=0)continue;
                    nearest=distance;hz=original.resonanceHz;
                    double shell=original.kind==ExhaustPartKind.ChamberedSilencer?.07:original.kind==ExhaustPartKind.AbsorptiveSilencer?.045:original.kind==ExhaustPartKind.Resonator?.035:.018;
                    coupling=.012+shell*original.amount/(1+distance*distance);
                }
                outlets[zone]=new ExhaustFilter(forward,profile.exhaust);outlets[zone].Configure(rate);
                returns[zone]=new ExhaustFilter(backward,profile.exhaust);returns[zone].Configure(rate);
                walls[zone,0]=new Mode(DragMath.Clamp(hz,90,420),.55,rate);walls[zone,1]=new Mode(DragMath.Clamp(hz*2.7,270,1250),.65,rate);
                wallGains[zone]=Math.Sqrt(coupling);outletGains[zone]=Math.Sqrt(1-coupling);
            }
            headerLow=new Mode(167*profile.acoustics.resonanceScale,.75,rate);headerMid=new Mode(345*profile.acoustics.resonanceScale,.9,rate);
            softAlpha=1-Math.Exp(-2*Math.PI*Math.Min(6000,rate*.4)/rate);scatterAlpha=1-Math.Exp(-2*Math.PI*Math.Min(3600,rate*.4)/rate);
        }
        public void Begin(ExhaustMixtureModel.Event e,double cutoff)
        {
            Clear();path=e.zone;
            double at=e.position*length,c=Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,300,1500));
            BeforeTurbine=profile.turboCount>0&&at<RuntimeExhaustState.HeaderLengthM;
            double boost=Math.Max(0,e.turboBoost);
            turbineAlpha=1-Math.Exp(-2*Math.PI*1700/(1+.8*boost)/rate);
            turbineHighAlpha=1-Math.Exp(-2*Math.PI*Math.Min(4800/(1+.3*boost),rate*.4)/rate);
            turbineGain=Math.Pow(10,-(1.5+3*boost)/20);
            riseAlpha=1-Math.Exp(-2*Math.PI*Math.Min(rate*.4,cutoff)/rate);
            travel=Math.Min(mask,(int)Math.Round(rate*Math.Max(0,.98-e.position)*length/c));
            double outside=Math.Max(0,at-RuntimeExhaustState.HeaderLengthM);
            double inHeader=Math.Min(1,at/RuntimeExhaustState.HeaderLengthM);
            collectorTap=Math.Max(1,Math.Min(mask,(int)Math.Round(rate*(2*outside/c+.002*profile.acoustics.collectorScale))));
            for(int i=0;i<runners.Length;i++)runners[i]=Math.Max(1,Math.Min(mask,(int)Math.Round(rate*(2*(outside+inHeader*profile.acoustics.Runner(i))/c+.003*profile.acoustics.collectorScale))));
        }
        public double Sample(double source,double coherentPressure)
        {
            // Metal is pressure-driven. Stochastic gas texture must not become
            // a dry structural shortcut around the turbine and outlet hardware.
            Wall=wallGains[path]*(.8*walls[path,0].Sample(coherentPressure)+.3*walls[path,1].Sample(coherentPressure));
            double upstream=returns[path].Process(source);
            if(profile.turboCount>0&&!BeforeTurbine)
            {
                returnLow+=turbineAlpha*(upstream-returnLow);returnSoft+=turbineAlpha*(returnLow-returnSoft);
                upstream=returnSoft*turbineGain*turbineGain;
            }
            upstream=.65*upstream+.25*headerLow.Sample(upstream)+.1*headerMid.Sample(upstream);
            returnHistory[cursor]=(float)upstream;
            double reflected=0;for(int i=0;i<runners.Length;i++)reflected+=returnHistory[(cursor-runners[i])&mask];
            double value=.92*source+.18*reflected/runners.Length-.12*returnHistory[(cursor-collectorTap)&mask];
            rise+=riseAlpha*(value-rise);value=rise;
            if(BeforeTurbine)
            {
                turbineLow+=turbineAlpha*(value-turbineLow);turbineHigh+=turbineHighAlpha*(value-turbineHigh);
                value=turbineGain*(.77*turbineLow+.15*turbineHigh);
            }
            value=outlets[path].Process(value);soft+=softAlpha*(value-soft);value=.83*value+.17*soft;
            scatter+=scatterAlpha*(value-scatter);history[cursor]=.9*value+.1*scatter;
            Outlet=history[(cursor-travel)&mask]*outletGains[path];cursor=(cursor+1)&mask;
            return Outlet+Wall;
        }
        public void Clear()
        {
            cursor=0;rise=soft=scatter=turbineLow=turbineHigh=returnLow=returnSoft=Wall=Outlet=0;
            Array.Clear(history,0,history.Length);Array.Clear(returnHistory,0,returnHistory.Length);
            foreach(var p in outlets)p.Clear();foreach(var p in returns)p.Clear();foreach(var p in walls)p.Clear();headerLow.Clear();headerMid.Clear();
        }
    }
}
