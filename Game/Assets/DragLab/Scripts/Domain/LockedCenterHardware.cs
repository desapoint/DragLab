using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public static class LockedCenterHardware
    {
        public static void Add(MechanicalCatalog catalog,IEnumerable<VehicleSpec> vehicles)
        {
            foreach(var spec in vehicles.Where(v=>v.drive==DriveType.AWD)){
                var standard=catalog.parts.Where(p=>p.category==PartCategory.TransferCase&&!PartTuning.Allows(p,PartTuningOptions.CenterRatio)&&catalog.Fits(spec,p)).OrderByDescending(p=>p.price).First();
                var p=standard.Copy();p.id=spec.id+"/TransferCase/RatioLocked";p.name="Vector Motorsport ratio-matched locked transfer case";p.brand="Vector Motorsport";p.price=checked(standard.price+2500);p.massKg+=2;p.reputationRequired=8;p.tuning|=PartTuningOptions.CenterRatio;p.centerRatioMin=.5f;p.centerRatioMax=2;
                p.description="Locked center with interchangeable front/rear speed gearsets. Match unequal tire rolling sizes without forcing straight-line scrub. Speed ratio is separate from torque bias; mismatched calibration still generates tire slip, heat and wear.";catalog.parts.Add(p);
            }
        }
        public static void Enforce(BuildResolution build,IDictionary<PartCategory,string> installed,MechanicalCatalog catalog)
        {
            if(build.spec.drive!=DriveType.AWD)return;
            var part=installed.TryGetValue(PartCategory.TransferCase,out var id)?catalog.Find(id):null;
            if(part!=null&&PartTuning.Allows(part,PartTuningOptions.CenterRatio)){
                var data=AxleDriveDynamics.Configuration(build.spec,build.tune);data.mode=AwdMode.LockedCenter;data.centerSpeedRatio=(float)DragMath.Clamp(data.LockedRatio,part.centerRatioMin,part.centerRatioMax);build.tune.awd=data;build.tune.awdConfigured=true;
            }else{
                build.spec.awd.centerSpeedRatio=0;if(build.tune.awd!=null)build.tune.awd.centerSpeedRatio=0;
            }
        }
    }
}
