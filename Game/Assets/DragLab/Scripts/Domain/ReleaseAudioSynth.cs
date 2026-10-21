using System;
namespace DragLab.Domain
{
    // Same BOV responses, independent overlapping voices and lossless bounded event publication.
    public sealed class ReleaseAudioSynth
    {
        readonly EngineSoundProfile profile;readonly int rate;
        readonly double[] ages=new double[16],strengths=new double[16],phases=new double[16],low=new double[16];
        RuntimeExhaustState source,retained;int cursor,retainedCursor;
        uint random=0x7a16b59u;
        public int Delivered {get;private set;}
        public int Overflows {get;private set;}
        public int TailSteals {get;private set;}
        public ReleaseAudioSynth(EngineSoundProfile profile,int rate){this.profile=profile;this.rate=rate;ClearVoices();}
        void ClearVoices(){for(int i=0;i<ages.Length;i++){ages[i]=10;low[i]=phases[i]=0;}}
        public void Connect(RuntimeExhaustState model)
        {
            if(ReferenceEquals(source,model))return;
            ClearVoices();
            if(model==null){retained=source;retainedCursor=cursor;source=null;return;}
            cursor=ReferenceEquals(retained,model)?retainedCursor:Math.Max(0,model.ReleaseSequence-255);retained=null;source=model;
            while(cursor<model.ReleaseSequence&&model.ReadRelease(cursor+1,out var prior)&&prior.time<model.mixture.Time-.04)cursor++;
        }
        double Noise(){random^=random<<13;random^=random>>17;random^=random<<5;return random/(double)uint.MaxValue*2-1;}
        public double Sample(double clock)
        {
            if(source==null||profile.turboCount==0||profile.fueling==FuelSystem.Diesel)return 0;
            int end=source.ReleaseSequence;
            if(end-cursor>=256){Overflows+=end-cursor-255;cursor=end-255;}
            while(cursor<end&&source.ReadRelease(cursor+1,out var e)&&e.time<=clock)
            {
                cursor++;Delivered++;int at=-1;
                for(int i=0;i<ages.Length;i++)if(ages[i]>=1.2){at=i;break;}
                if(at<0){at=0;for(int i=1;i<ages.Length;i++)if(ages[i]>ages[at])at=i;TailSteals++;}
                ages[at]=Math.Max(0,clock-e.time);strengths[at]=e.strength;low[at]=phases[at]=0;
            }
            double value=0,tau=2*Math.PI;
            for(int i=0;i<ages.Length;i++)
            {
                double age=ages[i];if(age>=1.2)continue;
                double envelope=Math.Min(1,age/.006)*Math.Exp(-age*(profile.valve==BlowOffType.Recirculated?9:6));
                double hz=Math.Min(Math.Max(600,(profile.valve==BlowOffType.Chirp?4500:2200)-age*2600),rate*.2);
                phases[i]=(phases[i]+hz/rate)%1;low[i]+=.09*(Noise()-low[i]);
                double air=profile.valve==BlowOffType.Recirculated?low[i]*.55:profile.valve==BlowOffType.Chirp?Math.Sin(tau*phases[i])*.35+low[i]*.12:profile.valve==BlowOffType.Flutter?(low[i]*.45+Math.Sin(tau*phases[i])*.18)*Math.Pow(.5+.5*Math.Sin(age*tau*(28-age*12)),3):low[i]*.75;
                value+=air*envelope*strengths[i]*profile.releaseGain;ages[i]+=1.0/rate;
            }
            return value;
        }
    }
}
