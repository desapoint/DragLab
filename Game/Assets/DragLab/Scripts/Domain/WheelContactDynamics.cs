using System;
namespace DragLab.Domain
{
    [Serializable]
    public sealed class WheelContactState
    {
        // Order FL, FR, RL, RR. Angular speed rad/s, angle rad, forces N, work J.
        public double speed,distance,acceleration,brakeWorkJ,contactWorkJ,differentialWorkJ,resistanceWorkJ,driveWorkJ;
        public double[] omega=new double[4],angle=new double[4],slip=new double[4],force=new double[4],slipWorkJ=new double[4],driveTorqueNm=new double[4],brakeTorqueNm=new double[4],contactTorqueNm=new double[4];
        public double[] normalLoadN=new double[4], friction=new double[4];
        public WheelContactState Copy(){var s=(WheelContactState)MemberwiseClone();s.normalLoadN=normalLoadN==null||normalLoadN.Length!=4?new double[4]:(double[])normalLoadN.Clone();s.friction=friction==null||friction.Length!=4?new double[4]:(double[])friction.Clone();s.omega=(double[])omega.Clone();s.angle=(double[])angle.Clone();s.slip=(double[])slip.Clone();s.force=(double[])force.Clone();s.slipWorkJ=(double[])slipWorkJ.Clone();s.driveTorqueNm=(double[])driveTorqueNm.Clone();s.brakeTorqueNm=(double[])brakeTorqueNm.Clone();s.contactTorqueNm=(double[])contactTorqueNm.Clone();return s;}
        public string Validate(){foreach(var values in new[]{omega,angle,slip,force,slipWorkJ,driveTorqueNm,brakeTorqueNm,contactTorqueNm}){if(values==null||values.Length!=4)return "Four wheel values required.";foreach(double value in values)if(!Mechanics.MechanicsSchema.Finite(value))return "Nonfinite wheel state.";}foreach(double value in new[]{speed,distance,acceleration,brakeWorkJ,contactWorkJ,differentialWorkJ,resistanceWorkJ,driveWorkJ})if(!Mechanics.MechanicsSchema.Finite(value))return "Nonfinite contact history.";if(speed<0)return "Invalid forward road speed.";return null;}
        public double KineticEnergy(double mass,double inertia)=>KineticEnergy(mass,new[]{inertia,inertia,inertia,inertia});
        public double KineticEnergy(double mass,double[] inertia){double e=.5*mass*speed*speed;for(int i=0;i<4;i++)e+=.5*inertia[i]*omega[i]*omega[i];return e;}
    }
    public sealed class WheelContactInput
    {
        public double massKg=1200,radiusM=.3,inertiaKgM2=1.1,frontTorqueNm,rearTorqueNm,resistanceN;
        public double[] radii,inertias;
        public double[] normalLoadN={300*DragMath.Gravity,300*DragMath.Gravity,300*DragMath.Gravity,300*DragMath.Gravity},friction={1,1,1,1},brakeNm=new double[4];
        public DifferentialSpec frontDifferential=new DifferentialSpec(),rearDifferential=new DifferentialSpec();
        public double frontDifferentialHealth=1,rearDifferentialHealth=1;
        public bool lockCenter;
        public double centerPreloadNm, centerTorqueBiasRatio = 1;
    }
    public static class WheelContactDynamics
    {
        public const string Model="draglab-wheel-contact-v4";
        static double Slip(double wheel,double road){return (wheel-road)/Math.Max(1,Math.Max(Math.Abs(wheel),Math.Abs(road)));}
        static bool Locked(DifferentialSpec d){return d.kind==DifferentialKind.Welded||d.kind==DifferentialKind.Spool;}
        // Orthogonal velocity constraints: axle locks remove left/right freedom;
        // a locked center equates AXLE MEANS, retaining each open axle's freedom.
        static double[,] Mobility(WheelContactInput input,double[] inertia)
        {
            var mobility=new double[4,4];for(int i=0;i<4;i++)mobility[i,i]=1/inertia[i];
            if(Locked(input.frontDifferential)&&input.frontDifferentialHealth>0)Constrain(mobility,new[]{1.0,-1,0,0});
            if(Locked(input.rearDifferential)&&input.rearDifferentialHealth>0)Constrain(mobility,new[]{0.0,0,1,-1});
            if(input.lockCenter&&input.frontDifferentialHealth>0&&input.rearDifferentialHealth>0)Constrain(mobility,new[]{.5,.5,-.5,-.5});
            return mobility;
        }
        static void Constrain(double[,] mobility,double[] constraint)
        {
            var response=new double[4];double denominator=0;
            for(int i=0;i<4;i++){for(int j=0;j<4;j++)response[i]+=mobility[i,j]*constraint[j];denominator+=constraint[i]*response[i];}
            for(int i=0;i<4;i++)for(int j=0;j<4;j++)mobility[i,j]-=response[i]*response[j]/denominator;
        }
        static double RotatingEnergy(double[] w,double[] inertia)
        {double sum=0;for(int i=0;i<4;i++)sum+=.5*inertia[i]*w[i]*w[i];return sum;}
        static void ProjectInitial(WheelContactState s,double[,] mobility,double[] inertia)
        {
            var w=new double[4];for(int i=0;i<4;i++)for(int j=0;j<4;j++)w[i]+=mobility[i,j]*inertia[j]*s.omega[j];
            double loss=Math.Max(0,RotatingEnergy(s.omega,inertia)-RotatingEnergy(w,inertia));s.differentialWorkJ+=loss;s.contactWorkJ+=loss;s.omega=w;
        }
        static void PairTorque(double total,DifferentialSpec diff,double health,WheelContactState s,int first,double[] inertia,double dt,double[] torque)
        {
            torque[first]=torque[first+1]=health>0?total*.5:0;
            if(health>0&&diff.kind==DifferentialKind.LimitedSlip){double delta=s.omega[first+1]-s.omega[first];double cap=diff.preloadNm*health+Math.Abs(total)*.5*(diff.torqueBiasRatio-1)/(diff.torqueBiasRatio+1)*health;double transfer=DragMath.Clamp(delta/(1/inertia[first]+1/inertia[first+1])/dt,-cap,cap);torque[first]+=transfer;torque[first+1]-=transfer;}
        }
        public static void Step(WheelContactState s,WheelContactInput input,double dt)
        {
            foreach(double value in new[]{input.massKg,input.radiusM,input.inertiaKgM2,input.frontTorqueNm,input.rearTorqueNm,input.resistanceN,input.frontDifferentialHealth,input.rearDifferentialHealth,input.centerPreloadNm,input.centerTorqueBiasRatio})if(!Mechanics.MechanicsSchema.Finite(value))throw new ArgumentException("Nonfinite wheel parameters.");
            foreach(var values in new[]{input.normalLoadN,input.friction,input.brakeNm}){if(values==null||values.Length!=4)throw new ArgumentException("Four contact inputs required.");foreach(double value in values)if(!Mechanics.MechanicsSchema.Finite(value)||value<0)throw new ArgumentException("Invalid contact input.");}
            if(s.Validate()!=null||input.frontDifferential==null||input.rearDifferential==null||input.frontDifferential.Validate()!=null||input.rearDifferential.Validate()!=null)throw new ArgumentException("Invalid wheel/differential state.");
            if(input.massKg<=0||input.radiusM<=0||input.inertiaKgM2<=0||!Mechanics.MechanicsSchema.Finite(dt)||dt<=0||dt>.05)throw new ArgumentException("Invalid wheel contact parameters.");
            if(input.centerPreloadNm<0||input.centerTorqueBiasRatio<1||input.centerTorqueBiasRatio>10)throw new ArgumentException("Invalid center LSD parameters.");
            if(s.normalLoadN==null||s.normalLoadN.Length!=4)s.normalLoadN=new double[4];if(s.friction==null||s.friction.Length!=4)s.friction=new double[4];
            var radii=input.radii??new[]{input.radiusM,input.radiusM,input.radiusM,input.radiusM};var inertia=input.inertias??new[]{input.inertiaKgM2,input.inertiaKgM2,input.inertiaKgM2,input.inertiaKgM2};
            foreach(var values in new[]{radii,inertia}){if(values.Length!=4)throw new ArgumentException("Four wheel geometry values required.");foreach(double value in values)if(!Mechanics.MechanicsSchema.Finite(value)||value<=0)throw new ArgumentException("Invalid per-wheel geometry.");}
            double before=s.speed;Array.Copy(input.normalLoadN,s.normalLoadN,4);Array.Copy(input.friction,s.friction,4);Array.Clear(s.force,0,4);Array.Clear(s.driveTorqueNm,0,4);Array.Clear(s.brakeTorqueNm,0,4);Array.Clear(s.contactTorqueNm,0,4);var torque=new double[4];var mobility=Mobility(input,inertia);ProjectInitial(s,mobility,inertia);int steps=(int)Math.Ceiling(dt/(1.0/960));double h=dt/steps;
            for(int sub=0;sub<steps;sub++)
            {
                double subSpeedBefore=s.speed;var previous=(double[])s.omega.Clone();
                PairTorque(input.frontTorqueNm,input.frontDifferential,input.frontDifferentialHealth,s,0,inertia,h,torque);PairTorque(input.rearTorqueNm,input.rearDifferential,input.rearDifferentialHealth,s,2,inertia,h,torque);
                if(!input.lockCenter && input.frontDifferentialHealth>0 && input.rearDifferentialHealth>0 && (input.centerPreloadNm>0 || input.centerTorqueBiasRatio>1)) {
                    double difference=(s.omega[0]+s.omega[1]-s.omega[2]-s.omega[3])*.5;
                    double limit=input.centerPreloadNm+Math.Abs(input.frontTorqueNm+input.rearTorqueNm)*.5*(input.centerTorqueBiasRatio-1)/(input.centerTorqueBiasRatio+1);
                    double transfer=DragMath.Clamp(difference/(.25*(1/inertia[0]+1/inertia[1]+1/inertia[2]+1/inertia[3]))/h,-limit,limit);
                    torque[0]-=transfer*.5;torque[1]-=transfer*.5;torque[2]+=transfer*.5;torque[3]+=transfer*.5;
                }
                for(int i=0;i<4;i++){for(int j=0;j<4;j++){s.omega[i]+=mobility[i,j]*torque[j]*h;s.driveTorqueNm[i]+=inertia[i]*mobility[i,j]*torque[j]*h/dt;}}
                double externalWork=0;for(int i=0;i<4;i++){double source=i<2?(input.frontDifferentialHealth>0?input.frontTorqueNm*.5:0):(input.rearDifferentialHealth>0?input.rearTorqueNm*.5:0);externalWork+=source*(previous[i]+s.omega[i])*.5*h;}
                double differentialLoss=Math.Max(0,externalWork-(RotatingEnergy(s.omega,inertia)-RotatingEnergy(previous,inertia)));s.driveWorkJ+=externalWork;s.differentialWorkJ+=differentialLoss;s.contactWorkJ+=differentialLoss;
                for(int i=0;i<4;i++)
                {
                    double oldEnergy=RotatingEnergy(s.omega,inertia);double brakeImpulse=Math.Sign(s.omega[i])*Math.Min(input.brakeNm[i]*h,Math.Abs(s.omega[i])/mobility[i,i]);
                    s.brakeTorqueNm[i]+=brakeImpulse/dt;for(int j=0;j<4;j++)s.omega[j]-=brakeImpulse*mobility[j,i];
                    s.brakeWorkJ+=Math.Max(0,oldEnergy-RotatingEnergy(s.omega,inertia));
                }
                for(int n=0;n<4;n++)
                {
                    int i=(n+sub)%4;double relative=s.omega[i]*radii[i]-s.speed,slip=Slip(s.omega[i]*radii[i],s.speed),normal=input.normalLoadN[i];
                    double grip=input.friction[i]*normal*Math.Pow(Math.Max(1,normal)/3000,-.1);if(Math.Abs(slip)>.12)grip*=.8+.2*Math.Exp(-(Math.Abs(slip)-.12)*2);
                    double inverseMass=1/input.massKg+radii[i]*radii[i]*mobility[i,i];double impulse=DragMath.Clamp(relative/inverseMass,-grip*h,grip*h);
                    double loss=Math.Max(0,impulse*relative-.5*impulse*impulse*inverseMass);s.slipWorkJ[i]+=loss;s.contactWorkJ+=loss;s.force[i]+=impulse/dt;s.contactTorqueNm[i]+=impulse*radii[i]/dt;s.speed+=impulse/input.massKg;
                    for(int j=0;j<4;j++)s.omega[j]-=impulse*radii[i]*mobility[j,i];
                }
                double old=s.speed;s.speed=Math.Max(0,s.speed-Math.Max(0,input.resistanceN)/input.massKg*h);s.resistanceWorkJ+=Math.Max(0,.5*input.massKg*(old*old-s.speed*s.speed));s.distance+=(subSpeedBefore+s.speed)*.5*h;for(int i=0;i<4;i++)s.angle[i]+=s.omega[i]*h;
            }
            s.acceleration=(s.speed-before)/dt;for(int i=0;i<4;i++)s.slip[i]=Slip(s.omega[i]*radii[i],s.speed);
        }
    }
}
