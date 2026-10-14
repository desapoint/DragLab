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
            build.spec.fittedParts=installed.Values.Select(catalog.Find).Where(p=>p!=null).Select(p=>p.Copy()).ToArray();build.spec.hasInstalledParts=true;
            AwdHardware.Enforce(build);
        }
    }
}
