using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public enum FuelKind { Petrol, Diesel }
    public enum PistonCrown { Flat, Dished, Domed, DieselBowl }
    [Serializable] public sealed class CombustionSpec
    {
        public FuelKind fuelKind;
        public float referenceCompression=10.3f, compressionRatio=10.3f, referenceCrownCc, crownCc;
        public float octaneRon=95, cetane=50, referenceValveClearanceMm=1.8f, valveReliefMm=2.3f;
        public float valveClearanceMm=1.8f;
        public PistonCrown crown;
        public bool separateSparkPlugs;
        public CombustionSpec Copy(){return (CombustionSpec)MemberwiseClone();}
        public string Validate(){return Enum.IsDefined(typeof(FuelKind),fuelKind)&&Enum.IsDefined(typeof(PistonCrown),crown)&&EngineBehaviorSpec.Range(referenceCompression,6,25)&&EngineBehaviorSpec.Range(compressionRatio,5,30)&&EngineBehaviorSpec.Range(octaneRon,70,120)&&EngineBehaviorSpec.Range(cetane,30,70)&&EngineBehaviorSpec.Range(valveClearanceMm,-10,10)?null:"Invalid combustion geometry/fuel specification.";}
    }
    public static class CombustionHardware
    {
        public static bool Diesel(VehicleSpec s)=>s.combustion.fuelKind==FuelKind.Diesel;
        public static double CompressionMultiplier(VehicleSpec s)=>Math.Pow(s.combustion.compressionRatio/s.combustion.referenceCompression,.32);
        public static double Knock(VehicleSpec spec,CarState state)
        {
            if(Diesel(spec))return 0;
            double factory=spec.induction.factory==InductionType.NaturallyAspirated?0:spec.induction.turboBoostBar+ (spec.induction.factory==InductionType.Twincharged?spec.induction.superchargerBoostBar:spec.induction.factory==InductionType.Supercharger?spec.induction.superchargerBoostBar-spec.induction.turboBoostBar:0);
            double safe=spec.combustion.referenceCompression+.65+(spec.combustion.octaneRon-95)*.14-Math.Max(0,state.boostBar-factory)*1.6-Math.Max(0,state.chargeTemperatureK-335)*.018;
            return DragMath.Clamp((spec.combustion.compressionRatio-safe)*.35*state.throttle*state.throttle,0,1);
        }
        public static void ApplyPistons(PartDefinition p,VehicleSpec spec)
        {
            var c=spec.combustion;double swept=spec.DisplacementLitres*1000/spec.sound.cylinders;
            double chamber=swept/(c.referenceCompression-1)-c.referenceCrownCc;
            c.compressionRatio=(float)(1+swept/Math.Max(2,chamber+p.crownVolumeCc));c.crown=p.pistonCrown;c.crownCc=p.crownVolumeCc;c.valveReliefMm=p.valveReliefMm;
        }
        public static void ValidateCombination(BuildResolution build,IDictionary<PartCategory,string> map,MechanicalCatalog catalog)
        {
            var s=build.spec;PartDefinition Part(PartCategory c)=>map.TryGetValue(c,out var id)?catalog.Find(id):null;
            var cam=Part(PartCategory.Camshaft);var followers=Part(PartCategory.Followers);var spring=Part(PartCategory.ValveSprings);
            if(cam!=null&&followers!=null&&!string.IsNullOrEmpty(cam.followerInterface)&&cam.followerInterface!=followers.followerInterface)build.errors.Add("Camshaft requires "+cam.followerInterface+" followers; fitted "+followers.followerInterface+" cannot mate with it.");
            if(cam!=null&&spring!=null&&cam.camLiftMm>spring.springMaxLiftMm)build.warnings.Add("Cam lift exceeds spring travel: coil binding can damage cam/followers. Fit springs rated for at least "+cam.camLiftMm.ToString("F1")+" mm lift.");
            if(s.valvetrain.profileDemandRpm>s.valvetrain.springSafeRpm)build.warnings.Add("Cam RPM demand exceeds spring control rating: valve float reduces power and can damage valves. Use matched springs or shift earlier.");
            s.combustion.valveClearanceMm=s.combustion.referenceValveClearanceMm+(s.combustion.valveReliefMm-2.3f)*.6f-(s.valvetrain.liftMm-8)*.35f-Math.Abs(s.valvetrain.camTimingDegrees)*.07f;
            if(s.combustion.valveClearanceMm<1.2)build.warnings.Add("Low estimated piston-to-valve clearance: "+s.combustion.valveClearanceMm.ToString("F2")+" mm. Use a lower-lift cam, deeper piston reliefs or less timing offset; interference contact can stop the engine.");
            if(!Diesel(s)&&s.combustion.compressionRatio>s.combustion.referenceCompression+.65)build.warnings.Add("Compression exceeds the factory fuel margin: knock is possible under load/boost. Use dished pistons, lower boost or suitable higher-octane fuel.");
        }
        public static void Prepare(CarSimulation car)
        {
            var s=car.state;var v=car.spec.valvetrain;
            s.knockSeverity=Knock(car.spec,s);
            s.valveBind=DragMath.Clamp((v.liftMm-v.springMaxLiftMm)/Math.Max(1,v.springMaxLiftMm)*6,0,1);
            s.valveContact=car.spec.interferenceEngine?DragMath.Clamp((.6-car.spec.combustion.valveClearanceMm)/.6,0,1):0;
            s.mechanicalPowerFactor*=1-s.knockSeverity*.4; s.mechanicalPowerFactor*=1-s.valveBind*.35;
        }
        public static void Tick(CarSimulation car,double dt)
        {
            var s=car.state;if(car.driver.WearRate<=0||s.stalled||s.engineFailed)return;
            foreach(var slot in new[]{PartCategory.Pistons,PartCategory.PistonRings,PartCategory.Camshaft,PartCategory.Followers,PartCategory.Valves}) {
                var p=car.mechanics.Part(slot);if(p==null||p.condition.failed)continue;
                double abuse=slot==PartCategory.Pistons||slot==PartCategory.PistonRings?s.knockSeverity*s.knockSeverity*.018:s.valveBind*s.valveBind*.06+s.valveContact*s.valveContact*1.5;
                double loss=abuse*dt*car.driver.WearRate*Math.Max(.15,s.rpm/car.spec.redlineRpm);
                EngineElements.Wear(p,loss);p.condition.health=(float)Math.Max(0,p.condition.health-loss);
                if(p.condition.health<=0){p.condition.failed=true;p.condition.failureReason=s.knockSeverity>.1?"Combustion knock damage":s.valveContact>.1?"Piston-to-valve contact":"Spring coil bind";if(MechanicalCatalog.DisablingEngineFailure(slot))s.engineFailed=true;}
            }
        }
        public static string Family(VehicleSpec s)=>string.IsNullOrEmpty(s.engineFamily)?s.id:s.engineFamily;
        public static string Chassis(VehicleSpec s)=>string.IsNullOrEmpty(s.chassisFamily)?s.id:s.chassisFamily;
    }
}
