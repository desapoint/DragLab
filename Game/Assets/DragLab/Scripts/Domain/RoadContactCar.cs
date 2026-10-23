using System;
namespace DragLab.Domain
{
    public sealed partial class CarSimulation
    {
        double roadStartSpeed,roadBrakeWork,roadCenterWork;
        readonly double[] roadForces=new double[4],roadDriveTorque=new double[4],roadBrakeTorque=new double[4],roadContactTorque=new double[4];
        public double FrontWheelOmega { get { return (state.contact.omega[0]+state.contact.omega[1])*.5; } }
        public double RearWheelOmega { get { return (state.contact.omega[2]+state.contact.omega[3])*.5; } }
        void EnsureWheelContact()
        {
            if(state.contact==null)state.contact=new WheelContactState();
            if(!state.wheelContactInitialized){state.contact.speed=state.speed;state.contact.distance=state.distance;for(int i=0;i<4;i++)state.contact.omega[i]=state.speed/wheels.radii[i];state.wheelContactInitialized=true;}
            if(!MassDynamics.Installed(spec,Mechanics.PartCategory.Wheels)||!MassDynamics.Installed(spec,Mechanics.PartCategory.Tires))state.drivetrainFailed=true;
        }
        public double DrivenWheelOmega
        {
            get { if(state.openCenterFreeBranch)return (state.frontShaftRpm*awd.frontBias+state.rearShaftRpm*(1-awd.frontBias))/(tune.finalDrive*FlywheelDynamics.RpmPerRadian);
                double f=spec.drive==DriveType.FWD?1:spec.drive==DriveType.RWD?0:awd.mode==AwdMode.OnDemand?(awd.primaryFront?1:0):state.frontTorqueShare;
                if(spec.drive==DriveType.AWD&&(awd.isolateFailedAxle||awd.mode==AwdMode.LockedCenter)){if(!state.frontDriveAvailable)f=0;else if(!state.rearDriveAvailable)f=1;}
                return FrontWheelOmega/awd.LockedRatio*f+RearWheelOmega*(1-f); }
        }
        public double ReflectedWheelInertia(double ratio)
        {
            double f=spec.drive==DriveType.FWD?1:spec.drive==DriveType.RWD?0:awd.mode==AwdMode.OnDemand?(awd.primaryFront?1:0):state.frontTorqueShare;
            if(spec.drive==DriveType.AWD&&(awd.isolateFailedAxle||awd.mode==AwdMode.LockedCenter)){if(!state.frontDriveAvailable)f=0;else if(!state.rearDriveAvailable)f=1;else if(awd.mode==AwdMode.LockedCenter)f=.5;}
            double frontI=Math.Max(1e-5,wheels.inertias[0]+wheels.inertias[1]),rearI=Math.Max(1e-5,wheels.inertias[2]+wheels.inertias[3]);
            double mobility=ratio*ratio*(f*f/(frontI*awd.LockedRatio*awd.LockedRatio)+(1-f)*(1-f)/rearI);
            if(spec.drive==DriveType.AWD&&awd.mode==AwdMode.LockedCenter&&state.frontDriveAvailable&&state.rearDriveAvailable)mobility=ratio*ratio/(frontI*awd.LockedRatio*awd.LockedRatio+rearI);
            if(state.openCenterFreeBranch){double gear=ratio/tune.finalDrive;mobility=0;if(state.frontDriveAvailable)mobility+=ratio*ratio*awd.frontBias*awd.frontBias/(frontI);else mobility+=gear*gear*awd.frontBias*awd.frontBias/awd.freeShaftInertiaKgM2;if(state.rearDriveAvailable)mobility+=ratio*ratio*(1-awd.frontBias)*(1-awd.frontBias)/(rearI);else mobility+=gear*gear*(1-awd.frontBias)*(1-awd.frontBias)/awd.freeShaftInertiaKgM2;}
            return 1/Math.Max(1e-12,mobility);
        }
        readonly double[] roadTireGrip=new double[4];
        void BeginRoadTick()
        {
            EnsureWheelContact();roadStartSpeed=state.speed;roadBrakeWork=state.contact.brakeWorkJ;roadCenterWork=state.contact.centerCouplingWorkJ;state.awdCouplingLossPowerW=0;state.contact.speed=state.speed;state.contact.distance=state.distance;Array.Clear(roadForces,0,4);Array.Clear(roadDriveTorque,0,4);Array.Clear(roadBrakeTorque,0,4);Array.Clear(roadContactTorque,0,4); var pack=mechanics.parts.Find(p=>p.slot==nameof(Mechanics.PartCategory.Tires)); for(int i=0;i<4;i++)roadTireGrip[i]=pack==null?0:TireDynamics.Grip(mechanics.TireDefinition(i),pack.condition.tires[i]);var rims=mechanics.parts.Find(p=>p.slot=="Wheels");if(rims?.condition.elements.Length==4)for(int i=0;i<4;i++)if(rims.condition.elements[i].Failed)roadTireGrip[i]=0;state.clutchFrictionPowerW=0;
        }
        double Friction(int wheel)=>spec.tireFriction*(mechanics.TireDefinition(wheel)?.grip??tune.GripMultiplier)*(1-Math.Abs(tune.AxlePressure(wheel<2)-26)*.007)*wheels.gripScales[wheel];
        void UpdateTorqueSplit(double dt,double ratio)
        {
            double weight=mass.TranslatingKg*DragMath.Gravity,transfer=mass.TranslatingKg*state.acceleration*mass.centerOfGravityHeightM/spec.wheelbaseM;
            state.frontLoadN=DragMath.Clamp(mass.frontNormalLoadN-transfer,0,weight);state.rearLoadN=weight-state.frontLoadN;
            AxleDriveDynamics.TorqueSplitFromCapacities(this,AxleTorqueCapacity(true)*awd.LockedRatio,AxleTorqueCapacity(false),dt);
        }
        double AxleTorqueCapacity(bool front)
        {
            int i=front?0:2;double load=(front?state.frontLoadN:state.rearLoadN)*.5;
            double left=WheelContactDynamics.GripLimit(load,Friction(i)*roadTireGrip[i]*DifferentialDynamics.Grip(front?tune.frontLeftGrip:tune.rearLeftGrip));
            double right=WheelContactDynamics.GripLimit(load,Friction(i+1)*roadTireGrip[i+1]*DifferentialDynamics.Grip(front?tune.frontRightGrip:tune.rearRightGrip));
            var axle=DifferentialDynamics.Solve(front?spec.frontDifferential:spec.rearDifferential,0,left,right,wheels.radii[i],front?state.frontDifferentialHealth:state.rearDifferentialHealth);
            return axle.capacity*wheels.radii[i];
        }
        void AdvanceContact(double dt,double wholeDt,double shaft,double ratio)
        {
            var s=state;double efficiency=shaft>=0?spec.drivetrainEfficiency:1/spec.drivetrainEfficiency;
            double port=shaft*(ratio/tune.finalDrive)*efficiency,frontPort=port*s.frontTorqueShare,rearPort=port*s.rearTorqueShare;
            bool centerLsd=spec.drive==DriveType.AWD&&awd.mode==AwdMode.LimitedSlipCenter;
            bool onDemand=spec.drive==DriveType.AWD&&awd.mode==AwdMode.OnDemand;
            if(onDemand){
                // A gearbox handover does not instantly release the AWD clutch pressure.
                if(Math.Abs(port)>1e-6)s.awdCouplingReferenceTorqueNm=Math.Abs(port);
                if(s.awdSecondaryShare<1e-8)s.awdCouplingReferenceTorqueNm=0;
                bool primary=awd.primaryFront?s.frontDriveAvailable:s.rearDriveAvailable;
                if(primary){frontPort=awd.primaryFront?port:0;rearPort=awd.primaryFront?0:port;}
                else{frontPort=awd.primaryFront?0:port*s.awdSecondaryShare;rearPort=awd.primaryFront?port*s.awdSecondaryShare:0;}
            }
            if(centerLsd&&s.openCenterFreeBranch){double frontI=s.frontDriveAvailable?(wheels.inertias[0]+wheels.inertias[1])/(tune.finalDrive*tune.finalDrive):awd.freeShaftInertiaKgM2,rearI=s.rearDriveAvailable?(wheels.inertias[2]+wheels.inertias[3])/(tune.finalDrive*tune.finalDrive):awd.freeShaftInertiaKgM2;double cap=awd.centerPreloadNm+Math.Abs(port)*.5*(awd.torqueBiasRatio-1)/(awd.torqueBiasRatio+1);double transfer=DragMath.Clamp((s.frontShaftRpm-s.rearShaftRpm)/FlywheelDynamics.RpmPerRadian/(1/frontI+1/rearI)/dt,-cap,cap);frontPort-=transfer;rearPort+=transfer;}
            double front=frontPort*tune.finalDrive/awd.LockedRatio,rear=rearPort*tune.finalDrive;
            double brakeTotal=mass.TranslatingKg*spec.BrakeDeceleration*s.brakeCapacityFactor*s.brake;
            double frontBrakeBias=s.warmupLineLock&&spec.drive!=DriveType.AWD?(spec.drive==DriveType.RWD?1:0):spec.frontBrakeBias;
            bool tires=MassDynamics.Installed(spec,Mechanics.PartCategory.Wheels)&&MassDynamics.Installed(spec,Mechanics.PartCategory.Tires);
            var input=new WheelContactInput { massKg=mass.TranslatingKg,radiusM=wheels.radius,inertiaKgM2=Math.Max(1e-5,wheels.totalInertia*.25),radii=wheels.radii,inertias=wheels.contactInertias,frontTorqueNm=s.frontDriveAvailable?front:0,rearTorqueNm=s.rearDriveAvailable?rear:0,
                normalLoadN=new[]{s.frontLoadN*.5,s.frontLoadN*.5,s.rearLoadN*.5,s.rearLoadN*.5},friction=new[]{tires?Friction(0)*roadTireGrip[0]*DifferentialDynamics.Grip(tune.frontLeftGrip):0,tires?Friction(1)*roadTireGrip[1]*DifferentialDynamics.Grip(tune.frontRightGrip):0,tires?Friction(2)*roadTireGrip[2]*DifferentialDynamics.Grip(tune.rearLeftGrip):0,tires?Friction(3)*roadTireGrip[3]*DifferentialDynamics.Grip(tune.rearRightGrip):0},
                brakeNm=new[]{brakeTotal*frontBrakeBias*.5*wheels.radii[0],brakeTotal*frontBrakeBias*.5*wheels.radii[1],brakeTotal*(1-frontBrakeBias)*.5*wheels.radii[2],brakeTotal*(1-frontBrakeBias)*.5*wheels.radii[3]},frontDifferential=spec.frontDifferential,rearDifferential=spec.rearDifferential,frontDifferentialHealth=s.frontDriveAvailable?s.frontDifferentialHealth:0,rearDifferentialHealth=s.rearDriveAvailable?s.rearDifferentialHealth:0,centerSpeedRatio=awd.LockedRatio,lockCenter=spec.drive==DriveType.AWD&&awd.mode==AwdMode.LockedCenter&&s.frontDriveAvailable&&s.rearDriveAvailable,
                centerPreloadNm=centerLsd&&!s.openCenterFreeBranch?awd.centerPreloadNm*tune.finalDrive:0,centerTorqueBiasRatio=centerLsd&&!s.openCenterFreeBranch?awd.torqueBiasRatio:1,centerClutchCapacityNm=onDemand&&s.frontDriveAvailable&&s.rearDriveAvailable?s.awdCouplingReferenceTorqueNm*tune.finalDrive*s.awdSecondaryShare:0,
                resistanceN=.5*1.225*spec.dragAreaM2*s.contact.speed*s.contact.speed+mass.TranslatingKg*DragMath.Gravity*spec.rollingResistance };
            WheelContactDynamics.Step(s.contact,input,dt);
            for(int i=0;i<4;i++){roadForces[i]+=s.contact.force[i]*dt/wholeDt;roadDriveTorque[i]+=s.contact.driveTorqueNm[i]*dt/wholeDt;roadBrakeTorque[i]+=s.contact.brakeTorqueNm[i]*dt/wholeDt;roadContactTorque[i]+=s.contact.contactTorqueNm[i]*dt/wholeDt;}
            s.frontShaftTorqueNm=frontPort/awd.LockedRatio;s.rearShaftTorqueNm=rearPort;
            if((onDemand||centerLsd)&&!s.openCenterFreeBranch){s.frontShaftTorqueNm=(s.contact.driveTorqueNm[0]+s.contact.driveTorqueNm[1])/tune.finalDrive;s.rearShaftTorqueNm=(s.contact.driveTorqueNm[2]+s.contact.driveTorqueNm[3])/tune.finalDrive;}
            if(s.openCenterFreeBranch){if(!s.frontDriveAvailable)s.frontShaftRpm=AxleDriveDynamics.AdvanceFreeShaft(awd,s.frontShaftRpm,s.frontShaftTorqueNm,dt);if(!s.rearDriveAvailable)s.rearShaftRpm=AxleDriveDynamics.AdvanceFreeShaft(awd,s.rearShaftRpm,s.rearShaftTorqueNm,dt);}
            AxleDriveDynamics.SyncConnected(this);s.speed=s.contact.speed;
        }
        void FinishRoadTick(double dt)
        {
            var s=state;s.speed=s.contact.speed;s.distance=s.contact.distance;s.acceleration=(s.speed-roadStartSpeed)/dt;s.contact.acceleration=s.acceleration;
            for(int i=0;i<4;i++){s.contact.force[i]=roadForces[i];s.contact.driveTorqueNm[i]=roadDriveTorque[i];s.contact.brakeTorqueNm[i]=roadBrakeTorque[i];s.contact.contactTorqueNm[i]=roadContactTorque[i];}
            s.brakeFrictionPowerW=Math.Max(0,s.contact.brakeWorkJ-roadBrakeWork)/dt;s.awdCouplingLossPowerW=Math.Max(0,s.contact.centerCouplingWorkJ-roadCenterWork)/dt;
            if(spec.drive==DriveType.AWD&&(awd.mode==AwdMode.OnDemand||awd.mode==AwdMode.LimitedSlipCenter)&&!s.openCenterFreeBranch){s.frontShaftTorqueNm=(roadDriveTorque[0]+roadDriveTorque[1])/tune.finalDrive;s.rearShaftTorqueNm=(roadDriveTorque[2]+roadDriveTorque[3])/tune.finalDrive;}
            s.frontContactForceN=roadForces[0]+roadForces[1];s.rearContactForceN=roadForces[2]+roadForces[3];s.frontDriveForceN=!s.drivetrainFailed&&s.frontDriveAvailable?s.frontContactForceN:0;s.rearDriveForceN=!s.drivetrainFailed&&s.rearDriveAvailable?s.rearContactForceN:0;
            s.frontLeftSlip=s.contact.slip[0];s.frontRightSlip=s.contact.slip[1];s.rearLeftSlip=s.contact.slip[2];s.rearRightSlip=s.contact.slip[3];s.frontSlip=(s.frontLeftSlip+s.frontRightSlip)*.5;s.rearSlip=(s.rearLeftSlip+s.rearRightSlip)*.5;
            s.slip=Math.Max(Math.Max(Math.Abs(s.frontLeftSlip),Math.Abs(s.frontRightSlip)),Math.Max(Math.Abs(s.rearLeftSlip),Math.Abs(s.rearRightSlip)));
            s.frontWheelAngleDegrees=s.contact.angle[0]*180/Math.PI;s.rearWheelAngleDegrees=s.contact.angle[2]*180/Math.PI;s.wheelAngleDegrees=spec.drive==DriveType.FWD?s.frontWheelAngleDegrees:s.rearWheelAngleDegrees;
            s.drivelineWheelRpm=DrivenWheelOmega*FlywheelDynamics.RpmPerRadian;AxleDriveDynamics.PublishPower(this);
        }
        double AdvancePhysicalDrive(double dt,double gas,bool manualPedal,bool launchControl,bool torqueInterrupt,bool shifting,double ratio)
        {
            BeginRoadTick();UpdateTorqueSplit(dt,ratio);int steps=(int)Math.Ceiling(dt/(1.0/960));double h=dt/steps,torqueSum=0,clutchWork=0,converterWork=0;
            for(int i=0;i<steps;i++) {
                double rpm=DrivenWheelOmega*FlywheelDynamics.RpmPerRadian*ratio,previousEngine=state.rpm,shaft;
                state.converterLossPowerW=0;
                if(state.drivetrainFailed){state.rpm=ClutchDynamics.FreeRev(spec,tune,state.rpm,gas,h,ref state.limiter,InductionDynamics.Torque(induction,state,state.rpm,gas)*state.combustionFraction,engine,state);shaft=0;}
                else if(transmission.type==TransmissionType.Automatic||transmission.type==TransmissionType.CVT)shaft=TransmissionDynamics.Converter(spec,tune,engine,induction,transmission,state,gas,rpm,shifting,h,wheels,ratio,ReflectedWheelInertia(ratio));
                else shaft=ClutchDynamics.Transmit(spec,tune,state,gas,rpm,manualPedal,launchControl,torqueInterrupt,h,induction,engine,wheels,ratio,shifting?TransmissionDynamics.Retention(transmission):1,ReflectedWheelInertia(ratio),frictionHardware);
                if(state.engineFailed)shaft=0;if(engagementPenalty>0)shaft*=.65;
                converterWork+=state.converterLossPowerW*h;
                AdvanceContact(h,dt,shaft,ratio);torqueSum+=shaft/steps;
                if(transmission.type==TransmissionType.Manual||transmission.type==TransmissionType.DCT){double slip=((previousEngine+state.rpm)*.5-(rpm+DrivenWheelOmega*FlywheelDynamics.RpmPerRadian*ratio)*.5)/FlywheelDynamics.RpmPerRadian;if(Math.Abs(slip*FlywheelDynamics.RpmPerRadian)>100)clutchWork+=Math.Max(0,shaft*slip*h);}
            }
            state.transmittedTorqueNm=torqueSum;state.clutchFrictionPowerW=clutchWork/dt;state.converterLossPowerW=converterWork/dt;FinishRoadTick(dt);return torqueSum;
        }
    }
}
