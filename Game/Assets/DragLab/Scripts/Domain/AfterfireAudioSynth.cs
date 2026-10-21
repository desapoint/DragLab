using System;
namespace DragLab.Domain
{
    // Prepared voices: shared reviewed pressure/texture banks + installed, source-relative paths.
    // Chemistry and event production stay on the simulation owner; callback only reads stamped history.
    public sealed class AfterfireAudioSynth
    {
        public struct EventEnvelope { public double seconds, stretch, riseCutoff; public int kind; }
        public static EventEnvelope Describe(ExhaustMixtureModel.Event e, EngineSoundProfile profile)
        {
            double length=1.1+(profile.turboCount>0?.18:0)+.8;
            foreach(var part in profile.exhaustSystem.parts)length+=RuntimeExhaustState.PartLength(part);
            double c=Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,250,2500));
            double seconds=Math.Max(.001,e.burnSeconds)+6*Math.Max(.00025,e.riseSeconds)+4*length*Math.Max(.05,1-e.position)/c;
            return new EventEnvelope { seconds=seconds,
                stretch=DragMath.Clamp(seconds/.035,.25,4)*DragMath.Clamp(profile.popDecay,.25,3)/Math.Pow(DragMath.Clamp(profile.popSharpness,.25,2.5),.25),
                riseCutoff=2000+.6/Math.Max(.00025,e.riseSeconds),
                kind=e.pressurePa>1000&&e.riseSeconds<.0015?2:e.acousticJ<.03?0:1 };
        }
        const int VoiceCount = 64, Variants = 4;
        static readonly object gate = new object();
        static double[][] bank;
        static double[] bankEnergy;
        static double[] bankCross;
        public static void PrepareSourceBank()
        {
            lock (gate)
            {
                if (bank != null) return;
                var built = new double[3 * Variants][];
                for (int k = 0; k < 3; k++) for (int v = 0; v < Variants; v++)
                    built[k * Variants + v] = BackfirePressureSource.Burst(BackfirePressureSource.Seed(20261020,"runtime-source",v,k),
                        k == 0 ? "crackle" : k == 1 ? "normal" : "crack", BackfirePressureSource.Cleanup.LessTicking);
                var energy = new double[built.Length];
                for (int i=0;i<built.Length;i++) foreach(double value in built[i]) energy[i]+=value*value/48000;
                var cross = new double[built.Length];
                for(int k=0;k<3;k++)for(int v=0;v<Variants;v++)
                    for(int i=0;i<built[k*Variants+v].Length;i++)cross[k*Variants+v]+=built[k*Variants+v][i]*built[k*Variants+(v+1)%Variants][i]/48000;
                bankEnergy = energy; bankCross = cross; bank = built;
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
            double lastChemical,tailEnergy,tailLow,tailDecay,voiceGain,riseLow,riseAlpha,tailGain;
            double frontStart,frontRise,frontEnergy,targetAmplitude,amplitudeAlpha;
            int frontId;
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
                Clear(); active=true; start=lastChemical=frontStart=e.time;frontId=e.frontId;
                frontRise=Math.Max(.00025,e.riseSeconds);
                frontEnergy=e.acousticJ;amplitudeAlpha=1-Math.Exp(-1.0/(rate*frontRise));
                var envelope=Describe(e,profile);
                double physicalDuration=envelope.seconds;
                tailDecay=Math.Exp(-1.0/(rate*DragMath.Clamp(physicalDuration,.008,.20)));
                tailGain=profile.popGain*.03*Math.Sqrt((1-tailDecay)*rate);
                riseAlpha=1-Math.Exp(-2*Math.PI*Math.Min(rate*.4,envelope.riseCutoff)/rate);
                noise=(uint)(e.seed^(e.seed>>32));if(noise==0)noise=117;
                double strength=Math.Sqrt(e.acousticJ);
                int kind=envelope.kind;
                uint bits=(uint)(e.seed^(e.seed>>32));int variant=(int)(bits%Variants);
                source=bank[kind*Variants+variant];other=bank[kind*Variants+(variant+1)%Variants];blend=((bits>>8)&255)/255.0;
                double u=((bits>>16)&65535)/65535.0;
                // Geometry/heat-release times set duration; seeded perturbations only
                // texture it. Mode names and RPM do not select a louder/sharper bang.
                double stretch=envelope.stretch*(1+.06*(u-.5));
                step=48000.0/rate/stretch;
                double sharp=DragMath.Clamp(.002/Math.Max(.00025,e.riseSeconds),.3,2);
                depth=1+.12*(u-.5);bite=sharp*(1+.12*(1-u));grain=.8+.4*DragMath.Clamp(e.flowMps/100,0,1);
                // Energy-normalized excitation: stretching the same explosion cannot
                // create additional acoustic energy merely by making it longer.
                double energy=(1-blend)*(1-blend)*bankEnergy[kind*Variants+variant]+blend*blend*bankEnergy[kind*Variants+(variant+1)%Variants]+2*blend*(1-blend)*bankCross[kind*Variants+variant];
                voiceGain=profile.popGain*.045/Math.Sqrt(Math.Max(1e-9,energy*stretch));targetAmplitude=strength*voiceGain;amplitude=0;
                path=e.zone;double at=e.position;
                turbine=profile.turboCount>0&&at*length<.45;
                // Snapshot boost is the existing turbine-loading proxy. It is not
                // a measured exhaust MAP value; pressure rise is carried separately.
                double boost=Math.Max(0,e.turboBoost);
                turbineAlpha=1-Math.Exp(-2*Math.PI*1700/(1+.8*boost)/rate);
                turbineHighAlpha=1-Math.Exp(-2*Math.PI*Math.Min(4800/(1+.3*boost),rate*.4)/rate);
                turbineGain=Math.Pow(10,-(1.5+3*boost)/20);
                travel=(int)Math.Round(rate*Math.Max(0,.98-at)*length/Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,300,1500)));
                start+=travel/(double)rate;
                taps1=(int)(rate*(.003+Math.Max(0,.98-at)*.007));taps2=(int)(rate*.009);taps3=(int)(rate*.0195);
                room1=.07;room2=.082;
            }
            public bool CanContinue(ExhaustMixtureModel.Event e) => active && frontId==e.frontId && path==e.zone && e.time>=lastChemical;
            public void Continue(ExhaustMixtureModel.Event e)
            {
                lastChemical=e.time;
                // Pressure-building heat release strengthens the original front
                // continuously. Later burning excites its tail, not another bang.
                if(e.time-frontStart<=frontRise){frontEnergy+=e.acousticJ;targetAmplitude=Math.Sqrt(frontEnergy)*voiceGain;}
                else tailEnergy+=e.acousticJ;
            }
            public double Sample(double clock)
            {
                if(!active||clock<start)return 0;
                amplitude+=amplitudeAlpha*(targetAmplitude-amplitude);
                int at=(int)position;double dry=0;
                if(at<source.Length-1)
                {double f=position-at;dry=(source[at]+(source[at+1]-source[at])*f)*(1-blend)+(other[at]+(other[at+1]-other[at])*f)*blend;}
                else if(position>source.Length+48000*.10&&clock-lastChemical>.12&&tailEnergy<1e-9){active=false;return 0;}
                position+=step;
                // Continuous reaction energy excites a diffuse tail, not another shock front.
                noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
                double turbulent=noise/(double)uint.MaxValue*2-1;tailLow+=midAlpha*(turbulent-tailLow);
                double tail=Math.Sqrt(tailEnergy)*tailGain*tailLow;tailEnergy*=tailDecay;
                bass+=bassAlpha*(dry-bass);mid+=midAlpha*(dry-mid);
                double value=depth*bass+grain*(mid-bass)+bite*(dry-mid);
                value+=amplitude>1e-15?tail/amplitude:0;
                riseLow+=riseAlpha*(value-riseLow);value=riseLow;
                if(turbine){turbineLow+=turbineAlpha*(value-turbineLow);turbineHigh+=turbineHighAlpha*(value-turbineHigh);value=turbineGain*(.77*turbineLow+.15*turbineHigh);}
                value=paths[path].Process(value);soft+=softAlpha*(value-soft);value=.83*value+.17*soft;
                scatter+=scatterAlpha*(value-scatter);history[cursor]=scatter;
                value+=.15*history[(cursor-taps1)&4095]-.094*history[(cursor-taps2)&4095]+.073*history[(cursor-taps3)&4095];
                env0*=decay0;env1*=decay1;env2*=decay2;
                value+=.4*.9*(.28*modes[0].Sample(value)*env0+.19*modes[1].Sample(value)*env1+.115*modes[2].Sample(value)*env2);
                // Low-level outlet-space reflections retained from the preview path.
                value+=room1*history[(cursor-(int)(rate*.011))&4095]+room2*history[(cursor-(int)(rate*.020))&4095];
                cursor=(cursor+1)&4095;return value*amplitude;
            }
            public void Clear()
            {
                active=false;position=bass=mid=turbineLow=turbineHigh=soft=scatter=riseLow=0;cursor=0;env0=env1=env2=1;tailEnergy=tailLow=0;
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
                // Preserve weak reactions too: numerical substepping must not turn
                // a sustained burn silent through a per-packet loudness threshold.
                if(e.acousticJ<1e-12){Inaudible++;continue;}
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
