using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;

namespace DragLab.Editor
{
    public static class NativeShopCacheVerification
    {
        public static void UnitPreviews()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/individual-part-preview"));Directory.CreateDirectory(folder);
            var log=new StringBuilder();int checks=0,failures=0;
            NativeShopDomainChecks.UnitPreviews(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{checks++;if(!ok)failures++;log.AppendLine((ok?"PASS: ":"FAIL: ")+label);});
            log.AppendLine("Checks: "+checks+"; failures: "+failures);
            File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));
            if(failures>0)throw new InvalidOperationException("Individual preview failures: "+failures);
            Debug.Log("Individual preview verification passed: "+checks);
        }
        public static void Run()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/shop-inventory"));
            Directory.CreateDirectory(folder);
            var log=new StringBuilder(); int checks=0;
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            NativeShopDomainChecks.Run(vehicles,(ok,label)=>{
                if(!ok)throw new Exception(label);
                checks++; log.AppendLine("PASS: "+label);
            });
            log.AppendLine("Cars: "+vehicles.Length+"; checks: "+checks+"; failures: 0");
            File.WriteAllText(Path.Combine(folder,"native-shop-cache-verification.txt"),log.ToString(),new UTF8Encoding(false));
            Debug.Log("Shop cache verification passed: "+checks+" checks across "+vehicles.Length+" cars.");
        }
    }
}
