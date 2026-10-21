using System;
namespace DragLab.Domain
{
    // Live pressure voices plus installed, source-relative paths; no canned burst bank.
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
        const int VoiceCount = 64;
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
            public double start;
            readonly ExhaustFilter[] paths = new ExhaustFilter[8];
            readonly Mode[,] walls = new Mode[8,2];
            readonly double[] wallGains = new double[8],outletGains = new double[8];
            public double WallSample {get;private set;}
            public double OutletSample {get;private set;}
            readonly int rate;
            readonly ReactivePressureBurst reaction;
            readonly double bassAlpha, midAlpha, softAlpha, scatterAlpha;
            readonly double[] history = new double[4096];
            int cursor, path, travel;
            double bass,mid,turbineLow,turbineHigh,turbineAlpha,turbineHighAlpha,turbineGain,soft,scatter;
            double lastChemical,voiceGain,riseLow,riseAlpha;
            int frontId;
            bool turbine;
            int taps1,taps2,taps3;
            readonly double length;
            public Voice(EngineSoundProfile profile,int rate)
            {
                this.rate=rate;reaction=new ReactivePressureBurst(rate,profile.popDecay,profile.popSharpness);
                length=1.1+(profile.turboCount>0?.18:0)+.8;
                foreach(var p in profile.exhaustSystem.parts)length+=RuntimeExhaustState.PartLength(p);
                for(int zone=0;zone<8;zone++)
                {
                    double at=(zone+.5)/8,cursor=1.1+(profile.turboCount>0?.18:0);
                    var setup=profile.exhaustSystem.Copy();
                    for(int i=0;i<setup.parts.Length;i++)
                    {double end=cursor+RuntimeExhaustState.PartLength(setup.parts[i]);setup.parts[i].amount*=(float)DragMath.Clamp((end-at*length)/(end-cursor),0,1);cursor=end;}
                    paths[zone]=new ExhaustFilter(setup,profile.exhaust);paths[zone].Configure(rate);
                    // Local wall radiation is parallel to the outlet, not passed
                    // through every downstream silencer. Authored resonance and
                    // component kind stand in for unavailable shell material data.
                    double nearest=double.MaxValue,hz=160,coupling=.012;cursor=1.1+(profile.turboCount>0?.18:0);
                    foreach(var part in profile.exhaustSystem.parts)
                    {
                        double extent=RuntimeExhaustState.PartLength(part),distance=Math.Abs(at*length-(cursor+extent*.5));cursor+=extent;
                        if(distance>=nearest||part.amount<=0)continue;
                        nearest=distance;hz=part.resonanceHz;
                        double shell=part.kind==ExhaustPartKind.ChamberedSilencer?.07:part.kind==ExhaustPartKind.AbsorptiveSilencer?.045:part.kind==ExhaustPartKind.Resonator?.035:.018;
                        coupling=.012+shell*part.amount/(1+distance*distance);
                    }
                    walls[zone,0]=new Mode(DragMath.Clamp(hz,90,420),.55,rate);
                    walls[zone,1]=new Mode(DragMath.Clamp(hz*2.7,270,1250),.65,rate);
                    wallGains[zone]=Math.Sqrt(coupling);outletGains[zone]=Math.Sqrt(1-coupling);
                }
                bassAlpha=1-Math.Exp(-2*Math.PI*450/rate);midAlpha=1-Math.Exp(-2*Math.PI*2550/rate);
                softAlpha=1-Math.Exp(-2*Math.PI*Math.Min(6000,rate*.4)/rate);scatterAlpha=1-Math.Exp(-2*Math.PI*Math.Min(3600,rate*.4)/rate);
            }
            public void Begin(ExhaustMixtureModel.Event e,EngineSoundProfile profile)
            {
                Clear(); active=true; start=lastChemical=e.time;frontId=e.frontId;
                reaction.Begin(e);
                var envelope=Describe(e,profile);
                riseAlpha=1-Math.Exp(-2*Math.PI*Math.Min(rate*.4,envelope.riseCutoff)/rate);
                voiceGain=profile.popGain*.35;
                path=e.zone;double at=e.position;
                turbine=profile.turboCount>0&&at*length<.45;
                // Snapshot boost is the existing turbine-loading proxy. It is not
                // a measured exhaust MAP value; pressure rise is carried separately.
                double boost=Math.Max(0,e.turboBoost);
                turbineAlpha=1-Math.Exp(-2*Math.PI*1700/(1+.8*boost)/rate);
                turbineHighAlpha=1-Math.Exp(-2*Math.PI*Math.Min(4800/(1+.3*boost),rate*.4)/rate);
                turbineGain=Math.Pow(10,-(1.5+3*boost)/20);
                travel=(int)Math.Round(rate*Math.Max(0,.98-at)*length/Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,300,1500)));
                taps1=(int)(rate*(.003+Math.Max(0,.98-at)*.007));taps2=(int)(rate*.009);taps3=(int)(rate*.0195);
            }
            public bool CanContinue(ExhaustMixtureModel.Event e) => active && frontId==e.frontId && path==e.zone && e.time>=lastChemical;
            public void Continue(ExhaustMixtureModel.Event e)
            {
                lastChemical=e.time;
                reaction.Excite(e);
            }
            public double Sample(double clock)
            {
                WallSample=OutletSample=0;
                if(!active||clock<start)return 0;
                double dry=reaction.Sample();
                if(clock-lastChemical>.8&&reaction.Quiet){active=false;return 0;}
                WallSample=wallGains[path]*(.8*walls[path,0].Sample(dry)+.3*walls[path,1].Sample(dry))*voiceGain;
                bass+=bassAlpha*(dry-bass);mid+=midAlpha*(dry-mid);
                double value=bass+.9*(mid-bass)+.85*(dry-mid);
                riseLow+=riseAlpha*(value-riseLow);value=riseLow;
                if(turbine){turbineLow+=turbineAlpha*(value-turbineLow);turbineHigh+=turbineHighAlpha*(value-turbineHigh);value=turbineGain*(.77*turbineLow+.15*turbineHigh);}
                value=paths[path].Process(value);soft+=softAlpha*(value-soft);value=.83*value+.17*soft;
                scatter+=scatterAlpha*(value-scatter);history[cursor]=.9*value+.1*scatter;
                int arrival=cursor-travel;
                value=history[arrival&4095]+.15*history[(arrival-taps1)&4095]-.094*history[(arrival-taps2)&4095]+.073*history[(arrival-taps3)&4095];
                OutletSample=value*outletGains[path]*voiceGain;
                cursor=(cursor+1)&4095;return OutletSample+WallSample;
            }
            public void Clear()
            {
                active=false;bass=mid=turbineLow=turbineHigh=soft=scatter=riseLow=0;cursor=0;reaction.Clear();
                WallSample=OutletSample=0;
                Array.Clear(history,0,history.Length);foreach(var filter in paths)filter.Clear();foreach(var mode in walls)mode.Clear();
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
        public double LastWallSample {get;private set;}
        public double LastOutletSample {get;private set;}
        public AfterfireAudioSynth(EngineSoundProfile profile,int rate)
        {
            this.profile=profile;this.rate=rate;
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
            LastWallSample=LastOutletSample=0;
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
            double value=0;foreach(var v in voices)if(v.active){value+=v.Sample(clock);LastWallSample+=v.WallSample;LastOutletSample+=v.OutletSample;}
            // Presentation time advances independently; the simulation owns all event times.
            clock+=1.0/rate;
            return value;
        }
        public void Clear(){foreach(var voice in voices)voice.Clear();source=null;cursor=0;clock=0;LastWallSample=LastOutletSample=0;}
    }
}
