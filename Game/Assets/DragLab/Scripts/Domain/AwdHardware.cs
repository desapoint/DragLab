using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    [Flags] public enum AwdAdjustments { None=0, FrontShare=1, ActiveLimits=2, Coupling=4, CenterBias=8, CenterPreload=16, SpeedRatio=32 }
    [Serializable] public sealed class AwdHardwareSpec
    {
        public int version=1,modes;
        public bool integratedCenter;
        public AwdAdjustments adjustments;
        public AwdSpec calibration=new AwdSpec();
        public float responseMin=.05f,responseMax=1;
        public bool Supports(AwdMode mode)=>Enum.IsDefined(typeof(AwdMode),mode)&&(modes&(1<<(int)mode))!=0;
        public AwdHardwareSpec Copy(){var copy=(AwdHardwareSpec)MemberwiseClone();copy.calibration=calibration?.Copy();return copy;}
        public string Validate()
        {
            if(version!=1||modes<1||(modes&~63)!=0||((int)adjustments&~63)!=0||calibration==null||calibration.Validate()!=null||!Supports(calibration.mode)||!EngineBehaviorSpec.Range(responseMin,.01,2)||!EngineBehaviorSpec.Range(responseMax,responseMin,2))return "Invalid AWD hardware specification.";
            if((adjustments&AwdAdjustments.ActiveLimits)!=0&&!Supports(AwdMode.ActiveBias)||(adjustments&AwdAdjustments.Coupling)!=0&&!Supports(AwdMode.OnDemand)||(adjustments&(AwdAdjustments.CenterBias|AwdAdjustments.CenterPreload))!=0&&!Supports(AwdMode.LimitedSlipCenter)||(adjustments&AwdAdjustments.SpeedRatio)!=0&&!Supports(AwdMode.LockedCenter))return "AWD adjustment needs matching mechanical hardware.";
            return null;
        }
    }
    public static class AwdHardware
    {
        static bool Declared(PartDefinition p)=>p?.awdHardware!=null&&p.awdHardware.version==1;
        public static PartDefinition Installed(VehicleSpec spec)
        {
            PartDefinition center=null;
            foreach(var p in spec.fittedParts??Array.Empty<PartDefinition>()){
                if(p.category==PartCategory.TransferCase&&Declared(p)&&p.awdHardware.integratedCenter)return p;
                if(p.category==PartCategory.Differential&&Declared(p))center=p;
            }return center;
        }
        public static bool Allows(VehicleSpec spec,AwdAdjustments option){var p=Installed(spec);return p!=null&&(p.awdHardware.adjustments&option)!=0&&(option!=AwdAdjustments.SpeedRatio||PartTuning.Allows(p,PartTuningOptions.CenterRatio));}
        public static AwdMode[] Modes(VehicleSpec spec){var p=Installed(spec);return p==null?new[]{spec.awd.mode}:Enum.GetValues(typeof(AwdMode)).Cast<AwdMode>().Where(p.awdHardware.Supports).ToArray();}
        static readonly string[] names={"Fixed split","Open center","Locked center","On-demand coupling","Active center","Limited-slip center"};
        public static string Name(AwdMode mode)=>names[(int)mode];
        public static AwdSpec Constrain(VehicleSpec spec,AwdSpec requested)
        {
            if(!spec.hasInstalledParts)return (requested??spec.awd??new AwdSpec()).Copy();
            var part=Installed(spec);if(part==null)return (spec.awd??new AwdSpec()).Copy();
            var h=part.awdHardware;var data=h.calibration.Copy();var raw=requested??data;
            if(h.Supports(raw.mode))data.mode=raw.mode;
            if(data.mode==AwdMode.LimitedSlipCenter){
                data.torqueBiasRatio=(h.adjustments&AwdAdjustments.CenterBias)!=0?(float)DragMath.Clamp(raw.torqueBiasRatio,1,part.torqueBiasRatio):part.torqueBiasRatio;
                data.centerPreloadNm=(h.adjustments&AwdAdjustments.CenterPreload)!=0?(float)DragMath.Clamp(raw.centerPreloadNm,0,part.preloadNm):part.preloadNm;
            }
            if(data.mode==AwdMode.ActiveBias&&(h.adjustments&AwdAdjustments.ActiveLimits)!=0){
                data.minimumFront=(float)DragMath.Clamp(raw.minimumFront,h.calibration.minimumFront,h.calibration.maximumFront);
                data.maximumFront=(float)DragMath.Clamp(raw.maximumFront,data.minimumFront,h.calibration.maximumFront);
            }
            if((h.adjustments&AwdAdjustments.FrontShare)!=0&&(data.mode==AwdMode.ActiveBias||data.mode==AwdMode.FixedSplit||data.mode==AwdMode.OpenCenter))data.frontBias=(float)DragMath.Clamp(raw.frontBias,data.mode==AwdMode.ActiveBias?data.minimumFront:data.mode==AwdMode.OpenCenter?0.05:0,data.mode==AwdMode.ActiveBias?data.maximumFront:data.mode==AwdMode.OpenCenter?0.95:1);
            if(data.mode==AwdMode.OnDemand&&(h.adjustments&AwdAdjustments.Coupling)!=0){
                data.maximumSecondary=(float)DragMath.Clamp(raw.maximumSecondary,0,h.calibration.maximumSecondary);data.preload=(float)DragMath.Clamp(raw.preload,0,1);data.slipThreshold=(float)DragMath.Clamp(raw.slipThreshold,.001,.5);
            }
            bool response=data.mode==AwdMode.OnDemand&&(h.adjustments&AwdAdjustments.Coupling)!=0||data.mode==AwdMode.ActiveBias&&(h.adjustments&AwdAdjustments.ActiveLimits)!=0;
            if(response)data.responseSeconds=(float)DragMath.Clamp(raw.responseSeconds,h.responseMin,h.responseMax);
            var transfer=Array.Find(spec.fittedParts,p=>p.category==PartCategory.TransferCase);
            if(transfer!=null&&transfer.stage>0){data.isolateFailedAxle=true;if(!response)data.responseSeconds=Math.Min(data.responseSeconds,.1f);}
            if(data.mode==AwdMode.LockedCenter&&Allows(spec,AwdAdjustments.SpeedRatio))data.centerSpeedRatio=(float)DragMath.Clamp(raw.centerSpeedRatio==0?1:raw.centerSpeedRatio,part.centerRatioMin,part.centerRatioMax);else data.centerSpeedRatio=0;
            return data;
        }
        public static void Enforce(BuildResolution build)
        {
            if(build.spec.drive!=DriveType.AWD)return;
            build.tune.awd=Constrain(build.spec,build.tune.awdConfigured?build.tune.awd:null);build.tune.awdConfigured=true;
        }
        public static string Summary(PartDefinition p)
        {
            if(!Declared(p))return "";var h=p.awdHardware;var labels=new List<string>();
            if((h.modes&(h.modes-1))!=0)labels.Add("mechanical lock/unlock");
            if((h.adjustments&AwdAdjustments.FrontShare)!=0)labels.Add("front torque target");
            if((h.adjustments&AwdAdjustments.ActiveLimits)!=0)labels.Add("active distribution limits/response");
            if((h.adjustments&AwdAdjustments.Coupling)!=0)labels.Add("coupling engagement/response");
            if((h.adjustments&AwdAdjustments.CenterBias)!=0)labels.Add("center bias 1–"+p.torqueBiasRatio.ToString("0.#")+":1");
            if((h.adjustments&AwdAdjustments.CenterPreload)!=0)labels.Add("center preload 0–"+p.preloadNm.ToString("0")+" Nm");
            return labels.Count==0?"Fixed AWD hardware/calibration.":"AWD adjustments: "+string.Join(", ",labels)+".";
        }
        static AwdHardwareSpec Hardware(AwdSpec calibration,AwdAdjustments adjustments=AwdAdjustments.None,int modes=0,bool integrated=false)=>new AwdHardwareSpec{calibration=calibration.Copy(),adjustments=adjustments,modes=modes==0?1<<(int)calibration.mode:modes,integratedCenter=integrated};
        public static void Add(MechanicalCatalog catalog,IEnumerable<VehicleSpec> vehicles)
        {
            foreach(var spec in vehicles.Where(s=>s.drive==DriveType.AWD)){
                foreach(var part in catalog.parts.Where(p=>p.category==PartCategory.Differential&&p.id.StartsWith(spec.id+"/",StringComparison.Ordinal)&&catalog.Fits(spec,p)).ToArray()){
                    var data=spec.awd.Copy();data.mode=part.stage==0?spec.awd.mode:part.stage==1?AwdMode.LimitedSlipCenter:AwdMode.LockedCenter;
                    if(data.mode==AwdMode.LimitedSlipCenter){if(part.stage==0){part.torqueBiasRatio=data.torqueBiasRatio;part.preloadNm=data.centerPreloadNm;}data.torqueBiasRatio=part.torqueBiasRatio;data.centerPreloadNm=part.preloadNm;}
                    part.differential=data.mode==AwdMode.LimitedSlipCenter?DifferentialKind.LimitedSlip:data.mode==AwdMode.LockedCenter?(part.stage==2?DifferentialKind.Welded:DifferentialKind.Spool):DifferentialKind.Open;
                    part.awdHardware=Hardware(data);part.name=part.brand+" "+(part.stage==0?"Touring ":part.stage==1?"reinforced ":part.stage==2?"welded ":"solid ")+Name(data.mode).ToLowerInvariant()+" assembly";
                    part.description="Mechanical center distribution assembly. "+Name(data.mode)+"; factory primary axle, geometry, disconnect capability and free-shaft properties are fixed. Replace this hardware to change the center system. Front/rear axle differentials remain independent parts.";
                }
                PartDefinition Add(string key,string label,AwdMode mode,int price,AwdAdjustments settings=AwdAdjustments.None){
                    var p=catalog.Find(catalog.Id(spec.id,PartCategory.Differential,1)).Copy();p.id=spec.id+"/Differential/"+key;p.name="Vector Motorsport "+label;p.brand="Vector Motorsport";p.price=price;p.quality="Aftermarket";p.reputationRequired=mode==AwdMode.ActiveBias?6:0;
                    var data=spec.awd.Copy();data.mode=mode;data.centerSpeedRatio=0;data.isolateFailedAxle=mode==AwdMode.OnDemand||mode==AwdMode.ActiveBias;
                    if(mode==AwdMode.FixedSplit)data.frontBias=.35f;if(mode==AwdMode.OpenCenter)data.frontBias=.5f;
                    if(mode==AwdMode.LimitedSlipCenter){data.torqueBiasRatio=3;data.centerPreloadNm=35;p.torqueBiasRatio=5;p.preloadNm=200;}
                    if(mode!=AwdMode.LimitedSlipCenter){p.torqueBiasRatio=1;p.preloadNm=0;}
                    if(mode==AwdMode.OnDemand)data.maximumSecondary=.6f;
                    p.awdHardware=Hardware(data,settings);p.tuning=settings==AwdAdjustments.None?PartTuningOptions.None:PartTuningOptions.AwdCalibration;p.differential=mode==AwdMode.LimitedSlipCenter?DifferentialKind.LimitedSlip:mode==AwdMode.LockedCenter?DifferentialKind.Spool:DifferentialKind.Open;
                    p.massKg=mode==AwdMode.ActiveBias?20:mode==AwdMode.OnDemand?18:mode==AwdMode.LockedCenter?10:14;
                    p.description=Name(mode)+" center hardware. "+(settings==AwdAdjustments.None?"Fixed calibration; no tune adjustment.":"Includes the matching adjustable mechanical setup/controller; only declared calibration ranges are editable.")+" Primary axle, failure isolation and shaft properties are physical specifications. Engine ECU upgrades do not change this system.";catalog.parts.Add(p);return p;
                }
                Add("FixedSplit35","35:65 fixed-distribution center",AwdMode.FixedSplit,850);
                Add("OpenCenter","open center differential",AwdMode.OpenCenter,650);
                Add("AdjustableLSD","adjustable mechanical center LSD",AwdMode.LimitedSlipCenter,1700,AwdAdjustments.CenterBias|AwdAdjustments.CenterPreload);
                Add("OnDemandFixed","on-demand coupling / fixed controller",AwdMode.OnDemand,1600);
                Add("OnDemandController","on-demand coupling / programmable controller",AwdMode.OnDemand,2300,AwdAdjustments.Coupling);
                Add("ActiveFixed","active center / fixed controller",AwdMode.ActiveBias,2600);
                Add("ActiveController","active center / programmable controller",AwdMode.ActiveBias,3400,AwdAdjustments.FrontShare|AwdAdjustments.ActiveLimits);
                var switchable=Add("SwitchableLock","open / mechanically lockable center",AwdMode.OpenCenter,1900);switchable.awdHardware.modes=(1<<(int)AwdMode.OpenCenter)|(1<<(int)AwdMode.LockedCenter);switchable.tuning=PartTuningOptions.AwdCalibration;switchable.description="Open center with an actual mechanical lock. Select Open or Locked; no on-demand or active conversion. Fixed 50:50 open-center geometry and 1:1 locked speed ratio. Front/rear axle differentials remain separate.";
                foreach(var transfer in catalog.parts.Where(p=>p.category==PartCategory.TransferCase&&p.id.StartsWith(spec.id+"/",StringComparison.Ordinal)&&!PartTuning.Allows(p,PartTuningOptions.CenterRatio))){var data=spec.awd.Copy();data.isolateFailedAxle=transfer.stage>0;if(transfer.stage>0)data.responseSeconds=Math.Min(data.responseSeconds,.1f);transfer.awdHardware=Hardware(data);transfer.description+=" This transfer housing does not convert the center system. "+(transfer.stage>0?"Includes failed-branch disconnect hardware; fixed couplings respond within 0.10 s.":"Pass-through housing without added failed-branch isolation.");}
                var ratio=catalog.Find(spec.id+"/TransferCase/RatioLocked");if(ratio!=null){var data=spec.awd.Copy();data.mode=AwdMode.LockedCenter;data.isolateFailedAxle=true;ratio.awdHardware=Hardware(data,AwdAdjustments.SpeedRatio,integrated:true);}
            }
        }
    }
}
