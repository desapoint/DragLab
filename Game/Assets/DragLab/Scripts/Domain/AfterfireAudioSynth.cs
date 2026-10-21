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
            double length=RuntimeExhaustState.PathLength(profile);
            double c=Math.Sqrt(1.34*287*DragMath.Clamp(e.gasK,250,2500));
            double seconds=Math.Max(.001,e.burnSeconds)+6*Math.Max(.00025,e.riseSeconds)+4*length*Math.Max(.05,1-e.position)/c;
            return new EventEnvelope { seconds=seconds,
                stretch=DragMath.Clamp(seconds/.035,.25,4)*DragMath.Clamp(profile.popDecay,.25,3)/Math.Pow(DragMath.Clamp(profile.popSharpness,.25,2.5),.25),
                riseCutoff=2000+.6/Math.Max(.00025,e.riseSeconds),
                kind=e.pressurePa>1000&&e.riseSeconds<.0015?2:e.acousticJ<.03?0:1 };
        }
        const int VoiceCount = 64;
        sealed class Voice
        {
            public bool active;
            public double start;
            readonly AfterfireLabSource reaction;
            readonly AfterfireLabSettings hardware;
            readonly AfterfirePipeResponse pipe;
            double lastChemical,voiceGain;
            int frontId,path;
            public double WallSample {get;private set;}
            public double OutletSample {get;private set;}
            public int PulseOverflows=>reaction.PulseOverflows;
            public Voice(EngineSoundProfile profile,int rate)
            {
                reaction=new AfterfireLabSource(rate);hardware=AfterfireLabSettings.Hardware(profile);
                pipe=new AfterfirePipeResponse(profile,rate);
            }
            public void Begin(ExhaustMixtureModel.Event e,EngineSoundProfile profile)
            {
                WallSample=OutletSample=0;active=true;start=lastChemical=e.time;frontId=e.frontId;path=e.zone;
                reaction.Begin(AfterfireLabSettings.Reaction(hardware,profile,e),e);pipe.Begin(e,Describe(e,profile).riseCutoff);
                voiceGain=profile.popGain*.35;
            }
            public bool CanContinue(ExhaustMixtureModel.Event e) => active && frontId==e.frontId && path==e.zone && e.time>=lastChemical;
            public void Continue(ExhaustMixtureModel.Event e)
            {
                lastChemical=e.time;reaction.Excite(e);
            }
            public double Sample(double clock)
            {
                WallSample=OutletSample=0;
                if(!active||clock<start)return 0;
                double dry=reaction.Sample();
                if(clock-lastChemical>.8&&reaction.Quiet){active=false;return 0;}
                double value=pipe.Sample(dry,reaction.PressureSample)*voiceGain;
                WallSample=pipe.Wall*voiceGain;OutletSample=pipe.Outlet*voiceGain;
                return value;
            }
            public void Clear()
            {
                active=false;WallSample=OutletSample=0;reaction.Clear();pipe.Clear();
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
        public long PressurePulseOverflows {get {long count=0;foreach(var v in voices)count+=v.PulseOverflows;return count;} }
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
