using System;

namespace DragLab.Domain
{
    // Acoustic response only. Shaft speed, pressure and release events remain
    // authoritative in InductionDynamics/EngineSoundController.
    public sealed class TurboAcoustics
    {
        sealed class Band
        {
            readonly double lowAlpha,highAlpha;double low1,low2,high1,high2;
            public Band(int rate,double lo,double hi){lowAlpha=1-Math.Exp(-2*Math.PI*Math.Min(hi,rate*.42)/rate);highAlpha=1-Math.Exp(-2*Math.PI*Math.Min(lo,rate*.42)/rate);}
            public double Sample(double x){low1+=lowAlpha*(x-low1);low2+=lowAlpha*(low1-low2);high1+=highAlpha*(low2-high1);high2+=highAlpha*(high1-high2);return low2-high2;}
            public void Clear(){low1=low2=high1=high2=0;}
        }
        readonly EngineSoundProfile profile;
        readonly int rate;
        readonly Band flowHigh,flowLow;
        readonly double smooth;
        readonly double sizePitch;
        double phase,secondPhase,shaft,pressure,throttle,airflow,shaftTarget,pressureTarget,throttleTarget,airflowTarget;
        EngineSoundFrame lastFrame;
        double noiseAmount=1;
        const double Tau=2*Math.PI;
        public TurboAcoustics(EngineSoundProfile profile,int sampleRate)
        {
            this.profile=profile;rate=sampleRate;flowHigh=new Band(rate,700,3900);flowLow=new Band(rate,140,760);smooth=1-Math.Exp(-1.0/(rate*.018));
            sizePitch=1/Math.Sqrt(Math.Max(.4,profile.turboSize>0?profile.turboSize:1));
        }
        static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
        public double Sample(EngineSoundFrame frame,double collectorResponse,double noise,double compressorNoise=1)
        {
            if(!ReferenceEquals(frame,lastFrame))
            {
                lastFrame=frame;shaftTarget=Clamp(frame.turboSpool,0,1);throttleTarget=Clamp(frame.throttle,0,1);
                pressureTarget=Clamp((frame.turboBoost>=0?frame.turboBoost:frame.boost)/Math.Max(.1,profile.boostBar),0,1);
                // Runtime uses actual mass flow, including compressor capacities,
                // density/temperature, wear and hardware restrictions in physics.
                airflowTarget=frame.airflowKgS>=0?Clamp(frame.airflowKgS/Math.Max(.01,profile.turboFlowCapacityKgS),0,1.5):throttleTarget*(.08+.92*Clamp(frame.load,0,1));
            }
            shaft+=smooth*(shaftTarget-shaft);pressure+=smooth*(pressureTarget-pressure);throttle+=smooth*(throttleTarget-throttle);
            airflow+=smooth*(airflowTarget-airflow);
            double noiseTarget=double.IsNaN(compressorNoise)||double.IsInfinity(compressorNoise)?1:Clamp(compressorNoise,0,1);
            noiseAmount+=smooth*(noiseTarget-noiseAmount);
            double hz=Math.Min((610+1490*shaft)*sizePitch,rate*.19);phase=(phase+hz/rate)%1;
            double blade=Math.Sin(Tau*phase)+.24*Math.Sin(2*Tau*phase+.35);
            if(profile.turboCount==2){secondPhase=(secondPhase+hz*.997/rate)%1;blade=(blade+Math.Sin(Tau*secondPhase)+.24*Math.Sin(2*Tau*secondPhase+.35))*.65;}
            double toneGain=(.0035*shaft+(.032*shaft*shaft+.033*pressure)*(.30+.70*throttle));
            double flow=(flowHigh.Sample(noise)+.24*flowLow.Sample(noise))*(.006*shaft+.017*pressure)*(.22+.78*throttle)*airflow;
            double turbine=collectorResponse*(.014*shaft+.036*pressure);
            return (blade*toneGain+flow*profile.compressorNoiseGain*noiseAmount+turbine)*profile.turboGain*1.5;
        }
        public void Clear(){flowHigh.Clear();flowLow.Clear();shaft=pressure=throttle=airflow=shaftTarget=pressureTarget=throttleTarget=airflowTarget=0;lastFrame=null;}
    }
}
