using System;
using DragLab.Domain;

namespace DragLab.Editor
{
    // Listening prototype only: no runtime callers, Unity objects or cause-specific sound recipes.
    [Serializable] public struct PreviewCombustionEvent
    {
        public double fuelMg, oxygenMg, heatingJPerKg, oxygenPerFuel, burnEfficiency;
        public double burnSeconds, mixingSeconds, volumeLitres, temperatureK, pressureKPa;
        public double sourceMetres;
        public bool intake, ignited;
        public uint seed;
        public double EnergyJ => ignited ? Math.Min(fuelMg, oxygenMg / oxygenPerFuel) * 1e-6 * heatingJPerKg * burnEfficiency : 0;
        public void Validate()
        {
            foreach(double value in new[]{fuelMg,oxygenMg,heatingJPerKg,oxygenPerFuel,burnEfficiency,burnSeconds,mixingSeconds,volumeLitres,temperatureK,pressureKPa,sourceMetres})
                if(double.IsNaN(value)||double.IsInfinity(value)||value<0)throw new ArgumentException("Invalid combustion-event input");
            if(heatingJPerKg<1||oxygenPerFuel<=0||burnEfficiency>1||burnSeconds<.0005||burnSeconds>.5||mixingSeconds<.001||mixingSeconds>.8||volumeLitres<.01||temperatureK<200||temperatureK>1800||pressureKPa<20)
                throw new ArgumentException("Combustion-event input outside preview bounds");
        }
    }

    public sealed class PreviewDuctSegment
    {
        public double lengthM,diameterM,temperatureK,leak;
        public bool turbine;
        public double turbineAmount=1;
        public ExhaustPart component;
    }

    // Audition calibration for one common source; no cause/engine-specific sound branches.
    [Serializable] public sealed class PreviewCombustionVoicing
    {
        public double pressureCoupling=1,turbulenceCoupling=1,frontCoupling=0,frontMilliseconds=1.5,expansionScale=1;
        public double heatReleaseCoupling=0,structureCoupling=0;
        public double burnTimeScale=1,reflectionDecaySeconds=0,reflectionMix=0;
        public double shellCoupling=0,crackleCoupling=0;
        public PreviewCombustionVoicing Copy()=>new PreviewCombustionVoicing{pressureCoupling=pressureCoupling,turbulenceCoupling=turbulenceCoupling,frontCoupling=frontCoupling,frontMilliseconds=frontMilliseconds,expansionScale=expansionScale,heatReleaseCoupling=heatReleaseCoupling,structureCoupling=structureCoupling,burnTimeScale=burnTimeScale,reflectionDecaySeconds=reflectionDecaySeconds,reflectionMix=reflectionMix,shellCoupling=shellCoupling,crackleCoupling=crackleCoupling};
        public void Validate()
        {
            foreach(double value in new[]{pressureCoupling,turbulenceCoupling,frontCoupling,frontMilliseconds,expansionScale,heatReleaseCoupling,structureCoupling,burnTimeScale,reflectionDecaySeconds,reflectionMix,shellCoupling,crackleCoupling})
                if(double.IsNaN(value)||double.IsInfinity(value))throw new ArgumentException("Invalid source calibration");
            if(pressureCoupling<0||pressureCoupling>12||turbulenceCoupling<0||turbulenceCoupling>3||frontCoupling<0||frontCoupling>4||frontMilliseconds<.04||frontMilliseconds>8||expansionScale<.5||expansionScale>2.5)
                throw new ArgumentException("Source calibration outside preview bounds");
            if(heatReleaseCoupling<0||heatReleaseCoupling>1||structureCoupling<0||structureCoupling>1)throw new ArgumentException("Invalid thermal/body coupling");
            if(burnTimeScale<.05||burnTimeScale>1||reflectionDecaySeconds<0||reflectionDecaySeconds>.6||reflectionMix<0||reflectionMix>.8)throw new ArgumentException("Invalid attack/reflection calibration");
            if(shellCoupling<0||shellCoupling>2||crackleCoupling<0||crackleCoupling>2)throw new ArgumentException("Invalid exhaust-wall coupling");
        }
    }

    // One source algorithm for every event. Labels/cause names deliberately do not enter this class.
    public sealed class SharedBackfirePreviewSynth
    {
        sealed class Segment
        {
            public readonly double[] forward,reverse;
            public readonly double impedance,loss,leak;
            public readonly double area;
            public readonly bool turbine;
            readonly double turbineAmount;
            public readonly ExhaustFilter forwardFilter,reverseFilter;
            public int at;
            double turbineForward,turbineReverse,turbineCoefficient=.3,turbineGain=1,targetCoefficient=.3,targetGain=1;
            public Segment(PreviewDuctSegment description,int rate,int stage)
            {
                double speed=Math.Sqrt(1.34*287*description.temperatureK);
                int delay=Math.Max(1,(int)Math.Round(description.lengthM/speed*rate));
                forward=new double[delay];reverse=new double[delay];
                area=Math.PI*description.diameterM*description.diameterM/4;
                double density=101325/(287*description.temperatureK);
                impedance=density*speed/area;
                loss=Math.Exp(-.08*description.lengthM)*(1-description.leak*.5);
                leak=description.leak;turbine=description.turbine;turbineAmount=description.turbineAmount;
                var setup=new ExhaustSetup{parts=description.component==null?new ExhaustPart[0]:new[]{description.component.Copy()}};
                forwardFilter=new ExhaustFilter(setup,stage);reverseFilter=new ExhaustFilter(setup,stage);
                forwardFilter.Configure(rate);reverseFilter.Configure(rate);
            }
            public void SetBoost(double boost,int rate)
            {
                targetCoefficient=1-Math.Exp(-2*Math.PI*(3200/(1+.8*Math.Max(0,boost)))/rate);
                targetGain=Math.Pow(10,-(1.5+3*Math.Max(0,boost))*turbineAmount/20);
            }
            public void Store(double goingForward,double goingReverse)
            {
                goingForward=forwardFilter.Process(goingForward)*loss;
                goingReverse=reverseFilter.Process(goingReverse)*loss;
                if(turbine){
                    turbineCoefficient+=(targetCoefficient-turbineCoefficient)*.002;
                    turbineGain+=(targetGain-turbineGain)*.002;
                    turbineForward+=turbineCoefficient*(goingForward-turbineForward);
                    turbineReverse+=turbineCoefficient*(goingReverse-turbineReverse);
                    goingForward=(goingForward+turbineAmount*(turbineForward-goingForward))*turbineGain;
                    goingReverse=(goingReverse+turbineAmount*(turbineReverse-goingReverse))*turbineGain;
                }
                forward[at]=goingForward;reverse[at]=goingReverse;at=(at+1)%forward.Length;
            }
        }
        sealed class Duct
        {
            readonly Segment[] segments;
            readonly double[] positions,forward,reverse,outForward,outReverse,injections;
            readonly double radiationPole,radiationGain;
            readonly ExhaustWall wall;
            readonly double[] wallWeights;
            public double ShellSquared => wall==null?0:wall.ShellSquared;
            public double CrackleSquared => wall==null?0:wall.CrackleSquared;
            double radiationInput,radiationOutput;
            public double Length => positions[positions.Length-1];
            public Duct(PreviewDuctSegment[] descriptions,int rate,int stage,PreviewCombustionVoicing voicing=null)
            {
                if(descriptions==null||descriptions.Length==0)throw new ArgumentException("Duct needs segments");
                segments=new Segment[descriptions.Length];positions=new double[segments.Length+1];
                forward=new double[segments.Length];reverse=new double[segments.Length];
                outForward=new double[segments.Length];outReverse=new double[segments.Length];injections=new double[positions.Length];
                for(int i=0;i<segments.Length;i++){
                    var d=descriptions[i];
                    if(d.lengthM<=0||d.lengthM>5||d.diameterM<.01||d.diameterM>.4||d.temperatureK<200||d.temperatureK>1800||d.leak<0||d.leak>.5||d.turbineAmount<0||d.turbineAmount>1)
                        throw new ArgumentException("Invalid preview duct geometry");
                    segments[i]=new Segment(d,rate,stage);positions[i+1]=positions[i]+d.lengthM;
                }
                if(voicing!=null&&(voicing.shellCoupling>0||voicing.crackleCoupling>0)){
                    wallWeights=new double[segments.Length];double surface=0,diameter=0;
                    for(int i=0;i<segments.Length;i++){
                        double weight=descriptions[i].lengthM*descriptions[i].diameterM;wallWeights[i]=weight;surface+=weight;diameter+=weight*descriptions[i].diameterM;
                    }
                    for(int i=0;i<wallWeights.Length;i++)wallWeights[i]/=surface;
                    wall=new ExhaustWall(rate,Length,diameter/surface,voicing.shellCoupling,voicing.crackleCoupling);
                }
                var outlet=descriptions[descriptions.Length-1];
                // Calibrated aperture-radiation proxy: absolute diameter matters even for uniform-area scaling.
                double cutoff=45*.06/outlet.diameterM*Math.Sqrt(outlet.temperatureK/650);
                radiationPole=Math.Exp(-2*Math.PI*cutoff/rate);radiationGain=Math.Sqrt(outlet.diameterM/.06);
            }
            public void Locate(double position,out int node,out double fraction)
            {
                if(position<0||position>Length)throw new ArgumentException("Source is outside acoustic path");
                node=0;while(node<segments.Length-1&&position>positions[node+1])node++;
                fraction=(position-positions[node])/(positions[node+1]-positions[node]);
            }
            public void Inject(int node,double fraction,double value)
            {injections[node]+=value*(1-fraction);injections[node+1]+=value*fraction;}
            public double VentSeconds(int node,double fraction,double volumeLitres,double temperatureK)
            {
                int first=Math.Min(segments.Length-1,node+(fraction>1-1e-8?1:0));
                double opening=segments[first].area;
                for(int i=first+1;i<segments.Length;i++)opening=Math.Min(opening,segments[i].area);
                // Reduced-order discharge proxy, not a measured exhaust-flow solution.
                return DragMath.Clamp(volumeLitres*.001/(opening*Math.Sqrt(1.34*287*temperatureK)*.07),.004,.09);
            }
            public void SetBoost(double boost,int rate){foreach(var segment in segments)segment.SetBoost(boost,rate);}
            public double Sample()
            {
                int last=segments.Length-1;
                for(int i=0;i<segments.Length;i++){forward[i]=segments[i].forward[segments[i].at];reverse[i]=segments[i].reverse[segments[i].at];}
                // Pressure scattering at impedance changes; both propagation directions retain their own delay/filter state.
                outForward[0]=.12*reverse[0]+injections[0]*.5;
                for(int node=1;node<segments.Length;node++){
                    double left=segments[node-1].impedance,right=segments[node].impedance;
                    double common=2*(right*forward[node-1]+left*reverse[node])/(left+right);
                    outReverse[node-1]=(common-forward[node-1])*.985+injections[node]*.5;
                    outForward[node]=(common-reverse[node])*.985+injections[node]*.5;
                }
                outReverse[last]=-.32*forward[last]+injections[last]*.5;
                double radiation=.68*forward[last]+injections[last]*.5,wallDrive=0;
                for(int i=0;i<segments.Length;i++){
                    radiation+=segments[i].leak*(forward[i]+reverse[i])*.15;
                    if(wall!=null)wallDrive+=wallWeights[i]*(forward[i]+reverse[i]);
                    segments[i].Store(outForward[i],outReverse[i]);
                }
                Array.Clear(injections,0,injections.Length);
                double emitted=radiation-radiationInput+radiationPole*radiationOutput;
                radiationInput=radiation;radiationOutput=emitted;return emitted*radiationGain+(wall==null?0:wall.Sample(wallDrive));
            }
        }
        // Preview-only exterior reflection bloom. Fed by the radiated pressure, never by independent noise.
        // Orthogonal feedback mixes four unequal paths; no single ringing oscillator or fixed-pitch comb sum.
        sealed class ReflectionBloom
        {
            readonly double[][] lines=new double[4][];
            readonly int[] at=new int[4];
            readonly double[] feedback=new double[4],low=new double[4],read=new double[4];
            readonly double coefficient;
            public ReflectionBloom(int rate,double decay)
            {
                var seconds=new[]{.0113,.0179,.0237,.0311};
                for(int i=0;i<4;i++){
                    int length=Math.Max(1,(int)Math.Round(rate*seconds[i]));lines[i]=new double[length];
                    feedback[i]=Math.Pow(10,-3*length/(rate*Math.Max(.01,decay)));
                }
                coefficient=1-Math.Exp(-2*Math.PI*Math.Min(6500,rate*.3)/rate);
            }
            public double Sample(double input)
            {
                for(int i=0;i<4;i++){
                    double delayed=lines[i][at[i]];low[i]+=coefficient*(delayed-low[i]);read[i]=low[i];
                }
                double a=read[0],b=read[1],c=read[2],d=read[3];
                lines[0][at[0]]=.5*input+.5*(a+b+c+d)*feedback[0];
                lines[1][at[1]]=.5*input+.5*(a-b+c-d)*feedback[1];
                lines[2][at[2]]=-.5*input+.5*(a+b-c-d)*feedback[2];
                lines[3][at[3]]=.5*input+.5*(a-b-c+d)*feedback[3];
                for(int i=0;i<4;i++)at[i]=(at[i]+1)%lines[i].Length;
                return .5*(a+b-c+d);
            }
        }
        // Stylized exhaust-wall proxy: broad damped modes driven by local duct pressure.
        // Nonlinear flex residual adds brief crackle without a separate random-noise emitter or new pop event.
        sealed class ExhaustWall
        {
            sealed class Mode
            {
                readonly double b0,b2,a1,a2;double x1,x2,y1,y2;
                public Mode(int rate,double hz,double q)
                {
                    double w=2*Math.PI*Math.Min(rate*.3,hz)/rate,alpha=Math.Sin(w)/(2*q),normal=1+alpha;
                    b0=alpha/normal;b2=-b0;a1=-2*Math.Cos(w)/normal;a2=(1-alpha)/normal;
                }
                public double Sample(double input)
                {
                    double value=b0*input+b2*x2-a1*y1-a2*y2;x2=x1;x1=input;y2=y1;y1=value;return value;
                }
            }
            readonly Mode low,mid,high;
            readonly double shellGain,crackleGain,highPassPole;
            double previousResidual,highPassed;
            public double ShellSquared {get;private set;}
            public double CrackleSquared {get;private set;}
            public ExhaustWall(int rate,double length,double diameter,double shell,double crackle)
            {
                // Calibrated geometry response, not measured steel shell eigenfrequencies/material properties.
                double hz=DragMath.Clamp(240*Math.Sqrt(.06/diameter)*Math.Sqrt(2.3/length),160,360);
                low=new Mode(rate,hz,.85);mid=new Mode(rate,hz*2.27,1.15);high=new Mode(rate,hz*4.13,1.35);
                shellGain=shell;crackleGain=crackle;highPassPole=Math.Exp(-2*Math.PI*700/rate);
            }
            public double Sample(double pressure)
            {
                double response=.55*low.Sample(pressure*4)+.30*mid.Sample(pressure*4)+.15*high.Sample(pressure*4);
                double residual=response-Math.Tanh(6*response)/6;
                highPassed=residual-previousResidual+highPassPole*highPassed;previousResidual=residual;
                double shell=shellGain*response,crackle=crackleGain*highPassed;
                ShellSquared+=shell*shell;CrackleSquared+=crackle*crackle;return shell+crackle;
            }
        }
        struct Voice
        {
            public bool active,intake;
            public int node,remaining;
            public uint random;
            public double fraction,amplitude,attack,burn,expansion,mixing,attackStep,burnStep,expansionStep,mixingStep;
            public double low,second,bass,air,drift,lowCoefficient,bassCoefficient,airCoefficient,driftCoefficient,pressureRatio;
            public double previousPressure,front,frontCoefficient,frontNormalization;
            public double heatWeight,heatPower,heatStep,gasPressure,gasAmbient,pressurePerJ,ventStep,previousGas,gasFront,bodyLow,bodySlow,bodyCoefficient;
            public int heatTick;
        }
        readonly Voice[] voices=new Voice[48];
        readonly Duct exhaust,intake;
        readonly int rate;
        readonly PreviewCombustionVoicing voicing;
        readonly ReflectionBloom reflection;
        int next;
        double lastInput,lastOutput;
        public int Accepted {get;private set;}
        public int Suppressed {get;private set;}
        public int RejectedOverlap {get;private set;}
        public double AcceptedHeatJ {get;private set;}
        public double ReleasedHeatJ {get;private set;}
        public double PressureSquaredSamples {get;private set;}
        public double TurbulenceSquaredSamples {get;private set;}
        public double StructureSquaredSamples {get;private set;}
        public double ExhaustLength => exhaust.Length;
        public double IntakeLength => intake.Length;
        public double ShellSquaredSamples => exhaust.ShellSquared;
        public double CrackleSquaredSamples => exhaust.CrackleSquared;
        public SharedBackfirePreviewSynth(PreviewDuctSegment[] exhaust,PreviewDuctSegment[] intake,int rate,int stage=0,PreviewCombustionVoicing voicing=null)
        {
            if(rate<8000||rate>192000)throw new ArgumentException("Unsupported sample rate");
            this.voicing=(voicing??new PreviewCombustionVoicing()).Copy();this.voicing.Validate();
            this.rate=rate;this.exhaust=new Duct(exhaust,rate,stage,this.voicing);this.intake=new Duct(intake,rate,stage);
            if(this.voicing.reflectionMix>0)reflection=new ReflectionBloom(rate,this.voicing.reflectionDecaySeconds);
        }
        public void SetBoost(double boost){exhaust.SetBoost(boost,rate);}
        public bool Trigger(PreviewCombustionEvent input)
        {
            input.Validate();var path=input.intake?intake:exhaust;
            path.Locate(input.sourceMetres,out int node,out double fraction);
            double energy=input.EnergyJ;
            if(energy<=0){Suppressed++;return false;}
            int at=next;while(voices[at].active){at=(at+1)%voices.Length;if(at==next){RejectedOverlap++;return false;}}
            next=(at+1)%voices.Length;
            double burn=input.burnSeconds,expansion=(burn*2.8+.003)*voicing.expansionScale;
            double mixing=input.mixingSeconds+burn*.65;
            double pressure=.33*energy/(input.volumeLitres*.001),ambient=input.pressureKPa*1000;
            double cutoff=DragMath.Clamp(1400*Math.Sqrt(input.temperatureK/650)*Math.Pow(.012/burn,.3),250,Math.Min(6000,rate*.3));
            double vent=path.VentSeconds(node,fraction,input.volumeLitres,input.temperatureK)*voicing.expansionScale;
            double thermalStep=Math.Exp(-1/(rate*burn*voicing.burnTimeScale));
            voices[at]=new Voice{
                active=true,intake=input.intake,node=node,fraction=fraction,random=input.seed==0?17731u:input.seed,
                amplitude=.8*Math.Sqrt(pressure/(ambient+pressure)),pressureRatio=burn/expansion,
                attack=1,burn=1,expansion=1,mixing=1,
                attackStep=Math.Exp(-1/(rate*Math.Max(.00012,burn*.09))),burnStep=Math.Exp(-1/(rate*burn)),
                expansionStep=Math.Exp(-1/(rate*expansion)),mixingStep=Math.Exp(-1/(rate*mixing)),
                lowCoefficient=1-Math.Exp(-2*Math.PI*cutoff/rate),bassCoefficient=1-Math.Exp(-2*Math.PI*45/rate),
                airCoefficient=1-Math.Exp(-2*Math.PI*Math.Min(3200,rate*.3)/rate),driftCoefficient=1-Math.Exp(-2*Math.PI*32/rate),
                frontCoefficient=1-Math.Exp(-1/(rate*voicing.frontMilliseconds*.001)),frontNormalization=rate*burn,
                heatWeight=energy*(1-thermalStep)*(1-thermalStep),heatPower=1,heatStep=thermalStep,
                gasAmbient=ambient,pressurePerJ=.33/(input.volumeLitres*.001),ventStep=Math.Exp(-1/(rate*vent)),bodyCoefficient=1-Math.Exp(-2*Math.PI*85/rate),
                remaining=(int)(rate*Math.Min(3,Math.Max(Math.Max(expansion,mixing)*11,voicing.heatReleaseCoupling>0?vent*12:0)+.05))
            };
            if(voicing.heatReleaseCoupling>0)AcceptedHeatJ+=energy;
            Accepted++;return true;
        }
        public double Sample()
        {
            for(int i=0;i<voices.Length;i++){
                var v=voices[i];if(!v.active)continue;
                v.random^=v.random<<13;v.random^=v.random>>17;v.random^=v.random<<5;
                double noise=v.random/(double)uint.MaxValue*2-1;
                v.low+=v.lowCoefficient*(noise-v.low);v.second+=v.lowCoefficient*(v.low-v.second);
                v.bass+=v.bassCoefficient*(v.second-v.bass);v.air+=v.airCoefficient*(noise-v.air);v.drift+=v.driftCoefficient*(noise-v.drift);
                double onset=1-v.attack;
                double pressure=onset*(v.burn-v.pressureRatio*v.expansion);
                double turbulence=onset*v.mixing*(1+.9*v.drift)*(3.7*(v.second-v.bass)+.14*v.air);
                double value;
                if(voicing.frontCoupling==0&&voicing.pressureCoupling==1&&voicing.turbulenceCoupling==1)value=(pressure*.8+turbulence)*v.amplitude;
                else{
                    // A smoothed rate-of-pressure-rise front; not white-noise amplification or a one-sample click.
                    double rise=(pressure-v.previousPressure)*v.frontNormalization;
                    v.front+=v.frontCoefficient*(rise-v.front);v.previousPressure=pressure;
                    value=(pressure*.8*voicing.pressureCoupling+turbulence*voicing.turbulenceCoupling+v.front*voicing.frontCoupling)*v.amplitude;
                }
                if(voicing.heatReleaseCoupling>0){
                    // Normalized gamma heat-release pulse: total released heat tends to the fuel/oxygen-limited energy.
                    double released=v.heatWeight*(++v.heatTick)*v.heatPower;v.heatPower*=v.heatStep;ReleasedHeatJ+=released;
                    v.gasPressure=(v.gasPressure+released*v.pressurePerJ)*v.ventStep;
                    double gas=v.gasPressure/(v.gasAmbient+.25*v.gasPressure);
                    double gasRise=(gas-v.previousGas)*rate*.003;v.previousGas=gas;
                    v.gasFront+=v.frontCoefficient*(gasRise-v.gasFront);
                    v.bodyLow+=v.bodyCoefficient*(gas-v.bodyLow);v.bodySlow+=v.bodyCoefficient*(v.bodyLow-v.bodySlow);
                    double compression=.25*gas*voicing.pressureCoupling+v.gasFront*voicing.frontCoupling;
                    double discharge=(v.second-v.bass)*Math.Sqrt(Math.Max(0,gas))*.12*voicing.turbulenceCoupling;
                    double structure=(v.bodyLow-v.bodySlow)*voicing.structureCoupling;
                    PressureSquaredSamples+=compression*compression;TurbulenceSquaredSamples+=discharge*discharge;StructureSquaredSamples+=structure*structure;
                    value+=(compression+discharge+structure-value)*voicing.heatReleaseCoupling;
                }
                if(v.remaining<rate*.015)value*=v.remaining/(rate*.015);
                (v.intake?intake:exhaust).Inject(v.node,v.fraction,value);
                v.attack*=v.attackStep;v.burn*=v.burnStep;v.expansion*=v.expansionStep;v.mixing*=v.mixingStep;
                if(--v.remaining<=0)v.active=false;voices[i]=v;
            }
            double sum=exhaust.Sample()+intake.Sample();
            double filtered=sum-lastInput+.997*lastOutput;lastInput=sum;lastOutput=filtered;
            return reflection==null?filtered:filtered+voicing.reflectionMix*reflection.Sample(filtered);
        }
    }
}
