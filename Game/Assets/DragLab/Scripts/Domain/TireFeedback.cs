using System;

namespace DragLab.Domain
{
    // Presentation derives from contact, never engine torque demand. Braking skids count too.
    public struct TireFeedback
    {
        public double intensity, slidingSpeed, slip;
        public static TireFeedback Read(CarState state,int wheel)
        {
            if(state==null||state.contact==null||!state.wheelContactInitialized||wheel<0||wheel>3)return default;
            double relative=Math.Abs(state.contact.omega[wheel]*state.wheelRadiusM-state.speed);
            double ratio=Math.Abs(state.contact.slip[wheel]);
            double force=Math.Abs(state.contact.force[wheel]);
            if(!Finite(relative)||!Finite(ratio)||!Finite(force)||relative<.3||ratio<.045||force<40)return default;
            double gate=DragMath.Clamp((ratio-.045)/.10,0,1)*DragMath.Clamp((relative-.3)/1.2,0,1);
            return new TireFeedback{slidingSpeed=relative,slip=ratio,intensity=gate*DragMath.Clamp(Math.Sqrt(relative*force/24000),0,1)};
        }
        static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        public static TireFeedback Authored(CarState state,int wheel,DriveType drive)
        {
            if(wheel<2&&drive==DriveType.RWD||wheel>=2&&drive==DriveType.FWD)return default;
            double slip=Math.Abs(state.slip);
            if(!Finite(slip)||!Finite(state.speed))return default;
            return new TireFeedback{slip=slip,slidingSpeed=slip*Math.Max(4,Math.Abs(state.speed)),intensity=DragMath.Clamp((slip-.045)*1.5,0,1)};
        }
    }
    // Immutable publication between the main and DSP threads; no shared mutable wheel arrays.
    public sealed class TireSoundFrame
    {
        public readonly TireFeedback fl,fr,rl,rr;
        public readonly double speed,volume;
        public readonly bool active;
        public TireSoundFrame(TireFeedback fl,TireFeedback fr,TireFeedback rl,TireFeedback rr,double speed,double volume,bool active=true)
        {this.fl=fl;this.fr=fr;this.rl=rl;this.rr=rr;this.speed=Math.Abs(speed);this.volume=DragMath.Clamp(volume,0,1);this.active=active;}
        public TireFeedback Wheel(int i){return i==0?fl:i==1?fr:i==2?rl:rr;}
        public static TireSoundFrame From(CarState state,bool active,double volume,DriveType drive=DriveType.FWD)
        {
            if(!state.wheelContactInitialized) {
                // Acoustic-only bench has an explicitly authored slip slider, not road contact.
                return new TireSoundFrame(TireFeedback.Authored(state,0,drive),TireFeedback.Authored(state,1,drive),TireFeedback.Authored(state,2,drive),TireFeedback.Authored(state,3,drive),state.speed,volume,active);
            }
            return new TireSoundFrame(TireFeedback.Read(state,0),TireFeedback.Read(state,1),TireFeedback.Read(state,2),TireFeedback.Read(state,3),state.speed,volume,active);
        }
    }
    public sealed class TireSoundSynth
    {
        readonly double[] level=new double[4],phase=new double[4],mod=new double[4],low=new double[4],high=new double[4];
        readonly uint[] random={0x28a7c5u,0x37ac51u,0x9df85u,0x982ddfu};
        double gain;
        public void Fill(float[] data,int channels,int rate,TireSoundFrame frame,double master=1)
        {
            if(rate<=0||channels<=0||frame==null){Clear();Array.Clear(data,0,data.Length);return;}
            double attack=1-Math.Exp(-1.0/(rate*.035)),release=1-Math.Exp(-1.0/(rate*.16));
            double slow=1-Math.Exp(-2*Math.PI*280/rate),fast=1-Math.Exp(-2*Math.PI*2600/rate),masterSmooth=1-Math.Exp(-1.0/(rate*.008));
            double target=double.IsNaN(master)||double.IsInfinity(master)?0:DragMath.Clamp(master,0,1);
            for(int n=0;n<data.Length;n+=channels) {
                double left=0,right=0;
                for(int w=0;w<4;w++) {
                    var tire=frame.Wheel(w);double demand=frame.active?tire.intensity*frame.volume:0;
                    level[w]+=(demand-level[w])*(demand>level[w]?attack:release);
                    random[w]^=random[w]<<13;random[w]^=random[w]>>17;random[w]^=random[w]<<5;
                    double noise=random[w]/(double)uint.MaxValue*2-1;
                    low[w]+=(noise-low[w])*slow;high[w]+=(noise-high[w])*fast;
                    mod[w]=(mod[w]+(9+w*1.7)/rate)%1;
                    double hz=650+w*71+Math.Min(450,tire.slidingSpeed*15)+90*Math.Sin(2*Math.PI*mod[w]);
                    phase[w]=(phase[w]+hz/rate)%1;
                    double moving=DragMath.Clamp(frame.speed/14,0,1);
                    // Broad rubber scrub for burnouts, irregular squeal for moving slip; no fixed pure whistle.
                    double voice=((high[w]-low[w])*.75+low[w]*.4+Math.Sin(2*Math.PI*phase[w])*(.10+.13*moving))*.18*level[w];
                    double pan=w<2?-.12:.12;left+=voice*(1-pan);right+=voice*(1+pan);
                }
                gain+=(target-gain)*masterSmooth;
                if(Math.Abs(gain-target)<1e-7)gain=target;
                for(int c=0;c<channels;c++) {
                    double value=channels==1?(left+right)*.5:c%2==0?left:right;
                    data[n+c]=(float)(value/(1+Math.Abs(value))*gain);
                }
            }
        }
        public void Clear(){Array.Clear(level,0,4);Array.Clear(low,0,4);Array.Clear(high,0,4);}
    }
}
