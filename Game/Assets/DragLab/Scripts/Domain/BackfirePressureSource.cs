using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DragLab.Domain;
using C=System.Numerics.Complex;

namespace DragLab.Domain
{
    // Shared pressure/texture source. Prepare banks outside real-time playback; no simulation ownership.
    public static class BackfirePressureSource
    {
        public const int Rate=48000;
        public enum Cleanup { Light, LessTicking }
        public enum Texture { Reference, Damped, NoShell }
        sealed class Rng
        {
            ulong state;bool spare;double gaussian;
            public Rng(ulong seed){state=seed+0x9e3779b97f4a7c15UL;Next();}
            uint Next(){unchecked{ulong old=state;state=old*6364136223846793005UL+1442695040888963407UL;uint x=(uint)(((old>>18)^old)>>27);int r=(int)(old>>59);return (x>>r)|(x<<((-r)&31));}}
            public double U()=> (Next()+.5)/4294967296.0;
            public double U(double a,double b)=>a+(b-a)*U();
            public double N(){if(spare){spare=false;return gaussian;}double radius=Math.Sqrt(-2*Math.Log(U())),phase=2*Math.PI*U();gaussian=radius*Math.Sin(phase);spare=true;return radius*Math.Cos(phase);}
        }
        public static ulong Seed(long master,string config,int index,int tag)
        {unchecked{ulong h=14695981039346656037UL;foreach(byte b in Encoding.UTF8.GetBytes("draglab-v8:"+master.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+config+":"+index+":"+tag)){h^=b;h*=1099511628211UL;}return h;}}
        static double Clamp(double x,double a,double b)=>Math.Max(a,Math.Min(b,x));
        static double[] Polynomial(C[] roots)
        {var coefficients=new C[]{C.One};foreach(var root in roots){var next=new C[coefficients.Length+1];for(int i=0;i<coefficients.Length;i++){next[i]+=coefficients[i];next[i+1]-=root*coefficients[i];}coefficients=next;}return coefficients.Select(x=>x.Real).ToArray();}
        static C Product(IEnumerable<C> values){C p=C.One;foreach(var x in values)p*=x;return p;}
        static double[] Iir(double[] x,double[] b,double[] a)
        {int order=Math.Max(a.Length,b.Length)-1;var state=new double[order];var y=new double[x.Length];for(int i=0;i<x.Length;i++){double value=b[0]*x[i]+state[0];for(int j=1;j<order;j++)state[j-1]=(j<b.Length?b[j]*x[i]:0)-(j<a.Length?a[j]*value:0)+state[j];state[order-1]=(order<b.Length?b[order]*x[i]:0)-(order<a.Length?a[order]*value:0);y[i]=value;}return y;}
        // Butterworth low/high/band transformations + bilinear mapping, not a cascade approximation to bandpass.
        public static double[] Band(double[] x,double low=0,double high=0,int order=2)
        {
            var poles=Enumerable.Range(0,order).Select(i=>-C.Exp(C.ImaginaryOne*Math.PI*(-order+1+2*i)/(2*order))).ToArray();var zeros=new C[0];double gain=1;
            Func<double,double> warp=f=>2*Rate*Math.Tan(Math.PI*f/Rate);
            if(low>0&&high>0){double a=warp(low),b=warp(high),bw=b-a,w=Math.Sqrt(a*b);poles=poles.SelectMany(p=>{C half=p*bw/2,root=C.Sqrt(half*half-w*w);return new[]{half+root,half-root};}).ToArray();zeros=new C[order];gain=Math.Pow(bw,order);}
            else if(high>0){double w=warp(high);poles=poles.Select(p=>p*w).ToArray();gain=Math.Pow(w,order);}
            else{double w=warp(low);gain=(C.One/Product(poles.Select(p=>-p))).Real;poles=poles.Select(p=>w/p).ToArray();zeros=new C[order];}
            double fs2=2*Rate;gain*=(Product(zeros.Select(z=>fs2-z))/Product(poles.Select(p=>fs2-p))).Real;
            var digitalZeros=zeros.Select(z=>(fs2+z)/(fs2-z)).Concat(Enumerable.Repeat(new C(-1,0),poles.Length-zeros.Length)).ToArray();
            var digitalPoles=poles.Select(p=>(fs2+p)/(fs2-p)).ToArray();return Iir(x,Polynomial(digitalZeros).Select(v=>v*gain).ToArray(),Polynomial(digitalPoles));
        }
        public static double[] Peak(double[] x,double hz,double q)
        {double w=2*Math.PI*hz/Rate,gain=1/(1+Math.Tan(w/q/2));return Iir(x,new[]{1-gain,0,gain-1},new[]{1.0,-2*gain*Math.Cos(w),2*gain-1});}
        static double[] Unit(double[] x){double max=x.Max(v=>Math.Abs(v))+1e-9;for(int i=0;i<x.Length;i++)x[i]/=max;return x;}
        static double[] Noise(Rng rng,int n){var x=new double[n];for(int i=0;i<n;i++)x[i]=rng.N();return x;}
        static double[] Low(double[] x,double hz,int order=2)=>Band(x,0,hz,order);
        public static double[] Burst(ulong seed,string kind,Cleanup cleanup,Texture texture=Texture.Reference)
        {
            var rng=new Rng(seed);int n=(int)(.28*Rate);var direct=new double[n];var shock=new double[n];
            double grit=kind=="crack"?1.28:kind=="crackle"?.88:1,depth=kind=="crack"?1.06:kind=="crackle"?.68:1,metal=kind=="crack"?1.05:kind=="crackle"?.77:1;
            int tiny=kind=="crack"?4:kind=="crackle"?8:6;double shockAt=rng.U(.0017,.003),microGain=cleanup==Cleanup.Light?.72:.47,highMetal=cleanup==Cleanup.Light?.94:.84,pressureGain=cleanup==Cleanup.Light?1:1.025;
            var body=Unit(Band(Noise(rng,n),80,1600));var gritNoise=Unit(Band(Noise(rng,n),350,7000));
            for(int i=0;i<n;i++){double t=i/(double)Rate,onset=1-Math.Exp(-t/.0003);double a=(t-shockAt)/.00032,b=(t-shockAt-.00175)/.00073,c=(t-shockAt-.00310)/.00110;shock[i]=.98*Math.Exp(-.5*a*a)-.62*Math.Exp(-.5*b*b)+.21*Math.Exp(-.5*c*c);direct[i]=.95*pressureGain*shock[i]+.85*1.02*depth*Math.Tanh(2.5*body[i])*onset*Math.Exp(-t/.028)+.66*1.15*grit*Math.Tanh(6*gritNoise[i])*onset*Math.Exp(-t/.011);}
            var offsets=Enumerable.Range(0,tiny).Select(_=>rng.U(.004,.067)).OrderBy(x=>x).ToArray();var bed=Unit(Band(Noise(rng,n),230,6100));for(int i=0;i<n;i++)bed[i]=Math.Tanh(3*bed[i]);
            for(int k=0;k<tiny;k++){int at=(int)(offsets[k]*Rate),length=Math.Min(n-at,(int)Math.Round(rng.U(.0014,.0045)*Rate));if(length<=0)continue;double strength=rng.U(.04,.22)*(1-k/(tiny+3.0)),decay=rng.U(.00045,.0016);for(int i=0;i<length;i++)direct[at+i]+=1.15*.75*microGain*strength*Math.Exp(-i/(Rate*decay))*(1+.7*bed[at+i]);}
            var shell=new double[n];var jitter=Low(Unit(Low(Noise(rng,n),240)),350,1);
            double[,] modes={{490,.020,.24},{792,.018,.22},{1247,.017,.25},{1694,.013,.20},{2522,.010,.20},{3651,.007,.14},{4807,.005,.07}};
            for(int k=0;k<7;k++){double freq=modes[k,0]*(1+rng.U(-.12,.12)),phase=rng.U(0,2*Math.PI),g=modes[k,2]*(modes[k,0]>=2500?highMetal:1);for(int i=0;i<n;i++){double t=i/(double)Rate;
                // Reference is preserved exactly. New audition: lower-Q, pressure-drooping,
                // noise-modulated shell response rather than seven clearly pitched struck modes.
                if(texture==Texture.Reference)shell[i]+=g*Math.Sin(2*Math.PI*freq*t+.13*jitter[i]+phase)*Math.Exp(-t/modes[k,1]);
                else if(texture==Texture.Damped)shell[i]+=.32*g*(.65+.35*bed[i])*Math.Sin(2*Math.PI*freq*(t-.8*t*t)+.38*jitter[i]+phase)*Math.Exp(-t/(modes[k,1]*.30));}}
            for(int i=0;i<n;i++)direct[i]+=1.12*metal*.38*Math.Tanh(3*shell[i])*(1-Math.Exp(-i/(Rate*.0003)));
            var y=(double[])direct.Clone();double[,] reflections={{.0031,.27,6300},{.0068,-.17,5200},{.0117,.13,3700},{.0195,-.09,2700}};
            for(int k=0;k<4;k++)Tap(y,Low(direct,reflections[k,2]),(int)Math.Round(reflections[k,0]*Rate),reflections[k,1]);
            double[,] cavity={{305,2.7,.47},{565,3.1,.35},{970,3.8,.22},{1530,4.4,.17},{2550,5.5,.08}};
            for(int k=0;k<5;k++){var mode=Peak(direct,cavity[k,0]*rng.U(.89,1.11),cavity[k,1]);double decay=rng.U(.014,.032);for(int i=0;i<n;i++)y[i]+=.4*1.10*cavity[k,2]*mode[i]*Math.Exp(-i/(Rate*decay));}
            var delays=new[]{rng.U(.009,.013),rng.U(.025,.033)};var depths=new[]{.10,.075};for(int k=0;k<2;k++)Tap(y,Low(direct,rng.U(1700,3700)),(int)Math.Round(delays[k]*Rate),.4*depths[k]);
            var warm=Band(y,55,6100-3900*.26);var present=Band(y,65,8400);double m=.13+.80*.26;for(int i=0;i<n;i++)y[i]=(1-m)*present[i]+m*warm[i];var mid=Band(y,230,900);for(int i=0;i<n;i++)y[i]=Math.Tanh(1.45*(y[i]+.09*.4*mid[i]));return Band(y,60,8600);
        }
        static void Tap(double[] target,double[] input,int shift,double gain){for(int i=shift;i<target.Length;i++)target[i]+=gain*input[i-shift];}
    }
}
