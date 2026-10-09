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
        readonly double[] level=new double[4],floor=new double[4],body1=new double[4],body2=new double[4],edge1=new double[4],edge2=new double[4];
        readonly double[] texture=new double[4],resonance1=new double[4],resonance2=new double[4];
        readonly double[] b0=new double[4],a1=new double[4],a2=new double[4];
        readonly double[] upper1=new double[4],upper2=new double[4],ub0=new double[4],ua1=new double[4],ua2=new double[4];
        readonly double[] squealLevel=new double[4];
        readonly uint[] random={0x28a7c5u,0x37ac51u,0x9df85u,0x982ddfu};
        double gain;
        public void Fill(float[] data,int channels,int rate,TireSoundFrame frame,double master=1)
        {
            if(rate<=0||channels<=0||frame==null){Clear();Array.Clear(data,0,data.Length);return;}
            double attack=1-Math.Exp(-1.0/(rate*.035)),release=1-Math.Exp(-1.0/(rate*.16));
            double floorSmooth=1-Math.Exp(-2*Math.PI*65/rate),bodySmooth=1-Math.Exp(-2*Math.PI*500/rate);
            double edgeSmooth=1-Math.Exp(-2*Math.PI*3400/rate),textureSmooth=1-Math.Exp(-2*Math.PI*22/rate),masterSmooth=1-Math.Exp(-1.0/(rate*.008));
            double target=double.IsNaN(master)||double.IsInfinity(master)?0:DragMath.Clamp(master,0,1);
            double moving=DragMath.Clamp(frame.speed/22,0,1);
            for(int w=0;w<4;w++) {
                // Noise excites a prominent rubber squeal and a rough upper resonance.
                // No independent pure tones or fixed per-wheel musical pitch offsets.
                double center=1150+moving*420+Math.Min(170,frame.Wheel(w).slidingSpeed*6);
                double angle=2*Math.PI*center/rate,alpha=Math.Sin(angle)/(2*9),normal=1+alpha;
                b0[w]=alpha/normal;a1[w]=-2*Math.Cos(angle)/normal;a2[w]=(1-alpha)/normal;
                angle=2*Math.PI*center*1.8/rate;alpha=Math.Sin(angle)/(2*4.5);normal=1+alpha;
                ub0[w]=alpha/normal;ua1[w]=-2*Math.Cos(angle)/normal;ua2[w]=(1-alpha)/normal;
            }
            for(int n=0;n<data.Length;n+=channels) {
                double left=0,right=0;
                for(int w=0;w<4;w++) {
                    var tire=frame.Wheel(w);double demand=frame.active?tire.intensity*frame.volume:0;
                    level[w]+=(demand-level[w])*(demand>level[w]?attack:release);
                    double squealDemand=frame.active?DragMath.Clamp((tire.intensity-.06)/.5,0,1):0;
                    squealLevel[w]+=(squealDemand-squealLevel[w])*(squealDemand>squealLevel[w]?attack:release);
                    random[w]^=random[w]<<13;random[w]^=random[w]>>17;random[w]^=random[w]<<5;
                    double noise=random[w]/(double)uint.MaxValue*2-1;
                    floor[w]+=(noise-floor[w])*floorSmooth;
                    body1[w]+=(noise-body1[w])*bodySmooth;body2[w]+=(body1[w]-body2[w])*bodySmooth;
                    edge1[w]+=(noise-edge1[w])*edgeSmooth;edge2[w]+=(edge1[w]-edge2[w])*edgeSmooth;
                    texture[w]+=(noise-texture[w])*textureSmooth;
                    double resonance=b0[w]*edge2[w]+resonance1[w];
                    resonance1[w]=-a1[w]*resonance+resonance2[w];resonance2[w]=-b0[w]*edge2[w]-a2[w]*resonance;
                    double upper=ub0[w]*edge2[w]+upper1[w];
                    upper1[w]=-ua1[w]*upper+upper2[w];upper2[w]=-ub0[w]*edge2[w]-ua2[w]*upper;
                    // Keep contact texture underneath the screech, without a continuous broadband hiss bed.
                    double rubber=(body2[w]-floor[w])*.12;
                    double squeal=(resonance*5.5+upper*1.9)*squealLevel[w];
                    double voice=(rubber+squeal)*(1+texture[w]*.8)*.24*level[w];
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
        public void Clear()
        {
            Array.Clear(level,0,4);Array.Clear(floor,0,4);Array.Clear(body1,0,4);Array.Clear(body2,0,4);
            Array.Clear(edge1,0,4);Array.Clear(edge2,0,4);Array.Clear(texture,0,4);Array.Clear(resonance1,0,4);Array.Clear(resonance2,0,4);
            Array.Clear(upper1,0,4);Array.Clear(upper2,0,4);
            Array.Clear(squealLevel,0,4);
        }
    }
}
