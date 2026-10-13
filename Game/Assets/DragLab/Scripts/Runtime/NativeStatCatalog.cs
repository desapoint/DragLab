using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed class NativeStatsSnapshot
    {
        internal CarInspection Inspection { get; }
        internal CarInspection DrivingInspection { get; set; }
        internal CarInspection Driving => DrivingInspection ?? Inspection;
        internal bool DrivingPending;
        internal string DrivingError;
        internal VehicleSpec Spec => Inspection.source.build.spec;
        internal EngineBehaviorProfile Engine { get; }
        internal Tune Tune => Inspection.source.build.tune;
        internal InductionProfile Induction { get; }
        internal TransmissionSpec Transmission { get; }
        internal AwdSpec Awd { get; }
        readonly Dictionary<string,PartInstance> parts;
        readonly Dictionary<PartCategory,PartDefinition> definitions;
        internal NativeStatsSnapshot(CarInspection inspection)
        {
            Inspection=inspection; Engine=new EngineBehaviorProfile(Spec,Tune);
            Induction=new InductionProfile(Spec,Tune);Transmission=TransmissionDynamics.Configuration(Spec,Tune);Awd=AxleDriveDynamics.Configuration(Spec,Tune);
            parts=inspection.source.components.ToDictionary(p=>p.slot);definitions=(Spec.fittedParts??new PartDefinition[0]).ToDictionary(p=>p.category);
        }
        internal PartInstance Part(PartCategory slot)=>parts.TryGetValue(slot.ToString(),out var p)?p:null;
        internal PartDefinition Definition(PartCategory slot)=>Part(slot)!=null&&definitions.TryGetValue(slot,out var p)?p:null;
        internal bool Has(PartCategory slot)=>Part(slot)!=null;
    }

    internal sealed class NativeStatDefinition
    {
        internal string Id, Label, Help, Unit, Format;
        internal double Minimum, Maximum;
        internal int BenefitDirection;
        internal Func<NativeStatsSnapshot,double?> Number;
        internal Func<NativeStatsSnapshot,string> Text;
        internal Func<NativeStatsSnapshot,string> Detail;
        internal string Explanation(NativeStatsSnapshot snapshot)=>Detail==null?Help:Detail(snapshot);
        internal string InfoExplanation(NativeStatsSnapshot current,NativeStatsSnapshot proposed=null)
        {
            if(Number!=null)return NativeStatHelp.Explain(this,current,proposed);
            if(proposed==null)return Explanation(current);
            if(!Id.StartsWith("warning:",StringComparison.Ordinal))return Help;
            return string.Join("\n\n",new[] {Explanation(current),Explanation(proposed)}.Where(text=>!string.IsNullOrWhiteSpace(text)).Distinct());
        }
        internal bool Differs(NativeStatsSnapshot current,NativeStatsSnapshot proposed)
        {
            if(proposed==null)return false;
            if(Text!=null)return !string.Equals(Text(current),Text(proposed),StringComparison.Ordinal);
            var a=Number(current);var b=Number(proposed);
            return a.HasValue!=b.HasValue||a.HasValue&&Math.Abs(a.Value-b.Value)>=.00001;
        }
        internal double Convert(double value,UnitPreferences units)=>Unit=="Nm"?DisplayUnits.Torque(value,units.torque):Unit=="bar"?DisplayUnits.Pressure(value,units.pressure):Unit=="m/s"?DisplayUnits.Speed(value,units.speed):value;
        internal string UnitLabel(UnitPreferences units)=>Unit=="Nm"?DisplayUnits.TorqueLabel(units.torque):Unit=="bar"?DisplayUnits.PressureLabel(units.pressure):Unit=="m/s"?DisplayUnits.SpeedLabel(units.speed):Unit;
        internal string Value(NativeStatsSnapshot snapshot,UnitPreferences units)
        {
            if(Text!=null)return Text(snapshot);
            var number=Number(snapshot);return number.HasValue?Convert(number.Value,units).ToString(Format)+(string.IsNullOrEmpty(Unit)?"":" "+UnitLabel(units)):"—";
        }
    }
    internal sealed class NativeStatGroup
    {
        internal string Id { get; }
        internal string Title { get; }
        internal IReadOnlyList<NativeStatDefinition> Definitions { get; }
        internal IReadOnlyList<NativeStatGroup> Children { get; set; }=Array.AsReadOnly(new NativeStatGroup[0]);
        internal bool InitiallyCollapsed { get; set; }
        internal NativeStatGroup(string id,string title,params NativeStatDefinition[] definitions)
        { Id=id;Title=title;Definitions=Array.AsReadOnly(definitions); }
    }
    internal static partial class NativeStatCatalog
    {
        static NativeStatDefinition N(string id,string label,Func<NativeStatsSnapshot,double?> get,string unit,string format,double min,double max,string help,int benefit=0)
            =>new NativeStatDefinition {Id=id,Label=label,Number=get,Unit=unit,Format=format,Minimum=min,Maximum=max,Help=help,BenefitDirection=benefit};
        static NativeStatDefinition T(string id,string label,Func<NativeStatsSnapshot,string> get,string help)
            =>new NativeStatDefinition {Id=id,Label=label,Text=get,Help=help};
        internal static readonly NativeStatGroup Engine=new NativeStatGroup("Engine","Engine",
            N("peakHp","Peak power",s=>s.Inspection.peakHp,"HP","F0",0,500,"Estimated full-load engine power with applied parts, tune, wear and supply limits; settled boost, not a measured race or transient response.",1),
            N("hpRpm","Power peak",s=>s.Inspection.peakHp>0?(double?)s.Inspection.hpRpm:null,"RPM","F0",0,10000,"RPM at maximum estimated power. A higher peak RPM changes where the engine works best; it is not automatically an improvement."),
            N("peakTorque","Peak torque",s=>s.Inspection.peakTorque,"Nm","F0",0,1000,"Estimated engine torque before gearing and driveline losses, including applied condition. More torque can require stronger components and better traction.",1),
            N("torqueRpm","Torque peak",s=>s.Inspection.peakTorque>0?(double?)s.Inspection.torqueRpm:null,"RPM","F0",0,10000,"RPM at maximum estimated engine torque. Compare the full curve to understand low- and high-RPM delivery."),
            N("cylinders","Cylinders",s=>s.Spec.sound.cylinders,"","F0",1,16,"Installed engine cylinder count; individual cylinder/component faults do not change its physical cylinder count."),
            T("engineLayout","Layout",s=>s.Spec.sound.layout==EngineLayout.Crossplane?"Cross-plane V":s.Spec.sound.layout.ToString(),"Installed engine layout. Cross-plane V engines have different firing-pulse behavior from inline engines."),
            N("displacement","Displacement",s=>s.Spec.DisplacementLitres,"L","F2",0,12,"Total installed engine swept volume. More displacement is a tradeoff involving torque, fuel use and mass."),
            T("fueling","Fueling",s=>s.Engine.data.fueling==FuelSystem.Diesel?"Diesel common rail":s.Engine.data.fueling==FuelSystem.Carburetor?"Carburetor":"Fuel injection","Effective installed fuel system, including an installed injection conversion. This describes hardware, not the player's fuel-consumption difficulty."),
            N("idleRpm","Idle target",s=>s.Spec.idleRpm,"RPM","F0",0,2000,"Authored warm idle target; cold starts, faults and cam behavior can make actual RPM vary."),
            N("redlineRpm","Redline",s=>s.Spec.redlineRpm,"RPM","F0",0,10000,"Installed engine's nominal redline. The limiter may act at a different saved target. This is not a guarantee against damage or mechanical overrev."),
            N("safeOperatingRpm","Safe operating RPM",s=>s.Inspection.safeOperatingRpm,"RPM","F0",0,15000,"Lowest safe speed from the fitted engine internals and valvetrain, including current spring condition and cam demand. The electronic cut target is separate; a higher setting does not strengthen weak parts.",1),
            T("rpmLimitPart","RPM limited by",s=>s.Inspection.rpmLimitPart,"The weakest fitted speed-related component; improving a different part may leave the complete engine limit unchanged."),
            N("engineTorqueHeadroom","Internal torque headroom",s=>s.Inspection.engineTorqueHeadroomNm,"Nm","F0",0,2000,"Lowest comparable engine-internal torque strength with captured condition. Clutch/gearbox/axle ratings belong to their own torque paths.",1),
            T("torqueLimitPart","Torque limited by",s=>s.Inspection.torqueLimitPart,"Weakest fitted engine-internal torque rating, not a sum of strengths."),
            T("limiterStyle","Rev limiter",s=>s.Engine.limiter==LimiterStyle.None?"None":s.Engine.limiter==LimiterStyle.Soft?"Soft cut":"Hard cut","Effective installed limiter behavior. Hard cut cycles combustion; soft cut progressively reduces torque. None leaves combustion without an RPM cut."),
            N("limiterRpm","Limiter target",s=>s.Engine.limiter==LimiterStyle.None?(double?)null:s.Engine.limitRpm,"RPM","F0",0,10000,"Effective saved limiter target after installed hardware permissions. A combustion limiter cannot prevent a bad downshift forcing engine RPM upward."),
            T("limiterCut","Cut method",s=>s.Engine.limiter==LimiterStyle.None?"—":s.Engine.cut==CutMethod.Ignition?"Ignition cut":"Fuel cut","Fuel cut stops injector delivery; ignition cut stops ignition while fuel may continue. The modeled carburetor uses ignition cut. No limiter has no active cut method."),
            N("bore","Bore",s=>s.Spec.engineBoreMm>0?(double?)s.Spec.engineBoreMm:null,"mm","F1",0,160,"Authored cylinder bore used for fitment. A dash means it is not specified; no value is inferred."),
            N("valvesPerCylinder","Valves / cylinder",s=>s.Spec.valvesPerCylinder>0?(double?)s.Spec.valvesPerCylinder:null,"","F0",0,8,"Authored valves per cylinder. A dash means the value is not specified; installed failures do not change the physical design count."),
            N("camLift","Cam lift",s=>s.Spec.valvetrain.liftMm,"mm","F1",0,30,"Installed cam valve lift. More lift can increase airflow demand, spring load and wear; it is not universally better."),
            N("camDuration","Cam duration @ 0.050 in",s=>s.Spec.valvetrain.durationAt050Degrees,"°","F0",80,360,"Crank degrees of valve opening measured at 0.050-inch lift. Longer duration shifts the useful RPM band and changes idle/fuel behavior."),
            N("camOverlap","Valve overlap",s=>s.Spec.valvetrain.overlapDegrees,"°","F0",0,180,"Crank degrees with intake and exhaust valves open together. Overlap affects idle, mixture behavior and the torque curve."),
            N("camSeparation","Lobe separation",s=>s.Spec.valvetrain.lobeSeparationDegrees,"°","F0",60,140,"Angle between cam lobe centers. This is a profile choice with tradeoffs, rather than a higher-is-better rating."),
            N("compression","Compression ratio",s=>s.Spec.combustion.compressionRatio,":1","F2",5,25,"Piston crown volume changes effective compression. Higher compression can improve efficiency but increases petrol knock risk under load/boost."),
            T("crown","Piston crown",s=>s.Spec.combustion.crown.ToString(),"Dish volume lowers compression; dome volume raises it. Diesel bowls support compression ignition."),
            N("valveClearance","Estimated valve clearance",s=>s.Spec.combustion.valveClearanceMm,"mm","F2",-1,5,"Estimated clearance from fitted lift, piston relief depth and cam timing. Low clearance can cause interference contact."),
            N("springTravel","Spring travel",s=>s.Spec.valvetrain.springMaxLiftMm,"mm","F1",5,20,"Maximum fitted spring travel. Greater cam lift causes coil bind; RPM control rating is a separate limit."),
            N("camTiming","Cam timing",s=>s.Spec.valvetrain.camTimingDegrees,"°","F1",-6,6,"Advance (+) favors low-RPM filling; retard (-) shifts filling upward. Offsets reduce estimated valve clearance. Match cam lift, spring travel and piston reliefs; fixed timing hardware remains fixed.")
        );
    }
}
