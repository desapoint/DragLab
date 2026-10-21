using System;

namespace DragLab.Domain
{
    // Live pressure-release excitation, not a replayed burst. Every reaction funds
    // its own pressure/ringing/flow energy; no invented secondary explosion train.
    public sealed class ReactivePressureBurst
    {
        readonly int rate;
        readonly double decayScale,sharpnessScale;
        double expansion, pressure, velocity, body, grit, pending;
        double attackAlpha, releaseAlpha, bodyAlpha, gritAlpha, energyDecay;
        double tailEnergy, frontEnergy, lastPressure;
        bool hasExcitation;
        double frontRoot,tailRoot,rootDecay,pressureScale,textureScale,flowTexture;
        uint noise;
        public double FundedEnergy { get; private set; }
        public double LastExcitation { get; private set; }
        public ReactivePressureBurst(int rate,double decayScale=1,double sharpnessScale=1)
        {
            if(rate<8000||rate>192000||double.IsNaN(decayScale)||double.IsInfinity(decayScale)||double.IsNaN(sharpnessScale)||double.IsInfinity(sharpnessScale))throw new ArgumentException("Invalid pressure-source configuration.");
            this.rate=rate;this.decayScale=DragMath.Clamp(decayScale,.25,3);this.sharpnessScale=DragMath.Clamp(sharpnessScale,.25,2.5);
        }
        public void Begin(ExhaustMixtureModel.Event e)
        {
            Clear();noise=(uint)(e.seed^(e.seed>>32));if(noise==0)noise=117;
            Excite(e);
        }
        public void Excite(ExhaustMixtureModel.Event e)
        {
            double rise=DragMath.Clamp(e.riseSeconds,.00025,.025)/Math.Sqrt(sharpnessScale);
            double duration=DragMath.Clamp(e.burnSeconds,.001,.16)*decayScale;
            attackAlpha=1-Math.Exp(-1.0/(rate*rise));
            releaseAlpha=1-Math.Exp(-1.0/(rate*(duration+rise*2)));
            bodyAlpha=1-Math.Exp(-2*Math.PI*DragMath.Clamp(1/(duration*2),100,1200)/rate);
            gritAlpha=1-Math.Exp(-2*Math.PI*DragMath.Clamp(.8/rise,1800,11000)/rate);
            energyDecay=Math.Exp(-1.0/(rate*(duration+rise*3)));
            rootDecay=Math.Sqrt(energyDecay);pressureScale=Math.Sqrt(rate*attackAlpha)*.055;
            textureScale=Math.Sqrt((1-energyDecay)*rate);flowTexture=DragMath.Clamp(e.flowMps/100,.2,1.5);
            // A declining pressure pocket funds weaker impulses. Renewed, stronger
            // pressure can legitimately produce a larger secondary ignition.
            double change=Math.Max(0,e.pressurePa-lastPressure)/Math.Max(1,e.pressurePa);
            double coherent=DragMath.Clamp(.18+.72*change,.18,.9);
            if(!hasExcitation)coherent=.9;
            hasExcitation=true;
            frontEnergy+=e.acousticJ*coherent;tailEnergy+=e.acousticJ*(1-coherent);
            frontRoot=Math.Sqrt(frontEnergy);tailRoot=Math.Sqrt(tailEnergy);
            FundedEnergy+=e.acousticJ;LastExcitation=e.acousticJ*coherent;
            lastPressure=e.pressurePa;
        }
        public double Sample()
        {
            pending=frontRoot*pressureScale;
            // A pressure rise followed by rarefaction. The difference of states has
            // zero steady pressure, rather than repeating a fixed explosion shape.
            pressure+=attackAlpha*(pending-pressure);pending*=1-releaseAlpha;
            expansion+=releaseAlpha*(pressure-expansion);
            double wave=pressure-expansion;
            noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
            double random=noise/(double)uint.MaxValue*2-1;
            body+=bodyAlpha*(random-body);grit+=gritAlpha*(random-grit);
            velocity+=bodyAlpha*(wave-velocity);
            double texture=tailRoot*textureScale;
            double frontTexture=frontRoot*textureScale;
            frontEnergy*=energyDecay;tailEnergy*=energyDecay;
            frontRoot*=rootDecay;tailRoot*=rootDecay;
            return wave + .07*frontTexture*(grit-body) + .06*texture*body*flowTexture + .2*velocity;
        }
        public bool Quiet => pending<1e-8&&Math.Abs(pressure)+Math.Abs(expansion)<1e-8&&frontEnergy+tailEnergy<1e-10;
        public void Clear()
        {
            expansion=pressure=velocity=body=grit=pending=tailEnergy=frontEnergy=lastPressure=0;
            hasExcitation=false;
            frontRoot=tailRoot=0;
            FundedEnergy=LastExcitation=0;
        }
    }
}
