using System;

namespace DragLab.Domain
{
    public enum AwdMode { FixedSplit, OpenCenter, LockedCenter, OnDemand, ActiveBias, LimitedSlipCenter }
    [Serializable]
    public sealed class AwdSpec
    {
        public AwdMode mode = AwdMode.FixedSplit;
        public float centerSpeedRatio; // 0 preserves the legacy 1:1 center. Front wheel speed / rear wheel speed.
        public double LockedRatio=>mode==AwdMode.LockedCenter?(centerSpeedRatio==0?1:centerSpeedRatio):1;
        public float torqueBiasRatio = 3, centerPreloadNm = 35;
        public float frontBias = .5f, minimumFront = .15f, maximumFront = .85f;
        public bool primaryFront = true, isolateFailedAxle;
        public float maximumSecondary = .5f, responseSeconds = .2f, slipThreshold = .05f, preload;
        public float freeShaftInertiaKgM2 = .03f, freeShaftDragNm = .25f, freeShaftViscousDrag = .001f;
        public AwdSpec Copy() { return (AwdSpec)MemberwiseClone(); }
        public string Validate()
        {
            if (!EngineBehaviorSpec.Range(centerPreloadNm, 0, 1000) || !EngineBehaviorSpec.Range(torqueBiasRatio, 1, 10)) return "Invalid center differential torque bias.";
            if(centerSpeedRatio!=0&&!EngineBehaviorSpec.Range(centerSpeedRatio,.5,2))return "Locked center speed ratio must be 0 (legacy) or 0.5–2.";
            if (!Enum.IsDefined(typeof(AwdMode), mode)) return "Invalid AWD topology.";
            if (mode == AwdMode.OpenCenter && !EngineBehaviorSpec.Range(frontBias, .05, .95)) return "Open-center geometry needs both output shares (5–95% front).";
            if (!EngineBehaviorSpec.Range(freeShaftInertiaKgM2, .005, 2) || !EngineBehaviorSpec.Range(freeShaftDragNm, 0, 100) || !EngineBehaviorSpec.Range(freeShaftViscousDrag, 0, 1)) return "Invalid free-shaft inertia/drag.";
            if (!EngineBehaviorSpec.Range(frontBias, 0, 1) || !EngineBehaviorSpec.Range(minimumFront, 0, 1) || !EngineBehaviorSpec.Range(maximumFront, minimumFront, 1) || !EngineBehaviorSpec.Range(maximumSecondary, 0, 1) || !EngineBehaviorSpec.Range(preload, 0, 1) || !EngineBehaviorSpec.Range(responseSeconds, .01, 5) || !EngineBehaviorSpec.Range(slipThreshold, .001, .5)) return "Invalid AWD calibration.";
            return null;
        }
    }
    public static class AxleDriveDynamics
    {
        public static AwdSpec Configuration(VehicleSpec spec, Tune tune) { return Mechanics.AwdHardware.Constrain(spec,tune.awdConfigured?tune.awd:null); }
        public static bool HasDrivePath(VehicleSpec spec, AwdSpec data, bool front, bool rear)
        {
            if (spec.drive == DriveType.FWD) return front;
            if (spec.drive == DriveType.RWD) return rear;
            if ((data.mode == AwdMode.OpenCenter || data.mode == AwdMode.LimitedSlipCenter) && !data.isolateFailedAxle) return front && rear;
            if (data.mode == AwdMode.OnDemand && data.maximumSecondary == 0) return data.primaryFront ? front : rear;
            if (data.isolateFailedAxle || data.mode == AwdMode.LockedCenter) return front || rear;
            if (data.mode == AwdMode.OnDemand) return data.primaryFront ? front : rear;
            if (data.mode == AwdMode.ActiveBias) return front && data.maximumFront > 0 || rear && data.minimumFront < 1;
            if (data.mode == AwdMode.FixedSplit) return front && data.frontBias > 0 || rear && data.frontBias < 1;
            return front || rear;
        }
        public static double TorqueSplit(CarSimulation car, double demand, double frontGrip, double rearGrip, double dt)
        {
            var s=car.state;
            var fd=DifferentialDynamics.Solve(car.spec.frontDifferential,0,frontGrip*.5*DifferentialDynamics.Grip(car.tune.frontLeftGrip),frontGrip*.5*DifferentialDynamics.Grip(car.tune.frontRightGrip),car.wheels.radii[0],s.frontDifferentialHealth);
            var rd=DifferentialDynamics.Solve(car.spec.rearDifferential,0,rearGrip*.5*DifferentialDynamics.Grip(car.tune.rearLeftGrip),rearGrip*.5*DifferentialDynamics.Grip(car.tune.rearRightGrip),car.wheels.radii[2],s.rearDifferentialHealth);
            return TorqueSplitFromCapacities(car,fd.capacity*car.wheels.radii[0]*car.awd.LockedRatio,rd.capacity*car.wheels.radii[2],dt);
        }
        // Comparable capacity at the center input, not a mix of force and torque.
        public static double TorqueSplitFromCapacities(CarSimulation car,double frontCapacityNm,double rearCapacityNm,double dt)
        {
            var s=car.state;var data=car.awd;double front;
            double frontGrip=Math.Max(0,frontCapacityNm),rearGrip=Math.Max(0,rearCapacityNm);
            if (car.spec.drive != DriveType.AWD) front = car.spec.drive == DriveType.FWD ? 1 : 0;
            else if (data.mode == AwdMode.OnDemand)
            {
                double slip = data.primaryFront ? s.frontSlip : s.rearSlip;
                double secondaryGrip=data.primaryFront?rearGrip:frontGrip;
                double preload=data.preload*s.throttle*2*secondaryGrip/Math.Max(1,frontGrip+rearGrip);
                double request=data.maximumSecondary*DragMath.Clamp(Math.Max(preload,(slip-data.slipThreshold)/Math.Max(.01,data.slipThreshold*3)),0,1);
                bool primaryAvailable = data.primaryFront ? s.frontDriveAvailable : s.rearDriveAvailable;
                if (!primaryAvailable && data.isolateFailedAxle && data.maximumSecondary > 0) request = 1;
                if (data.primaryFront ? !s.rearDriveAvailable : !s.frontDriveAvailable) request = 0;
                s.awdSecondaryShare += (request - s.awdSecondaryShare) * (1 - Math.Exp(-dt / data.responseSeconds));
                front = data.primaryFront ? 1 - s.awdSecondaryShare : s.awdSecondaryShare;
            }
            else if (data.mode == AwdMode.ActiveBias || data.mode == AwdMode.LockedCenter)
            {
                double gripShare = frontGrip / Math.Max(1, frontGrip + rearGrip);
                double preferred=data.frontBias*frontGrip/Math.Max(1e-9,data.frontBias*frontGrip+(1-data.frontBias)*rearGrip);
                double target=data.mode==AwdMode.LockedCenter?gripShare:DragMath.Clamp(preferred+(s.rearSlip-s.frontSlip)*.5,data.minimumFront,data.maximumFront);
                double current = s.frontTorqueShare;
                front = data.mode == AwdMode.LockedCenter ? target : current + (target - current) * (1 - Math.Exp(-dt / data.responseSeconds));
            }
            else front = data.frontBias;
            if (car.spec.drive == DriveType.AWD && (data.isolateFailedAxle || data.mode == AwdMode.LockedCenter))
            {
                // On-demand primary failure uses the same engagement dynamics above.
                if (!s.frontDriveAvailable && (data.mode != AwdMode.OnDemand || !data.primaryFront)) front = 0;
                else if (!s.rearDriveAvailable && (data.mode != AwdMode.OnDemand || data.primaryFront)) front = 1;
            }
            s.frontTorqueShare = front; s.rearTorqueShare = 1 - front;
            return front;
        }
        public static double RoadForce(CarSimulation car, double demand, double frontGrip, double rearGrip, double dt)
        {
            var s = car.state; var data = car.awd; double front = TorqueSplit(car, demand, frontGrip, rearGrip, dt);
            double originalFront = frontGrip, originalRear = rearGrip;
            var fd = DifferentialDynamics.Solve(car.spec.frontDifferential, 0, frontGrip * .5, frontGrip * .5, car.wheels.radius, s.frontDifferentialHealth);
            var rd = DifferentialDynamics.Solve(car.spec.rearDifferential, 0, rearGrip * .5, rearGrip * .5, car.wheels.radius, s.rearDifferentialHealth);
            // Quasistatic capacity diagnostic retained for old authoring fixtures only.
            // CarSimulation never uses this demand helper to integrate road motion.
            if (car.spec.drive == DriveType.AWD && data.mode == AwdMode.LimitedSlipCenter) {
                var center = DifferentialDynamics.Solve(new DifferentialSpec { kind = DifferentialKind.LimitedSlip, torqueBiasRatio = data.torqueBiasRatio, preloadNm = data.centerPreloadNm }, demand, frontGrip, rearGrip, car.wheels.radius / car.tune.finalDrive);
                frontGrip = Math.Min(frontGrip, center.capacity * front); rearGrip = Math.Min(rearGrip, center.capacity * (1-front));
            }
            double requestedFront = demand * front, requestedRear = demand * (1 - front);
            double centerTorque = demand * car.wheels.radius / car.tune.finalDrive;
            s.frontShaftTorqueNm = centerTorque * front / data.LockedRatio; s.rearShaftTorqueNm = centerTorque * (1 - front);
            if (s.openCenterFreeBranch)
            {
                // Open-differential torque shares remain geometric. A disconnected output
                // accelerates its remaining shaft mass instead of applying force to the road.
                if (!s.frontDriveAvailable) s.frontShaftRpm = AdvanceFree(data, s.frontShaftRpm, s.frontShaftTorqueNm, dt);
                if (!s.rearDriveAvailable) s.rearShaftRpm = AdvanceFree(data, s.rearShaftRpm, s.rearShaftTorqueNm, dt);
                requestedFront = s.frontDriveAvailable ? requestedFront : 0;
                requestedRear = s.rearDriveAvailable ? requestedRear : 0;
            }
            else
            {
                SyncConnected(car);
                if (!s.frontDriveAvailable) s.frontShaftRpm = AdvanceFree(data, s.frontShaftRpm, 0, dt);
                if (!s.rearDriveAvailable) s.rearShaftRpm = AdvanceFree(data, s.rearShaftRpm, 0, dt);
            }
            s.frontShaftPowerW = s.frontShaftTorqueNm * s.frontShaftRpm / FlywheelDynamics.RpmPerRadian;
            s.rearShaftPowerW = s.rearShaftTorqueNm * s.rearShaftRpm / FlywheelDynamics.RpmPerRadian;
            double frontCap = s.frontDriveAvailable ? frontGrip : 0, rearCap = s.rearDriveAvailable ? rearGrip : 0;
            if (car.spec.drive == DriveType.AWD && data.mode == AwdMode.OpenCenter && !s.openCenterFreeBranch)
            {
                // An open center cannot multiply torque against a disconnected/free branch.
                double capacity = Math.Min(front > 1e-9 ? frontCap / front : double.MaxValue, front < 1 - 1e-9 ? rearCap / (1 - front) : double.MaxValue);
                double coupled = Math.Sign(demand) * Math.Min(Math.Abs(demand), capacity); requestedFront = coupled * front; requestedRear = coupled * (1 - front);
            }
            s.frontSlip = Slip(s.frontSlip, requestedFront, frontCap, car.driver.tractionAssist, dt);
            s.rearSlip = Slip(s.rearSlip, requestedRear, rearCap, car.driver.tractionAssist, dt);
            if (car.spec.drive == DriveType.AWD && data.mode == AwdMode.LockedCenter && s.frontDriveAvailable && s.rearDriveAvailable)
                s.frontSlip = s.rearSlip = Slip(Math.Max(s.frontSlip, s.rearSlip), demand, frontCap + rearCap, car.driver.tractionAssist, dt);
            s.frontDriveForceN = Force(requestedFront, frontCap, s.frontSlip, car.driver.tractionAssist);
            s.rearDriveForceN = Force(requestedRear, rearCap, s.rearSlip, car.driver.tractionAssist);
            fd = DifferentialDynamics.Solve(car.spec.frontDifferential, requestedFront, originalFront * .5 * DifferentialDynamics.Grip(car.tune.frontLeftGrip), originalFront * .5 * DifferentialDynamics.Grip(car.tune.frontRightGrip), car.wheels.radius, s.frontDifferentialHealth);
            rd = DifferentialDynamics.Solve(car.spec.rearDifferential, requestedRear, originalRear * .5 * DifferentialDynamics.Grip(car.tune.rearLeftGrip), originalRear * .5 * DifferentialDynamics.Grip(car.tune.rearRightGrip), car.wheels.radius, s.rearDifferentialHealth);
            double mix = Math.Min(1, dt * 15);
            s.frontLeftSlip += (fd.leftSlip - s.frontLeftSlip) * mix; s.frontRightSlip += (fd.rightSlip - s.frontRightSlip) * mix;
            s.rearLeftSlip += (rd.leftSlip - s.rearLeftSlip) * mix; s.rearRightSlip += (rd.rightSlip - s.rearRightSlip) * mix;
            s.slip = Math.Max(s.frontSlip, s.rearSlip);
            // Signed engine braking follows the same surviving path, without inventing propulsion.
            return s.frontDriveForceN + s.rearDriveForceN;
        }
        public static void SyncConnected(CarSimulation car)
        {
            var s = car.state; double road = s.speed / car.wheels.radius * FlywheelDynamics.RpmPerRadian * car.tune.finalDrive;
            if (s.frontDriveAvailable) s.frontShaftRpm = s.wheelContactInitialized ? car.FrontWheelOmega * FlywheelDynamics.RpmPerRadian * car.tune.finalDrive : s.speed/car.wheels.radii[0]*FlywheelDynamics.RpmPerRadian*car.tune.finalDrive*(1+s.frontSlip);
            if (s.rearDriveAvailable) s.rearShaftRpm = s.wheelContactInitialized ? car.RearWheelOmega * FlywheelDynamics.RpmPerRadian * car.tune.finalDrive : s.speed/car.wheels.radii[2]*FlywheelDynamics.RpmPerRadian*car.tune.finalDrive*(1+s.rearSlip);
        }
        public static double FreeInputRpm(CarSimulation car, double ratio)
        {
            var s = car.state;
            return (s.frontShaftRpm * car.awd.frontBias + s.rearShaftRpm * (1 - car.awd.frontBias)) * ratio / car.tune.finalDrive;
        }
        public static void PublishPower(CarSimulation car)
        {
            SyncConnected(car); var s = car.state;
            s.frontShaftPowerW = s.frontShaftTorqueNm * s.frontShaftRpm / FlywheelDynamics.RpmPerRadian;
            s.rearShaftPowerW = s.rearShaftTorqueNm * s.rearShaftRpm / FlywheelDynamics.RpmPerRadian;
        }
        public static double FreeReflectedInertia(AwdSpec data, CarState s, double gear)
        {
            double freedom = (!s.frontDriveAvailable ? data.frontBias * data.frontBias : 0) + (!s.rearDriveAvailable ? (1 - data.frontBias) * (1 - data.frontBias) : 0);
            return data.freeShaftInertiaKgM2 / Math.Max(1e-8, freedom * gear * gear);
        }
        public static void CoastShafts(CarSimulation car, double dt)
        {
            SyncConnected(car); var s = car.state;
            if (!s.frontDriveAvailable) s.frontShaftRpm = AdvanceFree(car.awd, s.frontShaftRpm, 0, dt);
            if (!s.rearDriveAvailable) s.rearShaftRpm = AdvanceFree(car.awd, s.rearShaftRpm, 0, dt);
            s.frontShaftTorqueNm = s.rearShaftTorqueNm = s.frontShaftPowerW = s.rearShaftPowerW = 0;
        }
        public static double AdvanceFreeShaft(AwdSpec data, double rpm, double torque, double dt) { return AdvanceFree(data, rpm, torque, dt); }
        static double AdvanceFree(AwdSpec data, double rpm, double torque, double dt)
        {
            double omega = rpm / FlywheelDynamics.RpmPerRadian;
            double friction = data.freeShaftDragNm + data.freeShaftViscousDrag * omega;
            return Math.Max(0, omega + (torque - friction) / data.freeShaftInertiaKgM2 * dt) * FlywheelDynamics.RpmPerRadian;
        }
        static double Slip(double previous, double force, double grip, bool assist, double dt)
        {
            double target = grip <= 0 ? 0 : DragMath.Clamp(Math.Max(0, force / grip - 1) * .4, 0, 1.5);
            double slip = previous + (target - previous) * Math.Min(1, dt * 15);
            return assist ? Math.Min(slip, .08) : slip;
        }
        static double Force(double requested, double cap, double slip, bool assist)
        { if (cap <= 0) return 0; return DragMath.Clamp(requested, -cap, cap) * (assist ? 1 : 1 - DragMath.Clamp(slip - .08, 0, .6) * .4); }
    }
}
