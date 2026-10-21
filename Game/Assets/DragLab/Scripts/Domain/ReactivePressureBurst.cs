using System;

namespace DragLab.Domain
{
    // Live pressure-release excitation, not a replayed burst. Every reaction funds
    // its own pressure/ringing/flow energy; no invented secondary explosion train.
    public sealed class ReactivePressureBurst
    {
        readonly int rate;
        readonly double decayScale,sharpnessScale;
        readonly bool smoothTexture;
        double expansion, pressure, velocity, body, bodyLow, grit, roundedBody;
        double attackAlpha, releaseAlpha, bodyAlpha, gritAlpha, energyDecay;
        double tailEnergy, frontEnergy;
        double lastEventTime,lastEventPressure,lastCoherent;
        bool hasExcitation;
        double frontRoot,tailRoot,rootDecay,tailDecay,tailRootDecay,pressureScale,textureScale,flowTexture,bodyLowAlpha,bodyNormal,gritNormal;
        uint noise;
        public double FundedEnergy { get; private set; }
        public double LastExcitation { get; private set; }
        public double PressureSample { get; private set; }
        public double CombustionBodySample { get; private set; }
        public double TurbulenceSample { get; private set; }
        // The optional reference path supports deterministic texture-only A/B
        // checks. Production uses the smoother texture with identical pressure.
        public ReactivePressureBurst(int rate,double decayScale=1,double sharpnessScale=1,bool smoothTexture=true)
        {
            if(rate<8000||rate>192000||double.IsNaN(decayScale)||double.IsInfinity(decayScale)||double.IsNaN(sharpnessScale)||double.IsInfinity(sharpnessScale))throw new ArgumentException("Invalid pressure-source configuration.");
            this.rate=rate;this.decayScale=DragMath.Clamp(decayScale,.25,3);this.sharpnessScale=DragMath.Clamp(sharpnessScale,.25,2.5);this.smoothTexture=smoothTexture;
        }
        public void Begin(ExhaustMixtureModel.Event e)
        {
            Clear();noise=(uint)(e.seed^(e.seed>>32));if(noise==0)noise=117;
            Excite(e);
        }
        public void Excite(ExhaustMixtureModel.Event e)
        {
            // Faster/higher overpressure steepens the compression edge. These are
            // reaction outputs, not limiter/anti-lag labels or a stored pop shape.
            double overpressure=DragMath.Clamp(e.pressurePa/101325,0,3);
            double rise=DragMath.Clamp(e.riseSeconds,.00025,.025)/Math.Sqrt(sharpnessScale*(1+.25*overpressure));
            double duration=DragMath.Clamp(e.burnSeconds,.001,.16)*decayScale;
            attackAlpha=1-Math.Exp(-1.0/(rate*rise));
            // Acoustic compression/rarefaction and chemical burning are different
            // time scales. Using the full burn time for the pressure release put
            // its body below the mix's DC blocker and left only a thin edge.
            double release=Math.Max(rise*1.8,Math.Min(.012,duration*.22+.0015));
            releaseAlpha=1-Math.Exp(-1.0/(rate*release));
            energyDecay=Math.Exp(-1.0/(rate*(release+rise)));
            tailDecay=Math.Exp(-1.0/(rate*(duration*1.5+.012*decayScale)));
            rootDecay=Math.Sqrt(energyDecay);tailRootDecay=Math.Sqrt(tailDecay);
            pressureScale=Math.Sqrt(rate*attackAlpha)*.11;
            // Dense, low/mid combustion texture: this is not a wideband air jet.
            // Normalize the coloured noise's variance; the earlier unnormalized
            // low-pass made its audible body almost disappear.
            bodyAlpha=1-Math.Exp(-2*Math.PI*DragMath.Clamp(.8/rise,550,1400)/rate);
            bodyLowAlpha=1-Math.Exp(-2*Math.PI*65/rate);
            gritAlpha=1-Math.Exp(-2*Math.PI*Math.Min(5500,rate*.35)/rate);
            bodyNormal=BandNormal(bodyAlpha,bodyLowAlpha);
            gritNormal=BandNormal(gritAlpha,bodyAlpha);
            textureScale=Math.Sqrt((1-tailDecay)*rate);flowTexture=DragMath.Clamp(e.flowMps/100,.2,1.5);
            // Only renewed pressure forms another compression front. Continuous
            // heat release at steady pressure funds the rough combustion body,
            // not identical impulses at the simulation tick frequency.
            double change=hasExcitation?DragMath.Clamp((e.pressurePa-lastEventPressure)/Math.Max(1,e.pressurePa),0,1):1;
            double coherent=(.55+.10*overpressure/(1+overpressure))*change;
            if(hasExcitation&&e.time==lastEventTime&&e.pressurePa==lastEventPressure)coherent=lastCoherent;
            lastEventTime=e.time;lastEventPressure=e.pressurePa;lastCoherent=coherent;hasExcitation=true;
            frontEnergy+=e.acousticJ*coherent;tailEnergy+=e.acousticJ*(1-coherent);
            frontRoot=Math.Sqrt(frontEnergy);tailRoot=Math.Sqrt(tailEnergy);
            FundedEnergy+=e.acousticJ;LastExcitation=e.acousticJ*coherent;
        }
        static double BandNormal(double fast,double slow)
        {
            double variance=(fast/(2-fast)+slow/(2-slow)-2*fast*slow/(fast+slow-fast*slow))/3;
            return 1/Math.Sqrt(Math.Max(1e-10,variance));
        }
        public double Sample()
        {
            // A pressure rise followed by rarefaction. The difference of states has
            // zero steady pressure, rather than repeating a fixed explosion shape.
            pressure+=attackAlpha*(frontRoot*pressureScale-pressure);
            expansion+=releaseAlpha*(pressure-expansion);
            double wave=pressure-expansion;
            noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
            double random=noise/(double)uint.MaxValue*2-1;
            body+=bodyAlpha*(random-body);bodyLow+=bodyLowAlpha*(random-bodyLow);grit+=gritAlpha*(random-grit);
            velocity+=bodyAlpha*(wave-velocity);
            double texture=tailRoot*textureScale;
            double broad=(body-bodyLow)*bodyNormal;
            // Bounded roughness without a pitched carrier or a train of fake pops.
            broad/=1+.35*Math.Abs(broad);
            // Round the stochastic body after waveshaping, where its extra high
            // harmonics were being introduced. The pressure pulse is untouched.
            roundedBody+=bodyAlpha*(broad-roundedBody);
            double textureBody=smoothTexture?.2*broad+.8*roundedBody:broad;
            PressureSample=wave+.35*velocity;
            CombustionBodySample=.18*texture*textureBody;
            // High-frequency grit follows only the short compression, never the
            // complete chemical burn. Slow residual flow remains very subdued.
            TurbulenceSample=(smoothTexture?.006:.012)*Math.Abs(wave)*(grit-body)*gritNormal+.0008*texture*textureBody*flowTexture;
            frontEnergy*=energyDecay;tailEnergy*=tailDecay;
            frontRoot*=rootDecay;tailRoot*=tailRootDecay;
            return PressureSample+CombustionBodySample+TurbulenceSample;
        }
        public bool Quiet => Math.Abs(pressure)+Math.Abs(expansion)<1e-8&&frontEnergy+tailEnergy<1e-10;
        public void Clear()
        {
            expansion=pressure=velocity=body=bodyLow=grit=roundedBody=tailEnergy=frontEnergy=0;
            frontRoot=tailRoot=0;
            hasExcitation=false;lastEventTime=lastEventPressure=lastCoherent=0;
            FundedEnergy=LastExcitation=PressureSample=CombustionBodySample=TurbulenceSample=0;
        }
    }
}
