using System;
namespace DragLab.Domain
{
    // Prepared voices: shared reviewed pressure/texture banks + installed, source-relative paths.
    // Chemistry and event production stay on the simulation owner; callback only reads stamped history.
    public sealed class AfterfireAudioSynth
    {
        const int VoiceCount = 64, Variants = 4;
        static readonly object gate = new object();
        static double[][] bank;
        public static void PrepareSourceBank()
        {
            lock (gate)
            {
                if (bank != null) return;
                var built = new double[3 * Variants][];
                for (int k = 0; k < 3; k++) for (int v = 0; v < Variants; v++)
                    built[k * Variants + v] = BackfirePressureSource.Burst(BackfirePressureSource.Seed(20261020,"runtime-source",v,k),
                        k == 0 ? "crackle" : k == 1 ? "normal" : "crack", BackfirePressureSource.Cleanup.LessTicking);
                bank = built;
            }
        }
        sealed class Mode
        {
            readonly double b, a1, a2;
            double z1,z2;
            public Mode(double hz,double q,int rate)
            { double w=2*Math.PI*Math.Min(hz,rate*.4)/rate,alpha=Math.Sin(w)/(2*q),a=1+alpha;b=alpha/a;a1=-2*Math.Cos(w)/a;a2=(1-alpha)/a; }
            public double Sample(double x){double y=b*x+z1;z1=-a1*y+z2;z2=-b*x-a2*y;return y;}
            public void Clear(){z1=z2=0;}
        }
        sealed class Voice
        {
            public bool active;
            public double start,position,step,amplitude;
            public double[] source,other;
            public double blend;
            readonly ExhaustFilter[] paths = new ExhaustFilter[8];
            readonly Mode[] modes;
            readonly int rate;
            readonly double bassAlpha, midAlpha, softAlpha, scatterAlpha;
            readonly double[] history = new double[4096];
            int cursor, path, travel;
            double bass,mid,turbineLow,turbineHigh,turbineAlpha,turbineHighAlpha,turbineGain,soft,scatter;
            double depth,grain,bite,room1,room2,env0=1,env1=1,env2=1;
            double chemicalOnset,lastChemical,mergeWindow,tailEnergy,tailLow,tailDecay,voiceGain;
            uint noise;
            readonly double decay0,decay1,decay2;
            bool turbine;
            int taps1,taps2,taps3;
            readonly double length;
            public Voice(EngineSoundProfile profile,int rate)
            {
                this.rate=rate; decay0=Math.Exp(-1.0/(rate*.02));decay1=Math.Exp(-1.0/(rate*.017));decay2=Math.Exp(-1.0/(rate*.013));
                length=1.1+(profile.turboCount>0?.18:0)+.8;
                foreach(var p in profile.exhaustSystem.parts)length+=RuntimeExhaustState.PartLength(p);
                for(int zone=0;zone<8;zone++)
                {
                    double at=(zone+.5)/8,cursor=1.1+(profile.turboCount>0?.18:0);
                    var setup=profile.exhaustSystem.Copy();
                    for(int i=0;i<setup.parts.Length;i++)
                    {double end=cursor+RuntimeExhaustState.PartLength(setup.parts[i]);setup.parts[i].amount*=(float)DragMath.Clamp((end-at*length)/(end-cursor),0,1);cursor=end;}
                    paths[zone]=new ExhaustFilter(setup,profile.exhaust);paths[zone].Configure(rate);
                }
                modes=new[]{new Mode(410,2,rate),new Mode(940,2.6,rate),new Mode(2100,3,rate)};
                bassAlpha=1-Math.Exp(-2*Math.PI*450/rate);midAlpha=1-Math.Exp(-2*Math.PI*2550/rate);
                softAlpha=1-Math.Exp(-2*Math.PI*Math.Min(6000,rate*.4)/rate);scatterAlpha=1-Math.Exp(-2*Math.PI*Math.Min(3600,rate*.4)/rate);
            }
            public void Begin(ExhaustMixtureModel.Event e,EngineSoundProfile profile)
            {
                Clear(); active=true; start=e.time;chemicalOnset=lastChemical=e.time;mergeWindow=.08*Math.Max(1,profile.popDecay);
                tailDecay=Math.Exp(-1.0/(rate*.035));noise=(uint)(e.seed^(e.seed>>32));if(noise==0)noise=117;
                double strength=.28*Math.Sqrt(e.acousticJ);int kind=strength<.28?0:e.limiter||e.twoStep||strength>.72?2:1;
                uint bits=(uint)(e.seed^(e.seed>>32));int variant=(int)(bits%Variants);
                source=bank[kind*Variants+variant];other=bank[kind*Variants+(variant+1)%Variants];blend=((bits>>8)&255)/255.0;
                double u=((bits>>16)&65535)/65535.0;
                double stretch=(1+.90*(-.14+.31*u))*DragMath.Clamp(Math.Pow(profile.litres/2,.06)*Math.Pow(4500/Math.Max(700,e.rpm),.05)*Math.Pow(650/Math.Max(300,e.gasK),.05),.8,1.25)*DragMath.Clamp(profile.popDecay,.25,3)/Math.Pow(DragMath.Clamp(profile.popSharpness,.25,2.5),.25);
                step=48000.0/rate/stretch;
                depth=1+.9*(-.27+.56*u);bite=1+.9*(-.32+.69*(1-u));grain=1+.9*(-.24+.56*blend);
                voiceGain=profile.popGain*.45*(1+.9*(-.11+.23*u));amplitude=strength*voiceGain;
                path=e.zone;double at=e.position;
                turbine=profile.turboCount>0&&at*length<.45;
                // Event carries the actual pressure at ignition, not a later render frame.
                double boost=Math.Max(0,e.turboBoost);
                turbineAlpha=1-Math.Exp(-2*Math.PI*1700/(1+.8*boost)/rate);
                turbineHighAlpha=1-Math.Exp(-2*Math.PI*Math.Min(4800/(1+.3*boost),rate*.4)/rate);
                turbineGain=Math.Pow(10,-(1.5+3*boost)/20);
                travel=(int)Math.Round(rate*Math.Max(0,.98-at)*length/Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,300,1500)));
                start+=travel/(double)rate;
                taps1=(int)(rate*(.003+Math.Max(0,.98-at)*.007));taps2=(int)(rate*.009);taps3=(int)(rate*.0195);
                room1=.07;room2=.082;
            }
            public bool CanContinue(ExhaustMixtureModel.Event e) => active && path==e.zone && e.time>=lastChemical && e.time-lastChemical<=.020 && e.time-chemicalOnset<mergeWindow;
            public void Continue(ExhaustMixtureModel.Event e) { lastChemical=e.time;tailEnergy+=e.acousticJ; }
            public double Sample(double clock)
            {
                if(!active||clock<start)return 0;
                int at=(int)position;double dry=0;
                if(at<source.Length-1)
                {double f=position-at;dry=(source[at]+(source[at+1]-source[at])*f)*(1-blend)+(other[at]+(other[at+1]-other[at])*f)*blend;}
                else if(position>source.Length+48000*.10){active=false;return 0;}
                position+=step;
                // Continuous reaction energy excites a diffuse tail, not another shock front.
                noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
                double turbulent=noise/(double)uint.MaxValue*2-1;tailLow+=midAlpha*(turbulent-tailLow);
                double tail=.28*Math.Sqrt(tailEnergy)*voiceGain*tailLow;tailEnergy*=tailDecay;
                bass+=bassAlpha*(dry-bass);mid+=midAlpha*(dry-mid);
                double value=depth*bass+grain*(mid-bass)+bite*(dry-mid);
                value+=amplitude>1e-15?tail/amplitude:0;
                if(turbine){turbineLow+=turbineAlpha*(value-turbineLow);turbineHigh+=turbineHighAlpha*(value-turbineHigh);value=turbineGain*(.77*turbineLow+.15*turbineHigh);}
                value=paths[path].Process(value);soft+=softAlpha*(value-soft);value=.83*value+.17*soft;
                scatter+=scatterAlpha*(value-scatter);history[cursor]=scatter;
                value+=.15*history[(cursor-taps1)&4095]-.094*history[(cursor-taps2)&4095]+.073*history[(cursor-taps3)&4095];
                env0*=decay0;env1*=decay1;env2*=decay2;
                value+=.4*.9*(.28*modes[0].Sample(value)*env0+.19*modes[1].Sample(value)*env1+.115*modes[2].Sample(value)*env2);
                // Low-level outlet-space reflections retained from the preview path.
                value+=room1*history[(cursor-(int)(rate*.011))&4095]+room2*history[(cursor-(int)(rate*.020))&4095];
                cursor=(cursor+1)&4095;return value*amplitude*DragMath.Clamp((source.Length+48000*.10-position)/(48000*.10),0,1);
            }
            public void Clear()
            {
                active=false;position=bass=mid=turbineLow=turbineHigh=soft=scatter=0;cursor=0;env0=env1=env2=1;tailEnergy=tailLow=0;
                Array.Clear(history,0,history.Length);foreach(var filter in paths)filter.Clear();foreach(var mode in modes)mode.Clear();
            }
        }
        readonly Voice[] voices=new Voice[VoiceCount];
        readonly EngineSoundProfile profile;
        readonly int rate;
        ExhaustMixtureModel source,retained;
        int retainedCursor;
        int cursor;
        double clock;
        public double Clock => clock;
        public long Delivered {get;private set;}
        public long HistoryOverflows {get;private set;}
        public long TailSteals {get;private set;}
        public long Inaudible {get;private set;}
        public long ContinuedReactions {get;private set;}
        public AfterfireAudioSynth(EngineSoundProfile profile,int rate)
        {
            this.profile=profile;this.rate=rate;PrepareSourceBank();
            for(int i=0;i<voices.Length;i++)voices[i]=new Voice(profile,rate);
        }
        public void Connect(ExhaustMixtureModel model)
        {
            if(ReferenceEquals(source,model))return;
            if(model==null){retained=source;retainedCursor=cursor;Clear();return;}
            bool resume=ReferenceEquals(model,retained);Clear();source=model;
            clock=Math.Max(0,model.Time-.04);cursor=resume?retainedCursor:Math.Max(0,model.Sequence-ExhaustMixtureModel.HistoryCapacity+1);
            retained=null;
            // Reconfiguration/device change does not replay stale historical bangs.
            while(cursor<model.Sequence&&model.Read(cursor+1,out var e)&&e.time<clock)cursor++;
        }
        public double Sample()
        {
            if(source==null)return 0;
            int end=source.Sequence;
            if(end-cursor>=ExhaustMixtureModel.HistoryCapacity){HistoryOverflows+=end-cursor-ExhaustMixtureModel.HistoryCapacity+1;cursor=end-ExhaustMixtureModel.HistoryCapacity+1;}
            while(cursor<end)
            {
                if(!source.Read(cursor+1,out var e))break;
                if(e.time>clock)break;
                cursor++;Delivered++;
                if(e.acousticJ<.002){Inaudible++;continue;}
                Voice continuing=null;
                foreach(var v in voices)if(v.CanContinue(e)){continuing=v;break;}
                if(continuing!=null){continuing.Continue(e);ContinuedReactions++;continue;}
                Voice free=null;
                foreach(var v in voices)if(!v.active){free=v;break;}
                if(free==null){free=voices[0];foreach(var v in voices)if(v.start<free.start)free=v;TailSteals++;}
                free.Begin(e,profile);
            }
            double value=0;foreach(var v in voices)if(v.active)value+=v.Sample(clock);
            // Presentation time advances independently; the simulation owns all event times.
            clock+=1.0/rate;
            return value;
        }
        public void Clear(){foreach(var voice in voices)voice.Clear();source=null;cursor=0;clock=0;}
    }
}
