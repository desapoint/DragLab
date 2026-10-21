using System;

namespace DragLab.Domain
{
    // Common outdoor listener path for the complete vehicle mix, independent of
    // combustion/traction. Metres and seconds, not scaled race-view coordinates.
    public sealed class TracksidePropagation
    {
        public sealed class Scene
        {
            public readonly double temperatureK,humidity,pressurePa,reflectionScale,soundSpeed;
            readonly double a0,a1,a2,a3;
            public Scene(double temperatureK=293.15,double humidity=.5,double pressurePa=101325,double reflectionScale=1)
            {
                if(!Finite(temperatureK)||temperatureK<250||temperatureK>330||!Finite(humidity)||humidity<0||humidity>1||!Finite(pressurePa)||pressurePa<60000||pressurePa>120000||!Finite(reflectionScale)||reflectionScale<0||reflectionScale>1)throw new ArgumentException("Invalid acoustic atmosphere.");
                this.temperatureK=temperatureK;this.humidity=humidity;this.pressurePa=pressurePa;this.reflectionScale=reflectionScale;
                soundSpeed=Math.Sqrt(1.4*287.05*temperatureK);
                a0=AirAbsorptionDb(200,temperatureK,humidity,pressurePa);a1=AirAbsorptionDb(900,temperatureK,humidity,pressurePa);
                a2=AirAbsorptionDb(3500,temperatureK,humidity,pressurePa);a3=AirAbsorptionDb(10000,temperatureK,humidity,pressurePa);
            }
            Path Prepare(double dx,double lateral,double height,double listenerHeight,double reflection,double spread)
            {
                double distance=Math.Sqrt(dx*dx+lateral*lateral+(height-listenerHeight)*(height-listenerHeight))+spread;
                double left=Math.Sqrt((dx+.09)*(dx+.09)+lateral*lateral+(height-listenerHeight)*(height-listenerHeight));
                double right=Math.Sqrt((dx-.09)*(dx-.09)+lateral*lateral+(height-listenerHeight)*(height-listenerHeight));
                double gain=6/Math.Max(6,distance)*reflection,pan=DragMath.Clamp(dx/Math.Max(1,distance)*.75,-.85,.85);
                return new Path { distance=distance,leftSeconds=(left+spread)/soundSpeed,rightSeconds=(right+spread)/soundSpeed,
                    leftGain=Math.Sqrt(1-pan),rightGain=Math.Sqrt(1+pan),
                    g0=gain*Math.Pow(10,-a0*distance/20),g1=gain*Math.Pow(10,-a1*distance/20),
                    g2=gain*Math.Pow(10,-a2*distance/20),g3=gain*Math.Pow(10,-a3*distance/20) };
            }
            public Frame At(double longitudinal=0,double lateral=12,double sourceHeight=.55,double listenerHeight=1.65)
            {
                if(!Finite(longitudinal)||Math.Abs(longitudinal)>900||!Finite(lateral)||lateral<2||lateral>20||!Finite(sourceHeight)||sourceHeight<0||sourceHeight>4||!Finite(listenerHeight)||listenerHeight<.5||listenerHeight>5)throw new ArgumentException("Invalid trackside geometry.");
                return new Frame(this,longitudinal,lateral,sourceHeight,listenerHeight,
                    Prepare(longitudinal,lateral,sourceHeight,listenerHeight,1,0),
                    Prepare(longitudinal,lateral,-sourceHeight,listenerHeight,.52*reflectionScale,0),
                    Prepare(longitudinal,lateral+14,sourceHeight,listenerHeight,.23*reflectionScale,0),
                    Prepare(longitudinal,48-lateral,sourceHeight,listenerHeight,.13*reflectionScale,0),
                    Prepare(longitudinal-2,48-lateral,sourceHeight,listenerHeight,.055*reflectionScale,3),
                    Prepare(longitudinal+3,48-lateral,sourceHeight,listenerHeight,.035*reflectionScale,7));
            }
        }
        public struct Path { public double distance,leftSeconds,rightSeconds,leftGain,rightGain,g0,g1,g2,g3; }
        public sealed class Frame
        {
            public readonly Scene scene;
            public readonly double longitudinal,lateral,height,listenerHeight;
            public readonly Path direct,ground,barrier,stand,scatter1,scatter2;
            internal Frame(Scene scene,double x,double y,double h,double lh,Path a,Path b,Path c,Path d,Path e,Path f)
            {this.scene=scene;longitudinal=x;lateral=y;height=h;listenerHeight=lh;direct=a;ground=b;barrier=c;stand=d;scatter1=e;scatter2=f;}
            public Path At(int i)=>i==0?direct:i==1?ground:i==2?barrier:i==3?stand:i==4?scatter1:scatter2;
            public Frame TireHeight()=>scene.At(longitudinal,lateral,.08,listenerHeight);
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        // ISO 9613-1 pure-tone absorption, dB/m. Four smooth bands approximate its
        // frequency response in the time-domain renderer, not a blanket low pass.
        public static double AirAbsorptionDb(double hz,double kelvin,double humidity,double pascal)
        {
            double tr=kelvin/293.15,pr=pascal/101325;
            double h=humidity*100*Math.Pow(10,-6.8346*Math.Pow(273.16/kelvin,1.261)+4.6151)/pr;
            double oxygen=pr*(24+4.04e4*h*(.02+h)/(.391+h));
            double nitrogen=pr*Math.Pow(tr,-.5)*(9+280*h*Math.Exp(-4.17*(Math.Pow(tr,-1.0/3)-1)));
            return 8.686*hz*hz*(1.84e-11/pr*Math.Sqrt(tr)+Math.Pow(tr,-2.5)*(.01275*Math.Exp(-2239.1/kelvin)/(oxygen+hz*hz/oxygen)+.1068*Math.Exp(-3352/kelvin)/(nitrogen+hz*hz/nitrogen)));
        }
        sealed class State
        {
            public double dl,dr,l0,l1,l2,r0,r1,r2,g0,g1,g2,g3,gl,gr;
            public bool initialized;
            public void Clear(){dl=dr=l0=l1=l2=r0=r1=r2=g0=g1=g2=g3=gl=gr=0;initialized=false;}
        }
        readonly State[] paths=new State[6];
        readonly float[] left,right;
        readonly int rate,mask;
        public int SampleRate=>rate;
        readonly double lowAlpha,midAlpha,highAlpha,smooth;
        int cursor;
        bool dirty;
        public TracksidePropagation(int rate)
        {
            if(rate<8000||rate>192000)throw new ArgumentException("Unsupported acoustic rate.");
            this.rate=rate;int capacity=1;while(capacity<rate*4)capacity*=2;
            left=new float[capacity];right=new float[capacity];mask=capacity-1;
            for(int i=0;i<paths.Length;i++)paths[i]=new State();
            lowAlpha=1-Math.Exp(-2*Math.PI*400/rate);midAlpha=1-Math.Exp(-2*Math.PI*1800/rate);highAlpha=1-Math.Exp(-2*Math.PI*Math.Min(6500,rate*.4)/rate);
            smooth=1-Math.Exp(-1.0/(rate*.015));
        }
        double Read(float[] buffer,double delay)
        {
            double at=cursor-delay;int lower=(int)Math.Floor(at);double fraction=at-lower;
            return buffer[lower&mask]*(1-fraction)+buffer[(lower+1)&mask]*fraction;
        }
        public void Process(float[] pcm,int channels,Frame frame)
        {
            if(channels<1||pcm.Length%channels!=0)throw new ArgumentException("Invalid propagation block.");
            if(frame==null){Clear();return;}
            dirty=true;
            // All expensive atmosphere/geometry coefficients were prepared by the
            // owner before publication. The callback only delays, filters and mixes.
            for(int i=0;i<pcm.Length;i+=channels)
            {
                left[cursor]=pcm[i];right[cursor]=channels==1?pcm[i]:pcm[i+1];
                double l=0,r=0;
                for(int p=0;p<paths.Length;p++)
                {
                    var target=frame.At(p);var s=paths[p];
                    if(!s.initialized){s.dl=target.leftSeconds*rate;s.dr=target.rightSeconds*rate;s.g0=target.g0;s.g1=target.g1;s.g2=target.g2;s.g3=target.g3;s.gl=target.leftGain;s.gr=target.rightGain;s.initialized=true;}
                    // Continuous variable delay provides physical approach/recession
                    // pitch, bounded below sonic speed and without a second Doppler stage.
                    s.dl+=DragMath.Clamp(target.leftSeconds*rate-s.dl,-.65,.65);s.dr+=DragMath.Clamp(target.rightSeconds*rate-s.dr,-.65,.65);
                    s.g0+=(target.g0-s.g0)*smooth;s.g1+=(target.g1-s.g1)*smooth;s.g2+=(target.g2-s.g2)*smooth;s.g3+=(target.g3-s.g3)*smooth;
                    s.gl+=(target.leftGain-s.gl)*smooth;s.gr+=(target.rightGain-s.gr)*smooth;
                    double a=Read(left,s.dl),b=Read(right,s.dr);
                    s.l0+=lowAlpha*(a-s.l0);s.l1+=midAlpha*(a-s.l1);s.l2+=highAlpha*(a-s.l2);
                    s.r0+=lowAlpha*(b-s.r0);s.r1+=midAlpha*(b-s.r1);s.r2+=highAlpha*(b-s.r2);
                    l+=(s.g0*s.l0+s.g1*(s.l1-s.l0)+s.g2*(s.l2-s.l1)+s.g3*(a-s.l2))*s.gl;
                    r+=(s.g0*s.r0+s.g1*(s.r1-s.r0)+s.g2*(s.r2-s.r1)+s.g3*(b-s.r2))*s.gr;
                }
                pcm[i]=(float)(channels==1?(l+r)*.5:l);
                if(channels>1){pcm[i+1]=(float)r;for(int ch=2;ch<channels;ch++)pcm[i+ch]=(float)((l+r)*.5);}
                cursor=(cursor+1)&mask;
            }
        }
        public void Clear()
        {
            if(!dirty)return;
            Array.Clear(left,0,left.Length);Array.Clear(right,0,right.Length);cursor=0;foreach(var state in paths)state.Clear();dirty=false;
        }
    }
}
