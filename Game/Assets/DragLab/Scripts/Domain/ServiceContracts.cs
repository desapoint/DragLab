using System;
namespace DragLab.Domain.Mechanics
{
    public enum ConsumptionMode { Realistic, Reduced, None }
    public enum ConditionEffectMode { Full, Reduced, Assisted }
    public enum ServiceAssist { Manual, AutoTopUp }
    public enum ServiceAction { Clean, Adjust, Reseal, Recondition }
    [Serializable] public sealed class FluidSpec
    {
        public float oilCapacityLitres=5, oilMinimumLitres=2.5f, coolantCapacityLitres=7.5f, recoveryCapacityLitres=.75f;
        public float referenceOilLitres=5, referenceCoolantLitres=7.5f;
        public float capPressureBar=1.1f, pumpFlowScale=1, oilPumpScale=1, oilCoolerWattsPerC;
        public float oilPanBaffling=.3f;
        public FluidSpec Copy(){return (FluidSpec)MemberwiseClone();}
        public string Validate(){return EngineBehaviorSpec.Range(oilCapacityLitres,1,30)&&EngineBehaviorSpec.Range(oilMinimumLitres,.1,oilCapacityLitres)&&EngineBehaviorSpec.Range(coolantCapacityLitres,1,40)&&EngineBehaviorSpec.Range(recoveryCapacityLitres,0,coolantCapacityLitres*.4)&&EngineBehaviorSpec.Range(capPressureBar,.1,3)&&EngineBehaviorSpec.Range(pumpFlowScale,.1,3)&&EngineBehaviorSpec.Range(oilPumpScale,.1,3)&&EngineBehaviorSpec.Range(referenceOilLitres,0,30)&&EngineBehaviorSpec.Range(referenceCoolantLitres,0,40)&&EngineBehaviorSpec.Range(oilPanBaffling,0,1)&&EngineBehaviorSpec.Range(oilCoolerWattsPerC,0,2000)?null:"Invalid fluid hardware capacities.";}
    }
    [Serializable] public sealed class FluidState
    {
        public double oilLitres, coolantLitres, oilTemperatureC=80, oilPressureBar=2, coolantPressureBar;
        public double oilCondition=1, oilConsumedLitres, oilLeakedLitres, coolantLostLitres, serviceElapsedSeconds;
        public double oilBurnLps, oilLeakLps, coolantLeakLps, coolantVentLps;
        public FluidState Copy(){return (FluidState)MemberwiseClone();}
        public static FluidState Full(FluidSpec spec){return new FluidState{oilLitres=spec.oilCapacityLitres,coolantLitres=spec.coolantCapacityLitres};}
        public string Validate(FluidSpec spec){foreach(var f in typeof(FluidState).GetFields())if(f.FieldType==typeof(double)&&!MechanicsSchema.Finite((double)f.GetValue(this)))return "Nonfinite fluid state.";return oilLitres<0||oilLitres>30||coolantLitres<0||coolantLitres>40||oilPressureBar<0||oilPressureBar>30||coolantPressureBar<0||coolantPressureBar>10||oilBurnLps<0||oilLeakLps<0||coolantLeakLps<0||coolantVentLps<0||oilCondition<0||oilCondition>1||oilTemperatureC< -50||oilTemperatureC>500||oilConsumedLitres<0||oilLeakedLitres<0||coolantLostLitres<0||serviceElapsedSeconds<0?"Invalid fluid quantity/history.":null;}
    }
    [Serializable] public sealed class ValvetrainSpec
    {
        public float liftMm=8, durationAt050Degrees=200, overlapDegrees=10, lobeSeparationDegrees=114;
        public float camTorqueBias, camTimingDegrees, springSafeRpm=8500, profileDemandRpm=7500;
        public float springMaxLiftMm=9.2f;
        public bool timingAdjustable;
        public ValvetrainSpec Copy(){return (ValvetrainSpec)MemberwiseClone();}
        public string Validate(){return EngineBehaviorSpec.Range(springMaxLiftMm,1,30)&&EngineBehaviorSpec.Range(liftMm,1,30)&&EngineBehaviorSpec.Range(durationAt050Degrees,80,360)&&EngineBehaviorSpec.Range(overlapDegrees,0,180)&&EngineBehaviorSpec.Range(lobeSeparationDegrees,60,140)&&EngineBehaviorSpec.Range(camTorqueBias,-.5,.5)&&EngineBehaviorSpec.Range(camTimingDegrees,-6,6)&&EngineBehaviorSpec.Range(springSafeRpm,500,25000)&&EngineBehaviorSpec.Range(profileDemandRpm,500,25000)?null:"Invalid valvetrain profile.";}
    }
    // Reserved compatibility metadata only. No nitrous delivery or power bonus is implemented.
    [Serializable] public sealed class NitrousCompatibility
    { public bool wetSystemCompatible=true, drySystemRequiresInjection=true; public string provenance="Future nitrous support; no fitted system."; }
}
