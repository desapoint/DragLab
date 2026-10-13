using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    internal static partial class NativeStatCatalog
    {
        // Explicit potential effects for every category. Changed values alone must never admit unrelated rows.
        static readonly Dictionary<PartCategory,HashSet<string>> influence=CreateInfluence();
        static Dictionary<PartCategory,HashSet<string>> CreateInfluence()
        {
            var map=Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().ToDictionary(c=>c,c=>new HashSet<string>(new[]{"vehicleMass","tuningWeight","installedMass","dryInstalledMass","cgPosition","cgHeight","frontWeight","frontLoad","rearLoad","warningCount","buildStatus","probeStatus","quarterTime","trapSpeed","maxWheelSlip","maxClutchSlip"}));
            void Add(string ids,params PartCategory[] categories){foreach(var c in categories)foreach(string id in ids.Split('|'))map[c].Add(id);}
            foreach(var c in map.Keys.Where(PartSemantics.Wears).ToArray())Add("tuningCondition",c);
            Add("peakHp|hpRpm|peakTorque|torqueRpm|tuningHp|tuningTorque",PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.HeadIntake,PartCategory.FuelSystem,PartCategory.Ignition,PartCategory.SparkPlugs,PartCategory.GlowPlugs,PartCategory.Controls,PartCategory.Induction,PartCategory.Exhaust,PartCategory.Camshaft,PartCategory.ValveSprings,PartCategory.Valves,PartCategory.Followers,PartCategory.TimingDrive,PartCategory.PistonRings,PartCategory.CylinderBlock,PartCategory.Bearings,PartCategory.ValveSeals,PartCategory.Cooling,PartCategory.OilPump,PartCategory.OilFilter,PartCategory.CoolantPump,PartCategory.CoolingCap,PartCategory.CoolingHoses,PartCategory.OilCooler);
            Add("cylinders|engineLayout|displacement|bore|valvesPerCylinder|idleRpm|redlineRpm|fueling",PartCategory.CylinderBlock);
            Add("fueling|idleRpm",PartCategory.FuelSystem,PartCategory.Controls);Add("idleRpm",PartCategory.Camshaft);
            Add("safeOperatingRpm|rpmLimitPart|limiterRpm|engineTorqueHeadroom|torqueLimitPart",PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft);
            Add("safeOperatingRpm|rpmLimitPart",PartCategory.Camshaft,PartCategory.ValveSprings,PartCategory.Valves,PartCategory.Followers,PartCategory.TimingDrive,PartCategory.CylinderBlock);
            Add("limiterStyle|limiterRpm|limiterCut",PartCategory.RevLimiter,PartCategory.Controls);
            Add("camLift|camDuration|camOverlap|camSeparation|camTiming|valveClearance",PartCategory.Camshaft);Add("springTravel",PartCategory.ValveSprings);Add("camTiming|valveClearance",PartCategory.TimingDrive,PartCategory.Followers);
            Add("compression|crown|valveClearance",PartCategory.Pistons);Add("compression|valveClearance",PartCategory.CylinderBlock,PartCategory.HeadIntake);
            Add("boostSetting|inductionType|turboCount|superchargerCount|boostBar|turboSize|turboSpool|pulleyRatio|blowOffValve|intercooler|turboFlow|turboEfficiency|blowerFlow|blowerEfficiency|boostLimit",PartCategory.Induction);
            Add("exhaustPath",PartCategory.Exhaust);
            Add("transmission|downshiftProtection|finalDrive|shiftSeconds|shiftRetention|clutchType|clutchCapacity|converterStall|converterMultiply|converterLock|cvtMinimum|cvtMaximum|cvtTarget|gearRatio|gearSpeed|shiftLanding|cvtSpeed",PartCategory.Gearbox);
            Add("clutchType|clutchCapacity|shiftSeconds",PartCategory.Clutch);Add("flywheelMass|flywheelInertia|engineInertia",PartCategory.Flywheel);Add("engineInertia",PartCategory.CylinderBlock);
            Add("frontDifferential|rearDifferential|awdMode|awdFrontBias|awdPrimary|awdMinimumFront|awdMaximumFront|awdSecondary|awdResponse|awdPreload|awdSlipThreshold|awdIsolation|centerSpeedRatio|centerBias|centerPreload|wheelRadius|wheelDiameter|gearSpeed|cvtSpeed",PartCategory.Differential,PartCategory.TransferCase);
            Add("frontDifferential",PartCategory.FrontDifferential);Add("rearDifferential",PartCategory.RearDifferential);
            Add("wheelRadius|wheelDiameter|wheelMass|wheelInertia|effectiveMass|wheelWidth|tirePressure|gearSpeed|cvtSpeed",PartCategory.Wheels,PartCategory.Tires);
            Add("tireCompound|tireGrip|tireHealth|rollingLife|slidingLife",PartCategory.Tires);
            Add("brakeG",PartCategory.Brakes);
            Add("chassisMass",PartCategory.Body);
            Add("coolingTransfer|radiatorRejection",PartCategory.Cooling,PartCategory.CoolantPump);
            Add("coolantCapacity",PartCategory.Cooling);Add("coolingCap",PartCategory.CoolingCap);
            Add("oilCapacity|oilMinimum",PartCategory.OilPan,PartCategory.CylinderBlock);
            Add("fuelDensity|fuelLitres|tankCapacity",PartCategory.FuelSystem,PartCategory.CylinderBlock);
            Add("launchTarget|twoStepEnabled|twoStepTarget|twoStepStyle|preloadEnabled|antiLagEnabled",PartCategory.TwoStep,PartCategory.Controls);
            Add("shiftLight|shiftLightTarget",PartCategory.ShiftLight);Add("shiftTarget",PartCategory.Gearbox,PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft);
            return map;
        }
        internal static bool CanAffect(PartCategory category,string id)
        {
            if(id.StartsWith("warning:",StringComparison.Ordinal))return true;
            if(id.StartsWith("axle:",StringComparison.Ordinal))return category==PartCategory.Wheels||category==PartCategory.Tires;
            if(id.StartsWith("exhaust:",StringComparison.Ordinal))return category==PartCategory.Exhaust;
            string key=id.Contains(":")?id.Substring(0,id.IndexOf(':')):id;
            return influence.TryGetValue(category,out var rows)&&rows.Contains(key);
        }
        internal static IReadOnlyList<NativeStatGroup> PartSpecific(CarInspection current,CarInspection proposed,IEnumerable<PartCategory> categories)
        {
            var selected=new HashSet<string>(categories.Select(c=>"part:"+c));
            var own=Components(current,proposed).Children.Where(g=>selected.Contains(g.Id)).Select(g=>{
                var category=(PartCategory)Enum.Parse(typeof(PartCategory),g.Id.Substring(5));
                return new NativeStatGroup(g.Id,g.Title,g.Definitions.Concat(SavedTuneRows.Where(d=>CanAffect(category,d.Id))).ToArray()){Children=g.Children};
            }).ToArray();
            if(own.Length==1)return new[]{new NativeStatGroup("PartChanges","Part changes",own[0].Definitions.ToArray()){InitiallyCollapsed=false,Children=own[0].Children}};
            return new[]{new NativeStatGroup("PartChanges","Part changes"){InitiallyCollapsed=false,Children=Array.AsReadOnly(own)}};
        }
        internal static IReadOnlyList<NativeStatGroup> PartEffects(CarInspection current,CarInspection proposed,IEnumerable<PartCategory> categories)
        {
            var set=categories.Distinct().ToArray();
            NativeStatGroup Filter(NativeStatGroup group){
                if(group.Id=="Components")return null; // Actual selected hardware/members belong in Part changes.
                var rows=group.Definitions.Where(d=>set.Any(c=>CanAffect(c,d.Id))).ToArray();
                var children=group.Children.Select(Filter).Where(g=>g!=null).ToArray();
                return rows.Length==0&&children.Length==0?null:new NativeStatGroup(group.Id=="Performance"?"PartPerformance":group.Id,group.Title,rows){InitiallyCollapsed=true,Children=Array.AsReadOnly(children)};
            }
            return Groups(current,proposed).Select(Filter).Where(g=>g!=null).ToArray();
        }
        internal static NativeStatGroup PartSummary(CarInspection current,CarInspection proposed,IEnumerable<PartCategory> categories)
        {
            var set=categories.ToArray();
            return new NativeStatGroup("BuildEffects","Build effects",TuningSummary().Definitions.Where(d=>set.Any(c=>CanAffect(c,d.Id))).ToArray());
        }
    }
}
