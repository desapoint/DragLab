using System;
namespace DragLab.Domain
{
    // Live pressure voices plus installed, source-relative paths; no canned burst bank.
    // Chemistry and event production stay on the simulation owner; callback only reads stamped history.
    public sealed class AfterfireAudioSynth
    {
        public const double ReferenceGain=.25;
        public struct EventEnvelope { public double seconds, stretch, riseCutoff; public int kind; }
        public static EventEnvelope Describe(ExhaustMixtureModel.Event e, EngineSoundProfile profile)
        {
            double length=RuntimeExhaustState.PathLength(profile);
            double c=Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,250,2500));
            double seconds=Math.Max(.001,e.burnSeconds)+6*Math.Max(.00025,e.riseSeconds)+4*length*Math.Max(.05,1-e.position)/c;
            return new EventEnvelope { seconds=seconds,
                stretch=DragMath.Clamp(seconds/.035,.25,4)*DragMath.Clamp(profile.popDecay,.25,3)/Math.Pow(DragMath.Clamp(profile.popSharpness,.25,2.5),.25),
                riseCutoff=2000+.6/Math.Max(.00025,e.riseSeconds),
                kind=e.pressurePa>1000&&e.riseSeconds<.0015?2:e.acousticJ<.03?0:1 };
        }
#if UNITY_EDITOR
        public bool AuditEnabled;
        public double AuditRaw,AuditShaped,AuditAttenuated,AuditRawRouted,AuditRawDelayed,AuditUnattenuated;
#endif
        const int VoiceCount=64;
        sealed class Burst
        {
            public bool active;
            public int id,references;
            public double start;
            public uint seed;
            public void Begin(ExhaustMixtureModel.Event e)
            {
                active=true;id=e.frontId;references=0;start=e.time;seed=(uint)(e.seed^(e.seed>>32));
            }
            public void Clear(){active=false;references=0;}
        }
        sealed class Voice
        {
            public bool active;
            public double start;
            public Burst burst;
            readonly AfterfirePipeResponse pipe;
            public readonly AfterfireLabSource shape;
#if UNITY_EDITOR
            public AfterfirePipeResponse auditPipe;
            public AfterfireLabSource auditShape;
            public double auditRaw,auditShaped,auditAttenuated,auditRawRouted,auditRawDelayed,auditUnattenuated;
#endif
            double lastChemical,voiceGain;
            int path;
            public double WallSample {get;private set;}
            public double OutletSample {get;private set;}
            public Voice(EngineSoundProfile profile,int rate)
            {
                pipe=new AfterfirePipeResponse(profile,rate);shape=new AfterfireLabSource(rate);
            }
            public void Begin(ExhaustMixtureModel.Event e,Burst owner,EngineSoundProfile profile,AfterfireLabSettings settings)
            {
                Clear();burst=owner;burst.references++;active=true;start=lastChemical=e.time;path=e.zone;
                pipe.Begin(e);voiceGain=profile.popGain*ReferenceGain;
                shape.BeginReactive(settings,owner.seed);Continue(e);
            }
            public bool CanContinue(ExhaustMixtureModel.Event e)=>active&&burst.id==e.frontId&&path==e.zone&&e.time>=lastChemical;
            public void Continue(ExhaustMixtureModel.Event e)
            {
                lastChemical=e.time;shape.React(e);
#if UNITY_EDITOR
                auditShape?.React(e);
#endif
            }
            public double Sample(double clock)
            {
                WallSample=OutletSample=0;if(!active||clock<start)return 0;
                if(shape.Quiet&&clock-lastChemical>.2){Clear();return 0;}
                double dry=shape.Sample(),pressure=shape.PressureSample;
                double value=pipe.Sample(dry,pressure)*voiceGain;
                WallSample=pipe.Wall*voiceGain;OutletSample=pipe.Outlet*voiceGain;
#if UNITY_EDITOR
                if(auditPipe!=null)
                {
                    double raw=auditShape.Sample();auditRaw=raw*voiceGain;auditShaped=dry*voiceGain;
                    auditAttenuated=auditShaped*pipe.OutletScale;
                    auditRawRouted=auditPipe.Sample(raw,auditShape.PressureSample)*voiceGain;
                    auditRawDelayed=auditPipe.Outlet*voiceGain;
                    auditUnattenuated=OutletSample/pipe.OutletScale;
                }
#endif
                return value;
            }
            public void Clear()
            {
                if(active&&burst!=null)burst.references--;
#if UNITY_EDITOR
                auditPipe?.Clear();auditShape?.Clear();auditRaw=auditShaped=auditAttenuated=auditRawRouted=auditRawDelayed=auditUnattenuated=0;
#endif
                active=false;burst=null;WallSample=OutletSample=0;pipe.Clear();shape.Clear();
            }
        }
        readonly Voice[] voices=new Voice[VoiceCount];
        readonly Burst[] bursts=new Burst[VoiceCount];
        readonly AfterfireHardwareAcoustics hardware;
        readonly EngineSoundProfile profile;
        readonly int rate;
        ExhaustMixtureModel source,retained;
        int retainedCursor,cursor;
        double clock;
        public double Clock=>clock;
        public long Delivered {get;private set;}
        public long HistoryOverflows {get;private set;}
        public long TailSteals {get;private set;}
        public long Inaudible {get;private set;}
        public long ContinuedReactions {get;private set;}
        public long StartedBursts {get;private set;}
        public double FundedAcousticEnergy {get;private set;}
        public long PressurePulseOverflows {get {long n=0;foreach(var v in voices)n+=v.shape.PulseOverflows;return n;}}
        public double LastWallSample {get;private set;}
        public double LastOutletSample {get;private set;}
        public AfterfireAudioSynth(EngineSoundProfile profile,int rate,AfterfireLabSettings? calibration=null)
        {
            this.profile=profile;this.rate=rate;hardware=new AfterfireHardwareAcoustics(profile,calibration);
            for(int i=0;i<VoiceCount;i++){voices[i]=new Voice(profile,rate);bursts[i]=new Burst();}
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
        // All chemistry events, including weak continuations, enter here. Sound
        // Each reacting location has one ongoing source, not an attack per
        // numerical packet. Its reservoirs respond to real continuing combustion.
        void Deliver(ExhaustMixtureModel.Event e)
        {
            Delivered++;if(e.acousticJ<1e-12){Inaudible++;return;}FundedAcousticEnergy+=e.acousticJ;
            foreach(var v in voices)if(v.CanContinue(e)){v.Continue(e);ContinuedReactions++;return;}
            Burst owner=null;foreach(var b in bursts)if(b.active&&b.id==e.frontId){owner=b;break;}
            if(owner==null)
            {
                foreach(var b in bursts)if(!b.active||b.references==0){owner=b;break;}
                if(owner==null)
                {
                    owner=bursts[0];foreach(var b in bursts)if(b.start<owner.start)owner=b;
                    foreach(var v in voices)if(v.active&&ReferenceEquals(v.burst,owner)){v.Clear();TailSteals++;}
                }
                owner.Begin(e);StartedBursts++;
            }
            Voice free=null;foreach(var v in voices)if(!v.active){free=v;break;}
            if(free==null){free=voices[0];foreach(var v in voices)if(v.start<free.start)free=v;TailSteals++;}
            free.Begin(e,owner,profile,hardware.At(e));
#if UNITY_EDITOR
            if(AuditEnabled)
            {
                if(free.auditPipe==null)free.auditPipe=new AfterfirePipeResponse(profile,rate);
                if(free.auditShape==null)free.auditShape=new AfterfireLabSource(rate);
                free.auditShape.BeginReactive(AfterfireLabSettings.Hardware(profile),owner.seed);free.auditShape.React(e);free.auditPipe.Begin(e);
            }
#endif
        }
        public double Sample()
        {
            LastWallSample=LastOutletSample=0;
#if UNITY_EDITOR
            AuditRaw=AuditShaped=AuditAttenuated=AuditRawRouted=AuditRawDelayed=AuditUnattenuated=0;
#endif
            if(source==null)return 0;
            int end=source.Sequence;
            if(end-cursor>=ExhaustMixtureModel.HistoryCapacity){HistoryOverflows+=end-cursor-ExhaustMixtureModel.HistoryCapacity+1;cursor=end-ExhaustMixtureModel.HistoryCapacity+1;}
            while(cursor<end)
            {
                if(!source.Read(cursor+1,out var e)||e.time>clock)break;
                cursor++;Deliver(e);
            }
            double value=0;
            foreach(var v in voices)if(v.active)
            {
                value+=v.Sample(clock);LastWallSample+=v.WallSample;LastOutletSample+=v.OutletSample;
#if UNITY_EDITOR
                if(AuditEnabled){AuditRaw+=v.auditRaw;AuditShaped+=v.auditShaped;AuditAttenuated+=v.auditAttenuated;AuditRawRouted+=v.auditRawRouted;AuditRawDelayed+=v.auditRawDelayed;AuditUnattenuated+=v.auditUnattenuated;}
#endif
            }
            foreach(var b in bursts)if(b.active&&b.references==0)b.Clear();
            clock+=1.0/rate;return value;
        }
        public void Clear()
        {
            foreach(var v in voices)v.Clear();foreach(var b in bursts)b.Clear();source=null;cursor=0;clock=0;LastWallSample=LastOutletSample=0;
        }
    }
}
