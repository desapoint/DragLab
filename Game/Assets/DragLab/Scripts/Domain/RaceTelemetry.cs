using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragLab.Domain
{
    public sealed class TelemetrySample
    {
        public double time, distance, speedKmh, rpm, slip, throttle, clutch, brake, accelerationG, clutchSlipRpm, transmittedTorqueNm;
        public int gear;
        public double boostBar, turboBoostBar, superchargerBoostBar, turboSpool, engineTorqueNm, engineHorsepower, superchargerDriveTorqueNm, chargeTemperatureK, airflowKgS;
        public double effectiveRollingMassKg, wheelRadiusM, combustionFraction, fuelHesitation, engineTemperature, exhaustHeat, engineDamage, cvtRatio, converterSlipRpm;
        public bool stalled, engineFailed, launchControlActive, antiLagActive;
        public double flywheelInertiaKgM2, engineRotationalInertiaKgM2, flywheelEnergyJ, flywheelAngularMomentum;
        public double frontShare, rearShare, frontSlipEstimate, rearSlipEstimate, frontForceN, rearForceN, brakeCapacity;
        public bool frontAvailable, rearAvailable, brakesFailed, drivetrainFailed;
        public double frontShaftRpm, rearShaftRpm, frontShaftNm, rearShaftNm, frontShaftPowerW, rearShaftPowerW;
        public bool openCenterFreeBranch;
        public double frontLeftSlip, frontRightSlip, rearLeftSlip, rearRightSlip, coolingHeatRejectedW;
    }

    // Bounded 10 Hz samples use simulation time, independent of render frequency.
    public sealed class RaceTelemetry
    {
        public readonly List<TelemetrySample> samples = new List<TelemetrySample>();
        public double peakLaunchSlip;
        public double boggingSeconds;
        public double excessClutchSlipSeconds;
        double nextSample;
        public void Record(CarSimulation car, double greenTime, double dt = DragMath.FixedStep)
        {
            var s = car.state;
            if (!s.launched || s.disqualified || s.finished) return;
            double elapsed = Math.Max(0, greenTime - s.launchTime);
            if (elapsed <= 2) peakLaunchSlip = Math.Max(peakLaunchSlip, s.slip);
            if (s.bogging && !s.finished) boggingSeconds += dt;
            if (elapsed > 2 && s.clutch < .3 && s.clutchSlipRpm > 650 && !s.finished) excessClutchSlipSeconds += dt;
            if (elapsed + .000001 < nextSample || samples.Count >= 1000) return;
            nextSample = elapsed + .1;
            samples.Add(new TelemetrySample { time = elapsed, distance = s.distance, speedKmh = s.speed * 3.6,
                rpm = s.rpm, gear = s.gear, slip = s.slip, throttle = s.throttle, clutch = s.clutch, brake = s.brake,
                accelerationG = s.acceleration / DragMath.Gravity, clutchSlipRpm = s.clutchSlipRpm, transmittedTorqueNm = s.transmittedTorqueNm,
                boostBar = s.boostBar, turboBoostBar = s.turboBoostBar, superchargerBoostBar = s.superchargerBoostBar, turboSpool = s.turboSpool,
                engineTorqueNm = s.engineTorqueNm, engineHorsepower = s.engineHorsepower, superchargerDriveTorqueNm = s.superchargerDriveTorqueNm,
                chargeTemperatureK = s.chargeTemperatureK, airflowKgS = s.airflowKgS,
                effectiveRollingMassKg = s.effectiveRollingMassKg, wheelRadiusM = s.wheelRadiusM, combustionFraction = s.combustionFraction,
                fuelHesitation = s.fuelHesitation, engineTemperature = s.engineTemperature, exhaustHeat = s.exhaustHeat, engineDamage = s.engineDamage,
                cvtRatio = s.cvtRatio, converterSlipRpm = s.converterSlipRpm, stalled = s.stalled, engineFailed = s.engineFailed,
                launchControlActive = s.launchControlActive, antiLagActive = s.antiLagActive,
                flywheelInertiaKgM2 = s.flywheelInertiaKgM2, engineRotationalInertiaKgM2 = s.engineRotationalInertiaKgM2,
                flywheelEnergyJ = s.flywheelEnergyJ, flywheelAngularMomentum = s.flywheelAngularMomentum });
            var sample = samples[samples.Count - 1]; sample.frontShare = s.frontTorqueShare; sample.rearShare = s.rearTorqueShare;
            sample.frontSlipEstimate = s.frontSlip; sample.rearSlipEstimate = s.rearSlip; sample.frontForceN = s.frontDriveForceN; sample.rearForceN = s.rearDriveForceN;
            sample.frontAvailable = s.frontDriveAvailable; sample.rearAvailable = s.rearDriveAvailable; sample.brakeCapacity = s.brakeCapacityFactor; sample.brakesFailed = s.brakesFailed; sample.drivetrainFailed = s.drivetrainFailed;
            sample.frontLeftSlip = s.frontLeftSlip; sample.frontRightSlip = s.frontRightSlip; sample.rearLeftSlip = s.rearLeftSlip; sample.rearRightSlip = s.rearRightSlip; sample.coolingHeatRejectedW = s.coolingHeatRejectedW;
            sample.frontShaftRpm = s.frontShaftRpm; sample.rearShaftRpm = s.rearShaftRpm; sample.frontShaftNm = s.frontShaftTorqueNm; sample.rearShaftNm = s.rearShaftTorqueNm; sample.frontShaftPowerW = s.frontShaftPowerW; sample.rearShaftPowerW = s.rearShaftPowerW; sample.openCenterFreeBranch = s.openCenterFreeBranch;
        }
        public string Analysis(CarSimulation car)
        {
            if (car.state.disqualified) return "Launch after green; a red light earns no reward.";
            var tips = new List<string>();
            if (car.state.brakesFailed) tips.Add("Brakes failed: propulsion still works, but friction braking is unavailable. Replace the brakes.");
            if (car.state.drivetrainFailed) tips.Add("Drive path disconnected: the engine can run, but no torque reaches the road. Inspect shafts, differentials, axle and transmission.");
            else if (car.state.openCenterFreeBranch) tips.Add("Open center spins the disconnected shaft; most shaft power enters the free branch and useful road torque is limited. Replace the broken branch.");
            else if (car.spec.drive == DriveType.AWD && (!car.state.frontDriveAvailable || !car.state.rearDriveAvailable)) tips.Add("AWD is driving through one surviving axle. Replace its disconnected branch to regain full traction.");
            if (car.state.engineFailed) tips.Add("Engine failed: reduce the RPM limit or sustained anti-lag use.");
            if (car.state.stalled) tips.Add("Engine stalled: clutch in + Enter to restart; use more launch RPM or release gradually, especially with heavy wheels.");
            if (peakLaunchSlip > .2) tips.Add("Heavy launch wheelspin: reduce launch RPM or improve tires.");
            if (boggingSeconds > .3) tips.Add("Launch bogged down: stage more RPM or release the clutch more gradually.");
            if (excessClutchSlipSeconds > .5) tips.Add("Clutch slipped after launch: reduce torque or upgrade the gearbox/clutch capacity.");
            if (car.state.roughEngagements > 0) tips.Add("Rough clutch engagement: match RPM before releasing the pedal.");
            if (car.state.earlyShifts > 0) tips.Add(car.state.earlyShifts + " early shifts: aim for the torque-based shift marker.");
            if (car.state.lateShifts > 0) tips.Add(car.state.lateShifts + " late shifts: shift before the limiter.");
            if (!car.state.finished) tips.Add("Incomplete run: check throttle, clutch and gearing.");
            if (tips.Count == 0) tips.Add("Clean pass. Compare gearing and tire pressure against your saved tune.");
            return string.Join("\n", tips);
        }
        public string Csv()
        {
            var text = new StringBuilder("time_s,distance_m,speed_kmh,rpm,gear,slip_ratio,throttle,clutch,acceleration_g,clutch_slip_rpm,shaft_torque_nm,brake,boost_bar,turbo_boost_bar,supercharger_boost_bar,turbo_spool,engine_torque_nm,engine_hp,supercharger_drive_nm,charge_temperature_k,airflow_kg_s,effective_rolling_mass_kg,wheel_radius_m,combustion_fraction,fuel_hesitation,engine_warm_fraction,exhaust_heat,engine_damage,cvt_ratio,converter_slip_rpm,stalled,engine_failed,two_step,anti_lag,flywheel_inertia_kg_m2,total_engine_inertia_kg_m2,flywheel_energy_j,flywheel_angular_momentum_kg_m2_s\n");
            foreach (var s in samples)
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "{0:F4},{1:F3},{2:F3},{3:F1},{4},{5:F4},{6:F3},{7:F3},{8:F4},{9:F2},{10:F3},{11:F3},{12:F4},{13:F4},{14:F4},{15:F4},{16:F3},{17:F3},{18:F3},{19:F2},{20:F5},",
                    s.time, s.distance, s.speedKmh, s.rpm, s.gear, s.slip, s.throttle, s.clutch, s.accelerationG, s.clutchSlipRpm, s.transmittedTorqueNm, s.brake,
                    s.boostBar, s.turboBoostBar, s.superchargerBoostBar, s.turboSpool, s.engineTorqueNm, s.engineHorsepower, s.superchargerDriveTorqueNm, s.chargeTemperatureK, s.airflowKgS);
                text.AppendFormat(CultureInfo.InvariantCulture, "{0:F3},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F2},{9},{10},{11},{12},{13:F5},{14:F5},{15:F3},{16:F4}\n",
                    s.effectiveRollingMassKg, s.wheelRadiusM, s.combustionFraction, s.fuelHesitation, s.engineTemperature, s.exhaustHeat,
                    s.engineDamage, s.cvtRatio, s.converterSlipRpm, s.stalled, s.engineFailed, s.launchControlActive, s.antiLagActive,
                    s.flywheelInertiaKgM2, s.engineRotationalInertiaKgM2, s.flywheelEnergyJ, s.flywheelAngularMomentum);
            }
            var rows = text.ToString().TrimEnd('\n').Split('\n');
            var enriched = new StringBuilder(rows[0] + ",front_share,rear_share,front_slip_ESTIMATE,rear_slip_ESTIMATE,front_force_n,rear_force_n,front_available,rear_available,brake_capacity,brakes_failed,drivetrain_failed,front_shaft_rpm,rear_shaft_rpm,front_shaft_nm,rear_shaft_nm,front_shaft_power_w,rear_shaft_power_w,open_center_free_branch,front_left_slip_ESTIMATE,front_right_slip_ESTIMATE,rear_left_slip_ESTIMATE,rear_right_slip_ESTIMATE,cooling_heat_rejected_w\n");
            for (int i = 0; i < samples.Count; i++) { var s = samples[i]; enriched.Append(rows[i + 1]); enriched.AppendFormat(CultureInfo.InvariantCulture, ",{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6},{7},{8:R},{9},{10},{11:R},{12:R},{13:R},{14:R},{15:R},{16:R},{17},{18:R},{19:R},{20:R},{21:R},{22:R}\n", s.frontShare, s.rearShare, s.frontSlipEstimate, s.rearSlipEstimate, s.frontForceN, s.rearForceN, s.frontAvailable, s.rearAvailable, s.brakeCapacity, s.brakesFailed, s.drivetrainFailed, s.frontShaftRpm, s.rearShaftRpm, s.frontShaftNm, s.rearShaftNm, s.frontShaftPowerW, s.rearShaftPowerW, s.openCenterFreeBranch, s.frontLeftSlip, s.frontRightSlip, s.rearLeftSlip, s.rearRightSlip, s.coolingHeatRejectedW); }
            return enriched.ToString();
        }
    }
}
