using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain
{
    public static class MassDynamics
    {
        public static void ApplyClutchFluids(MassBreakdown mass,VehicleSpec spec,IEnumerable<PartInstance> parts,PartDefinition clutchDefinition=null)
        {
            var clutch=parts.FirstOrDefault(p=>p.slot==PartCategory.Clutch.ToString());var d=clutchDefinition??Array.Find(spec.fittedParts,p=>p.category==PartCategory.Clutch);
            if(clutch==null||d==null||d.clutchEnvironment!=ClutchEnvironment.Wet)return;
            double delta=(clutch.condition.clutchOilLitres-d.clutchOilCapacityLitres)*.86,oldTotal=mass.TranslatingKg;
            // Authored installed mass contains the filled clutch bath; subtract only missing oil.
            double xMoment=mass.centerOfGravityFromFrontM*oldTotal+delta*d.positionFromFrontAxleM;
            double yMoment=mass.centerOfGravityHeightM*oldTotal+delta*d.heightM;
            mass.clutchFluidDeltaKg=delta;mass.dryInstalledKg+=delta;mass.installedComponentKg+=delta;
            double total=mass.TranslatingKg;mass.centerOfGravityFromFrontM=xMoment/total;mass.centerOfGravityHeightM=yMoment/total;
            mass.frontWeightFraction=DragMath.Clamp(1-xMoment/(total*spec.wheelbaseM),.05,.95);
            mass.frontNormalLoadN=total*DragMath.Gravity*mass.frontWeightFraction;mass.rearNormalLoadN=total*DragMath.Gravity*(1-mass.frontWeightFraction);
        }
        // Curb reference includes factory wheels, flywheel and a full tank. Replace
        // their deltas once; rotating inertia is never a gravitational load.
        public static string ValidateInputs(VehicleSpec spec, Tune tune) { if(tune==null || !EngineBehaviorSpec.Range(tune.driverMassKg,0,200) || !EngineBehaviorSpec.Range(tune.payloadMassKg,0,1000) || !EngineBehaviorSpec.Range(tune.payloadPositionM,-.5,spec.wheelbaseM+.5) || !EngineBehaviorSpec.Range(tune.payloadHeightM,0,2)) return "Invalid mass/placement inputs.";return null; }
        public static bool Installed(VehicleSpec spec, Mechanics.PartCategory slot) { return !spec.hasInstalledParts || Array.Exists(spec.fittedParts ?? new Mechanics.PartDefinition[0], p=>p.category==slot); }
        public static double ReferenceBodyMass(VehicleSpec spec) { return (spec.massKg - spec.fuel.capacityLitres * spec.fuel.densityKgPerLitre) * .35; }
        public static MassBreakdown Resolve(VehicleSpec spec, Tune tune, WheelProfile wheels, double fuelLitres, FluidState fluids=null)
        {
            var tank = spec.fuel; double referenceFuel = tank.capacityLitres * tank.densityKgPerLitre;
            double bodyReference = ReferenceBodyMass(spec);
            double bodyDelta = spec.hasStructuralBody ? bodyReference * (tune.MassMultiplier - 1) : -bodyReference;
            double baseDry = spec.massKg - referenceFuel + bodyDelta;
            double flywheelDelta = FlywheelDynamics.Mass(spec, tune) - spec.flywheel.massKg;
            double frontWheelDelta=wheels.assemblyMasses[0]+wheels.assemblyMasses[1]-2*spec.wheels.assemblyMassKg,rearWheelDelta=wheels.assemblyMasses[2]+wheels.assemblyMasses[3]-2*spec.wheels.assemblyMassKg;
            double wheelDelta=frontWheelDelta+rearWheelDelta;
            var fluidSpec=spec.fluids??new FluidSpec();var currentFluids=fluids??FluidState.Full(fluidSpec);
            double fluidDelta=(currentFluids.oilLitres-fluidSpec.referenceOilLitres)*.86+(currentFluids.coolantLitres-fluidSpec.referenceCoolantLitres)*1.05;
            double dry = baseDry + spec.partMassDeltaKg + flywheelDelta + wheelDelta + fluidDelta;
            double baselineX = (spec.massKg * spec.wheelbaseM * (1 - spec.frontWeightFraction) - referenceFuel * tank.positionFromFrontAxleM) + bodyDelta * spec.wheelbaseM * .5;
            double baselineHeight = (spec.massKg * spec.centerOfGravityM - referenceFuel * tank.heightM) + bodyDelta * .6;
            double fuel = fuelLitres * tank.densityKgPerLitre;
            double total = dry + tune.driverMassKg + tune.payloadMassKg + fuel;
            double xMoment = baselineX + fluidDelta*spec.wheelbaseM*.15 + spec.partLongitudinalMomentKgM + flywheelDelta * spec.wheelbaseM * .15 + rearWheelDelta * spec.wheelbaseM + tune.driverMassKg * spec.wheelbaseM * .45 + tune.payloadMassKg * tune.payloadPositionM + fuel * tank.positionFromFrontAxleM;
            double heightMoment = baselineHeight + fluidDelta*.35 + spec.partVerticalMomentKgM + flywheelDelta * .4 + Enumerable.Range(0,4).Sum(i=>wheels.assemblyMasses[i]*wheels.radii[i])-4*spec.wheels.assemblyMassKg*spec.wheelRadiusM + tune.driverMassKg * .65 + tune.payloadMassKg * tune.payloadHeightM + fuel * tank.heightM;
            double bodyMass=total-4*wheels.assemblyMass,bodyX=xMoment-(wheels.assemblyMasses[2]+wheels.assemblyMasses[3])*spec.wheelbaseM;
            if(bodyMass>0){double frontLift=wheels.radii[0]-spec.wheelRadiusM,rearLift=wheels.radii[2]-spec.wheelRadiusM;heightMoment+=bodyMass*frontLift+(rearLift-frontLift)*bodyX/spec.wheelbaseM;}
            double fraction = DragMath.Clamp(1 - xMoment / (total * spec.wheelbaseM), .05, .95);
            return new MassBreakdown { chassisKg = spec.hasStructuralBody ? bodyReference * tune.MassMultiplier : 0, installedComponentKg = dry - (spec.hasStructuralBody ? bodyReference * tune.MassMultiplier : 0), dryInstalledKg = dry, driverKg = tune.driverMassKg, payloadKg = tune.payloadMassKg, fuelKg = fuel, centerOfGravityFromFrontM = xMoment / total, centerOfGravityHeightM = heightMoment / total, frontWeightFraction = fraction, frontNormalLoadN = total * DragMath.Gravity * fraction, rearNormalLoadN = total * DragMath.Gravity * (1 - fraction), engineInertiaKgM2 = spec.RotationalInertia(tune), wheelInertiaKgM2 = wheels.totalInertia };
        }
    }
}
