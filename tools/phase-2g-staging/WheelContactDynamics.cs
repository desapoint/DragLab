using System;
namespace DragLab.Domain
{
    [Serializable]
    public sealed class WheelContactState
    {
        // Order: front-left, front-right, rear-left, rear-right. rad/s, radians, N, J.
        public double speed, distance, acceleration, brakeWorkJ, contactWorkJ, resistanceWorkJ, driveWorkJ;
        public double[] omega = new double[4], angle = new double[4], slip = new double[4], force = new double[4], slipWorkJ = new double[4];
        public WheelContactState Copy() { var s=(WheelContactState)MemberwiseClone();s.omega=(double[])omega.Clone();s.angle=(double[])angle.Clone();s.slip=(double[])slip.Clone();s.force=(double[])force.Clone();s.slipWorkJ=(double[])slipWorkJ.Clone();return s; }
        public string Validate() { foreach(var values in new[]{omega,angle,slip,force,slipWorkJ}) { if(values==null||values.Length!=4)return "Four wheel values required.";foreach(double value in values)if(!Mechanics.MechanicsSchema.Finite(value))return "Nonfinite wheel state."; } if(!Mechanics.MechanicsSchema.Finite(speed)||speed<0||!Mechanics.MechanicsSchema.Finite(distance))return "Invalid road state.";return null; }
        public double KineticEnergy(double mass, double inertia) { double e=.5*mass*speed*speed;foreach(double w in omega)e+=.5*inertia*w*w;return e; }
    }
    public sealed class WheelContactInput
    {
        public double massKg=1200,radiusM=.3,inertiaKgM2=1.1,frontTorqueNm,rearTorqueNm,resistanceN;
        public double[] normalLoadN={3000,3000,3000,3000}, friction={1,1,1,1}, brakeNm=new double[4];
        public DifferentialSpec frontDifferential=new DifferentialSpec(), rearDifferential=new DifferentialSpec();
        public double frontDifferentialHealth=1,rearDifferentialHealth=1;
        public bool lockCenter;
    }
    public static class WheelContactDynamics
    {
        // Impulse-limited contact dissipates energy rather than setting wheel speed from road speed.
        // Translation carries only actual mass. Rotational inertia lives in each independent wheel.
        static double Slip(double wheelSpeed,double roadSpeed) { return (wheelSpeed-roadSpeed)/Math.Max(1,Math.Max(Math.Abs(wheelSpeed),Math.Abs(roadSpeed))); }
        static void PairTorque(double total,DifferentialSpec diff,double health,WheelContactState s,int first,double inertia,double dt,double[] torque)
        {
            if(health<=0){torque[first]=torque[first+1]=0;return;}
            torque[first]=torque[first+1]=total*.5;
            if(diff.kind==DifferentialKind.LimitedSlip) {
                double delta=s.omega[first+1]-s.omega[first];
                double cap=diff.preloadNm*health+Math.Abs(total)*.5*(diff.torqueBiasRatio-1)/(diff.torqueBiasRatio+1)*health;
                double transfer=DragMath.Clamp(delta*inertia*.5/dt,-cap,cap);
                torque[first]+=transfer;torque[first+1]-=transfer;
            }
        }
        static void Lock(WheelContactState s,int first,int count,double inertia)
        {
            double mean=0,energy=0;for(int i=first;i<first+count;i++){mean+=s.omega[i]/count;energy+=.5*inertia*s.omega[i]*s.omega[i];}
            double loss=Math.Max(0,energy-.5*inertia*count*mean*mean);s.contactWorkJ+=loss;
            for(int i=first;i<first+count;i++){s.omega[i]=mean; /* Differential locking loss is not tire heat. */}
        }
        static bool Locked(DifferentialSpec d) { return d.kind==DifferentialKind.Welded || d.kind==DifferentialKind.Spool; }
        public static void Step(WheelContactState s,WheelContactInput input,double dt)
        {
            foreach(double value in new[]{input.massKg,input.radiusM,input.inertiaKgM2,input.frontTorqueNm,input.rearTorqueNm,input.resistanceN,input.frontDifferentialHealth,input.rearDifferentialHealth})if(!Mechanics.MechanicsSchema.Finite(value))throw new ArgumentException("Nonfinite wheel parameters.");
            foreach(var values in new[]{input.normalLoadN,input.friction,input.brakeNm}) {if(values==null||values.Length!=4)throw new ArgumentException("Four contact inputs required.");foreach(double value in values)if(!Mechanics.MechanicsSchema.Finite(value)||value<0)throw new ArgumentException("Invalid contact input.");}
            if(s.Validate()!=null||input.frontDifferential==null||input.rearDifferential==null||input.frontDifferential.Validate()!=null||input.rearDifferential.Validate()!=null)throw new ArgumentException("Invalid wheel/differential state.");
            if(input.massKg<=0||input.radiusM<=0||input.inertiaKgM2<=0||!Mechanics.MechanicsSchema.Finite(dt)||dt<=0||dt>.05)throw new ArgumentException("Invalid wheel contact parameters.");
            double before=s.speed;Array.Clear(s.force,0,4);var torque=new double[4];int steps=(int)Math.Ceiling(dt/(1.0/960));double h=dt/steps;
            for(int sub=0;sub<steps;sub++) {
                double subSpeedBefore=s.speed;
                PairTorque(input.frontTorqueNm,input.frontDifferential,input.frontDifferentialHealth,s,0,input.inertiaKgM2,h,torque);
                PairTorque(input.rearTorqueNm,input.rearDifferential,input.rearDifferentialHealth,s,2,input.inertiaKgM2,h,torque);
                for(int i=0;i<4;i++) {
                    double previous=s.omega[i];s.omega[i]=Math.Max(0,previous+torque[i]/input.inertiaKgM2*h);
                    double shaftWork=.5*input.inertiaKgM2*(s.omega[i]*s.omega[i]-previous*previous);s.driveWorkJ+=shaftWork;
                    previous=s.omega[i];s.omega[i]=Math.Max(0,previous-Math.Max(0,input.brakeNm[i])/input.inertiaKgM2*h);
                    s.brakeWorkJ+=.5*input.inertiaKgM2*(previous*previous-s.omega[i]*s.omega[i]);
                }
                if(input.lockCenter&&input.frontDifferentialHealth>0&&input.rearDifferentialHealth>0)Lock(s,0,4,input.inertiaKgM2);
                else {if(Locked(input.frontDifferential)&&input.frontDifferentialHealth>0)Lock(s,0,2,input.inertiaKgM2);if(Locked(input.rearDifferential)&&input.rearDifferentialHealth>0)Lock(s,2,2,input.inertiaKgM2);}
                for(int n=0;n<4;n++) {
                    int i=(n+sub)%4;double relative=s.omega[i]*input.radiusM-s.speed;
                    double slip=Slip(s.omega[i]*input.radiusM,s.speed);double normal=Math.Max(0,input.normalLoadN[i]);
                    double grip=Math.Max(0,input.friction[i])*normal*Math.Pow(Math.Max(1,normal)/3000,-.1);
                    if(Math.Abs(slip)>.12)grip*=.8+.2*Math.Exp(-(Math.Abs(slip)-.12)*2);
                    double inverseMass=1/input.massKg+input.radiusM*input.radiusM/input.inertiaKgM2;
                    double impulse=DragMath.Clamp(relative/inverseMass,-grip*h,grip*h);
                    double loss=Math.Max(0,impulse*relative-.5*impulse*impulse*inverseMass);
                    s.slipWorkJ[i]+=loss;s.contactWorkJ+=loss;s.force[i]+=impulse/dt;
                    s.speed+=impulse/input.massKg;s.omega[i]-=impulse*input.radiusM/input.inertiaKgM2;
                    s.omega[i]=Math.Max(0,s.omega[i]);s.angle[i]+=s.omega[i]*h;
                }
                double old=s.speed;s.speed=Math.Max(0,s.speed-Math.Max(0,input.resistanceN)/input.massKg*h);
                s.resistanceWorkJ+=.5*input.massKg*(old*old-s.speed*s.speed);s.distance+=(subSpeedBefore+s.speed)*.5*h;
            }
            s.acceleration=(s.speed-before)/dt;
            for(int i=0;i<4;i++)s.slip[i]=Slip(s.omega[i]*input.radiusM,s.speed);
        }
    }
}
