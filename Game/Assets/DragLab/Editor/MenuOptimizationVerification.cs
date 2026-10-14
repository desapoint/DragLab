using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor
{
    public static class MenuOptimizationVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);log.AppendLine("PASS "+label);}
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var vehicle in vehicles){var original=vehicle.spec;foreach(PartCategory category in Enum.GetValues(typeof(PartCategory)))Check(MechanicalCatalog.SupportsSlot(original,category)==MechanicalCatalog.Slots(original).Contains(category),original.id+" indexed slot support "+category);
                var build=catalog.Resolve(original.id,catalog.Recipe(original.id),vehicle.CreateStockTune());var optimized=new MechanicalStress(build.spec,build.tune,null);var baselineSpec=build.spec.Copy();baselineSpec.hasInstalledParts=false;var baseline=new MechanicalStress(baselineSpec,build.tune,optimized.parts);
                foreach(var part in optimized.parts)Check(JsonUtility.ToJson(optimized.Definition(part))==JsonUtility.ToJson(baseline.Definition(part)),original.id+" captured definition agrees with authored baseline "+part.slot);
                Check(optimized.parts.Count==baseline.parts.Count,original.id+" retains the same simulation component topology");
            }
            var first=catalog.parts[0];var next=first.Copy();next.id="lookup-replacement";catalog.parts[0]=next;Check(catalog.Find(next.id)==next&&catalog.Find(first.id)==null,"Same-length editable catalogue replacement invalidates index identity");catalog.parts[0]=first;Check(catalog.Find(first.id)==first&&catalog.Find(next.id)==null,"Index supports restoring the authored entry without stale aliases");
            log.AppendLine("Checks: "+checks+"; failures: 0");string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/responsiveness"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"optimization-verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Menu optimization checks passed: "+checks);
        }
    }
}
